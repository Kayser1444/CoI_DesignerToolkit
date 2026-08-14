// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.GameLoop;
using Mafi.Core.Input;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Localization;
using Mafi.Unity.Ui;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;

namespace CoIDesignerToolkit;

internal sealed class TransportProductRemovalAoEWindow : Window
{
    private sealed class RemovalGroupItem : Row
    {
        private readonly Toggle m_toggle;

        public string GroupId { get; }
        public bool IsChecked => m_toggle.GetValue();

        public RemovalGroupItem(EntityProto proto, string groupId, int count, Action onChanged)
        {
            GroupId = groupId;
            m_toggle = new Toggle(standalone: true)
                .Value(true)
                .OnValueChanged(_ => onChanged());

            this.AlignItemsCenter().Padding(2.pt());
            Add(m_toggle);
            Add(new Label((count + "x").AsLoc()).FontBold().MarginLeft(6.px()).MarginRight(6.px()));
            Icon icon = new Icon().Size(36.px());
            if (proto is IProtoWithIcon iconProto)
                icon.Value(iconProto.SomeOption(), noTooltip: true);
            Add(icon);
            LocStrFormatted displayName = groupId switch
            {
                nameof(Mafi.Core.Factory.Zippers.MiniZipper) => BdtLocalization.TransportProductRemovalAoEConnector.AsFormatted,
                nameof(Mafi.Core.Factory.Zippers.Zipper) => BdtLocalization.TransportProductRemovalAoEBalancer.AsFormatted,
                _ => groupId.AsLoc(),
            };
            Add(new Label(displayName).MarginLeft(4.px()));
        }

        public void SetChecked(bool isChecked)
        {
            m_toggle.Value(isChecked);
        }
    }

    private readonly UiContext m_context;
    private readonly IInputScheduler m_scheduler;
    private readonly Action<IEnumerable<IEntity>> m_updateHighlights;
    private readonly ScrollColumn m_groupsColumn;
    private readonly PanelWithHeader m_productsPanel;
    private readonly Row m_productsRow;
    private readonly ButtonIconText m_regularButton;
    private readonly ButtonIconText m_cancelRegularButton;
    private readonly ButtonTextUpoints m_quickButton;
    private readonly List<IEntity> m_entities = new List<IEntity>();
    private readonly Dictionary<int, string> m_selectedGroupIds = new Dictionary<int, string>();
    private bool m_updating;
    private bool m_isSubscribed;

    public TransportProductRemovalAoEWindow(
        UiContext context,
        IInputScheduler scheduler,
        Action<IEnumerable<IEntity>> updateHighlights)
        : base(BdtLocalization.TransportProductRemovalAoEToolWindowTitle.AsFormatted)
    {
        m_context = context;
        m_scheduler = scheduler;
        m_updateHighlights = updateHighlights;
        MakeMovable();
        WindowSize(580.px(), Px.Auto);

        var selectAllRow = new Row().AlignItemsCenter().Padding(4.pt()).PaddingLeft(9.px());
        Toggle selectAll = new Toggle(standalone: true)
            .Value(true)
            .OnValueChanged(SetAllGroupsChecked);
        selectAllRow.Add(selectAll);
        selectAllRow.Add(new Label(BdtLocalization.TransportProductRemovalAoESelectAll.AsFormatted).MarginLeft(4.px()));

        m_productsRow = new Row().MinHeight(ProductQuantityUi.EXPECTED_HEIGHT).Wrap();

        m_productsPanel = new PanelWithHeader();
        Column productsBody = new Column(2.pt()).Padding(4.pt());
        productsBody.Add(m_productsRow);
        m_productsPanel.BodyAdd(productsBody);

        m_regularButton = new ButtonIconText(
                Button.Danger,
                "Assets/Unity/UserInterface/General/Trash128.png",
                BdtLocalization.TransportProductRemovalAoERemoveCount.Format("0"))
            .OnClick(ScheduleRegularRemoval);

        m_cancelRegularButton = new ButtonIconText(
                Button.General,
                "Assets/Unity/UserInterface/General/Cancel.svg",
                BdtLocalization.TransportProductRemovalAoECancelCount.Format("0"))
            .OnClick(ScheduleCancelRegularRemoval);

        m_quickButton = new ButtonTextUpoints(
                BdtLocalization.TransportProductRemovalAoEQuickRemove.AsFormatted,
                "Assets/Unity/UserInterface/General/Trash128.png")
            .OnClick(ScheduleQuickRemoval);

        m_regularButton.FlexGrow(1f).Height(38.px());
        m_quickButton.FlexGrow(1.5f).Height(38.px());
        m_cancelRegularButton.FlexGrow(1f).Height(38.px());
        Row actionsRow = new Row(6.px())
        {
            m_regularButton,
            m_quickButton,
            m_cancelRegularButton,
        };
        actionsRow.FlexGrow(1f);

        AddBodySingle(
            selectAllRow,
            new HorizontalDivider().MarginTopBottom(4.pt()),
            m_groupsColumn = new ScrollColumn().Gap(1.pt()).MaxHeight(300.px()),
            new HorizontalDivider().MarginTopBottom(4.pt()),
            m_productsPanel,
            new PanelFooterRow().BodyAdd(
                actionsRow
            )
        );

        OnCloseStart += _ => StopLiveUpdates();
    }

