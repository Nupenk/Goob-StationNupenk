using Content.Shared.Hands.EntitySystems;
using System.Numerics;
using Robust.Shared.Player;
using Content.Shared.CombatMode;
using Robust.Shared.Physics.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Pirate.Shared.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Dynamics.Joints;
using Content.Shared.Inventory.Events;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics;
using Content.Shared.Physics;

namespace Content.Pirate.Shared.Systems;

public abstract class SharedCollarSystem : EntitySystem
{

        [Dependency] protected readonly IGameTiming Timing = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly SharedJointSystem _joints = default!;
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;

        private const string LeashJoint = "leash";

    public override void Initialize()
    {
        base.Initialize();

        // Collar events
        SubscribeLocalEvent<CollarComponent, UseInHandEvent>(OnCollarUseInHand);
        SubscribeLocalEvent<CollarComponent, AfterInteractEvent>(OnCollarAfterInteract);
        SubscribeLocalEvent<CollarComponent, CollarDoAfterEvent>(OnCollarDoAfter);
        SubscribeLocalEvent<CollarComponent, RemoveCollarDoAfterEvent>(OnRemoveCollarDoAfter);

        // Collar wearer events
        SubscribeLocalEvent<CollarWearerComponent, InteractUsingEvent>(OnCollarWearerInteractUsing);
        SubscribeLocalEvent<CollarWearerComponent, UseInHandEvent>(OnCollarWearerUseInHand);
        SubscribeLocalEvent<CollarWearerComponent, ComponentShutdown>(OnCollarWearerShutdown);
        // Network events for leash reeling
        SubscribeAllEvent<Content.Pirate.Shared.Systems.RequestLeashReelMessage>(OnLeashReel);
    }

    #region Collar System

    private void OnCollarUseInHand(EntityUid uid, CollarComponent component, UseInHandEvent args)
    {
        if (component.Used)
        {
            var msg = Loc.GetString("collar-component-already-being-used");
            _popup.PopupClient(msg, args.User, args.User);
            return;
        }

        var clickMsg = Loc.GetString("collar-component-click-to-put-on");
        _popup.PopupClient(clickMsg, args.User, args.User);
        args.Handled = true;
    }

    private void OnCollarAfterInteract(EntityUid uid, CollarComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target == null || component.Used)
            return;

        if (HasComp<CollarWearerComponent>(args.Target))
        {
            var msg = Loc.GetString("collar-component-already-wearing", ("target", Name(args.Target.Value)));
            _popup.PopupClient(msg, args.User, args.User);
            return;
        }

        var collarEvent = new CollarAttemptEvent(args.User, args.Target.Value);
        RaiseLocalEvent(args.User, ref collarEvent);

        if (collarEvent.Cancelled)
            return;

