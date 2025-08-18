using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Systems;

[Serializable, NetSerializable]
public sealed class RequestLeashReelMessage : EntityEventArgs
{
    public bool Reeling;

    public RequestLeashReelMessage(bool reeling)
    {
        Reeling = reeling;
    }
}
