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
using Mafi.Unity.Audio;
using Mafi.Unity.Entities;
using Mafi.Unity.InputControl;
using Mafi.Unity.InputControl.AreaTool;
using Mafi.Unity.InputControl.Factory;
using Mafi.Unity.Terrain;
using Mafi.Unity.Ui;
using Mafi.Unity.Ui.Hud;
using Mafi.Unity.Ui.Controllers.Tools;
using Mafi.Unity.UiStatic;
using Mafi.Unity.UiStatic.Cursors;
using Mafi.Unity.UiToolkit.Component;
using UnityEngine;

namespace CoIDesignerToolkit;

[GlobalDependency(RegistrationMode.AsEverything, false, false)]
internal class ThroughputAoETool : BaseEntityCursorInputController<IAreaSelectableEntity>, IDisposable
{
    private static readonly ColorRgba COLOR_HIGHLIGHT = new ColorRgba(0, 255, 255, 128); // Cyan highlight
    private static readonly ColorRgba COLOR_HIGHLIGHT_CONFIRM = new ColorRgba(0, 255, 255, 192);

    private readonly IGameLoopEvents m_gameLoopEvents;
    private ThroughputAoEToolWindow? m_window;
    private bool m_isSubscribed;

    public override ControllerConfig Config
    {
        get
        {
            if (m_window != null && m_window.IsOpen)
            {
                return ControllerConfig.Window;
            }
            return ControllerConfig.Tool;
        }
    }

    public ThroughputAoETool(
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
            (CursorStyle?)CursorsStyles.Upgrade,
            "Assets/Unity/UserInterface/Audio/ButtonClick.prefab",
            Option<Mafi.Unity.Ui.Controllers.Tools.FilterToolbox>.None)
    {
        m_gameLoopEvents = gameLoopEvents;
        
        InitHighlightColors(COLOR_HIGHLIGHT, COLOR_HIGHLIGHT_CONFIRM);
        SetEdgeSizeLimit(new RelTile1i(512));
        ClearSelectionOnDeactivateOnly();
    }

    public void Initialize()
    {
        if (m_isSubscribed) return;
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
    }

    private void OnGlobalInputUpdate(GameTime gameTime)
    {
        if (HotkeysRegistry.IsPressed(HotkeysRegistry.ThroughputAoETool))
        {
            HotkeysRegistry.PlayClickSound();
            Context.InputMgr.ToggleController(this);
        }
    }

    public override bool Matches(IAreaSelectableEntity entity, bool isAreaSelection, bool isLeftClick)
    {
        if (entity == null || entity.IsDestroyed) return false;

        return entity is Mafi.Core.Factory.Transports.Transport || 
               entity is Mafi.Core.Factory.Lifts.Lift || 
               entity is Mafi.Core.Factory.Zippers.Zipper || 
               entity is Mafi.Core.Factory.Sorters.Sorter || 
               entity is Mafi.Core.Factory.Zippers.MiniZipper || 
               entity is Mafi.Base.Prototypes.Sandbox.ProductsSourceEntity || 
               entity is Mafi.Base.Prototypes.Sandbox.ProductsSinkEntity || 
               entity is Mafi.Base.Prototypes.Buildings.UniversalProductsSource || 
               entity is Mafi.Base.Prototypes.Buildings.UniversalProductsSink;
    }

    public override bool OnFirstActivated(IAreaSelectableEntity hoveredEntity, Lyst<IAreaSelectableEntity> selectedEntities, Lyst<SubTransport> selectedPartialTransports)
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
        if (!isLeftMouse || selectedEntities.Count == 0)
        {
            DeactivateSelf();
            return;
        }

        if (m_window == null)
        {
            m_window = new ThroughputAoEToolWindow(Context);
            m_window.OnCloseStart += _ => DeactivateSelf();
        }

        List<IEntity> entities = new List<IEntity>();
        foreach (var entity in selectedEntities)
        {
            entities.Add(entity);
        }

        m_window.SetEntities(entities);
        if (!m_window.IsOpen)
        {
            m_window.Open(Context.UiRoot);
        }
        DesignerToolkitSettings.SetThroughputOverlayEnabled(true);
        HideCursor();
    }

    public void DeactivateSelf()
    {
        Context.InputMgr.DeactivateController(this);
    }

    public override void Deactivate()
    {
        base.Deactivate();
        if (m_window != null && m_window.IsOpen)
        {
            m_window.CloseNoFade();
        }
    }

    public override bool InputUpdate()
    {
        if (m_window != null && m_window.IsOpen)
        {
            return false;
        }
        return base.InputUpdate();
    }
}
