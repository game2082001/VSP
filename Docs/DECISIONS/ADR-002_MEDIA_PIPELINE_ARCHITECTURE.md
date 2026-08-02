# ADR-002 Media Pipeline Architecture

## Status

Accepted — Stable architecture document. This is the architectural foundation of the VSP video subsystem (Live View, Recording, Playback, and future AI/analytics). Future Video Epics shall conform to this architecture unless a later ADR explicitly supersedes it.

Revision history: Proposed (Frame Bus) → Revised (expanded to a full Media Pipeline: Buffer Layer, Encoded/Decoded pipeline stages, Frame Dispatcher, streaming-consumer distinction, Recorder mode abstraction, AI pipeline staging) → Revised (added Media Controller, Media Clock, Dispatcher Metrics, Metadata Bus, extended Future Evolution) → **Accepted**.

No library has been chosen. That decision belongs to ADR-003, evaluated against this document.

---

## Context

VSP needs real-time RTSP video decode and WPF rendering for Live View, with Recording, Playback, and future AI/analytics expected to reuse the same foundation rather than each becoming a separate architecture. `VSP.Player` already reserves `Interfaces\`/`Decoder\`/`Renderer\`/`Entities\` folders, anticipating this decomposition; nothing has been implemented yet. This ADR defines the contracts independent of any specific media library, so ADR-003 can evaluate candidate libraries against a fixed, deliberate target rather than letting the first library chosen shape the abstraction after the fact.

---

## Decision

### Architecture Overview

```
ViewModel
    │
    ▼
IMediaController  ──(owns lifecycle, reconnect, pause/resume, statistics)
    │
    ▼
IMediaSession  ──(generic video source: Live / RecordedFile / Remote / Synthetic)
    │  (encoded packets, each carrying a FrameTimestamp — PTS/DTS)
    ▼
[Encoded Buffer] → Frame Dispatcher (encoded) ──┬──→ IVideoDecoder → [Decoded Buffer] → Frame Dispatcher (decoded) ──┬──→ IFrameRenderer (streaming)
                                                 │                                                                   ├──→ ISnapshotService (on-demand)
                                                 └──→ IRecordingSession (streaming, mode-driven)                    └──→ IAiPipeline (streaming, multi-stage)
                                                                                                                              │
                                                                                                                              ▼
                                                                                                                        IMetadataBus ──→ any subscriber
                                                                                                                        (overlay rendering, recording triggers, future Rules/Events)
```

Both Frame Dispatchers expose live `IDispatcherMetrics`. `IMediaClock` provides the timestamp/ordering model every stage reads from.

### `IMediaSession` — a generic video source, not a camera-specific contract

Live RTSP camera is one implementation, not the definition. The same contract is satisfiable by a recorded file (Playback) or a remote/relayed feed (future multi-site), without a second pipeline being built for either.

```csharp
public interface IMediaSession : IDisposable
{
    VideoSourceKind Kind { get; }              // Live, RecordedFile, Remote, Synthetic
    MediaSessionState State { get; }
    Task OpenAsync(CancellationToken cancellationToken);
    Task CloseAsync();
    event EventHandler<MediaSessionStateChangedEventArgs>? StateChanged;
    event EventHandler<EncodedPacketReceivedEventArgs>? PacketReceived;
}
```

### `IMediaController` — session lifecycle, reconnect, pause/resume, statistics

The one component a ViewModel actually talks to; everything else stays internal to `VSP.Player`. Owns reconnect (bounded attempts, retry policy — the same pattern already validated by `RetryingDiscoveryRunner` elsewhere in this codebase, not shared code but a precedented shape), pause/resume (distinct from stop; concrete strategy left as an implementation choice), and aggregate statistics.

```csharp
public interface IMediaController : IDisposable
{
    MediaControllerState State { get; }
    MediaSessionStatistics Statistics { get; }

    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
    Task PauseAsync();
    Task ResumeAsync();

    event EventHandler<MediaControllerStateChangedEventArgs>? StateChanged;
}

public enum MediaControllerState { Idle, Connecting, Connected, Paused, Reconnecting, Disconnected, Error }

