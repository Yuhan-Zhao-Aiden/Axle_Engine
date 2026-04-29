using System.Text.Json;

namespace Axle.Client;
using Axle.Core;
using Axle.Net;
using Axle.Core.AxleMath;
using Axle.Core.Utility;
using Axle.Ecs;
using Axle.Graphics;
using Axle.Sim;
using Axle.Sim.Map;
using Axle.Client.System;

public class Program
{
    public static void Main(string[] args)
    {
        bool useNet = args.Contains("--net");

        // Resolve relative to the output directory so the path is correct
        // regardless of which directory dotnet run is invoked from.
        string mapPath      = Path.Combine(AppContext.BaseDirectory, "assets", "test.map");
        string settingsPath = Path.Combine(AppContext.BaseDirectory, "assets", "settings.json");
        string tilesPath    = Path.Combine(AppContext.BaseDirectory, "assets", "test.tiles.json");
        string spritesPath  = Path.Combine(AppContext.BaseDirectory, "assets", "sprites.json");
        string contentRoot  = AppContext.BaseDirectory;

        var jsonOpts = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
        var settings = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(settingsPath), jsonOpts) ?? new GameSettings();

        MapData map = MapLoader.Load(mapPath, settings.Mode);

        // Optional: load tile sprite mappings if a companion .tiles.json exists.
        // When absent, rendering falls back to colored squares (no behaviour change).
        TileSet? tileSet = null;
        if (File.Exists(tilesPath))
        {
            try { tileSet = TileSetLoader.Load(tilesPath, contentRoot); }
            catch (Exception ex) { Console.WriteLine($"[Warn] tiles.json load failed: {ex.Message}"); }
        }

        var camController = new CameraController(settings.Camera, map);

        var window = new WindowHost();
        var world = new World();

        // Register stores
        world.Register<Transform>();
        world.Register<RenderRect>();
        world.Register<RenderSprite>();
        world.Register<SimPosition>();
        world.Register<Velocity>();
        world.Register<MoveInput>();
        world.Register<LocalPlayer>();
        world.Register<PlayerCollider>();
        world.Register<CollisionLayer>();
        world.Register<Collectible>();
        world.Register<SpriteAnimator>();

        // Spawn player at first A spawn, or origin if none present
        Fixed32 spawnX = Fixed32.Zero;
        Fixed32 spawnY = Fixed32.Zero;
        if (map.PlayerSpawns.Count > 0)
        {
            spawnX = Fixed32.FromInt(map.PlayerSpawns[0].X * 32);
            spawnY = Fixed32.FromInt(map.PlayerSpawns[0].Y * 32);
        }

        var player = world.CreateEntity();
        world.Add(player, new SimPosition(spawnX, spawnY));
        world.Add(player, new Velocity(Fixed32.Zero, Fixed32.Zero));
        world.Add(player, new MoveInput(0, 0));
        world.Add<LocalPlayer>(player);
        world.Add(player, new Transform(new Vector2f(spawnX.ToFloat(), spawnY.ToFloat())));
        world.Add(player, new RenderRect(new Vector2f(32f, 32f), new Color4(1f, 1f, 0f)));
        world.Add(player, new PlayerCollider(Fixed32.FromInt(16), Fixed32.FromInt(16)));
        world.Add(player, new CollisionLayer(CollisionLayers.Player, Fixed32.FromInt(16), Fixed32.FromInt(16)));

        // Spawn collectibles at each 'C' position from the map
        var gold = new Color4(1f, 0.85f, 0f);
        foreach (var spawn in map.CoinSpawns)
        {
            Fixed32 cx = Fixed32.FromInt(spawn.X * 32);
            Fixed32 cy = Fixed32.FromInt(spawn.Y * 32);
            var coin = world.CreateEntity();
            world.Add(coin, new SimPosition(cx, cy));
            world.Add(coin, new CollisionLayer(CollisionLayers.Collectible, Fixed32.FromInt(16), Fixed32.FromInt(16)));
            world.Add<Collectible>(coin);
            world.Add(coin, new Transform(new Vector2f(cx.ToFloat(), cy.ToFloat())));
            world.Add(coin, new RenderRect(new Vector2f(32f, 32f), gold));
        }

        // Build generalized overlap system
        var overlapSys = new EntityOverlapSystem();
        overlapSys.Register(CollisionLayers.Player, CollisionLayers.Collectible, new CollectibleHandler(world));

        // Systems order declares the pipeline
        var input = new LocalInputSystem();
        var sync = new SyncTransformSystem();
        var renderRunner = new RenderRunner(world, sync, () =>
        {
            int vpW = window.ClientSize.X;
            int vpH = window.ClientSize.Y;
            var (cx2, cy2) = camController.Update(world, vpW, vpH);
            return new Camera(vpW, vpH, cx2, cy2);
        });
        var tileMap = new TileCollisionMap(map);

