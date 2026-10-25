// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The delta-quoted volatility surface's smile section and its forward (the S-van plan's VR4 (xxxii)). QuantLib's
/// <c>blackVolSmile</c> passes the at-the-money volatility as the section's at-the-money level, or 1 without an
/// at-the-money quote (<c>blackvolsurfacedelta.cpp:118</c>, <c>:135</c>), which <c>InterpolatedSmileSection</c> takes as
/// the forward (<c>interpolatedsmilesection.hpp:52-58</c>) and its option prices, vega, digital prices and density read
/// as the forward (<c>smilesection.cpp:78</c>, <c>:120</c>, <c>:124</c>, <c>:135</c>): on QuantLib's cases' surface, the
/// six-month section prices on a forward of 0.11, the at-the-money volatility, where the surface's own forward, its
/// <c>atmLevel(t)</c>, is S·P_f/P_d, about 1.17 [read]. nanoql's section takes the surface's forward; its volatilities
/// are QuantLib's. The checks: the forward from QuantLib's curves, and the section's prices against Black's formula at
/// the section's own volatilities.
/// </summary>
public class DeltaSurfaceSmileSectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheSectionPricesOnTheSurfacesForward(bool hasAtm)
    {
        var reference = new Date(1, Month.January, 2010);
        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        var dts = new FlatForward(reference, 0.02, dc);
        var fts = new FlatForward(reference, 0.035, dc);
        var vols = new Matrix(4, hasAtm ? 3 : 2);
        double[][] rows = [[0.15, 0.13, 0.135], [0.14, 0.11, 0.125], [0.13, 0.10, 0.12], [0.125, 0.095, 0.115]];
        for (int i = 0; i < 4; ++i)
        {
            vols[i, 0] = rows[i][0];
            vols[i, vols.Columns - 1] = rows[i][2];
            if (hasAtm)
            {
                vols[i, 1] = rows[i][1];
            }
        }

        Date[] dates =
        [
            reference + new Period(1, TimeUnit.Months),
            reference + new Period(6, TimeUnit.Months),
            reference + new Period(1, TimeUnit.Years),
            reference + new Period(2, TimeUnit.Years),
        ];
        var surface = new BlackVolatilitySurfaceDelta(
            reference, dates, [-0.25], [0.25], hasAtm, vols, dc, new TARGET(), 1.18, dts, fts, DeltaVolQuoteDeltaType.Spot);

        Date expiry = reference + new Period(6, TimeUnit.Months);
        double t = surface.TimeFromReference(expiry);
        SmileSection section = surface.BlackVolSmile(expiry);

        // the forward from the curves, where QuantLib's section holds 0.11 or 1
        double forward = 1.18 * fts.Discount(expiry) / dts.Discount(expiry);
        Assert.Equal(forward, section.AtmLevel()!.Value, 1e-15);
        Assert.Equal(surface.AtmLevel(t)!.Value, section.AtmLevel()!.Value);

        // the section's prices: Black's formula at its own volatilities on that forward
        double df = dts.Discount(expiry);
        foreach (double k in new[] { 1.05, 1.12, 1.18, 1.25, 1.32 })
        {
            double stdDev = section.Volatility(k) * Math.Sqrt(section.ExerciseTime);
            Assert.Equal(
                BlackFormulaFunctions.BlackFormula(OptionType.Call, k, forward, stdDev, df), section.OptionPrice(k, OptionType.Call, df), 1e-14);
            Assert.Equal(
                BlackFormulaFunctions.BlackFormula(OptionType.Put, k, forward, stdDev, df), section.OptionPrice(k, OptionType.Put, df), 1e-14);
        }
    }
}
