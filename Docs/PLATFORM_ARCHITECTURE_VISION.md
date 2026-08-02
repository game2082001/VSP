# Platform Architecture Vision

Version: 1.0

Status: Draft — Pending Product Owner Review

Date: 2026-07-29

---

# Purpose

This document defines the long-term platform architecture for VSP.

This is a product architecture vision, not an implementation specification.

It describes the target platform after all future Epics have been completed.

It does not define APIs, classes, database schema, or network protocols. Those belong to future ADRs and Task Specs, each requiring its own Product Owner approval before Coding begins, per `Docs/DEVELOPMENT_ROLES.md`.

Related documents: `Docs/00_MASTER_PLAN.md`, `Docs/00_ARCHITECTURE_VISION.md`, `Docs/03_PRODUCT_ROADMAP.md`, `Docs/DECISIONS/ADR-002_MEDIA_PIPELINE_ARCHITECTURE.md`, and the 2026-07-28 Distributed Platform Architecture Review. This document extends those toward the distributed target; it does not replace them.

---

# 1. Product Vision

VSP is a distributed video surveillance and device management platform.

VSP manages cameras and, over time, other security and IoT devices, across single-site and multi-site deployments.

VSP separates where video is watched from where video is recorded and analyzed.

VSP is built to scale from a single machine to a distributed, multi-server, multi-client platform without a rewrite.

---

# 2. Core Platform

The Core Platform is the set of capabilities every deployment has, regardless of size:

- Device / Camera Management
- Driver Framework
- Discovery
- Live View
- Recording
- Playback
- Dashboard / System Status
- User and Role Management

The Core Platform is always present. Optional capability is layered on top of it (see §3).

The Core Platform must behave the same way for the operator whether it is running on a single machine (§17, Standalone) or across a fully distributed deployment.

---

# 3. Optional Plugin Architecture

Beyond the Core Platform, VSP supports optional capability delivered as plugins:

- Additional device drivers (vendor-specific cameras, NVRs, access control, IoT)
- AI Analysis modules (motion, face, vehicle, ANPR, custom models)
- Event Center modules (alarm rules, notification channels)
- Future integrations (CMS federation, cloud relay, third-party systems)

A plugin is a unit of optional capability that:

- Can be installed without modifying the Core Platform
- Can be licensed independently of installation
- Consumes no runtime resources when not installed or not licensed (see §19)

Plugins do not change Core Platform behavior for operators who do not install them.

---

# 4. Client / Server Architecture

VSP separates Server responsibilities from Client responsibilities.

Server responsibilities: device connectivity, recording, AI analysis, storage, licensing, orchestration, and the authoritative system state.

Client responsibilities: presentation and operator interaction — nothing a Client does is required for the rest of the system to keep working while no Client is connected.

A Client never owns recording, AI analysis, or the authoritative device registry. A Client always connects to a Server.

This applies uniformly to Windows, Web, and Mobile Clients (§8–10).

---

# 5. Management Server

The Management Server is the authority for the platform.

Responsibilities:

- Device / camera registry
- User, role, and permission management
- License and entitlement authority
- Discovery orchestration
- Event and metadata aggregation
- Dashboard and system-wide status
- The single point every Client and every other Server registers with

There is exactly one logical Management Server per deployment — a single instance in smaller deployments, a highly available cluster in larger ones (see §17–18). "Logical" does not mean "un-scalable"; it means every other role treats it as the one authority, however it is made resilient.

---

# 6. Recording Server

A Recording Server owns live camera connections and recorded media for the cameras assigned to it.

Responsibilities:

- Opening and maintaining camera sessions
- Recording (continuous today; scheduled and motion-triggered in future)
- Storage and retention of recorded media
- Serving Live View and Playback streams to Clients
- Reporting capacity and health to the Management Server

A deployment may run zero, one, or many Recording Servers. Cameras are distributed across Recording Servers; no Client opens a camera connection directly.

---

# 7. AI Analysis Server

An AI Analysis Server performs video analysis (motion, face, vehicle, ANPR, and future models) against a media feed supplied by a Recording Server.

Responsibilities:

- Consuming a media feed for analysis
- Producing events and metadata — never modifying the media itself
- Reporting results into the platform's event flow (see §15)

An AI Analysis Server is optional. If no AI capability is licensed, no AI Analysis Server needs to run at all (see §19).

AI Analysis Servers typically require dedicated (GPU) hardware and scale independently of Recording Servers.

---

# 8. Windows Client

The Windows Client is a full-featured operator application for desktop use.

It presents device management, Live View, Playback, Dashboard, and administrative screens.

It connects to a Management Server — directly or via an API Gateway — and to Recording Servers for media.

