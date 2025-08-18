using Content.Pirate.Shared.Components;
using Robust.Shared.GameObjects;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using System.Numerics;

namespace Content.Pirate.Client.Systems;

/// <summary>
/// Client-side leash system for handling leash visuals only
/// </summary>
public sealed class LeashSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to joint visuals (rope/chain)
        SubscribeLocalEvent<Content.Shared.Physics.JointVisualsComponent, ComponentInit>((uid, comp, args) => OnJointVisualsInit(uid, comp, args));
        SubscribeLocalEvent<Content.Shared.Physics.JointVisualsComponent, ComponentShutdown>((uid, comp, args) => OnJointVisualsShutdown(uid, comp, args));
    }
    private void OnJointVisualsInit(EntityUid uid, Content.Shared.Physics.JointVisualsComponent component, ComponentInit args)
    {
        AddJointVisuals(uid, component);
    }

    private void OnJointVisualsShutdown(EntityUid uid, Content.Shared.Physics.JointVisualsComponent component, ComponentShutdown args)
    {
        RemoveJointVisuals(uid);
    }

    private void AddJointVisuals(EntityUid uid, Content.Shared.Physics.JointVisualsComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) || component.Sprite == null)
            return;
        // Add rope/chain overlay
        var ropeLayer = sprite.AddLayer(component.Sprite);
        // Optionally set color/visibility if needed
        // sprite.LayerSetColor(ropeLayer, component.Color);
        // sprite.LayerSetVisible(ropeLayer, component.Visible);
        // Store layer index if needed
    }

    private void RemoveJointVisuals(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;
        // Remove or hide rope layer if you have its index
    }
}
