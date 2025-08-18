using System.Numerics;
using Content.Shared.CombatMode;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Pirate.Shared.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Events;
using Content.Shared.Movement.Events;
using Content.Shared.Projectiles;
using Robust.Shared.Player;

namespace Content.Pirate.Shared.Systems;

public abstract class SharedLeashSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public const string LeashJoint = "leash";

    public override void Initialize()
    {
        base.Initialize();

        // Leash events
        SubscribeLocalEvent<LeashComponent, UseInHandEvent>(OnLeashUseInHand);
        SubscribeLocalEvent<LeashComponent, AfterInteractEvent>(OnLeashAfterInteract);
        SubscribeLocalEvent<LeashComponent, HandDeselectedEvent>(OnLeashDeselected);
        SubscribeLocalEvent<LeashComponent, ActivateInWorldEvent>(OnLeashActivate);
        SubscribeLocalEvent<LeashComponent, JointRemovedEvent>(OnLeashJointRemoved);
        SubscribeLocalEvent<LeashComponent, ComponentShutdown>(OnLeashShutdown);

        // Network events
        SubscribeAllEvent<RequestLeashReelMessage>(OnLeashReel);
    }

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
            var visuals = EnsureComp<JointVisualsComponent>(args.User);
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

    private void OnLeashReel(RequestLeashReelMessage msg, EntitySessionEventArgs args)
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

            if (!TryComp<JointComponent>(leash.Holder, out var jointComp) ||
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
