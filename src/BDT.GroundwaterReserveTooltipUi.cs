// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.Reflection;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ReadonlyCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Factory.WellPumps;
using Mafi.Core.Products;
using Mafi.Core.Terrain.Generation;
using Mafi.Localization;
using Mafi.Unity;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.Ui.Library.Charts;
using Mafi.Unity.UiStatic;
using UnityEngine;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using Mafi.Unity.UiToolkit.Library.FloatingPanel;
using CoI.AutoHelpers.Logging;

namespace CoIDesignerToolkit;

public sealed class GroundwaterReserveTooltipUi : Column
{
    private static readonly ModLogger s_log = new ModLogger("BDT.GroundwaterReserveTooltipUi");
    private static readonly FieldInfo? s_barChartGraphField = typeof(BarChart).GetField("m_graph", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly ColorRgba COLOR_WATER_SAFE = new ColorRgba(56, 189, 248);     // Sky/Cyan Blue
    private static readonly ColorRgba COLOR_WATER_WARNING = new ColorRgba(245, 158, 11);  // Amber
    private static readonly ColorRgba COLOR_WATER_CRITICAL = new ColorRgba(239, 68, 68); // Red
    private static readonly ColorRgba COLOR_CONTRAST_BG = new ColorRgba(75, 85, 99);      // Slate grey for dark bar contrast

    private readonly Title m_title;
    private readonly BarChart m_monthlyChart;
    private readonly DataSeries m_monthlySafeSeries;
    private readonly DataSeries m_monthlyWarningSeries;
    private readonly Lyst<long> m_monthlySafeData;
    private readonly Lyst<long> m_monthlyWarningData;

    private readonly BarChart m_yearlyChart;
    private readonly DataSeries m_yearlySafeSeries;
    private readonly DataSeries m_yearlyWarningSeries;
    private readonly Lyst<long> m_yearlySafeData;
    private readonly Lyst<long> m_yearlyWarningData;

    private readonly Label m_lastYearAvgDrawValue;
    private readonly Row m_sustainableDrawRow;
    private readonly Label m_sustainableDrawValue;
    private readonly Label m_expectedYearlyChangeValue;
    private readonly Label m_expectedYearsRemainingValue;
    private readonly Label m_trackingNoticeLabel;

    public GroundwaterReserveTooltipUi() : base(2.pt())
    {
        this.ClassRoot(Cls.floater, Cls.interactive);
        this.Padding(4.pt()).MinWidth(420.px()).AlignItemsStretch();

        // Header Title
        m_title = new Title().NoBorder().FlexGrow(1f);
        Add(new Row(2.pt())
        {
            (Action<Row>)(r => r.JustifyItemsSpaceBetween().AlignItemsCenter().MarginBottom(2.pt())),
            m_title
        });

        // KPI Summary Box
        var kpiColumn = new Column(1.pt()).Class(Cls.panelBody).Padding(3.pt()).MarginBottom(3.pt()).AlignItemsStretch();

        m_lastYearAvgDrawValue = new Label().FontBold();
        m_sustainableDrawValue = new Label().FontBold();
        m_sustainableDrawRow = CreateKpiRow(BdtLocalization.GroundwaterSustainableDrawLabel, m_sustainableDrawValue);
        m_expectedYearlyChangeValue = new Label().FontBold();
        m_expectedYearsRemainingValue = new Label().FontBold();
        m_trackingNoticeLabel = new Label().FontSize(12).Color(Theme.InactiveColor).Hide();

        kpiColumn.Add(
            CreateKpiRow(BdtLocalization.GroundwaterLastYearAvgDrawLabel, m_lastYearAvgDrawValue),
            m_sustainableDrawRow,
            CreateKpiRow(BdtLocalization.GroundwaterExpectedYearlyChangeLabel, m_expectedYearlyChangeValue),
            CreateKpiRow(BdtLocalization.GroundwaterExpectedYearsRemainingLabel, m_expectedYearsRemainingValue),
            m_trackingNoticeLabel
        );
        Add(kpiColumn);

        // --- Monthly Chart Section ---
        Add(new Row(1.pt())
        {
            (Action<Row>)(r => r.JustifyItemsSpaceBetween().AlignItemsCenter().MarginBottom(1.pt())),
            new Label(BdtLocalization.GroundwaterMonthlyChartTitle).FontBold().FlexGrow(1f)
        });

        m_monthlyChart = new BarChart(rightToLeft: true, hideTotals: true);
        m_monthlySafeData = new Lyst<long>();
        m_monthlyWarningData = new Lyst<long>();
        m_monthlySafeData.Count = GroundwaterStatsManager.MONTHLY_HISTORY_COUNT;
        m_monthlyWarningData.Count = GroundwaterStatsManager.MONTHLY_HISTORY_COUNT;

        m_monthlySafeSeries = new DataSeries(BdtLocalization.GroundwaterSafeLevelLabel, Option<string>.None, COLOR_WATER_SAFE, m_monthlySafeData);
        m_monthlyWarningSeries = new DataSeries(BdtLocalization.GroundwaterLowLevelLabel, Option<string>.None, COLOR_WATER_WARNING, m_monthlyWarningData);
        m_monthlyChart.AddSeries(m_monthlySafeSeries);
        m_monthlyChart.AddSeries(m_monthlyWarningSeries);

        m_monthlyChart.ConfigureXAxis(
            GroundwaterStatsManager.MONTHLY_HISTORY_COUNT,
            (int i) => (i != 0) ? (-i).ToLocCached() : ((LocStrFormatted)Tr.Statistics__Now),
            null,
            0,
            2
        );

        Add(new Row(1.pt()) { m_monthlyChart.Width(410.px()).Height(115.px()).PaddingRight(4.pt()) });

        // --- Divider ---
        Add(new HorizontalDivider().MarginTopBottom(2.pt()));

        // --- Yearly Chart Section ---
        Add(new Row(1.pt())
        {
            (Action<Row>)(r => r.JustifyItemsSpaceBetween().AlignItemsCenter().MarginBottom(1.pt())),
            new Label(BdtLocalization.GroundwaterYearlyChartTitle).FontBold().FlexGrow(1f)
        });

        m_yearlyChart = new BarChart(rightToLeft: true, hideTotals: true);
        m_yearlySafeData = new Lyst<long>();
        m_yearlyWarningData = new Lyst<long>();
        m_yearlySafeData.Count = GroundwaterStatsManager.YEARLY_HISTORY_COUNT;
        m_yearlyWarningData.Count = GroundwaterStatsManager.YEARLY_HISTORY_COUNT;

        m_yearlySafeSeries = new DataSeries(BdtLocalization.GroundwaterSafeLevelLabel, Option<string>.None, COLOR_WATER_SAFE, m_yearlySafeData);
        m_yearlyWarningSeries = new DataSeries(BdtLocalization.GroundwaterLowLevelLabel, Option<string>.None, COLOR_WATER_WARNING, m_yearlyWarningData);
        m_yearlyChart.AddSeries(m_yearlySafeSeries);
        m_yearlyChart.AddSeries(m_yearlyWarningSeries);

        m_yearlyChart.ConfigureXAxis(
            GroundwaterStatsManager.YEARLY_HISTORY_COUNT,
            (int i) => (i != 0) ? (-i).ToLocCached() : ((LocStrFormatted)Tr.Statistics__Now),
            null,
            0,
            2
        );

        Add(new Row(1.pt()) { m_yearlyChart.Width(410.px()).Height(115.px()).PaddingRight(4.pt()) });
    }

    private static Row CreateKpiRow(LocStr label, Label valueLabel)
    {
        var row = new Row(2.pt()).Fill().JustifyItemsSpaceBetween().AlignItemsCenter();
        var lbl = new Label(label).Color(Theme.InactiveColor).FlexShrink(0f);
        var spacer = new UiComponent().FlexGrow(1f);
        valueLabel.FlexShrink(0f);

        UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.TextElement>(valueLabel.Element).style.unityTextAlign = TextAnchor.MiddleRight;

        row.Add(lbl);
        row.Add(spacer);
        row.Add(valueLabel);
        return row;
    }

    public void UpdateData(IVirtualResourceMiningEntity? miningEntity, IVirtualTerrainResource? resource)
    {
        if (miningEntity == null || resource == null || GroundwaterStatsManager.Instance == null)
        {
            return;
        }

        var manager = GroundwaterStatsManager.Instance;
        var record = manager.GetOrCreateRecord(resource);
        if (record == null)
        {
            return;
        }

        bool isGroundwater = resource.Product != null && resource.Product.Id == IdsCore.Products.Groundwater;
        ProductProto? minedProduct = miningEntity.ProductToMine ?? resource.Product?.Product;

        // Title
        LocStr productName = (resource.Product != null && isGroundwater)
            ? resource.Product.Strings.Name
            : (minedProduct != null ? minedProduct.Strings.Name : (resource.Product != null ? resource.Product.Strings.Name : LocStr.Empty));
        m_title.Value(BdtLocalization.GroundwaterInsightsTitle.Format(productName));

        // Bar Chart Colors matching product characteristic color (groundwater hardcoded to vibrant water blue)
        ColorRgba safeColor = COLOR_WATER_SAFE;
        if (isGroundwater)
        {
            safeColor = COLOR_WATER_SAFE;
        }
        else if (minedProduct != null && !minedProduct.Graphics.Color.IsEmpty)
        {
            safeColor = minedProduct.Graphics.Color;
        }
        else if (resource.Product?.Graphics != null && !resource.Product.Graphics.ResourcesVizColor.IsEmpty)
        {
            safeColor = resource.Product.Graphics.ResourcesVizColor;
        }
        m_monthlySafeSeries.Color = safeColor;
        m_yearlySafeSeries.Color = safeColor;
        SetChartBackground(m_monthlyChart, safeColor);
        SetChartBackground(m_yearlyChart, safeColor);

        long current = resource.Quantity.Value;
        long capacity = resource.Capacity.Value;

        // 1. Last year's average monthly draw
        Quantity avgMonthlyDraw = record.CalculateAverageMonthlyDraw();
        m_lastYearAvgDrawValue.Value($"{avgMonthlyDraw.Value:N0} / month".AsLoc());

        // 2. Max Sustainable Monthly Draw (shown only for groundwater)
        Quantity sustainable = Quantity.Zero;
        if (isGroundwater)
        {
            m_sustainableDrawRow.Show();
            sustainable = manager.CalculateMaxSustainableMonthlyDraw(resource);
            m_sustainableDrawValue.Value($"{sustainable.Value:N0} / month".AsLoc());
            m_sustainableDrawValue.Tooltip(BdtLocalization.GroundwaterSustainableTooltip);
        }
        else
        {
            m_sustainableDrawRow.Hide();
        }

        // 3. Expected Yearly Change
        long expectedAnnualReplenish = (long)sustainable.Value * 12L;
        long expectedAnnualDraw = (long)avgMonthlyDraw.Value * 12L;
        long expectedYearlyChange = expectedAnnualReplenish - expectedAnnualDraw;

        if (avgMonthlyDraw.IsPositive)
        {
            if (expectedYearlyChange > 0)
            {
                m_expectedYearlyChangeValue.Value($"+{expectedYearlyChange:N0} / year ({BdtLocalization.GroundwaterTrendFilling})".AsLoc()).Color(Theme.PositiveColor);
            }
            else if (expectedYearlyChange < 0)
            {
                m_expectedYearlyChangeValue.Value($"{expectedYearlyChange:N0} / year ({BdtLocalization.GroundwaterTrendEmptying})".AsLoc()).Color(COLOR_WATER_WARNING);
            }
            else
            {
                m_expectedYearlyChangeValue.Value($"0 / year ({BdtLocalization.GroundwaterTrendBalanced})".AsLoc()).Color(Theme.PrimaryColor);
            }
        }
        else
        {
            m_expectedYearlyChangeValue.Value(BdtLocalization.GroundwaterTrendIdle).Color(Theme.InactiveColor);
        }

        // 4. Expected Years Remaining
        if (expectedYearlyChange >= 0)
        {
            m_expectedYearsRemainingValue.Value(BdtLocalization.GroundwaterYearsRemainingStable).Color(Theme.PositiveColor);
        }
        else
        {
            long drainRate = -expectedYearlyChange;
            double yearsRemaining = drainRate > 0 ? (double)current / drainRate : 9999.0;

            if (yearsRemaining > 1000.0)
            {
                m_expectedYearsRemainingValue.Value(BdtLocalization.GroundwaterYearsRemainingOver1000).Color(COLOR_WATER_WARNING);
            }
            else
            {
                m_expectedYearsRemainingValue.Value($"{yearsRemaining:F1} years".AsLoc());
                if (yearsRemaining < 10.0)
                {
                    m_expectedYearsRemainingValue.Color(COLOR_WATER_CRITICAL);
                }
                else
                {
                    m_expectedYearsRemainingValue.Color(COLOR_WATER_WARNING);
                }
            }
        }

        // 5. Tracking Notice
        if (record.RecordedMonthsCount < GroundwaterStatsManager.MONTHLY_DRAWS_COUNT)
        {
            m_trackingNoticeLabel.Show().Value(BdtLocalization.GroundwaterTrackingNotice.Format((record.RecordedMonthsCount).ToString(), GroundwaterStatsManager.MONTHLY_DRAWS_COUNT.ToString()));
        }
        else
        {
            m_trackingNoticeLabel.Hide();
        }

        // 6. Update Monthly and Yearly BarCharts
        long yMax = Math.Max(1L, capacity);
        long[] yTicks = new long[5] { 0L, yMax / 4, yMax / 2, 3 * yMax / 4, yMax };

        m_monthlyChart.ConfigureYAxis(
            yMax,
            (long val) => Percent.FromRatio(val, yMax).ToStringRounded().AsLoc(),
            0,
            0L,
            yTicks
        );

        m_yearlyChart.ConfigureYAxis(
            yMax,
            (long val) => Percent.FromRatio(val, yMax).ToStringRounded().AsLoc(),
            0,
            0L,
            yTicks
        );

        Percent lowThreshold = miningEntity is WellPump pump ? pump.Prototype.NotifyWhenBelow : 25.Percent();

        // Populate Monthly History
        for (int i = 0; i < GroundwaterStatsManager.MONTHLY_HISTORY_COUNT; i++)
        {
            if (i > record.RecordedMonthsCount && i > 0)
            {
                m_monthlySafeData[i] = 0;
                m_monthlyWarningData[i] = 0;
                continue;
            }

            long val = record.MonthlyHistory[i];
            Percent valPercent = Percent.FromRatio(val, yMax);

            if (valPercent < lowThreshold)
            {
                m_monthlySafeData[i] = 0;
                m_monthlyWarningData[i] = val;
            }
            else
            {
                m_monthlySafeData[i] = val;
                m_monthlyWarningData[i] = 0;
            }
        }

        // Populate Yearly History
        for (int i = 0; i < GroundwaterStatsManager.YEARLY_HISTORY_COUNT; i++)
        {
            if (i > record.RecordedYearsCount && i > 0)
            {
                m_yearlySafeData[i] = 0;
                m_yearlyWarningData[i] = 0;
                continue;
            }

            long val = record.YearlyHistory[i];
            Percent valPercent = Percent.FromRatio(val, yMax);

            if (valPercent < lowThreshold)
            {
                m_yearlySafeData[i] = 0;
                m_yearlyWarningData[i] = val;
            }
            else
            {
                m_yearlySafeData[i] = val;
                m_yearlyWarningData[i] = 0;
            }
        }

        m_monthlyChart.MarkDirtyRepaint();
        m_monthlyChart.RenderUpdate(null);
        m_yearlyChart.MarkDirtyRepaint();
        m_yearlyChart.RenderUpdate(null);
    }

    private static void SetChartBackground(BarChart chart, ColorRgba barColor)
    {
        try
        {
            if (s_barChartGraphField?.GetValue(chart) is OrthogonalChart graph)
            {
                ColorRgba defaultBg = ChartConstants.CHART_BACKGROUND_COLOR;
                float dr = barColor.R - defaultBg.R;
                float dg = barColor.G - defaultBg.G;
                float db = barColor.B - defaultBg.B;
                float distance = (float)Math.Sqrt(dr * dr + dg * dg + db * db);

                // If bar color is very dark (like crude oil) or too close to the chart background,
                // adjust background to a lighter slate grey for high contrast.
                if ((barColor.R < 45 && barColor.G < 45 && barColor.B < 45) || distance < 45f)
                {
                    graph.GraphArea.Background(COLOR_CONTRAST_BG);
                }
                else
                {
                    graph.GraphArea.Background(defaultBg);
                }
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to set chart background: {ex.Message}");
        }
    }
}
