// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using Mafi;
using Mafi.Core.Entities;
using Mafi.Core.Syncers;
using Mafi.Localization;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.Ui.Library;
using UnityEngine;

namespace CoIDesignerToolkit;

public static class RateLimitUI
{
    public static PanelWithHeader BuildPanel(UiComponent inspector, Func<IEntity> getEntity)
    {
        var panel = new PanelWithHeader().Title(BdtLocalization.RateLimitTitle);
        var col = new Column(2.pt()).AlignItemsStretch();

        var row = new Row(2.pt()).AlignItemsCenter();

        var toggle = new Toggle(standalone: true)
            .Label(BdtLocalization.RateLimitEnable);

        var spacer = new UiComponent().FlexGrow(1f);

        var inputRow = new Row(2.pt()).AlignItemsCenter();

        var minusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Minus128.png")
            .Compact().IconSize(14.px());
        var plusBtn = new ButtonIcon(Button.General, "Assets/Unity/UserInterface/General/Plus128.png")
            .Compact().IconSize(14.px());

        var input = new TextField()
            .Class(Cls.displayFont, Cls.displayBg)
            .Width(50.px());
        UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.TextElement>(input.Element).style.unityTextAlign = TextAnchor.MiddleRight;
        var unitsLabel = new Label(BdtLocalization.RateLimitItemsPerMin).Color(Theme.InactiveColor);

        inputRow.Add(minusBtn);
        inputRow.Add(input);
        inputRow.Add(plusBtn);
        inputRow.Add(unitsLabel);

        row.Add(toggle);
        row.Add(spacer);
        row.Add(inputRow);

        int lastKnownLimit = 0;

        inspector.Observe(getEntity).Do(entity =>
        {
            if (entity == null || entity.IsDestroyed)
            {
                panel.Hide();
                return;
            }

            panel.Show();

            int defaultMax = 5000;
            if (entity is Mafi.Core.Factory.Transports.Transport transport)
                defaultMax = transport.Prototype.ThroughputPer60.Value;

            var currentLimitOpt = RateLimitManager.GetLimit(entity.Id);
            int currentLimit = currentLimitOpt.HasValue ? currentLimitOpt.Value : 0;
            bool isEnabled = currentLimit > 0;

            if (currentLimit > 0)
            {
                lastKnownLimit = currentLimit;
            }

            int toShow = isEnabled ? currentLimit : (lastKnownLimit > 0 ? lastKnownLimit : defaultMax);

            toggle.Value(isEnabled);
            input.Text(toShow.ToString());
        });

        Action<int> updateLimit = (val) =>
        {
            var entity = getEntity();
            if (entity == null || entity.IsDestroyed) return;

            int defaultMax = 5000;
            if (entity is Mafi.Core.Factory.Transports.Transport transport)
                defaultMax = transport.Prototype.ThroughputPer60.Value;

            if (val <= 0 || !toggle.GetValue())
            {
                RateLimitManager.RemoveLimit(entity.Id);
            }
            else
            {
                lastKnownLimit = val;
                RateLimitManager.SetLimit(entity.Id, val);
                input.Text(val.ToString());
            }
        };

        toggle.OnValueChanged(isOn => 
        {
            if (isOn)
            {
                int defaultMax = 5000;
                var entity = getEntity();
                if (entity != null && entity is Mafi.Core.Factory.Transports.Transport transport)
                    defaultMax = transport.Prototype.ThroughputPer60.Value;

                int toSet = lastKnownLimit > 0 ? lastKnownLimit : defaultMax;
                updateLimit(toSet);
            }
            else
            {
                var entity = getEntity();
                if (entity != null && !entity.IsDestroyed) 
                    RateLimitManager.RemoveLimit(entity.Id);
            }
        });

        input.OnValueChanged((text) => 
        {
            if (int.TryParse(text, out int val) && toggle.GetValue())
            {
                updateLimit(val);
            }
        });

        Action<int> adjustLimit = (sign) =>
        {
            if (!toggle.GetValue())
            {
                toggle.Value(true);
            }
            if (int.TryParse(input.GetText(), out int current))
            {
                int next = Math.Max(1, current + sign * ModifierStepSize());
                updateLimit(next);
            }
        };

        minusBtn.OnClick(() => adjustLimit(-1), allowKeyPresses: true);
        plusBtn.OnClick(() => adjustLimit(1), allowKeyPresses: true);

        col.Add(row);
        
        panel.BodyAdd(col);
        return panel;
    }

    private static int ModifierStepSize()
    {
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return 100;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return 10;
        return 1;
    }
}
