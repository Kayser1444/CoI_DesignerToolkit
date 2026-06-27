// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Prototypes;
using Mafi.Localization;
using Mafi.Unity.Ui;
using Mafi.Unity.Ui.Library;
using Mafi.Collections.ReadonlyCollections;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using UnityEngine;

namespace CoIDesignerToolkit;

public class ThroughputAoEToolWindow : Window
{
    private class ThroughputAoEItem : Row
    {
        private readonly EntityProto m_proto;
        private readonly Lyst<IEntity> m_entities;
        private readonly Toggle m_checkbox;

        public bool IsChecked => m_checkbox.GetValue();

        public ThroughputAoEItem(UiContext context, EntityProto proto, IEnumerable<IEntity> entities, Action onChanged)
        {
            m_proto = proto;
            m_entities = new Lyst<IEntity>(entities);

            this.AlignItemsCenter().Padding(2.pt());

            var manager = ThroughputManager.Instance;
            bool initialValue = false;
            if (manager != null)
            {
                foreach (var e in m_entities)
                {
                    if (manager.GetOrCreateState(e.Id.Value).DisplayThroughput)
                    {
                        initialValue = true;
                        break;
                    }
                }
            }

            m_checkbox = new Toggle(standalone: true).Value(initialValue);
            m_checkbox.OnValueChanged(isOn =>
            {
                if (manager == null) return;
                foreach (var entity in m_entities)
                {
                    var state = manager.GetOrCreateState(entity.Id.Value);
                    state.DisplayThroughput = isOn;
                }
                manager.SaveConfigState();
                onChanged();
            });

            var icon = new Icon().Size(36.px());
            if (proto is IProtoWithIcon iconProto)
            {
                icon.Value(iconProto.SomeOption(), noTooltip: true);
            }

            var nameLabel = new Label(proto.Strings.Name.AsFormatted).MarginLeft(4.px());
            var countLabel = new Label((m_entities.Count.ToString() + "x").AsLoc()).FontBold().MarginLeft(6.px()).MarginRight(6.px());

            Add(m_checkbox);
            Add(countLabel);
            Add(icon);
            Add(nameLabel);
        }

        public void SetChecked(bool isOn)
        {
            m_checkbox.Value(isOn);

            var manager = ThroughputManager.Instance;
            if (manager == null) return;
            foreach (var entity in m_entities)
            {
                var state = manager.GetOrCreateState(entity.Id.Value);
                state.DisplayThroughput = isOn;
            }
            manager.SaveConfigState();
        }

        public void ApplyDays(int days)
        {
            if (!IsChecked) return;

            var manager = ThroughputManager.Instance;
            if (manager == null) return;

            foreach (var entity in m_entities)
            {
                var state = manager.GetOrCreateState(entity.Id.Value);
                state.DaysToAverage = days;
                state.RecalculateAverage();
            }
        }
    }

    private readonly UiContext m_context;
    private readonly ScrollColumn m_itemsColumn;
    private ButtonIconText? m_applyBtn;

    private TextField? m_globalDaysInput;
    private ButtonIcon? m_globalDaysMinusBtn;
    private ButtonIcon? m_globalDaysPlusBtn;

    private Toggle? m_masterToggle;
    private bool m_isUpdatingStates;

    public ThroughputAoEToolWindow(UiContext context)
        : base(BdtLocalization.ThroughputAoEToolWindowTitle.AsFormatted)
    {
        m_context = context;
        MakeMovable();
        WindowSize(540.px(), Px.Auto);

        var selectionRow = new Row().AlignItemsCenter().Padding(4.pt()).PaddingLeft(9.px()).Gap(6.px());
        m_masterToggle = new Toggle(standalone: true);
        m_masterToggle.OnValueChanged(isOn => SetAllChecked(isOn));
        
        var selectAllLabel = new Label(BdtLocalization.ThroughputAoEToolSelectAllLabel.AsFormatted).MarginLeft(4.px());
        selectionRow.Add(m_masterToggle);
        selectionRow.Add(selectAllLabel);

        AddBodySingle(
            selectionRow,
            new HorizontalDivider().MarginTopBottom(4.pt()),
            m_itemsColumn = new ScrollColumn().Gap(1.pt()).MaxHeight(300.px()),
            BuildGlobalActionsPanel(),
            new PanelFooterRow().BodyAdd(
                new UiComponent().FlexGrow(1f),
                new ButtonText(Button.General, BdtLocalization.ThroughputAoEToolClose.AsFormatted, Close)
            )
        );
    }

    private void SetAllChecked(bool isOn)
    {
        if (m_isUpdatingStates) return;
        m_isUpdatingStates = true;
        try
        {
            foreach (var item in m_itemsColumn)
            {
                if (item is ThroughputAoEItem aoeItem)
                {
                    aoeItem.SetChecked(isOn);
                }
            }
        }
        finally
        {
            m_isUpdatingStates = false;
        }
    }

