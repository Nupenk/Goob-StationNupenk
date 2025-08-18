using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Pirate.Shared.Components;
/// <summary>
/// Component for leash items that can be used to leash entities wearing collars.
/// </summary>
/// <remarks>
/// Access: SharedCollarSystem (Content.Pirate.Shared.Systems)
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(Content.Pirate.Shared.Systems.SharedCollarSystem), typeof(Content.Pirate.Shared.Systems.SharedLeashSystem))]
public sealed partial class LeashComponent : Component
{
    /// <summary>
    /// Maximum length of the leash rope.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxLength = 5.0f;

    /// <summary>
    /// Minimum length of the leash rope.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MinLength = 0.5f;

    /// <summary>
    /// Rate at which the leash can be reeled in.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ReelRate = 2.0f;

    /// <summary>
    /// Whether the leash is currently being pulled/reeled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Reeling;

    /// <summary>
    /// The entity currently leashed by this leash (if any).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? LeashedEntity;

    /// <summary>
    /// The entity holding the leash.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Holder;

    /// <summary>
    /// Sprite for the rope/leash visual.
    /// </summary>
    [DataField, ViewVariables]
    public SpriteSpecifier RopeSprite = new SpriteSpecifier.Rsi(new ResPath("Objects/Misc/leash.rsi"), "rope");

    /// <summary>
    /// Sound played when pulling on the leash.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? ReelSound = new SoundPathSpecifier("/Audio/_Goobstation/Items/Fishing/fishing_rod_reel.ogg")
    {
        Params = AudioParams.Default.WithLoop(true)
    };

    /// <summary>
    /// Sound played when attaching the leash.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? AttachSound = new SoundPathSpecifier("/Audio/_Goobstation/Items/Fishing/fishing_rod_cast.ogg");

    /// <summary>
    /// Sound played when detaching the leash.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? DetachSound = new SoundPathSpecifier("/Audio/_Goobstation/Items/handling/accessory_drop.ogg");

    /// <summary>
    /// Audio stream for the reel sound.
    /// </summary>
    [DataField]
    public EntityUid? Stream;
}

/// <summary>
/// Event fired when attempting to attach a leash to a collared entity.
/// </summary>
[ByRefEvent]
public record struct LeashAttemptEvent(EntityUid User, EntityUid Target)
{
    public readonly EntityUid User = User;
    public readonly EntityUid Target = Target;
    public bool Cancelled = false;
}

/// <summary>
/// Event fired when attempting to detach a leash.
/// </summary>
[ByRefEvent]
public record struct UnleashAttemptEvent(EntityUid User, EntityUid Target)
{
    public readonly EntityUid User = User;
    public readonly EntityUid Target = Target;
    public bool Cancelled = false;
}