public sealed class MediaSessionStatistics
{
    public DateTimeOffset? ConnectedSince { get; init; }
    public int ReconnectAttempts { get; init; }
    public DateTimeOffset? LastErrorAt { get; init; }
    public string? LastErrorMessage { get; init; }
    public TimeSpan TotalConnectedDuration { get; init; }
}
```

### `IMediaClock` — timestamps, PTS/DTS, playback synchronization

DTS (decode order) and PTS (presentation order) diverge whenever B-frames are present (routine in H.264/H.265). `EncodedFrame`/`DecodedFrame` each carry a `FrameTimestamp`.

```csharp
public readonly struct FrameTimestamp
{
    public required TimeSpan DecodeTimestamp { get; init; }       // DTS
    public required TimeSpan PresentationTimestamp { get; init; } // PTS
}

public interface IMediaClock
{
    TimeSpan CurrentPresentationTime { get; }
    double PlaybackRate { get; }
    TimeSpan? Seek(TimeSpan target); // meaningful for RecordedFile sources; Live sources return null/unsupported
}
```

### Buffer Layer — explicit, per-consumer policy

Different consumers legitimately need different behavior when they fall behind:

```csharp
public interface IFrameBuffer<TFrame>
{
    BufferPolicy Policy { get; }
    void Enqueue(TFrame frame);
    bool TryDequeue(out TFrame? frame);
    int Count { get; }
    event EventHandler<FrameDroppedEventArgs>? FrameDropped;
}

public enum BufferPolicy
{
    DropOldestWhenFull,    // Live View rendering — a stale frame is worse than no frame
    BlockProducerWhenFull, // Recording — must not silently lose frames
    DropNewestWhenFull     // AI inference — keep processing steadily through bursts
}
```

### Encoded Pipeline / Decoded Pipeline, and the Frame Dispatcher

Named pipeline stages, each owned by a Frame Dispatcher that actively routes to per-subscriber buffers (not a passive fan-out bus):

```csharp
public interface IFrameDispatcher<TFrame>
{
    IDisposable Subscribe(IFrameConsumer<TFrame> consumer, BufferPolicy policy);
    void Dispatch(TFrame frame);
    IDispatcherMetrics Metrics { get; }
}

public interface IFrameConsumer<TFrame> { void OnFrame(TFrame frame); }
```

Recording attaches at the **encoded** tier (mux the original compressed stream — never decode-then-reencode purely to record); Renderer, Snapshot, and AI attach at the **decoded** tier.

### Dispatcher Metrics — FPS, latency, queue length, dropped frames

Live, queryable operational data per Dispatcher — not just for debugging: this is the kind of already-computed, trustworthy data a future Dashboard extension could surface, unlike Discovery's session data (Epic-009 established that isn't retrievable; this is designed from day one so that gap doesn't repeat for video).

```csharp
public interface IDispatcherMetrics
{
    double FramesPerSecond { get; }
    TimeSpan AverageLatency { get; }
    int QueueLength { get; }
    long DroppedFrameCount { get; }
    event EventHandler? MetricsUpdated;
}
```

### Streaming consumer vs. on-demand

```csharp
public interface IStreamingFrameConsumer<TFrame> : IFrameConsumer<TFrame>
{
    bool IsActive { get; }
    void Start();
    void Stop();
}
```

`IFrameRenderer` and `IRecordingSession` implement this; `ISnapshotService` (single pull request, not a continuous subscriber) deliberately does not.

### `IFrameRenderer`

```csharp
public interface IFrameRenderer : IStreamingFrameConsumer<DecodedFrame>, IDisposable
{
    ImageSource? CurrentFrameSource { get; }
    event EventHandler? FrameRendered;
}
```

### `ISnapshotService`

```csharp
public interface ISnapshotService
{
    Task<byte[]> CaptureAsync(Guid sessionId, CancellationToken cancellationToken);
}
```

### Recorder mode abstraction

Continuous, Manual, Scheduled, and Motion-triggered are all real recording modes; `IRecordingSession` delegates to a mode strategy rather than hardcoding one behavior.

```csharp
public interface IRecordingMode
{
    string ModeName { get; }
    bool ShouldRecord(RecordingModeContext context);
}