    private void UpdateMasterToggleState()
    {
        if (m_isUpdatingStates || m_masterToggle == null) return;
        m_isUpdatingStates = true;
        try
        {
            bool allChecked = true;
            int count = 0;
            foreach (var item in m_itemsColumn)
            {
                if (item is ThroughputAoEItem aoeItem)
                {
                    count++;
                    if (!aoeItem.IsChecked)
                    {
                        allChecked = false;
                    }
                }
            }
            if (count > 0)
            {
                m_masterToggle.Value(allChecked);
            }
        }
        finally
        {
            m_isUpdatingStates = false;
        }
    }

    private UiComponent BuildGlobalActionsPanel()
    {
        var col = new Column(2.pt()).AlignItemsStretch().Padding(4.pt());
        
        col.Add(new HorizontalDivider().MarginTopBottom(4.pt()));
        
        // Days row
        var daysRow = new Row().AlignItemsCenter().Padding(2.pt());
        var setDaysLabel = new Label(BdtLocalization.ThroughputAoEToolOverrideDaysLabel.AsFormatted);
        
        var rightDaysLabel = new Label(BdtLocalization.ThroughputDays.AsFormatted).MarginRight(6.px());

        m_globalDaysInput = new TextField()
            .Class(Cls.displayFont, Cls.displayBg)
            .Width(35.px())
            .PositiveIntegersOnly()
            .OnEditEnd(_ => ClampGlobalDaysInput());
        m_globalDaysInput.Text("30");
        UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.TextElement>(m_globalDaysInput.Element).style.unityTextAlign = TextAnchor.MiddleRight;

        m_globalDaysMinusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Minus128.png")
            .Compact().IconSize(12.px());
        m_globalDaysPlusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Plus128.png")
            .Compact().IconSize(12.px());

        m_applyBtn = new ButtonIconText(Button.Primary, "Assets/Unity/UserInterface/General/Checkmark.svg", BdtLocalization.ThroughputAoEToolApply.AsFormatted)
            .OnClick(onApplyClick)
            .MarginLeft(15.px());

        daysRow.Add(setDaysLabel);
        daysRow.Add(new UiComponent().FlexGrow(1f));
        daysRow.Add(rightDaysLabel);
        daysRow.Add(m_globalDaysMinusBtn);
        daysRow.Add(m_globalDaysInput);
        daysRow.Add(m_globalDaysPlusBtn);
        daysRow.Add(m_applyBtn);
        col.Add(daysRow);

        // Buttons logic
        Action<int> adjustDays = (sign) =>
        {
            if (m_globalDaysInput == null) return;

            if (int.TryParse(m_globalDaysInput.GetText(), out int current))
            {
                int step = 1;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) step = 10;
                else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) step = 5;

                int next = Math.Max(1, Math.Min(360, current + sign * step));
                m_globalDaysInput.Text(next.ToString());
            }
        };

        m_globalDaysMinusBtn.OnClick(() => adjustDays(-1), allowKeyPresses: true);
        m_globalDaysPlusBtn.OnClick(() => adjustDays(1), allowKeyPresses: true);

        return col;
    }

    public void SetEntities(IEnumerable<IEntity> entities)
    {
        m_itemsColumn.Clear();
        var grouped = entities.GroupBy(e => m_context.ProtosDb.GetOrThrow<EntityProto>(e.Prototype.Id).Strings.Name.TranslatedString);
        foreach (var group in grouped)
        {
            var firstEntity = group.First();
            var proto = m_context.ProtosDb.GetOrThrow<EntityProto>(firstEntity.Prototype.Id);
            m_itemsColumn.Add(new ThroughputAoEItem(m_context, proto, group, UpdateMasterToggleState));
        }
        UpdateMasterToggleState();
    }

    private void ClampGlobalDaysInput()
    {
        if (m_globalDaysInput == null) return;
        int days = 30;
        if (int.TryParse(m_globalDaysInput.GetText(), out int daysVal))
        {
            days = Math.Max(1, Math.Min(360, daysVal));
        }
        m_globalDaysInput.Text(days.ToString());
    }

    private void onApplyClick()
    {
        if (m_globalDaysInput == null)
        {
            return;
        }

        ClampGlobalDaysInput();
        int days = int.Parse(m_globalDaysInput.GetText());

        foreach (var item in m_itemsColumn)
        {
            if (item is ThroughputAoEItem aoeItem)
            {
                aoeItem.ApplyDays(days);
            }
        }

        var manager = ThroughputManager.Instance;
        if (manager != null)
        {
            manager.SaveConfigState();
        }
    }
}

