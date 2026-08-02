# Product Capability Matrix

Version: 1.0

Status: Draft — Pending Product Owner Review

Date: 2026-07-29

---

# Purpose

This document defines VSP as a **Video Intelligence Platform** organized by capability rather than by screen, module, or codebase folder.

This is a product planning document. It does not define APIs, classes, database schema, network protocols, or UI screens. It does not create ADRs and it authorizes no implementation work by itself. Every capability marked below as anything other than **Implemented** requires its own Spec, Task Plan, and Product Owner approval before any Coding step begins, per `Docs/DEVELOPMENT_ROLES.md` and `Docs/AI_DEVELOPMENT_WORKFLOW.md`.

Related documents: `Docs/PLATFORM_ARCHITECTURE_VISION.md` (platform roles, principles, deployment models), `Docs/03_PRODUCT_ROADMAP.md` (what has actually shipped, by version), `Docs/DECISIONS/ADR-002_MEDIA_PIPELINE_ARCHITECTURE.md` (media pipeline contracts and evolution stages), `Docs/DECISIONS/ADR-003_MEDIA_LIBRARY_SELECTION.md` (FFmpeg selection). This document restates nothing from those documents that it can instead reference; where this matrix and any of those four disagree, those four win.

---

# 1. Capability Philosophy

### Capability First Principle

VSP is organized around **capabilities**, not menus, screens, or source folders. A capability is a coherent unit of product value (e.g., "Motion Recording," "Face Recognition") that can be described, owned, licensed, and evolved independently of how any particular Client happens to present it today. Two Clients may expose the same capability through completely different UI without that being two capabilities.

### Service Ownership Principle

Every capability has exactly one owning Server role — Management Server, Recording Server, or AI Analysis Server — consistent with `PLATFORM_ARCHITECTURE_VISION.md` §5–7 and §14 (Data Ownership). No capability is jointly owned by two Server roles. A Client never owns a capability's authoritative state; a Client's involvement in a capability is presentation and operator interaction only (Vision §4).

### Zero Resource Principle

A capability that is not installed consumes no resources. A capability that is installed but not licensed consumes no meaningful runtime resource beyond recognizing that it is unlicensed. This is a restatement of Vision §19 and governs the "Resource Impact When Disabled" column below — it is a design target for every capability, not a claim that today's implementation already validates it everywhere.

### Build Once, Deploy Anywhere, Enable by Capability

A capability's implementation does not change based on deployment shape (Standalone, Local Server/Client, Distributed, Multi-site — Vision §17) or scale (Vision §18, §20 Distributed by Design). What changes across deployments and customers is **which capabilities are installed and licensed**, not how any single capability is built. Enabling a capability for a customer is a licensing/installation decision, never a separate code branch or a rebuild.

---

# 2. Capability Categories

| Category | Description | Primary Vision Reference |
|---|---|---|
| Core Platform | Present in every deployment regardless of size; the platform's baseline | Vision §2 |
| Media | Protocol- and pipeline-level capability that moves and decodes video | Vision §16, ADR-002 |
| Recording | Capability governing what gets recorded and how it is retained | Vision §6 |
| Playback | Capability governing how recorded media is retrieved and reviewed | ADR-002 evolution (v3) |
| AI / Analytics | Capability that analyzes media and produces metadata, never modifies media | Vision §7, ADR-002 (`IAiPipeline`, `IMetadataBus`) |
| Integration | Capability connecting VSP to external systems and devices | Vision §3 |
| Enterprise | Deployment-scale and operational capability (servers, HA, multi-site, audit) | Vision §5–7, §17–18 |
| Mobile / Client | Capability delivered through a specific Client surface | Vision §8–10 |

---

# 3. How to Read the Matrix