It owns no authoritative data and no recording or AI workload.

---

# 9. Web Client

The Web Client provides browser-based access to VSP for operators who do not need or want a native application.

It offers a subset or full set of Windows Client capability, prioritized by operator need rather than technical parity.

It connects the same way any other Client does: through the Management Server's API surface, never directly to a Recording Server's or camera's internals.

---

# 10. Mobile App

The Mobile App (iOS and Android) provides remote access to Live View, Playback, Dashboard, and alerts, for operators away from a desktop.

It connects exclusively through VSP Server APIs (see §11). It never connects directly to a Recording Server, an AI Analysis Server, or a camera.

Mobile is expected to operate over constrained, variable networks; media delivered to Mobile may be adapted for that environment rather than delivered unchanged.

---

# 11. API Gateway

The API Gateway is the boundary between external clients (Mobile, Web, third-party integrations) and the Server platform.

Responsibilities:

- Authentication and session issuance
- Routing requests to the Management Server and, where appropriate, Recording/AI Analysis Servers
- Adapting protocols and media delivery to the needs of the calling client
- Protecting internal servers from direct external exposure

The Windows Client may connect directly to a Management Server in simpler deployments, or through the same API Gateway in larger ones — this is a deployment choice, not a platform requirement.

---

# 12. Plugin Runtime

The Plugin Runtime is the mechanism by which optional capability (§3) is installed, discovered, and activated.

Principles:

- A plugin is inert until installed.
- An installed plugin is inert until licensed.
- The Plugin Runtime is the only part of the platform that knows how to load a plugin — the Core Platform never hardcodes knowledge of an optional capability's existence.

The Plugin Runtime applies wherever optional capability runs: driver plugins on a Recording Server, AI modules on an AI Analysis Server, or future modules on the Management Server.

---

# 13. Licensing Model

Licensing determines what capability is entitled to run, independent of what is installed.

Principles:

- The Management Server is the authority for license state.
- A license governs activation, not installation — software may be present without being active.
- License state changes (grant, revoke, expiry) take effect without requiring a full system redeploy.
- Licensing applies at the level of a capability (a driver, an AI module, a feature), not only at the level of the whole product.

This document does not define license tiers, pricing, or enforcement mechanisms — those are product/business decisions for a future document.

---

# 14. Data Ownership

Every piece of platform state has exactly one owning Server role:

- **Management Server** owns: device/camera registry, users/roles, licenses/entitlements, discovery history, event and metadata records, system configuration.
- **Recording Server** owns: recorded media and whatever index is needed to locate it.
- **AI Analysis Server** owns: no durable state — it is a processing role, not a system of record.
- **Clients** own: no durable platform state. Local caching for responsiveness is permitted; it is never authoritative.

No two Server roles own the same data. A Client is never the source of truth for anything.

---

# 15. Event Flow

Events (Discovery results, recording lifecycle, AI detections, license changes, server health) flow from the Server role that produces them toward the Management Server, which is the aggregation point.

Clients subscribe to events through the Management Server or API Gateway; Clients produce requests, not authoritative events.

Event flow is decoupled from media flow (§16): an event describes something that happened; it never carries the media itself.

---

# 16. Media Flow

Media (live and recorded video) flows from a camera to the Recording Server responsible for it, and from that Recording Server to whichever Client or AI Analysis Server needs it.

A camera has exactly one owning Recording Server at a time. Any number of Clients may view the same camera without each opening a separate connection to the camera itself.

Media flow into AI Analysis is one-directional: analysis consumes media and produces events; it does not alter or re-inject media into the flow.

Media delivered to a Client may be adapted (for example, for a constrained mobile network) without changing the underlying recording.

---

# 17. Deployment Models

VSP supports more than one deployment shape without changing the platform's architecture:

- **Standalone** — every Server role and the Client run on one machine. Suited to a single small site.
- **Local Server / Client** — Server roles run as a background service on one machine; one or more Clients on that machine or the local network connect to it.
- **Distributed** — Management, Recording, and AI Analysis run as independent services on separate machines, scaled independently; Clients connect over a wider network, optionally through an API Gateway.
- **Multi-site** — multiple Recording Servers (and optionally local Management capability) across sites, reporting to a central Management Server.

A given deployment may sit anywhere on this spectrum. The platform does not treat the largest or the smallest shape as "the real one."

---

# 18. Scalability Strategy

Scalability is achieved by adding more of a Server role, not by growing a single instance without bound:

- More cameras → add Recording Servers, redistribute camera ownership.
- More analysis load → add AI Analysis Servers.
- More concurrent viewers → Recording Servers serve many Clients from one camera session; Client count does not multiply camera connections.
- More sites → add Recording Servers per site, reporting to a shared or federated Management layer.