    public void SetEntities(IEnumerable<IEntity> entities)
    {
        EnsureLiveUpdates();
        m_entities.Clear();
        m_selectedGroupIds.Clear();
        HashSet<int> seen = new HashSet<int>();
        foreach (IEntity entity in entities)
        {
            if (entity != null && !entity.IsDestroyed && seen.Add(entity.Id.Value) &&
                TransportProductRemovalManager.SupportsEntity(entity) &&
                TransportProductRemovalManager.TryGetInspectorGroupId(entity, out string groupId))
            {
                m_entities.Add(entity);
                m_selectedGroupIds.Add(entity.Id.Value, groupId);
            }
        }

        m_groupsColumn.Clear();
        foreach (IGrouping<string, IEntity> group in m_entities
            .GroupBy(entity => m_selectedGroupIds[entity.Id.Value], StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            IEntity representative = group.First();
            EntityProto proto = m_context.ProtosDb.GetOrThrow<EntityProto>(representative.Prototype.Id);
            m_groupsColumn.Add(new RemovalGroupItem(proto, group.Key, group.Count(), OnFiltersChanged));
        }
        RefreshState();
        UpdateHighlights();
    }

    private void EnsureLiveUpdates()
    {
        if (m_isSubscribed)
            return;

        m_context.GameLoopEvents.InputUpdate.AddNonSaveable(this, OnInputUpdate);
        m_isSubscribed = true;
    }

    private void StopLiveUpdates()
    {
        if (!m_isSubscribed)
            return;

        try { m_context.GameLoopEvents.InputUpdate.RemoveNonSaveable(this, OnInputUpdate); }
        catch { }
        m_isSubscribed = false;
    }

    private void OnInputUpdate(GameTime _)
    {
        if (IsOpen)
            RefreshState();
    }

    private void SetAllGroupsChecked(bool isChecked)
    {
        if (m_updating)
            return;

        m_updating = true;
        try
        {
            foreach (UiComponent child in m_groupsColumn)
            {
                if (child is RemovalGroupItem item)
                    item.SetChecked(isChecked);
            }
        }
        finally
        {
            m_updating = false;
        }
        RefreshState();
        UpdateHighlights();
    }

    private void OnFiltersChanged()
    {
        if (m_updating)
            return;
        RefreshState();
        UpdateHighlights();
    }

    private void UpdateHighlights()
    {
        m_updateHighlights(GetEnabledEntities());
    }

    private void RefreshState()
    {
        List<IEntity> enabledEntities = GetEnabledEntities();
        HashSet<string> enabledGroups = new HashSet<string>(
            enabledEntities.Select(GetGroupId),
            StringComparer.Ordinal);

        int totalBufferedQuantity = GetTotalBufferedQuantity(enabledEntities);
        m_productsPanel.Title(
            BdtLocalization.TransportProductRemovalAoEProductsToRemove.Format(
                totalBufferedQuantity.ToString()));
        RefreshProductsPreview(enabledEntities);
        int activeOrderCount = enabledEntities.Count(TransportProductRemovalManager.IsRegularRemovalActive);
        int regularRemovalTargetCount = enabledEntities.Count(TransportProductRemovalManager.WouldReceiveRegularRemoval);
        m_regularButton.SetValue(
            BdtLocalization.TransportProductRemovalAoERemoveCount.Format(
                regularRemovalTargetCount.ToString()));
        m_cancelRegularButton.SetValue(
            BdtLocalization.TransportProductRemovalAoECancelCount.Format(
                activeOrderCount.ToString()));

        bool hasTargetedEntities = enabledEntities.Count > 0;
        bool hasProductsToRemove = hasTargetedEntities && totalBufferedQuantity > 0;
        bool hasRegularRemovalTargets = regularRemovalTargetCount > 0;
        m_regularButton.Enabled(hasRegularRemovalTargets);
        m_cancelRegularButton.Enabled(activeOrderCount > 0);
        m_regularButton.Tooltip(
            !hasTargetedEntities
                ? BdtLocalization.TransportProductRemovalAoENoEntitiesTooltip
                : !hasRegularRemovalTargets
                    ? BdtLocalization.TransportProductRemovalAoENoProductsTooltip
                    : BdtLocalization.TransportProductRemovalAoERemoveTooltip);
        m_cancelRegularButton.Tooltip(
            activeOrderCount > 0
                ? BdtLocalization.TransportProductRemovalAoECancelTooltip
                : BdtLocalization.TransportProductRemovalAoENothingToCancelTooltip);

        Upoints cost = TransportProductRemovalManager.GetQuickRemoveCost(
            enabledEntities,
            enabledGroups,
            out bool canAfford);
        m_quickButton.SetCost(cost);
        m_quickButton.Visible(true);
        m_quickButton.Enabled(hasProductsToRemove && cost.IsPositive && canAfford);
        m_quickButton.Tooltip(
            !hasTargetedEntities
                ? BdtLocalization.TransportProductRemovalAoENoEntitiesTooltip
                : !hasProductsToRemove || !cost.IsPositive
                    ? BdtLocalization.TransportProductRemovalAoENoProductsTooltip
                    : !canAfford
                        ? BdtLocalization.TransportProductRemovalAoENotEnoughUnityTooltip
                        : BdtLocalization.TransportProductRemovalAoEQuickRemoveTooltip);
    }

    private List<IEntity> GetEnabledEntities()
    {
        HashSet<string> enabledGroups = new HashSet<string>(StringComparer.Ordinal);
        foreach (UiComponent child in m_groupsColumn)
        {
            if (child is RemovalGroupItem item && item.IsChecked)
                enabledGroups.Add(item.GroupId);
        }

        return m_entities
            .Where(entity => !entity.IsDestroyed &&
                TransportProductRemovalManager.SupportsEntity(entity) &&
                m_selectedGroupIds.TryGetValue(entity.Id.Value, out string groupId) &&
                enabledGroups.Contains(groupId))
            .ToList();
    }

    private void ScheduleRegularRemoval()
    {
        ImmutableArray<EntityId> ids = GetEnabledSelectedIds();
        if (ids.IsEmpty)
            return;

        m_scheduler.ScheduleInputCmd(new TransportProductRemovalBatchCmd(ids, quick: false));
        Close();
    }

    private void ScheduleQuickRemoval()
    {
        ImmutableArray<EntityId> ids = GetEnabledSelectedIds();
        if (ids.IsEmpty)
            return;

        m_scheduler.ScheduleInputCmd(new TransportProductRemovalBatchCmd(ids, quick: true));
        Close();
    }

    private void ScheduleCancelRegularRemoval()
    {
        ImmutableArray<EntityId> ids = GetEnabledSelectedIds();
        if (ids.IsEmpty)
            return;

        m_scheduler.ScheduleInputCmd(new TransportProductRemovalBatchCmd(
            ids,
            quick: false,
            cancelRegular: true));
        Close();
    }

    private static string GetGroupId(IEntity entity)
    {
        return TransportProductRemovalManager.TryGetInspectorGroupId(entity, out string groupId)
            ? groupId
            : entity.Prototype.Id.ToString();
    }

    private ImmutableArray<EntityId> GetEnabledSelectedIds()
    {
        HashSet<string> enabledGroups = new HashSet<string>(StringComparer.Ordinal);
        foreach (UiComponent child in m_groupsColumn)
        {
            if (child is RemovalGroupItem item && item.IsChecked)
                enabledGroups.Add(item.GroupId);
        }

        return ImmutableArray.CreateRange(m_entities
            .Where(entity => m_selectedGroupIds.TryGetValue(entity.Id.Value, out string groupId) &&
                enabledGroups.Contains(groupId))
            .Select(entity => entity.Id));
    }

    private static int GetTotalBufferedQuantity(IEnumerable<IEntity> entities)
    {
        int total = 0;
        foreach (IEntity entity in entities)
        {
            try
            {
                total += TransportProductRemovalManager.GetTotalBufferedProductQuantity(entity).Value;
            }
            catch
            {
                // Product enumeration is diagnostic UI only; action validity
                // is still determined by the simulation command.
            }
        }
        return total;
    }

    private void RefreshProductsPreview(IEnumerable<IEntity> entities)
    {
        Dictionary<ProductProto, Quantity> totals = new Dictionary<ProductProto, Quantity>();
        Dict<ProductProto, Quantity> entityProducts = new Dict<ProductProto, Quantity>();
        foreach (IEntity entity in entities)
        {
            if (!TransportProductRemovalManager.AddBufferedProducts(entity, entityProducts))
                continue;
            foreach (KeyValuePair<ProductProto, Quantity> product in entityProducts)
            {
                if (totals.TryGetValue(product.Key, out Quantity existing))
                    totals[product.Key] = existing + product.Value;
                else
                    totals.Add(product.Key, product.Value);
            }
        }

        m_productsRow.Clear();
        foreach (KeyValuePair<ProductProto, Quantity> product in totals
            .OrderBy(product => product.Key.Strings.Name.TranslatedString, StringComparer.Ordinal))
        {
            m_productsRow.AddCached<ProductQuantityUi>().Values(product.Key, product.Value);
        }
    }
}