- **Core or Plugin** — *Core* capability is part of the baseline platform (Vision §2) and is not separately licensed. *Plugin* capability is optional (Vision §3): installable independently, licensable independently, and required to consume zero resources when absent (Vision §19).
- **Owner Service** — the single Server role that owns this capability's authoritative state or execution, per Service Ownership Principle above. Where a Server role is itself not yet split out as an independently deployable service (see Enterprise category), the owner is stated as the *logical* role it will become.
- **Client Role** — what a Client is permitted to do regarding this capability. Per Vision §4, a Client never owns recording, AI analysis, or the authoritative device/user/license registry.
- **Server Role** — what the owning Server does for this capability.
- **Mobile Support** — whether the Mobile App is expected to expose this capability, per the Mobile App's scope in Vision §10. `Future` means anticipated but not yet designed for Mobile specifically.
- **License Controlled** — whether this capability is intended to be gated by the Licensing Model (Vision §13) once that model is implemented. Because License Management itself is **Not Started** (see matrix below), no capability is actually license-enforced today regardless of what this column says — this column states intent, not current enforcement.
- **Resource Impact When Disabled** — the Zero Resource Principle target for this capability. Stated as a target in all cases; only capabilities marked `Implemented` or `Partial` have any real runtime behavior to validate it against today.
- **Current Status** — one of the values defined in §4 below. This is the honesty-critical column; do not read Owner Service / Client Role / Server role as implying a capability exists — those columns describe the target design regardless of status.
- **Future Dependencies** — what has to exist first before this capability can move forward, referencing ADR-002's evolution stages (v1–v8) or other capabilities in this matrix where relevant.

---

# 4. Status Legend

| Status | Meaning |
|---|---|
| **Implemented** | Shipped, in the current product, per `Docs/03_PRODUCT_ROADMAP.md` |
| **Partial** | Some part shipped; the rest is explicitly Planned/Future/Not Started |
| **Planned** | On the roadmap (`03_PRODUCT_ROADMAP.md` Version 2.0–4.0) but unscheduled; no task breakdown or code exists |
| **Future** | Anticipated by `PLATFORM_ARCHITECTURE_VISION.md` or ADR-002's evolution table, but not yet on the roadmap as a numbered version |
| **Not Started** | No design or implementation work has begun |

No capability below is marked `Implemented` unless `03_PRODUCT_ROADMAP.md` says it shipped. Where this matrix's judgment and the roadmap could be read to disagree, the roadmap is authoritative.

---

# 5. Capability Matrix

## 5.1 Core Platform

| Capability | Core/Plugin | Owner Service | Client Role | Server Role | Mobile | License Controlled | Resource Impact When Disabled | Status | Future Dependencies |
|---|---|---|---|---|---|---|---|---|---|
| Camera Management | Core | Management Server | View/edit camera registry via Server | Owns camera registry (Vision §5) | Future | No | N/A — Core, always present | **Implemented** (v1.1) | — |
| Driver Framework | Core | Management Server (registry/framework); Recording Server (execution against owned cameras) | N/A | Loads/executes drivers | N/A | No (framework) / Yes for individual vendor drivers as plugins | N/A — Core, always present | **Partial** — framework and RTSP/ONVIF drivers implemented; Hikvision/Dahua/Axis drivers unimplemented | Vendor-specific drivers depend on Plugin Runtime maturity |
| Discovery | Core | Management Server | Trigger/view discovery results | Orchestrates ONVIF WS-Discovery, RTSP probe, network scan | Future | No | N/A — Core, always present | **Partial** (v1.3) — discovery runs; no persisted discovery history retrievable (Epic-009 gap) | Dashboard/history persistence not yet designed |
| Live View | Core | Recording Server | Renders decoded frames | Owns camera session, decode, dispatch | Future | No | N/A — Core, always present | **Implemented** (Epic-010) — RTSP session, decode, render, bounded reconnect | Recording Server role split (ADR-002 v5) for headless operation |
| Recording | Core | Recording Server | Start/stop control (Continuous only today) | Owns encoded-tier capture and storage | Future | No | N/A — Core, always present | **Partial** (Epic-011) — Continuous stream-copy only; Scheduled/Motion/Alarm modes not started | See Recording category below |
| Playback | Core | Recording Server | Requests/views recorded media | Serves file-backed media session | Future | No | N/A — Core, always present | **Not Started** — ADR-002 v3; `IMediaClock.Seek` not yet meaningful | File-backed `IMediaSession` (ADR-002 v3) |
| User / Role / Permission | Core | Management Server | Authenticates, acts within granted role | Sole authority for identity/roles (Vision §21) | Future | No | N/A — Core, always present | **Not Started** — Roadmap Version 4.0, planned/unscheduled | — |
| License Management | Core | Management Server | N/A | Sole authority for license state (Vision §13) | No | No (License Management gates other capabilities; it is not itself gated) | N/A — Core, always present | **Not Started** — Roadmap Version 4.0, planned/unscheduled | — |
| Plugin Runtime | Core | Cross-cutting — hosted by whichever Server role loads a given plugin (Vision §12) | N/A | Installs/discovers/activates optional capability | N/A | No | N/A — Core, always present | **Not Started** — described in Vision §12 only, no implementation | Blocks every Plugin-type capability below from being "installable" in the Vision §3 sense |
| Configuration | Core | Management Server | Views/edits allowed settings | Owns system configuration (Vision §14) | Future | No | N/A — Core, always present | **Not Started** as a distinct system-wide capability (driver/camera-level settings exist per-feature, e.g. Task-303) | — |

