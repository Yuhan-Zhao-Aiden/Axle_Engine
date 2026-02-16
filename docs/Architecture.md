# Axle Engine - Reference Graph

This doc defines project dependencies inside Axle Engine

The goal is: 
- Server can run headless (no graphics deps)
- Networking independent from rendering
- Simulation deterministic and platform-agnostic
- Clean architectural boundaries

---

# 1. Dependency Direction Rule

If:

    A -> B

Then:

- Project **A references B**
- B must NOT reference A
- Dependencies must flow **downward toward foundational libraries**

---

# 2. Layer Overview
```
Hosts (Executables)
│
├── Application Layer
│ ├── Axle.Sim
│ ├── Axle.Replication
│ └── Axle.Netcode.Client
│
├── Infrastructure Layer
│ ├── Axle.Ecs
│ ├── Axle.Net
│ ├── Axle.Graphics
│ └── Axle.Platform
│
└── Foundation
└── Axle.Core
```

---

# 3. Project Dependency Rules

---

## Foundation

### Axle.Core

(No reference)


**Contains:**
- Math primitives (Vec2, Rect, etc.)
- Time abstractions
- ID types
- Shared utilities
- Base interfaces

**Hard rule:** Must not reference any other Axle project.

---

## Infrastructure Layer

### Axle.Ecs

- Axle.Ecs -> Axle.Core


**Contains:**
- World
- EntityId
- Component storage
- Query logic
- System scheduling

Must NOT reference:
- Sim
- Graphics
- Net
- Platform

---

### Axle.Platform

- Axle.Platform -> Axle.Core


**Contains:**
- IWindow abstraction
- IInput abstraction
- OpenTK implementation

Must NOT reference:
- Graphics
- Sim
- ECS
- Net

---

### Axle.Graphics

- Axle.Graphics -> Axle.Core
- Axle.Graphics -> Axle.Platform

**Contains:**
- OpenGL wrappers
- SpriteBatch
- Renderer
- Camera
- Render command execution

Must NOT reference:
- Sim
- ECS
- Net
- Netcode

---

### Axle.Net

- Axle.Net -> Axle.Core

**Contains:**
- INetTransport
- PacketReader / PacketWriter
- Peer identity
- Channel abstraction

Must NOT reference:
- ECS
- Sim
- Graphics
- Platform

---

## Application Layer

### Axle.Sim

- Axle.Sim -> Axle.Core
- Axle.Sim -> Axle.Ecs

**Contains:**
- Gameplay components
- Gameplay systems
- Fixed timestep simulation logic

Must NOT reference:
- Graphics
- Platform
- Netcode.Client

---

### Axle.Replication

- Axle.Replication -> Axle.Core
- Axle.Replication -> Axle.Net
- Axle.Replication -> Axle.Ecs
- Axle.Replication -> Axle.Sim


**Contains:**
- Snapshot model
- Delta compression
- Component replication rules

Must NOT reference:
- Graphics
- Platform

---

### Axle.Netcode.Client

- Axle.Netcode.Client -> Axle.Core
- Axle.Netcode.Client -> Axle.Net
- Axle.Netcode.Client -> Axle.Replication
- Axle.Netcode.Client -> Axle.Ecs
- Axle.Netcode.Client -> Axle.Sim

**Contains:**
- Client prediction
- Reconciliation
- Interpolation
- Command buffering

Must NOT reference:
- Graphics
- Platform

---

## Support Libraries

### Axle.Assets

- Axle.Assets -> Axle.Core

**Contains:**
- Asset loading
- File parsing
- Atlas definitions

Should avoid direct Graphics dependency.
If GPU upload is required, expose it via interfaces.

---

### Axle.Debug

- Axle.Debug -> Axle.Core
- (optional) Axle.Debug -> Axle.Graphics

**Contains:**
- Metrics
- Frame timing
- Debug overlays

Graphics dependency allowed only if drawing overlays.

---

# 4. Executables (Hosts)
