// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="InflationTermStructureExtensions.SetSeasonality"/> on a piecewise zero-inflation curve: QuantLib's
/// <c>setSeasonality</c> on the curve becomes a copy, bootstrapped again with the seasonality on the same base date,
/// and the curve set on is unchanged. A seasonality inconsistent with the curve is refused with QuantLib's
/// <see cref="NanoQLException"/>, and the curve keeps none.
/// </summary>
public class SetSeasonalityTests
{
    private static readonly Date Today = new(15, Month.June, 2024);

    private static readonly double[] Cycle =
        [1.003245, 1.000000, 0.999715, 1.000495, 1.000929, 0.998687, 0.995949, 0.994682, 0.995949, 1.000519, 1.003705, 1.004186];

    [Fact]
    public void TheCopyCarriesTheSeasonality()
    {
        (PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits> curve, EvaluationContext context) = Curve();
        var seasonality = new MultiplicativePriceSeasonality(new Date(1, Month.January, 2024), Frequency.Monthly, Cycle);

        PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits> copy = curve.SetSeasonality(seasonality, context);

        Assert.Same(seasonality, copy.Seasonality);
        Assert.Equal(curve.BaseDate, copy.BaseDate);
        Assert.Null(curve.Seasonality);
        Assert.False(copy.Nodes().SequenceEqual(curve.Nodes()));
        Assert.Null(copy.SetSeasonality(null, context).Seasonality);
    }

    [Fact]
    public void AnInconsistentSeasonalityIsRefused()
    {
        (PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits> curve, EvaluationContext context) = Curve();
        var inconsistent = new MultiplicativePriceSeasonality(
            new Date(1, Month.January, 2024), Frequency.Monthly, [.. Cycle, .. Cycle.Select(f => f + 0.001)]);

        NanoQLException e = Assert.Throws<NanoQLException>(() => curve.SetSeasonality(inconsistent, context));

        Assert.StartsWith("seasonality is inconsistent with inflation term structure", e.Message, StringComparison.Ordinal);
        Assert.Null(curve.Seasonality);
    }

    private static (PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits> Curve, EvaluationContext Context) Curve()
    {
        var rpi = new UKRPI();
        var context = new EvaluationContext { EvaluationDate = Today };
        var d = new Date(1, Month.June, 2022);
        double v = 250.0;
        while (d <= new Date(1, Month.April, 2024))
        {
            context = context with { Fixings = context.Fixings.AddFixing(rpi, d, v) };
            v *= 1.003;
            d += new Period(1, TimeUnit.Months);
        }

        Calendar uk = new UnitedKingdom();
        DayCounter dc = new Thirty360(Thirty360Convention.BondBasis);
        var helpers = new List<BootstrapHelper<ZeroInflationTermStructure>>();
        foreach ((int years, double quote) in new[] { (1, 0.0290), (3, 0.0300), (5, 0.0305) })
        {
            helpers.Add(new ZeroCouponInflationSwapHelper(
                quote,
                new Period(3, TimeUnit.Months),
                uk.Advance(Today, years, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing),
                uk,
                BusinessDayConvention.ModifiedFollowing,
                dc,
                rpi,
                CPIInterpolationType.Flat,
                context));
        }

        return (new PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits>(
            Today, rpi.LastFixingDate(context), Frequency.Monthly, dc, helpers, context), context);
    }
}