## 5.2 Media

| Capability | Core/Plugin | Owner Service | Client Role | Server Role | Mobile | License Controlled | Resource Impact When Disabled | Status | Future Dependencies |
|---|---|---|---|---|---|---|---|---|---|
| RTSP | Core | Recording Server | N/A | Ingests RTSP streams; only real driver besides ONVIF | Future | No | N/A — Core, always present | **Implemented** — `TestConnection` with Basic/Digest auth (Epic-003) | — |
| ONVIF | Core | Recording Server | N/A | `TestConnection` + `GetDeviceInformation` | Future | No | N/A — Core, always present | **Implemented** (Epic-007) | — |
| Media Pipeline | Core | Recording Server (encode/decode/dispatch); Client (render) | Consumes decoded frames | Owns Buffer Layer, Frame Dispatcher, `IMediaController` (ADR-002) | Future | No | N/A — Core, always present | **Partial** — v1 Live View + v2 Recording stages implemented; v3–v8 not started | ADR-002 Future Evolution table, v3–v8 |
| Streaming Gateway | Plugin | API Gateway (Vision §11 — not yet built) | Connects through gateway rather than directly to Recording Server | Adapts protocol/media delivery for external clients | Future | Yes (planned) | Zero — gateway not built, nothing runs | **Not Started** — Vision §11 describes the role only | API Gateway existence; Web/Mobile Client rollout |
| WebRTC | Plugin | Recording Server / API Gateway | Consumes low-latency stream | Transcodes/relays for browser delivery | Future | Yes (planned) | Zero if not installed (Zero Resource Principle) | **Not Started** | Streaming Gateway, Media Pipeline v6 (Transcoding) |
| HLS | Plugin | Recording Server / API Gateway | Consumes segmented stream | Packages segments for constrained/mobile delivery | Future | Yes (planned) | Zero if not installed | **Not Started** | Streaming Gateway, Media Pipeline v6 (Transcoding) |

## 5.3 Recording

| Capability | Core/Plugin | Owner Service | Client Role | Server Role | Mobile | License Controlled | Resource Impact When Disabled | Status | Future Dependencies |
|---|---|---|---|---|---|---|---|---|---|
| Continuous Recording | Core | Recording Server | Start/stop control, status indicator | Stream-copy recording on encoded tier | Future | No | N/A — Core, always present | **Implemented** (Epic-011) | — |
| Scheduled Recording | Core | Recording Server | Configure schedule | Executes `IRecordingMode` "Scheduled" strategy | Future | No | Zero if mode unused (no schedule configured) | **Not Started** | `IRecordingMode` strategy pattern exists (ADR-002); Scheduled mode not implemented |
| Motion Recording | Plugin | Recording Server (records); AI Analysis Server (detects) | View recording triggered by motion | `MotionTriggered` mode subscribing to `IMetadataBus` | Future | Yes (planned) | Zero if AI Analysis Server/Motion Detection not installed | **Not Started** — ADR-002 v4 | Motion Detection capability, `IAiPipeline`, `IMetadataBus` |
| Alarm Recording | Plugin | Recording Server; Management Server (event rules) | View recording triggered by alarm | Records on Event Center alarm rule match | Future | Yes (planned) | Zero if Event Center not installed | **Not Started** — Roadmap Version 2.0, planned/unscheduled | Event Center (Roadmap v2.0), Motion Recording pattern |
| Retention | Core | Recording Server | View retention policy | Enforces retention/expiry of recorded media | No | No | N/A — Core, always present | **Not Started** | Storage Management |
| Storage Management | Core | Recording Server (local); future Storage Server (distributed) | View storage status | Manages capacity, allocation, reporting to Management Server (Vision §6) | No | No | N/A — Core, always present | **Not Started** | Storage Server (Enterprise) for distributed case |

