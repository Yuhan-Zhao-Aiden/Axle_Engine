using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Graphics.OpenGL4;

namespace Axle.Graphics;

public class WindowHost : GameWindow 
{
    private double _accum;
    private int _frames;

    /// <summary>The quad renderer. Valid after the window has loaded.</summary>
    public QuadRenderer Renderer { get; private set; } = null!;

    /// <summary>
    /// Called once after the GL context and <see cref="Renderer"/> are ready,
    /// before the first render frame. Use this to initialise systems that
    /// need a valid <see cref="QuadRenderer"/>.
    /// </summary>
    public Action? OnReady { get; set; }

    /// <summary>
    /// Called each render frame before GL.Clear, with the current keyboard state.
    /// Use this to feed input into simulation systems.
    /// </summary>
    public Action<KeyboardState>? OnInput { get; set; }

    /// <summary>
    /// Called each render frame after GL.Clear and before SwapBuffers.
    /// Use this to drive an <see cref="Axle.Core.EngineLoop"/> — it handles
    /// both simulation ticks and rendering internally.
    /// </summary>
    public Action? OnFrame { get; set; }

    /// <summary>
    /// Called each render frame after GL.Clear and before SwapBuffers.
    /// Receives a <see cref="Camera"/> built from the current viewport.
    /// The client render system should call Renderer.Begin/DrawQuad/End here.
    /// </summary>
    public Action<Camera>? OnRender { get; set; }

    public WindowHost(int width = 1280, int height = 720, string title = "Axle Engine")
    : base(GameWindowSettings.Default, new NativeWindowSettings
    {
        ClientSize = new Vector2i(width, height),
        Title = title
    })
    {
        VSync = VSyncMode.Off;
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        Renderer = new QuadRenderer();
        OnReady?.Invoke();
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        OnInput?.Invoke(KeyboardState);

        GL.ClearColor(0.08f, 0.09f, 0.10f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        OnFrame?.Invoke();
        OnRender?.Invoke(new Camera(ClientSize.X, ClientSize.Y));

        SwapBuffers();

        _accum += args.Time;
        _frames++;

        if (_accum >= 1.0)
        {
            var fps = _frames/_accum;
            Title = $"FPS: {fps:F1}";
            _accum = 0;
            _frames = 0;
        } 
    }

    protected override void OnUnload()
    {
        Renderer.Dispose();
        base.OnUnload();
    }
}
