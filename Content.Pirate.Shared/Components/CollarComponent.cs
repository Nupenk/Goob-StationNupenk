using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Components;

/// <summary>
/// Component for collars that can be worn by entities, independent of handcuffs.
/// </summary>
/// <remarks>
/// Access: SharedCollarSystem (Content.Pirate.Shared.Systems)
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(Content.Pirate.Shared.Systems.SharedCollarSystem), typeof(Content.Pirate.Shared.Systems.SharedLeashSystem))]
public sealed partial class CollarComponent : Component
{
    /// <summary>
    /// The time it takes to put on a collar.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CollarTime = 2.5f;

    /// <summary>
    /// The time it takes to remove a collar.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float RemoveTime = 3.0f;

    /// <summary>
    /// The time it takes for an entity to remove the collar themselves.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SelfRemoveTime = 12f;

    /// <summary>
    /// If an entity being collared is stunned, this amount of time is subtracted from the time it takes to add/remove collar.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float StunBonus = 1.5f;

    /// <summary>
    /// Whether the collar is currently being used on someone.
    /// </summary>
    [DataField]
    public bool Used;

    /// <summary>
    /// Whether the collar is currently being removed.
    /// </summary>
    [DataField]
    public bool Removing;

    /// <summary>
    /// The path of the RSI file used for the collar overlay.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string? CollarRSI = "_Pirate/Objects/Misc/collar.rsi";

    /// <summary>
    /// The iconstate used with the RSI file for the collar overlay.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public string? BodyIconState = "collar-overlay";

    /// <summary>
    /// Color specification for the collar overlay.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public Color Color = Color.White;

    /// <summary>
    /// Sound played when starting to put on collar.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier StartCollarSound = new SoundPathSpecifier("/Audio/Items/Handcuffs/rope_start.ogg");

    /// <summary>
    /// Sound played when collar is successfully put on.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier EndCollarSound = new SoundPathSpecifier("/Audio/Items/Handcuffs/rope_end.ogg");

    /// <summary>
    /// Sound played when starting to remove collar.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier StartRemoveSound = new SoundPathSpecifier("/Audio/Items/Handcuffs/rope_start.ogg");

    /// <summary>
    /// Sound played when collar is successfully removed.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier EndRemoveSound = new SoundPathSpecifier("/Audio/Items/Handcuffs/rope_breakout.ogg");
}

/// <summary>
/// Component for entities wearing a collar.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(Content.Pirate.Shared.Systems.SharedCollarSystem), typeof(Content.Pirate.Shared.Systems.SharedLeashSystem))]
public sealed partial class CollarWearerComponent : Component
{
    /// <summary>
    /// The collar entity being worn.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid Collar;

    /// <summary>
    /// Entity that is leashing this collar wearer (if any).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? LeashedBy;
}

/// <summary>
/// Event fired when attempting to collar an entity.
/// </summary>
[ByRefEvent]
public record struct CollarAttemptEvent(EntityUid User, EntityUid Target)
{
    public readonly EntityUid User = User;
    public readonly EntityUid Target = Target;
    public bool Cancelled = false;
}

/// <summary>
/// Event fired when attempting to remove a collar.
/// </summary>
[ByRefEvent]
public record struct RemoveCollarAttemptEvent(EntityUid User, EntityUid Target)
{
    public readonly EntityUid User = User;
    public readonly EntityUid Target = Target;
    public bool Cancelled = false;
}