public interface IRecordingSession : IStreamingFrameConsumer<EncodedFrame>, IDisposable
{
    IRecordingMode Mode { get; }
    Task StartAsync(string filePath, CancellationToken cancellationToken);
    Task StopAsync();
}
```

Recording has no dependency on a Renderer being attached — it subscribes to the encoded-tier Dispatcher independently. This is required for the Recording Server evolution row below.

### Future AI pipeline — multi-stage, not a single callback

```csharp
public interface IAiPipeline : IStreamingFrameConsumer<DecodedFrame>
{
    IReadOnlyList<IAiPipelineStage> Stages { get; }
}

public interface IAiPipelineStage
{
    string StageName { get; }
    DecodedFrame Process(DecodedFrame frame);
}
```

### Metadata Bus — AI publishes metadata, never mutates frames

A firm principle: AI/analytics output never modifies `DecodedFrame` pixel data. Results are published as a parallel, timestamp-correlated stream; consumers (a renderer drawing overlays, a recording mode watching for triggers) read and compose, never the reverse. This is the canonical output path for `IAiPipeline` — not a bespoke event.

```csharp
public interface IMetadataBus
{
    void Publish(FrameMetadata metadata);
    IDisposable Subscribe(IMetadataConsumer consumer);
}

public sealed class FrameMetadata
{
    public required Guid SourceId { get; init; }
    public required FrameTimestamp Timestamp { get; init; }
    public required string ProducerId { get; init; }
    public required IReadOnlyList<MetadataAnnotation> Annotations { get; init; }
}

public sealed class MetadataAnnotation
{
    public required string Kind { get; init; }  // open vocabulary — new AI capabilities never require a contract change
    public RectangleF? BoundingBox { get; init; }
    public double? Confidence { get; init; }
    public IReadOnlyDictionary<string, string>? Properties { get; init; }
}

public interface IMetadataConsumer { void OnMetadata(FrameMetadata metadata); }
```

`IRecordingMode`'s future `MotionTriggered` implementation subscribes to `IMetadataBus` for `MotionRegion` annotations — a data-flow relationship, not a direct coupling between `IAiPipeline` and `IRecordingSession`.

---

## Consequences

- `IMediaController` is the integration surface for future ViewModels; every other contract above stays internal to `VSP.Player`.
- Every later capability (Recording, Playback, AI, Recording Server, Cluster, Cloud, Transcoding — see Evolution below) is expected to be a new *implementation* against these *unchanged* contracts. If a future stage genuinely requires changing `IMediaSession`, the Dispatcher, or the Buffer Layer's shape, that is a signal this ADR missed something and should be treated as a real finding, addressed via a superseding ADR — not patched around silently.
- Zero library commitment. Every contract here must remain implementable by any ADR-003 candidate; ADR-003 evaluates FFmpeg, LibVLC, GStreamer, and Windows Media Foundation specifically against this shape.
- No VSP.Domain, VSP.Device, or VSP.UI code changes result from this ADR alone — it governs `VSP.Player`'s internal design once implementation begins, which requires a separate approved Epic.

---

## Future Media Pipeline Evolution

| Stage | Adds | Architecture change required |
|---|---|---|
| v1 — Live View | `IMediaSession` (RTSP) via `IMediaController`, decoded-tier Dispatcher, `IFrameRenderer` | — |
| v2 — Recording | `IRecordingSession` (Continuous/Manual modes) on the encoded-tier Dispatcher | None |
| v3 — Playback | File-backed `IMediaSession`, `IMediaClock.Seek` becomes meaningful | None |
| v4 — Motion-triggered recording / basic AI | `IAiPipeline` on the decoded-tier Dispatcher; `MotionTriggered` mode subscribing to `IMetadataBus` | None |
| v5 — Recording Server | `IRecordingSession` deployed with zero `IFrameRenderer` subscribers | None (validated by the fan-out design, not a new contract) |
| v6 — Transcoding | A future `IVideoEncoder` (Decoded → Encoded, mirroring `IVideoDecoder`) feeding a re-encoded stream back into distribution | New component, same Dispatcher/Buffer pattern |
| v7 — Cluster | A `Remote` `VideoSourceKind` relaying another node's session; `IDispatcherMetrics` aggregated across nodes for load-balancing | New `IMediaSession` implementation + metrics aggregation |
| v8 — Cloud | Cloud-relayed viewing as another `Remote`-kind source; cloud storage as a Recorder target | New implementations, same contracts |