        TryCollar(uid, component, args.Target.Value, args.User);
        args.Handled = true;
    }

    private void TryCollar(EntityUid collar, CollarComponent component, EntityUid target, EntityUid user)
    {
        var collarTime = component.CollarTime;

            if (HasComp<StunnedComponent>(target))
            {
                collarTime = MathF.Max(0.1f, collarTime - component.StunBonus);
            }

        component.Used = true;

        var doAfter = new CollarDoAfterEvent();
        var args = new DoAfterArgs(EntityManager, user, collarTime, doAfter, collar, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            RequireCanInteract = true,
            NeedHand = true
        };

        _audio.PlayPredicted(component.StartCollarSound, collar, user);
        _doAfter.TryStartDoAfter(args);
    }

    private void OnCollarDoAfter(EntityUid uid, CollarComponent component, CollarDoAfterEvent args)
    {
        component.Used = false;

        if (args.Cancelled || args.Target == null)
            return;

        if (HasComp<CollarWearerComponent>(args.Target))
            return;

        // Add collar wearer component
        var collarWearer = EnsureComp<CollarWearerComponent>(args.Target.Value);
        collarWearer.Collar = uid;

    _audio.PlayPredicted(component.EndCollarSound, uid, args.User);
    var observerMsg = Loc.GetString("collar-component-put-on-observer", ("user", Name(args.User)), ("target", Name(args.Target.Value)));
    var selfMsg = Loc.GetString("collar-component-put-on-success", ("target", Name(args.Target.Value)));
    _popup.PopupEntity(observerMsg, args.Target.Value, PopupType.Medium);
    _popup.PopupEntity(selfMsg, args.User, PopupType.Medium);

    // Move collar to null space (like handcuffs do)
    var xform = Transform(uid);
    var xformSys = EntitySystem.Get<SharedTransformSystem>();
    xformSys.AttachToGridOrMap(uid);
    xformSys.SetLocalPosition(uid, Vector2.Zero);

        Dirty(args.Target.Value, collarWearer);
    }

    private void OnRemoveCollarDoAfter(EntityUid uid, CollarComponent component, RemoveCollarDoAfterEvent args)
    {
        component.Removing = false;

        if (args.Cancelled || args.Target == null)
            return;

        if (!TryComp<CollarWearerComponent>(args.Target, out var collarWearer) || collarWearer.Collar != uid)
            return;

        RemoveCollar(uid, args.Target.Value, args.User);
    }

    private void RemoveCollar(EntityUid collar, EntityUid target, EntityUid? user)
    {
        if (!TryComp<CollarWearerComponent>(target, out var collarWearer))
            return;

        // Detach leash if attached
        if (collarWearer.LeashedBy != null)
        {
            var leashedBy = collarWearer.LeashedBy.Value;
            if (TryComp<HandsComponent>(leashedBy, out var hands) &&
                hands.ActiveHandEntity != null &&
                TryComp<LeashComponent>(hands.ActiveHandEntity.Value, out var leash))
            {
                DetachLeash(hands.ActiveHandEntity.Value, leash, leashedBy);
            }
        }

        RemComp<CollarWearerComponent>(target);

        if (TryComp<CollarComponent>(collar, out var collarComp))
        {
            _audio.PlayPredicted(collarComp.EndRemoveSound, collar, user != null ? user.Value : target);
        }

        if (user != null)
        {
            var observerMsg = Loc.GetString("collar-component-remove-observer", ("user", Name(user.Value)), ("target", Name(target)));
            var selfMsg = Loc.GetString("collar-component-remove-success", ("target", Name(target)));
            _popup.PopupEntity(observerMsg, target, PopupType.Medium);
            _popup.PopupEntity(selfMsg, user.Value, PopupType.Medium);

            // Return collar to user's hands
            if (TryComp<HandsComponent>(user.Value, out var hands))
            {
                var handsSys = EntityManager.System<SharedHandsSystem>();
                handsSys.TryPickup(user.Value, collar);
            }
        }
        else
        {
            var selfMsg = Loc.GetString("collar-component-remove-success", ("target", Name(target)));
            _popup.PopupEntity(selfMsg, target, PopupType.Medium);
            // Drop collar on ground
            var xformSys = EntitySystem.Get<SharedTransformSystem>();
            xformSys.SetWorldPosition(collar, xformSys.GetWorldPosition(target));
        }
    }

    private void OnCollarWearerInteractUsing(EntityUid uid, CollarWearerComponent component, InteractUsingEvent args)
    {
        if (!HasComp<CollarComponent>(args.Used))
            return;

        args.Handled = true;
        _popup.PopupClient($"{Name(uid)} is already wearing a collar!", args.User, args.User);
    }

    private void OnCollarWearerUseInHand(EntityUid uid, CollarWearerComponent component, UseInHandEvent args)
    {
        // Allow self-removal attempt by using hands on yourself
        if (args.User == uid)
        {
            TryRemoveCollar(component.Collar, uid, uid, true);
            args.Handled = true;
        }
    }

    private void TryRemoveCollar(EntityUid collar, EntityUid target, EntityUid user, bool selfRemove = false)
    {
        if (!TryComp<CollarComponent>(collar, out var component))
            return;

        if (component.Removing)
        {
            _popup.PopupClient("Collar is already being removed!", user, user);
            return;
        }

        var removeEvent = new RemoveCollarAttemptEvent(user, target);
        RaiseLocalEvent(user, ref removeEvent);

        if (removeEvent.Cancelled)
            return;

        var removeTime = selfRemove ? component.SelfRemoveTime : component.RemoveTime;

            if (HasComp<StunnedComponent>(target))
            {
                removeTime = MathF.Max(0.1f, removeTime - component.StunBonus);
            }

        component.Removing = true;

        var doAfter = new RemoveCollarDoAfterEvent();
        var args = new DoAfterArgs(EntityManager, user, removeTime, doAfter, collar, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            RequireCanInteract = !selfRemove,
            NeedHand = true
        };

        _audio.PlayPredicted(component.StartRemoveSound, collar, user);
        _doAfter.TryStartDoAfter(args);
    }

    private void OnCollarWearerShutdown(EntityUid uid, CollarWearerComponent component, ComponentShutdown args)
    {
        // Clean up when collar wearer component is removed
        if (component.LeashedBy != null)
        {
            var leashedBy = component.LeashedBy.Value;
            if (TryComp<HandsComponent>(leashedBy, out var hands) &&
                hands.ActiveHandEntity != null &&
                TryComp<LeashComponent>(hands.ActiveHandEntity.Value, out var leash))
            {
                DetachLeash(hands.ActiveHandEntity.Value, leash, leashedBy);
            }
        }
    }

    #endregion

    #region Leash System

    private void OnLeashUseInHand(EntityUid uid, LeashComponent component, UseInHandEvent args)
    {
        if (component.LeashedEntity != null)
        {
            // Detach leash
            DetachLeash(uid, component, args.User);
        }
        else
        {
            _popup.PopupClient("Click on someone wearing a collar to leash them.", args.User, args.User);
        }
        args.Handled = true;
    }

    private void OnLeashAfterInteract(EntityUid uid, LeashComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target == null)
            return;

        if (component.LeashedEntity != null)
            return;

        if (!TryComp<CollarWearerComponent>(args.Target, out var collarWearer))
        {
            _popup.PopupClient($"{Name(args.Target.Value)} is not wearing a collar!", args.User, args.User);
            return;
        }

        if (collarWearer.LeashedBy != null)
        {
            _popup.PopupClient($"{Name(args.Target.Value)} is already leashed!", args.User, args.User);
            return;
        }

        var leashEvent = new LeashAttemptEvent(args.User, args.Target.Value);
        RaiseLocalEvent(args.User, ref leashEvent);

        if (leashEvent.Cancelled)
            return;

        AttachLeash(uid, component, args.Target.Value, args.User);
        args.Handled = true;
    }

    private void AttachLeash(EntityUid leash, LeashComponent component, EntityUid target, EntityUid user)
    {
        if (!TryComp<CollarWearerComponent>(target, out var collarWearer))
            return;

        component.LeashedEntity = target;
        component.Holder = user;
        collarWearer.LeashedBy = user;

        // Create joint between user and target
        var jointComp = EnsureComp<JointComponent>(user);
        var joint = _joints.CreateDistanceJoint(user, target, anchorA: Vector2.Zero, id: LeashJoint);
        joint.MaxLength = component.MaxLength;
        joint.MinLength = component.MinLength;
        joint.Length = Vector2.Distance(Transform(user).WorldPosition, Transform(target).WorldPosition);
        joint.Stiffness = 0.8f;
        joint.Damping = 0.1f;

        Dirty(user, jointComp);

        // Add visual rope
        var visuals = EnsureComp<JointVisualsComponent>(user);
        visuals.Sprite = component.RopeSprite;
        visuals.Target = GetNetEntity(target);
        Dirty(user, visuals);

        if (component.AttachSound != null)
            _audio.PlayPredicted(component.AttachSound, leash, user);

    _popup.PopupEntity($"{Name(user)} attaches a leash to {Name(target)}.", target, PopupType.Medium);
    _popup.PopupEntity($"You attach the leash to {Name(target)}.", user, PopupType.Medium);

        Dirty(leash, component);
        Dirty(target, collarWearer);
    }

    private void DetachLeash(EntityUid leash, LeashComponent component, EntityUid user)
    {
        if (component.LeashedEntity == null)
            return;

        var target = component.LeashedEntity.Value;

        if (TryComp<CollarWearerComponent>(target, out var collarWearer))
        {
            collarWearer.LeashedBy = null;
            Dirty(target, collarWearer);
        }

        // Remove joint
        if (TryComp<JointComponent>(user, out var jointComp) && jointComp.GetJoints.ContainsKey(LeashJoint))
        {
            _joints.RemoveJoint(user, LeashJoint);
        }

        // Remove visuals
        RemComp<JointVisualsComponent>(user);

        component.LeashedEntity = null;
        component.Holder = null;
        SetReeling(leash, component, false, user);

        if (component.DetachSound != null)
            _audio.PlayPredicted(component.DetachSound, leash, user);

    _popup.PopupEntity($"{Name(user)} detaches the leash from {Name(target)}.", target, PopupType.Medium);
    _popup.PopupEntity($"You detach the leash from {Name(target)}.", user, PopupType.Medium);

        Dirty(leash, component);
    }

    private void OnLeashDeselected(EntityUid uid, LeashComponent component, HandDeselectedEvent args)
    {
        SetReeling(uid, component, false, args.User);
    }

    private void OnLeashActivate(EntityUid uid, LeashComponent component, ActivateInWorldEvent args)
    {
        if (!Timing.IsFirstTimePredicted || args.Handled || !args.Complex)
            return;

        if (component.LeashedEntity != null)
        {
            DetachLeash(uid, component, args.User);
        }

        args.Handled = true;
    }

    private void OnLeashJointRemoved(EntityUid uid, LeashComponent component, JointRemovedEvent args)
    {
        if (args.Joint.ID == LeashJoint)
        {
            // Clean up when joint is removed externally
            if (component.LeashedEntity != null && TryComp<CollarWearerComponent>(component.LeashedEntity, out var collarWearer))
            {
                collarWearer.LeashedBy = null;
                Dirty(component.LeashedEntity.Value, collarWearer);
            }

            component.LeashedEntity = null;
            component.Holder = null;
            SetReeling(uid, component, false, null);
            Dirty(uid, component);
        }
    }

    private void OnLeashShutdown(EntityUid uid, LeashComponent component, ComponentShutdown args)
    {
        // Clean up when leash component is removed
        if (component.LeashedEntity != null && TryComp<CollarWearerComponent>(component.LeashedEntity, out var collarWearer))
        {
            collarWearer.LeashedBy = null;
            Dirty(component.LeashedEntity.Value, collarWearer);
        }

        if (component.Holder != null)
        {
            if (TryComp<JointComponent>(component.Holder, out var jointComp) && jointComp.GetJoints.ContainsKey(LeashJoint))
            {
                _joints.RemoveJoint(component.Holder.Value, LeashJoint);
            }
            RemComp<JointVisualsComponent>(component.Holder.Value);
        }
    }

    private void OnLeashReel(Content.Pirate.Shared.Systems.RequestLeashReelMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (!TryComp<HandsComponent>(player, out var hands) ||
            !TryComp<LeashComponent>(hands.ActiveHandEntity, out var leash))
        {
            return;
        }

        if (msg.Reeling && leash.LeashedEntity == null)
            return;

        if (msg.Reeling &&
            (!TryComp<CombatModeComponent>(player, out var combatMode) ||
             !combatMode.IsInCombatMode))
        {
            return;
        }

        SetReeling(hands.ActiveHandEntity.Value, leash, msg.Reeling, player.Value);
    }

    private void SetReeling(EntityUid uid, LeashComponent component, bool value, EntityUid? user)
    {
        if (component.Reeling == value)
            return;

        if (value)
        {
            if (Timing.IsFirstTimePredicted && component.ReelSound != null)
                component.Stream = _audio.PlayPredicted(component.ReelSound, uid, user)?.Entity;
        }
        else
        {
            if (Timing.IsFirstTimePredicted)
            {
                component.Stream = _audio.Stop(component.Stream);
            }
        }

        component.Reeling = value;
        Dirty(uid, component);
    }

    #endregion

    #region Collar Visual System

    private void OnCollarWearerEquipped(EntityUid uid, CollarWearerComponent component, DidEquipEvent args)
    {
        // Update collar visuals when equipment changes
        UpdateCollarVisuals(uid, component);
    }

    private void OnCollarWearerUnequipped(EntityUid uid, CollarWearerComponent component, DidUnequipEvent args)
    {
        // Update collar visuals when equipment changes
        UpdateCollarVisuals(uid, component);
    }

    private void UpdateCollarVisuals(EntityUid uid, CollarWearerComponent component)
    {
        // This would be implemented in the client-side system
        // to handle collar overlay rendering
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Handle leash reeling
        var query = EntityQueryEnumerator<LeashComponent>();

        while (query.MoveNext(out var uid, out var leash))
        {
            if (!leash.Reeling || leash.Holder == null || leash.LeashedEntity == null)
            {
                if (Timing.IsFirstTimePredicted)
                {
                    leash.Stream = _audio.Stop(leash.Stream);
                }
                continue;
            }

            if (!TryComp<JointComponent>(leash.Holder.Value, out var jointComp) ||
                !jointComp.GetJoints.TryGetValue(LeashJoint, out var joint) ||
                joint is not DistanceJoint distance)
            {
                SetReeling(uid, leash, false, null);
                continue;
            }

            // Reel in the leash
            distance.MaxLength = MathF.Max(distance.MinLength, distance.MaxLength - leash.ReelRate * frameTime);
            distance.Length = MathF.Min(distance.MaxLength, distance.Length);

            _physics.WakeBody(joint.BodyAUid);
            _physics.WakeBody(joint.BodyBUid);

            if (jointComp.Relay != null)
            {
                _physics.WakeBody(jointComp.Relay.Value);
            }

            Dirty(leash.Holder.Value, jointComp);

            if (distance.MaxLength.Equals(distance.MinLength))
            {
                SetReeling(uid, leash, false, null);
            }
        }
    }

}

[Serializable, NetSerializable]
public sealed partial class CollarDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class RemoveCollarDoAfterEvent : SimpleDoAfterEvent
{
}
