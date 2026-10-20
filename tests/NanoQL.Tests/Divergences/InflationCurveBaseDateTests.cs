// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A piecewise zero-inflation curve whose base date is a function, given a seasonality of more than one cycle.
/// QuantLib's lazy constructor gives its base <c>Date()</c> (<c>piecewisezeroinflationcurve.hpp:84</c>), and a
/// seasonality of more than one cycle checks itself against that date (<c>seasonality.cpp:70-76</c>), whose month reads
/// before the date tables, which is undefined. nanoql's curve passes the base date its function computes, so the check
/// reads the real one. QuantLib's check is its curve with that base date given: on
/// <see cref="InflationFixture"/>'s fixings and five UK RPI zero-coupon swaps from 2.9% to 3.15%, with two equal
/// twelve-month cycles, the wheel's curve based on 1 April 2024 has the nodes 0.027822937658004258 (twice),
/// 0.028915718188625192, 0.029630103472299253, 0.030288922160623725 and 0.03140364761530262; with a second cycle 0.1%
/// higher, it refuses "seasonality is inconsistent with inflation term structure, factors 1.00049 and later factor
/// 1.00149, 1 years later from inflation curve  with base date at April 30th, 2024".
/// </summary>
public class InflationCurveBaseDateTests
{
    private static readonly double[] Cycle =
        [1.003245, 1.000000, 0.999715, 1.000495, 1.000929, 0.998687, 0.995949, 0.994682, 0.995949, 1.000519, 1.003705, 1.004186];

    private static readonly double[] Nodes =
        [0.027822937658004258, 0.027822937658004258, 0.028915718188625192, 0.029630103472299253, 0.030288922160623725, 0.03140364761530262];

    [Fact]
    public void TheSeasonalityChecksTheComputedBaseDate()
    {
        EvaluationContext context = InflationFixture.Context();
        var rpi = new UKRPI();
        var twoCycles = new MultiplicativePriceSeasonality(new Date(1, Month.January, 2024), Frequency.Monthly, [.. Cycle, .. Cycle]);

        PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits> curve = Curve(rpi, context, twoCycles);

        Assert.Equal(new Date(1, Month.April, 2024), curve.BaseDate);
        double[] nodes = [.. curve.Nodes().Select(n => n.Value)];
        Assert.Equal(Nodes.Length, nodes.Length);
        for (int i = 0; i < Nodes.Length; i++)
        {
            Assert.Equal(Nodes[i], nodes[i], 1e-12);
        }

        var inconsistent = new MultiplicativePriceSeasonality(
            new Date(1, Month.January, 2024), Frequency.Monthly, [.. Cycle, .. Cycle.Select(f => f + 0.001)]);
        NanoQLException e = Assert.Throws<NanoQLException>(() => Curve(rpi, context, inconsistent));
        Assert.EndsWith("with base date at April 30th, 2024", e.Message, StringComparison.Ordinal);
    }

    private static PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits> Curve(
        UKRPI rpi, EvaluationContext context, Seasonality seasonality)
    {
        Date today = InflationFixture.Today;
        Calendar uk = new UnitedKingdom();
        DayCounter dc = new Thirty360(Thirty360Convention.BondBasis);
        var helpers = new List<BootstrapHelper<ZeroInflationTermStructure>>();
        foreach ((int years, double quote) in new[] { (1, 0.0290), (2, 0.0295), (3, 0.0300), (5, 0.0305), (10, 0.0315) })
        {
            Date maturity = uk.Advance(today, years, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing);
            helpers.Add(new ZeroCouponInflationSwapHelper(
                quote,
                new Period(3, TimeUnit.Months),
                maturity,
                uk,
                BusinessDayConvention.ModifiedFollowing,
                dc,
                rpi,
                CPIInterpolationType.Flat,
                context));
        }

        return new PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits>(
            today, ctx => rpi.LastFixingDate(ctx), Frequency.Monthly, dc, helpers, context, seasonality);
    }
}