## 5.4 Playback

| Capability | Core/Plugin | Owner Service | Client Role | Server Role | Mobile | License Controlled | Resource Impact When Disabled | Status | Future Dependencies |
|---|---|---|---|---|---|---|---|---|---|
| Timeline Playback | Core | Recording Server | Requests time range, scrubs timeline | File-backed `IMediaSession`, meaningful `Seek` | Future | No | N/A — Core, always present | **Not Started** — ADR-002 v3 | File-backed `IMediaSession` |
| Search | Core | Recording Server (media index); Management Server (metadata) | Issues search query | Locates media by index | Future | No | N/A — Core, always present | **Not Started** | Timeline Playback |
| Bookmark | Core | Recording Server | Creates/views bookmarks | Persists bookmark reference into media index | Future | No | N/A — Core, always present | **Not Started** | Timeline Playback |
| Export | Core | Recording Server | Requests export, downloads result | Produces exported media file/clip | Future | No | N/A — Core, always present | **Not Started** (video clip export — distinct from the existing camera-list CSV export, Task-215) | Timeline Playback |
| Snapshot | Core | Recording Server | Requests snapshot | `ISnapshotService.CaptureAsync` (contract defined, ADR-002) | Future | No | N/A — Core, always present | **Not Started** — interface defined in ADR-002; not confirmed implemented in Epic-010 | Live View (present) |
| Smart Search | Plugin | Recording Server; AI Analysis Server (metadata source) | Issues metadata-driven query (e.g., "search by detected object") | Queries `IMetadataBus`-derived index | Future | Yes (planned) | Zero if no AI Analysis Server installed | **Not Started** / **Future** | AI / Analytics capabilities, `IMetadataBus`, Search |

## 5.5 AI / Analytics

All capabilities in this category are **Plugin**, owned by the **AI Analysis Server** (Vision §7), and depend on ADR-002 v4 (`IAiPipeline`, `IMetadataBus`) which is itself Not Started. None are scheduled — Roadmap Version 2.0 (Event Center) and Version 3.0 (AI Device Center) are both **Planned, unscheduled**, with no task breakdown.

| Capability | Core/Plugin | Owner Service | Client Role | Server Role | Mobile | License Controlled | Resource Impact When Disabled | Status | Future Dependencies |
|---|---|---|---|---|---|---|---|---|---|
| Motion Detection | Plugin | AI Analysis Server | Views detection overlay/events | Runs `IAiPipeline` stage, publishes `IMetadataBus` annotations | Future | Yes (planned) | Zero — no AI Analysis Server needed if unlicensed (Vision §7) | **Not Started** | `IAiPipeline`, `IMetadataBus` (ADR-002 v4) |
| Face Recognition | Plugin | AI Analysis Server | Views match results/alerts | Runs face-recognition pipeline stage | Future | Yes (planned) | Zero if uninstalled/unlicensed | **Not Started** | `IAiPipeline`; Motion Detection precedent |
| Vehicle Detection | Plugin | AI Analysis Server | Views detection results | Runs vehicle-detection pipeline stage | Future | Yes (planned) | Zero if uninstalled/unlicensed | **Not Started** | `IAiPipeline` |
| LPR / ANPR | Plugin | AI Analysis Server | Views plate-read results | Runs ANPR pipeline stage | Future | Yes (planned) | Zero if uninstalled/unlicensed | **Not Started** | `IAiPipeline`, Vehicle Detection precedent |
| People Counting | Plugin | AI Analysis Server | Views count/dashboard | Runs counting pipeline stage | Future | Yes (planned) | Zero if uninstalled/unlicensed | **Not Started** | `IAiPipeline` |
| Behavior Detection | Plugin | AI Analysis Server | Views behavior alerts | Runs behavior-analysis pipeline stage | Future | Yes (planned) | Zero if uninstalled/unlicensed | **Not Started** | `IAiPipeline`; likely depends on other detection capabilities first |
| Heatmap | Plugin | AI Analysis Server | Views heatmap overlay | Aggregates positional metadata over time | Future | Yes (planned) | Zero if uninstalled/unlicensed | **Not Started** | People Counting or Motion Detection metadata |
| PPE Detection | Plugin | AI Analysis Server | Views compliance alerts | Runs PPE-detection pipeline stage | Future | Yes (planned) | Zero if uninstalled/unlicensed | **Not Started** | `IAiPipeline` |
| Fire / Smoke Detection | Plugin | AI Analysis Server | Views alarm/alerts | Runs fire/smoke pipeline stage | Future | Yes (planned) | Zero if uninstalled/unlicensed | **Not Started** | `IAiPipeline`; likely feeds Alarm Recording |