        // Pre-instantiate the two systems that ReconciliationApplicator needs to replay
        // unacknowledged inputs — same instances run inside the SimRunner.
        var velocitySystem = new PlayerVelocitySystem();
        var movementSystem = new TileCollisionMovementSystem(SimTime.Dt, tileMap);
        var history        = new InputHistory();

        // Build system pipeline — ClientNetInputSystem injected after LocalInputSystem in --net mode.
        List<ISystem> systems = [input];

        // Optional network client — created when --net flag is provided.
        NetClient? netClient = null;
        ReconciliationApplicator? reconciliator = null;
        ClientNetInputSystem? netInputSystem = null;
        RemoteInterpolationSystem? remoteInterp = null;
        if (useNet)
        {
            ITransport clientTransport = settings.NetSim?.Enabled == true
                ? new SimulatedTransport(new UdpTransport(), settings.NetSim)
                : new UdpTransport();
            netClient = new NetClient(clientTransport);
            netClient.Connect(new NetEndpoint("127.0.0.1", 7777));
            netInputSystem = new ClientNetInputSystem(netClient, history);
            systems.Add(netInputSystem);

            var ghost = world.CreateEntity();
            world.Add(ghost, new Transform(new Vector2f(spawnX.ToFloat(), spawnY.ToFloat())));
            world.Add(ghost, new RenderRect(new Vector2f(32f, 32f), new Color4(1f, 0f, 0f, 0.45f)));

            reconciliator = new ReconciliationApplicator(history, world, player, velocitySystem, movementSystem, ghost);
            remoteInterp = new RemoteInterpolationSystem();
            Console.WriteLine("[Client] Connecting to 127.0.0.1:7777...");
        }

        systems.Add(velocitySystem);
        if (settings.Mode == GameMode.Platformer)
            systems.Add(new GravitySystem());
        systems.Add(movementSystem);
        systems.Add(overlapSys);
        var simRunner = new SimRunner(world, [.. systems]);

        EngineLoop? loop = null;
        ulong netTick = 0;
        bool serverEntitySet = false;
        var remoteEntities = new Dictionary<(int index, int version), EntityId>();
        ulong debugLogTick = 0;

        var animSystem = new AnimationSystem(settings.Mode);

