# ADR-003 Media Library Selection

## Status

**Status:** Accepted

**Product Owner Decision:** FFmpeg

**Implemented by:** Epic-010 (Live View Foundation)

This decision was made with the full comparison below presented neutrally — no library was recommended. FFmpeg is evaluated and adopted specifically against the contracts fixed by [ADR-002 Media Pipeline Architecture](./ADR-002_MEDIA_PIPELINE_ARCHITECTURE.md), which remains unchanged by this decision.

Implementation note (Epic-010): the media library decision is FFmpeg itself, not a specific .NET binding package — Epic-010's own implementation phase found that `FFmpeg.AutoGen.Bindings.DynamicallyLoaded` (the binding style anticipated when this ADR was written) throws `NotSupportedException` on every call in this environment, and switched to `FFmpeg.AutoGen.Bindings.DynamicallyLinked` (compile-time `DllImport`) instead. This is a binding-layer implementation detail within the already-approved FFmpeg package family, not a reopening of this ADR's decision. See Docs/CHANGELOG.md (Version 1.14) for the full root-cause record.

---

## Context

ADR-002 defines the media pipeline contracts (`IMediaSession`, `IMediaController`, `IMediaClock`, Frame Dispatcher, Buffer Layer, `IFrameRenderer`, `IRecordingSession`, `IAiPipeline`, `IMetadataBus`) independent of any specific library, so a candidate could be evaluated against a fixed, deliberate target rather than shaping the abstraction around whichever library was chosen first. Four candidates were evaluated: FFmpeg, LibVLC, GStreamer, and Windows Media Foundation.

---

## Evaluation Summary

### Seam-by-seam fit (from the initial comparison)

| Criterion | FFmpeg | LibVLC | GStreamer | Media Foundation |
|---|---|---|---|---|
| RTSP ingest maturity | Very strong | Strong (uses FFmpeg internally) | Strong | Weak/inconsistent |
| PTS/DTS exposed distinctly | Yes, native `AVPacket` fields | Limited — internal clock model | Yes, native `GstBuffer` fields | Presentation-oriented, DTS not cleanly separated |
| Encoded-tier passthrough recording | Yes, stream-copy muxing | Coarser, own transcode/output chain | Yes, `tee` + muxer | Yes, Sink Writer stream-copy |
| Controller fit (pause/resume/reconnect) | Must build state machine | Best native fit | State machine maps directly | Native session pause/stop |
| WPF renderer integration | Manual (swscale → WriteableBitmap) | Best — official WPF control | Manual, no first-class WPF control | Strong — documented D3DImage/EVR pattern |
| Hardware acceleration | Yes | Yes, automatic | Yes, less turnkey on Windows | Yes, OS-native |
| .NET binding maturity | Good (`FFmpeg.AutoGen`) | Best (official, WPF samples) | Weakest | No first-party managed wrapper |
| Deployment footprint | Moderate | Moderate | Heaviest | None — OS component |
| Closed-source licensing | LGPL achievable with careful build config | LGPL core, verify plugin set | LGPL core, many plugins are GPL | No third-party license |
| Codec patent exposure | Separate from software license | Same caveat as FFmpeg | Same caveat as FFmpeg | Covered under Windows OS patent license |
| Architectural fit to Dispatcher/Buffer model | Good, but VSP builds the dispatcher | Weak — resists exposing a clean encoded-tier tap | Best conceptual match | Moderate |

### Product Capability Matrix (against ADR-002's Future Evolution table)

| Stage | FFmpeg |
|---|---|
| v1 Live View | Native, strong RTSP demux+decode |
| v2 Recording | Native stream-copy — reference implementation for this pattern |
| v3 Playback | Native, same API surface as live ingest |
| v4 Motion-triggered / AI | Native raw `AVFrame` access, most flexible of the four candidates |
| v5 Recording Server | Natural, given explicit stream-copy support |
| v6 Transcoding | Native, flagship use case (own encode API) |
| v7 Cluster / v8 Cloud | Library-agnostic — VSP-level relay/networking concern |

*(Full four-way capability matrix, community/maintenance comparison, and migration-cost validation are recorded in the Epic-010 conversation record; this document retains the decision-relevant summary.)*

### Community / Long-Term Maintenance

Largest ecosystem of the four candidates; embedded in most media software in existence, with extensive documentation and the broadest contributor base. Regular release cadence (~2 major releases/year). Extremely broad enterprise adoption — the de facto standard across the industry. Lowest project-continuity risk given its diffuse contributor base; the corresponding cost is that VSP owns more glue code long-term since FFmpeg provides no high-level session API — `IMediaController`, `IMediaSession`, and the Dispatcher/Buffer layer must be built by VSP.Player rather than adopted from the library.

### Migration Cost / Isolation Validation

`IMediaController` is the only surface a ViewModel touches — confirmed as a real isolation boundary regardless of library choice; VSP.UI and VSP.Device carry no compile-time dependency on FFmpeg. Isolation *within* VSP.Player was found incomplete as ADR-002 was originally specified: the neutral frame payload contract, hardware frame abstraction, and normalized error model were not yet defined. These three gaps are addressed as explicit Definition of Done items in Epic-010 (Live View Foundation), so the FFmpeg-specific types (`AVFrame`, FFmpeg error codes, hardware frame contexts) are contained inside VSP.Player's concrete implementation rather than leaking through the ADR-002 contracts.

---

## Decision

FFmpeg is adopted as the media library implementing the ADR-002 contracts, via a managed binding (binding package to be confirmed during Epic-010 implementation). No other library is adopted at this time; this decision may be revisited by a future ADR if a specific seam proves unworkable in practice.

## Consequences

- FFmpeg native binaries and a managed .NET binding become a new external dependency of `VSP.Player` — introduced under Epic-010 with the Product Owner's selection here serving as the authorization for this specific package.
- VSP.Player owns the full `IMediaController`/Dispatcher/Buffer implementation; no equivalent is inherited from FFmpeg itself.
- Codec patent licensing (H.264/H.265) remains a separate legal question, unresolved by this ADR, and should be confirmed by the Product Owner/legal counsel independent of the architecture decision.
- Commercial closed-source distribution requires dynamic linking and a build configuration that excludes GPL-only components, to preserve LGPL compliance.
