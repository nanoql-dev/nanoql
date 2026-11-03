// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The two-asset correlation engine on a smile. QuantLib reads the second asset's volatility at the first strike X₁
/// (<c>analytictwoassetcorrelationengine.cpp:54-56</c>), so on a smile it prices the flat-volatility value at X₁ (9.758
/// on the test's smile, where X₂'s volatility gives 4.239). nanoql reads it at X₂: QuantLib's engine on a flat
/// volatility equal to the smile's at X₂ is the check, bit for bit.
/// </summary>
public class TwoAssetCorrelationSmileTests
{
    [Fact]
    public void TheSecondVolatilityIsReadAtTheSecondStrike()
    {
        // a smile falling with the strike, from 0.45 at 40 to 0.20 at 100
        var smile = new BlackVarianceSurface(
            Today,
            new NullCalendar(),
            [Today + 180, Today + 365, Today + 730],
            [40.0, 70.0, 100.0],
            new Numerics.Matrix(new[,] { { 0.45, 0.45, 0.45 }, { 0.32, 0.32, 0.32 }, { 0.20, 0.20, 0.20 } }),
            A365);
        var option = new TwoAssetCorrelationOption(OptionType.Call, 50.0, 70.0, European());
        var p1 = Process(52.0, 0.0, 0.1, 0.2);
        var onSmile = new BlackScholesMertonProcess(65.0, Flat(0.0), Flat(0.1), smile);
        double vol2 = smile.BlackVol(Today + 365, 70.0);
        var flat = new BlackScholesMertonProcess(65.0, Flat(0.0), Flat(0.1), Vol(vol2));

        double calculated = AnalyticTwoAssetCorrelationEngine.Calculate(option, Context, p1, onSmile, 0.75).NPV!.Value;
        double expected = AnalyticTwoAssetCorrelationEngine.Calculate(option, Context, p1, flat, 0.75).NPV!.Value;
        Assert.Equal(expected, calculated);
        Assert.NotEqual(
            AnalyticTwoAssetCorrelationEngine.Calculate(
                option, Context, p1, new BlackScholesMertonProcess(65.0, Flat(0.0), Flat(0.1), Vol(smile.BlackVol(Today + 365, 50.0))), 0.75).NPV!.Value,
            calculated);
    }
}