## 5.6 Integration

All capabilities in this category are **Plugin** and **Not Started**. None appear on the numbered roadmap (Versions 1.0–4.0) yet; they are named in Vision §3 only as categories of future integration.

| Capability | Core/Plugin | Owner Service | Client Role | Server Role | Mobile | License Controlled | Resource Impact When Disabled | Status | Future Dependencies |
|---|---|---|---|---|---|---|---|---|---|
| Access Control | Plugin | Management Server | Views combined access/video events | Integrates external access-control system as a device source | No | Yes (planned) | Zero if uninstalled/unlicensed | **Not Started** | Driver Framework extension for non-camera devices |
| POS | Plugin | Recording Server (transaction/video correlation); Management Server (config) | Views transaction overlay on video | Correlates POS transaction feed with recorded media | No | Yes (planned) | Zero if uninstalled/unlicensed | **Not Started** | External API |
| E-map / GIS | Plugin | Management Server | Views camera positions on map | Serves map/geo metadata for registered devices | Future | Yes (planned) | Zero if uninstalled/unlicensed | **Not Started** | Camera Management (present) |
| Video Wall | Plugin | Client (Windows/Web); Management Server (layout config) | Renders multi-camera wall layout | Serves multiple concurrent Live View streams | No | Yes (planned) | Zero if uninstalled/unlicensed | **Not Started** | Live View (present), TV Wall client |
| Notification | Plugin | Management Server | Receives notification | Publishes event-driven notification (Vision §15 Event Flow) | Future | Yes (planned) | Zero if uninstalled/unlicensed | **Not Started** — Roadmap Version 4.0, planned/unscheduled | Event flow aggregation |
| External API | Plugin | API Gateway (Vision §11 — not yet built) | Third-party systems consume this, not an end-user Client | Exposes authenticated API surface | N/A | Yes (planned) | Zero if API Gateway not deployed | **Not Started** | API Gateway |

## 5.7 Enterprise

