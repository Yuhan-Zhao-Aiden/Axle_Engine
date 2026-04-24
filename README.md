# Axle Engine

![Logo](./docs/image/Frame%201.svg)
![Axle Engine](./docs/image/Screenshot%202026-03-29%20005013.png)

Axle Engine is a 2D game engine in C# built around OpenTK. The project is aimed at authoritative multiplayer games, with deterministic simulation, a lightweight ECS core, and a clean split between simulation, rendering, and platform concerns.

The current focus is MVP infrastructure rather than content tooling or advanced graphics. The design prioritizes correctness, fixed-step simulation, and architecture that can support prediction, reconciliation, interpolation, and replication later.

## Architecture

- `Axle.Core`: foundational utilities, timing, data structures, and math primitives
- `Axle.Ecs`: entity lifecycle, component storage, queries, and deferred structural changes through a `CommandBuffer`
- `Axle.Platform`: platform-facing abstractions intended to stay independent from rendering and simulation
- `Axle.Graphics`: OpenTK/OpenGL rendering code, window host, camera, and quad rendering
- `Axle.Sim`: simulation layer intended to own deterministic gameplay logic on a fixed timestep
- `demos/`: executable samples for a client and server host

Key MVP decisions:

- server authoritative multiplayer model
- deterministic simulation using fixed-step updates
- ECS-driven game state with sparse-set component storage
- deferred structural changes applied at stage boundaries
- fixed-point math for authoritative simulation state
- rendering isolated from simulation so headless server execution remains possible

## Current Progress

What is in place now:

- ECS foundations: `World`, `EntityId`, component stores, dense queries, and join queries
- Deferred mutation path: `CommandBuffer` with playback for create, destroy, add, and remove
- Core loop and timing: fixed-step `EngineLoop` targeting 30 Hz simulation
- Rendering bootstrap: OpenTK window host, camera, and batched quad renderer
- Demo client: basic scene creation and rectangle rendering through ECS components
- Test coverage for core ECS behavior, sparse-set storage, command buffer playback, and `Fixed32`

What is still early or incomplete:

- `Axle.Sim` and `Axle.Platform` are still mostly placeholders
- demo server is currently a stub
- networking, replication, prediction, reconciliation, and map loading are planned architecture, not implemented systems yet
- fixed-point math exists, but parts of the spec-driven surface are still being tightened up

At the moment the repository is strongest in engine foundations: data structures, ECS rules, fixed-step flow, and a minimal rendering path. The higher-level multiplayer stack is still being built on top of that base.
