namespace VSP.Player.Entities;

public enum BufferPolicy
{
    /// <summary>Live rendering: a stale frame is worse than no frame.</summary>
    DropOldestWhenFull,

    /// <summary>Recording: must not silently lose frames.</summary>
    BlockProducerWhenFull,

    /// <summary>AI inference: keep processing steadily through bursts.</summary>
    DropNewestWhenFull
}