| Capability | Core/Plugin | Owner Service | Client Role | Server Role | Mobile | License Controlled | Resource Impact When Disabled | Status | Future Dependencies |
|---|---|---|---|---|---|---|---|---|---|
| Management Server | Core | Itself — the platform authority (Vision §5) | Registers with it | Device registry, users/roles, licensing, discovery orchestration, event aggregation | N/A | No | N/A — Core, always present in some logical form | **Partial** — the role exists today only as logic co-located inside the single desktop app; not yet split into an independently deployable service | Distributed deployment work (Vision §17 Distributed, §18) |
| Recording Server | Core | Itself (Vision §6) | Connects to it for Live View/Playback | Owns camera sessions, recording, storage, retention | N/A | No | N/A — Core, always present in some logical form | **Partial** — same as above; co-located, not yet independently deployable (ADR-002 v5 "zero renderer subscribers" not yet built) | ADR-002 v5 Recording Server stage |
| AI Analysis Server | Plugin | Itself (Vision §7) | N/A (server-to-server; no direct Client connection) | Consumes media feed, produces events/metadata, no durable state | N/A | Yes (planned) | Zero — does not need to run at all if no AI capability is licensed (Vision §7, §19) | **Not Started** | AI / Analytics capabilities, ADR-002 v4 |
| Storage Server | Plugin | Future distributed storage tier beyond Recording Server local storage | N/A | Serves as a Recorder target at scale | N/A | Yes (planned) | Zero if uninstalled/unlicensed | **Not Started** | Storage Management, Multi-site/Cluster deployment work |
| Audit Log | Core | Management Server | Views audit trail | Records administrative/security-relevant actions | No | No | N/A — Core, always present | **Not Started** — Roadmap Version 4.0, planned/unscheduled | User / Role / Permission |
| Backup / Restore | Core | Management Server | Triggers/monitors backup or restore | Backs up/restores platform state it owns (Vision §14) | No | No | N/A — Core, always present | **Not Started** — Roadmap Version 4.0, planned/unscheduled | — |
| Multi-site | Plugin | Management Server (central); Recording Servers (per site) | Switches between sites | Federates multiple Recording Servers to one Management Server (Vision §17) | Future | Yes (planned) | Zero if only one site configured | **Not Started** — Vision §17 deployment model, no task breakdown | Management Server / Recording Server split (above) |
| Cluster / HA | Plugin | Management Server (resilience); Recording Server (cluster relay) | Transparent to Client (no visible change) | `Remote` session relaying another node; cross-node metrics aggregation | N/A | Yes (planned) | Zero if deployment is not clustered | **Not Started** — ADR-002 v7 | Management Server / Recording Server split, Media Pipeline v7 |

## 5.8 Mobile / Client

| Capability | Core/Plugin | Owner Service | Client Role | Server Role | Mobile | License Controlled | Resource Impact When Disabled | Status | Future Dependencies |
|---|---|---|---|---|---|---|---|---|---|
| Windows Client | Core | Itself (Vision §8) | Full-featured desktop presentation; owns no authoritative data | Serves it via Management Server / Recording Server | No (this is the desktop client) | No | N/A — Core, always present | **Partial** — this is the current product; its feature set covers only what's marked Implemented/Partial elsewhere in this matrix, not the full Vision §8 scope | Every capability it surfaces individually |
| Web Client | Core | Itself (Vision §9) | Browser-based presentation, subset or full of Windows Client capability | Serves it via Management Server API surface only | No (distinct from Mobile) | No | N/A — Core, once built, always present | **Not Started** — Vision §9 | API Gateway or direct Management Server API exposure |
| Mobile App | Core | Itself (Vision §10) | Remote presentation: Live View, Playback, Dashboard, alerts | Serves it exclusively via Server APIs, never direct camera/Recording Server access (Vision §10) | Yes (this is the Mobile capability itself) | No | N/A — Core, once built, always present | **Not Started** — Vision §10 | API Gateway, adapted media delivery (WebRTC/HLS) |
| Tablet | Core | Itself | Presentation, likely a layout variant of Web Client or Mobile App | Same as Web Client / Mobile App | Yes | No | N/A — Core, once built, always present | **Not Started** | Web Client or Mobile App (whichever it extends) |
| TV Wall | Plugin | Client-side rendering of Video Wall integration | Renders fixed multi-camera layout for unattended display | Serves multiple concurrent Live View streams | No | Yes (planned) | Zero if uninstalled/unlicensed | **Not Started** | Video Wall (Integration), Live View (present) |

---

# 6. What This Document Is Not

- Not an API specification, database schema, or UI design — none of the above.
- Not a commitment to build every listed capability, or to build them in the order listed. Sequencing remains the responsibility of `Docs/03_PRODUCT_ROADMAP.md`.
- Not a claim that any capability marked `Not Started`, `Planned`, or `Future` exists in the product today.
- Not an ADR. No architectural decision is made or changed by this document; where a capability's future shape is already fixed by ADR-002 or ADR-003, this document only references that, it does not restate or reinterpret it.

---

# Status

This is a product planning document, pending Product Owner review.

No Task Plan, Spec, or implementation work may begin from this document alone. Each capability's move from `Not Started` / `Planned` / `Future` toward `Implemented` requires its own Spec and Task Plan approval, per `Docs/AI_DEVELOPMENT_WORKFLOW.md` and `Docs/DEVELOPMENT_ROLES.md`.
