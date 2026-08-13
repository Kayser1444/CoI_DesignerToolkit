// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Collections.ReadonlyCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Factory.Transports;
using Mafi.Core.GameLoop;
using Mafi.Core.Prototypes;
using Mafi.Core.Terrain;
using Mafi.Unity;
using Mafi.Unity.Entities;
using Mafi.Unity.InputControl;
using Mafi.Unity.InputControl.AreaTool;
using Mafi.Unity.InputControl.Factory;
using Mafi.Unity.Terrain;
using Mafi.Unity.Ui;
using Mafi.Unity.Ui.Controllers.Tools;
using Mafi.Unity.Ui.Hud;
using Mafi.Unity.UiStatic;
using Mafi.Unity.UiStatic.Cursors;
using Mafi.Unity.UiToolkit.Component;

namespace CoIDesignerToolkit;

/// <summary>
/// The single AoE product-removal tool. Selection is performed by the vanilla
/// area-selection controller; the dialog owns type filtering and chooses the
/// regular or quick batch command.
/// </summary>
internal sealed class TransportProductRemovalAoETool : BaseEntityCursorInputController<IAreaSelectableEntity>, IDisposable
{
    private static readonly ColorRgba COLOR_HIGHLIGHT = new ColorRgba(255, 180, 40, 160);
    private static readonly ColorRgba COLOR_HIGHLIGHT_CONFIRM = new ColorRgba(255, 180, 40, 220);

    private readonly IGameLoopEvents m_gameLoopEvents;
    private readonly EntityHighlighter m_dialogHighlighter;
    private TransportProductRemovalAoEWindow? m_window;
    private bool m_isSubscribed;

    public override ControllerConfig Config =>
        m_window != null && m_window.IsOpen ? ControllerConfig.Window : ControllerConfig.Tool;

    public TransportProductRemovalAoETool(
        ToolbarHud toolbar,
        UiContext context,
        CursorPickingManager cursorPickingManager,
        CursorManager cursorManager,
        AreaSelectionToolFactory areaSelectionToolFactory,
        IEntitiesManager entitiesManager,
        NewInstanceOf<EntityHighlighter> highlighter,
        NewInstanceOf<TerrainAreaOutlineRenderer> terrainOutlineRenderer,
        IGameLoopEvents gameLoopEvents)
        : base(
            toolbar,
            context,
            cursorPickingManager,
            cursorManager,
            areaSelectionToolFactory,
            terrainOutlineRenderer,
            entitiesManager,
            highlighter,
            (Option<NewInstanceOf<TransportTrajectoryHighlighter>>)Option.None,
            (Proto.ID?)null,
            (CursorStyle?)CursorsStyles.SelectArea,
            "Assets/Unity/UserInterface/Audio/ButtonClick.prefab",
            Option<Mafi.Unity.Ui.Controllers.Tools.FilterToolbox>.None)
    {
        m_gameLoopEvents = gameLoopEvents;
        m_dialogHighlighter = highlighter.Instance;
        InitHighlightColors(COLOR_HIGHLIGHT, COLOR_HIGHLIGHT_CONFIRM);
        SetEdgeSizeLimit(new RelTile1i(512));
        ClearSelectionOnDeactivateOnly();

        toolbar.AddToolButton(
            BdtLocalization.TransportProductRemovalAoEToolName.AsFormatted,
            this,
            "Assets/Unity/UserInterface/General/Trash128.png",
            1080f,
            (ShortcutsManager _) => HotkeysRegistry.TransportProductRemovalAoETool);
    }

    public void Initialize()
    {
        if (m_isSubscribed)
            return;

        m_gameLoopEvents.InputUpdate.AddNonSaveable(this, OnGlobalInputUpdate);
        m_isSubscribed = true;
    }

    public void Dispose()
    {
        if (m_isSubscribed)
        {
            try { m_gameLoopEvents.InputUpdate.RemoveNonSaveable(this, OnGlobalInputUpdate); }
            catch { }
            m_isSubscribed = false;
        }

        if (m_window != null)
        {
            m_window.CloseNoFade();
            m_window = null;
        }
        ClearDialogHighlights();
    }

    public override bool Matches(IAreaSelectableEntity entity, bool isAreaSelection, bool isLeftClick)
    {
        return entity is IEntity candidate &&
            !candidate.IsDestroyed &&
            TransportProductRemovalManager.SupportsEntity(candidate);
    }

    public override bool OnFirstActivated(
        IAreaSelectableEntity hoveredEntity,
        Lyst<IAreaSelectableEntity> selectedEntities,
        Lyst<SubTransport> selectedPartialTransports)
    {
        return false;
    }

    public override void OnEntitiesSelected(
        IIndexable<IAreaSelectableEntity> selectedEntities,
        IIndexable<SubTransport> selectedPartialTransports,
        ImmutableArray<TileSurfaceCopyPasteData> selectedSurfaces,
        ImmutableArray<TileSurfaceCopyPasteData> selectedDecals,
        bool isAreaSelection,
        bool isLeftMouse,
        RectangleTerrainArea2i? area)
    {
        if (!isLeftMouse)
        {
            DeactivateSelf();
            return;
        }

        List<IEntity> entities = new List<IEntity>();
        HashSet<int> seen = new HashSet<int>();
        foreach (IAreaSelectableEntity selected in selectedEntities)
        {
            if (selected is IEntity entity && seen.Add(entity.Id.Value))
                entities.Add(entity);
        }

        // Kept defensively for compatibility with callers that may supply
        // partial selections, although this controller intentionally uses the
        // same whole-entity overlap predicate as Upgrade and Throughput.
        foreach (SubTransport partial in selectedPartialTransports)
        {
            Transport original = partial.OriginalTransport;
            if (!original.IsDestroyed && seen.Add(original.Id.Value))
                entities.Add(original);
        }

        if (entities.Count == 0)
        {
            DeactivateSelf();
            return;
        }

        if (m_window == null)
        {
            m_window = new TransportProductRemovalAoEWindow(
                Context,
                Context.InputScheduler,
                UpdateDialogHighlights);
            m_window.OnCloseStart += _ => DeactivateSelf();
        }

        ClearDialogHighlights();
        m_window.SetEntities(entities);
        if (!m_window.IsOpen)
            m_window.Open(Context.UiRoot);
        HideCursor();
    }

    public void DeactivateSelf()
    {
        Context.InputMgr.DeactivateController(this);
    }

    public override void Deactivate()
    {
        base.Deactivate();
        ClearDialogHighlights();
        if (m_window != null && m_window.IsOpen)
            m_window.CloseNoFade();
    }

    public override bool InputUpdate()
    {
        if (m_window != null && m_window.IsOpen)
            return false;
        return base.InputUpdate();
    }

    private void OnGlobalInputUpdate(GameTime gameTime)
    {
        if (HotkeysRegistry.IsPressed(HotkeysRegistry.TransportProductRemovalAoETool))
        {
            HotkeysRegistry.PlayClickSound();
            Context.InputMgr.ToggleController(this);
        }
    }

    private void UpdateDialogHighlights(IEnumerable<IEntity> entities)
    {
        m_dialogHighlighter.ClearAllHighlights();
        foreach (IEntity entity in entities)
        {
            if (!entity.IsDestroyed && entity is IRenderedEntity renderedEntity)
                m_dialogHighlighter.Highlight(renderedEntity, COLOR_HIGHLIGHT_CONFIRM);
        }
    }

    private void ClearDialogHighlights()
    {
        m_dialogHighlighter.ClearAllHighlights();
    }
}