The Management Server is the one role expected to stay singular per deployment (made resilient, not horizontally partitioned), since every other role and every Client depends on it as the authority.

---

# 19. Zero Resource Principle

An optional capability that is not installed consumes no resources.

An installed capability that is not licensed consumes no meaningful runtime resources beyond what is needed to recognize that it is unlicensed.

This principle governs the Plugin Runtime (§12) and Licensing Model (§13) together: installation and licensing are both gates, and failing either gate must be close to free — not merely hidden in the UI while still running underneath.

This applies at every scale: a Recording Server with no AI license installed should look, to the operating system, like a Recording Server with no AI workload at all.

---

# 20. Distributed by Design Principle

VSP is designed as a distributed system from the Core Platform outward, even when deployed on a single machine.

This means:

- Every Server role in this document (§5–7) is a logical role first. It may be co-located with another role in a small deployment, but it is never assumed to be.
- Boundaries between roles (Management / Recording / AI Analysis) are treated as real interfaces, not internal implementation convenience, regardless of whether they currently cross a process or network boundary.
- A capability built for Standalone deployment must not assume it will always run co-located with any other capability.

"Distributed by Design" does not mean every deployment must be distributed. It means nothing in the platform's design silently assumes it isn't.

---

# 21. Security Principles

- Every Client connection and every Server-to-Server connection is authenticated.
- The Management Server is the authority for identity, roles, and permissions; no other role independently decides who is allowed to act.
- A Client or external integration never has direct, unmediated access to a camera, to recorded media storage, or to another Server's internals.
- Licensing and security are separate concerns: a licensed capability is not automatically an unauthenticated one.
- Least privilege applies between Server roles as much as between Clients and Servers — a Recording Server does not require, and should not hold, capability belonging to the Management Server's authority.

This section states principles, not mechanisms. Specific authentication/authorization approaches are a future ADR concern.

---

# 22. Future Evolution

This vision anticipates, without committing to a specific order beyond what is already sequenced in the product roadmap and prior architecture reviews:

- Growth from a single Recording Server to many, across one or many sites.
- Growth from no AI capability to one or more licensed AI Analysis modules.
- Growth from Windows-only to Windows, Web, and Mobile Clients on equal footing.
- Growth from a fixed built-in driver set to a runtime plugin ecosystem, potentially including third-party drivers.
- Federation between independently managed VSP deployments (multi-site, multi-tenant), if and when the product direction calls for it.

Nothing in this document commits to building all of the above by any date. It describes the shape the platform grows into, not a schedule.

---

# 23. Product Design Principles

- Specification Driven Development, Architecture First, Documentation First, Review Before Completion — unchanged from existing VSP practice.
- Prefer one clear owner per responsibility over shared ownership between roles.
- Prefer a capability being absent over a capability being present but silently degraded.
- Prefer boundaries that hold under distribution over boundaries that only happen to work because everything runs in one process today.
- Extend, don't redesign, features that already work — this vision guides what is added next, not a mandate to rebuild what already exists.
- Keep it simple: a deployment that doesn't need a capability should not have to pay for it, configure it, or reason about it.

---

# 24. What VSP Is

- A platform for managing, viewing, recording, and analyzing video from cameras and, over time, related security/IoT devices.
- A system designed to run at any scale, from a single machine to a distributed, multi-site deployment.
- A platform where optional capability is genuinely optional — installable, licensable, and free of cost when unused.
- A system with one authoritative source of truth (the Management Server) and clearly separated Server roles around it.
- A product that grows its architecture deliberately, one incremental migration at a time.

---

# 25. What VSP Is Not

- Not a single desktop application that happens to have a lot of features — the desktop application is one Client among several the platform will support.
- Not a platform where every deployment must run every Server role — a small deployment running one process is a deployment choice, not a lesser version of the product.
- Not a platform that assumes unlimited hardware — the Zero Resource Principle (§19) and Scalability Strategy (§18) exist because resources are finite and must be spent deliberately.
- Not a rewrite of the current implementation — this document describes a destination; the migration path is incremental, and existing, working features are not redesigned to reach it.
- Not an implementation specification — this document defines no API, no class, no schema, and no protocol. Those belong to future ADRs and Task Specs, each requiring its own Product Owner approval before work begins.

---

# Status

This is a Vision document, pending Product Owner review.

No ADR, Task Plan, or implementation work may begin from this document alone. Each future Epic it anticipates requires its own approval before any Coding step begins, per `Docs/DEVELOPMENT_ROLES.md`.