        window.OnReady = () =>
        {
            // Build texture cache from TileSet (must happen on GL thread, inside OnReady).
            Dictionary<char, Texture2D>? textures = null;
            if (tileSet is not null)
            {
                textures = new Dictionary<char, Texture2D>();
                foreach (var (sym, def) in tileSet.Tiles)
                {
                    if (string.IsNullOrEmpty(def.SpritePath)) continue;
                    string absPath = Path.Combine(contentRoot, def.SpritePath);
                    try   { textures[sym] = Texture2D.Load(absPath); }
                    catch (Exception ex) { Console.WriteLine($"[Warn] Texture load failed for '{sym}': {ex.Message}"); }
                }
            }

            // Load sprite animations declared in sprites.json.
            // For each entity type, each state maps to a subfolder of numbered PNGs (1.png, 2.png, ...).
            // Missing folders/files are silently skipped; entities keep their RenderRect fallback.
            if (File.Exists(spritesPath))
            {
                try
                {
                    var spriteConfig = JsonSerializer.Deserialize<Dictionary<string, EntityAnimConfig>>(
                        File.ReadAllText(spritesPath), jsonOpts);

                    if (spriteConfig is not null)
                    {
                        // Loads all numerically-named PNGs from {contentRoot}/{baseFolder}/{state}/
                        AnimationClip? LoadClip(string stateName, AnimStateConfig cfg, string baseFolder)
                        {
                            string dir = Path.Combine(contentRoot, baseFolder, stateName);
                            if (!Directory.Exists(dir)) return null;
                            var frameFiles = Directory.GetFiles(dir, "*.png")
                                .Where(f => int.TryParse(Path.GetFileNameWithoutExtension(f), out _))
                                .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f)))
                                .ToArray();
                            if (frameFiles.Length == 0) return null;
                            var frames = frameFiles.Select(f => Texture2D.Load(f)).ToArray();
                            return new AnimationClip(stateName, frames, cfg.Fps, cfg.Loop);
                        }

                        if (spriteConfig.TryGetValue("player", out var playerCfg))
                        {
                            var clips = new Dictionary<string, AnimationClip>();
                            foreach (var (state, cfg) in playerCfg.States)
                            {
                                var clip = LoadClip(state, cfg, playerCfg.BaseFolder);
                                if (clip is not null) clips[state] = clip;
                            }
                            if (clips.Count > 0)
                            {
                                string defClip = clips.ContainsKey("idle") ? "idle" : clips.Keys.First();
                                var firstTex = clips[defClip].Frames[0];
                                world.Remove<RenderRect>(player);
                                world.Add(player, new RenderSprite(firstTex, new Vector2f(32f, 32f)));
                                world.Add(player, new SpriteAnimator(clips, "idle", new Vector2f(32f, 32f)));
                            }
                        }

                        if (spriteConfig.TryGetValue("coin", out var coinCfg))
                        {
                            var clips = new Dictionary<string, AnimationClip>();
                            foreach (var (state, cfg) in coinCfg.States)
                            {
                                var clip = LoadClip(state, cfg, coinCfg.BaseFolder);
                                if (clip is not null) clips[state] = clip;
                            }
                            if (clips.Count > 0)
                            {
                                string defClip = clips.ContainsKey("idle") ? "idle" : clips.Keys.First();
                                var firstTex = clips[defClip].Frames[0];
                                // Collect before modifying to avoid mutating during iteration.
                                var coinIndexes = new List<int>();
                                foreach (var item in world.Query<Collectible, RenderRect>())
                                    coinIndexes.Add(item.Entity);
                                foreach (var idx in coinIndexes)
                                {
                                    var coinId = world.GetEntityId(idx);
                                    world.Remove<RenderRect>(coinId);
                                    world.Add(coinId, new RenderSprite(firstTex, new Vector2f(32f, 32f)));
                                    world.Add(coinId, new SpriteAnimator(clips, "idle", new Vector2f(32f, 32f)));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine($"[Warn] sprites.json load failed: {ex.Message}"); }
            }

            renderRunner.Initialize(window.Renderer, map, textures, animSystem, remoteInterp);
            loop = new EngineLoop(simRunner, renderRunner);
        };

        window.OnFrame = () =>
        {
            netClient?.Tick(netTick++);

            // One-time: tell the reconciliator which server entity is ours.
            if (!serverEntitySet && netClient?.State == ConnectionState.Connected)
            {
                reconciliator?.SetServerEntity(netClient.AssignedEntityIndex, netClient.AssignedEntityVersion);
                serverEntitySet = true;
                Console.WriteLine($"[Client] Connected — server entity {netClient.AssignedEntityIndex}");
            }

            while (netClient is not null && netClient.TryDequeueSnapshot(out var snap))
            {
                reconciliator?.Apply(snap);

                // Advance the interpolation clock once per snapshot.
                double serverTimeMs = snap.TickId * (1000.0 / 30.0);
                remoteInterp?.UpdateClock(serverTimeMs, snap.ReceivedMs);

                // Update or create visual entities for remote players.
                if (netClient.AssignedEntityIndex >= 0)
                {
                    foreach (var entry in snap.Entries)
                    {
                        // Skip our own server entity — reconciliator handles it.
                        if (entry.EntityIndex == netClient.AssignedEntityIndex &&
                            entry.EntityVersion == netClient.AssignedEntityVersion)
                            continue;

                        var key = (entry.EntityIndex, entry.EntityVersion);
                        float rx = (entry.Mask & ChangeMask.PositionX) != 0 ? entry.PositionX.ToFloat() : 0f;
                        float ry = (entry.Mask & ChangeMask.PositionY) != 0 ? entry.PositionY.ToFloat() : 0f;

                        if (!remoteEntities.TryGetValue(key, out var remoteId))
                        {
                            // First time we see this entity: create it with a placeholder position.
                            // The interpolation system will move it to the correct place once it
                            // has accumulated enough samples.
                            remoteId = world.CreateEntity();
                            world.Add(remoteId, new Transform(new Vector2f(rx, ry)));
                            world.Add(remoteId, new RenderRect(new Vector2f(32f, 32f), new Color4(0f, 0.5f, 1f, 0.8f)));
                            remoteEntities[key] = remoteId;
                        }

                        // Push the authoritative sample into the interpolation buffer.
                        // The system lerps between samples each render frame instead of
                        // warping Transform directly on every snapshot arrival.
                        remoteInterp?.PushSample(remoteId, serverTimeMs, rx, ry);
                    }
                }

                if (reconciliator is not null &&
                    (reconciliator.LastErrorX.RawValue != 0 || reconciliator.LastErrorY.RawValue != 0))
                    Console.WriteLine($"[Predict] tick={snap.TickId} ackSeq={snap.AckSeq} errX={reconciliator.LastErrorX} errY={reconciliator.LastErrorY}");
            }

            // Periodic debug log every 90 frames (~3 s at 30 Hz).
            if (netClient is not null && netClient.State == ConnectionState.Connected && ++debugLogTick % 90 == 0)
            {
                int pending = reconciliator is not null ? history.PendingCount(reconciliator.LastAckedSeq) : 0;
                ushort sent = netInputSystem?.LastSentSeq ?? 0;
                ushort acked = reconciliator?.LastAckedSeq ?? 0;
                float errTiles = reconciliator is not null
                    ? MathF.Sqrt(MathF.Pow(reconciliator.LastErrorX.ToFloat(), 2) +
                                 MathF.Pow(reconciliator.LastErrorY.ToFloat(), 2)) / 32f
                    : 0f;
                Console.WriteLine($"[Debug] RTT={netClient.LatestRttMs}ms | SentSeq={sent} AckedSeq={acked} Pending={pending} | Err={errTiles:F3}tiles");
            }
            loop?.Frame();
        };

        window.OnInput = ks => input.Update(ks);

        window.Run();
        netClient?.Dispose();
    }
}