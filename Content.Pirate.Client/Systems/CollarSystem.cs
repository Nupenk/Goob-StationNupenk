using Content.Pirate.Shared.Components;
using Robust.Shared.Utility;
using Robust.Shared.GameObjects;
using Content.Shared.Physics;
using Content.Pirate.Shared.Systems;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Input;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Content.Shared.Physics;
using Content.Shared.Interaction.Events;

namespace Content.Pirate.Client.Systems;

/// <summary>
/// Client-side collar system for handling visuals and input
/// </summary>
public sealed class CollarSystem : SharedCollarSystem
{
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;

    public override void Initialize()
    {
        base.Initialize();

    // Client-specific events
    SubscribeLocalEvent<CollarWearerComponent, ComponentInit>(OnCollarWearerInit);
    // SubscribeLocalEvent<CollarWearerComponent, ComponentShutdown>(OnCollarWearerClientShutdown);

        // Input commands
        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnActivateItem))
            .Register<CollarSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<CollarSystem>();
    }

    #region Collar Visual System

    private void OnCollarWearerInit(EntityUid uid, CollarWearerComponent component, ComponentInit args)
    {
        AddCollarVisuals(uid, component);
    }

    private void OnCollarWearerClientShutdown(EntityUid uid, CollarWearerComponent component, ComponentShutdown args)
    {
        RemoveCollarVisuals(uid);
    }

    private void AddCollarVisuals(EntityUid uid, CollarWearerComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) ||
            !TryComp<CollarComponent>(component.Collar, out var collar))
            return;

        // Add collar overlay similar to handcuff overlay
        if (!string.IsNullOrEmpty(collar.CollarRSI) && !string.IsNullOrEmpty(collar.BodyIconState))
        {
            var rsi = sprite.BaseRSI?.Path.ToString() ?? "Mobs/Species/Human/parts.rsi";

            // Use int for layer index, not nullable
            sprite.LayerMapTryGet((int)HumanoidVisualLayers.Handcuffs, out var layer);
            if (layer == null)
            {
                layer = sprite.AddLayer(new SpriteSpecifier.Rsi(new ResPath(collar.CollarRSI), collar.BodyIconState));
                sprite.LayerMapSet((int)HumanoidVisualLayers.Handcuffs, layer);
            }
            else
            {
                sprite.LayerSetSprite(layer, new SpriteSpecifier.Rsi(new ResPath(collar.CollarRSI), collar.BodyIconState));
            }

            sprite.LayerSetColor(layer, collar.Color);
            sprite.LayerSetVisible(layer, true);
        }
    }

    private void RemoveCollarVisuals(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (sprite.LayerMapTryGet((int)HumanoidVisualLayers.Handcuffs, out var layer))
        {
            sprite.LayerSetVisible(layer, false);
        }
    }

    #endregion

    #region Joint Visual System

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

        // Create rope/chain visual overlay
        var ropeLayer = sprite.AddLayer(component.Sprite);
        // If you have color/visible fields, add them here, else skip
        // sprite.LayerSetColor(ropeLayer, component.Color);
        // sprite.LayerSetVisible(ropeLayer, component.Visible);

        // Store layer for later updates
        // If you want to store layer index, you need to extend the shared component with a [ViewVariables] int? LayerIndex property
    }

    private void RemoveJointVisuals(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;
        // If you have a way to get the layer index, remove it here
    }

    #endregion

    #region Input Handling

    private bool OnActivateItem(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (session?.AttachedEntity == null)
            return false;

        var user = session.AttachedEntity.Value;

        // Check if we're trying to activate a leash
        if (TryComp<LeashComponent>(uid, out var leash))
        {
            var activateEvent = new Content.Shared.Interaction.ActivateInWorldEvent(user, uid, true);
            RaiseLocalEvent(uid, activateEvent);
            return activateEvent.Handled;
        }

        return false;
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Update joint visuals
        UpdateJointVisuals();
    }

    private void UpdateJointVisuals()
    {
        var query = EntityQueryEnumerator<Content.Shared.Physics.JointVisualsComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out var visuals, out var sprite))
        {
            if (visuals.Target == null)
                continue;

            var targetUid = GetEntity(visuals.Target.Value);
            if (!Exists(targetUid))
                continue;

            // Update rope visual to connect between entities
            var startPos = Transform(uid).WorldPosition + visuals.OffsetA;
            var endPos = Transform(targetUid).WorldPosition + visuals.OffsetB;
            var distance = Vector2.Distance(startPos, endPos);
            var angle = Math.Atan2(endPos.Y - startPos.Y, endPos.X - startPos.X);

            // If you have a layer index and visibility, update here
        }
    }
}

// Extension to JointVisualsComponent for client-side data
public sealed partial class JointVisualsComponent
{
    /// <summary>
    /// Layer index for the visual sprite (client-side only)
    /// </summary>
    [ViewVariables]
    public int? LayerIndex;
}
