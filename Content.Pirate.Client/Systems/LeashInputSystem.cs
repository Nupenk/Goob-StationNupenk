using Content.Pirate.Shared.Components;
using Content.Pirate.Shared.Systems;
using Content.Shared.Hands.Components;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Client.Input;
using Robust.Shared.Player;
using System;
using Robust.Shared.Network;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Content.Client.Hands.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Weapons.Misc;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Physics;

namespace Content.Pirate.Client.Systems;

/// <summary>
/// Handles client-side input for leash reeling
/// </summary>
public sealed class LeashInputSystem : EntitySystem
{
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    private bool _reeling = false;

    public override void Initialize()
    {
        base.Initialize();
        CommandBinds.Builder
            .Bind(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler((session, coords, uid) => OnUseSecondary(session)))
            .Register<LeashInputSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
    CommandBinds.Unregister<LeashInputSystem>();
    }


    private bool OnUse(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        return false;
    }

    private bool OnUseSecondary(ICommonSession? session)
    {
        var player = session?.AttachedEntity;
        if (player == null)
            return false;

        if (!TryComp<HandsComponent>(player, out var hands) ||
            !TryComp<LeashComponent>(hands.ActiveHandEntity, out var leash))
            return false;

        if (leash.LeashedEntity == null)
            return false;

        var inputSys = IoCManager.Resolve<InputSystem>();
        var reelKey = inputSys.CmdStates.GetState(EngineKeyFunctions.UseSecondary) == BoundKeyState.Down;

        if (leash.Reeling == reelKey)
            return false;

        var msg = new RequestLeashReelMessage(reelKey);
        RaiseNetworkEvent(msg);
        return true;
    }
}
