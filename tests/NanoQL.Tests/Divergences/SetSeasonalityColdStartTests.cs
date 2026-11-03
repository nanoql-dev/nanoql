// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="InflationTermStructureExtensions.SetSeasonality"/> on a piecewise zero-inflation curve. QuantLib's
/// <c>setSeasonality</c> marks the curve for recalculation, and its bootstrap restarts from the nodes it last found, so
/// the curve it gives differs from one built with the seasonality from the start: on the wheel, a curve on nine UK RPI
/// zero-coupon swaps from 2.9% to 3.25%, bootstrapped to 1e-14, differs from the fresh one by up to 7.9e-15 at its
/// nodes, and its five-year zero rate is 0.030262300008121912 against the fresh curve's 0.0302623000081228. nanoql's
/// copy is bootstrapped from a cold start, as every bootstrapped curve is, so it is the curve built with the
/// seasonality, bit for bit.
/// </summary>
public class SetSeasonalityColdStartTests
{
    private static readonly Date Today = new(15, Month.June, 2024);

    [Fact]
    public void TheCopyIsTheCurveBuiltWithTheSeasonality()
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

        var seasonality = new MultiplicativePriceSeasonality(
            new Date(1, Month.January, 2023),
            Frequency.Monthly,
            [1.003245, 1.000000, 0.999715, 1.000495, 1.000929, 0.998687, 0.995949, 0.994682, 0.995949, 1.000519, 1.003705, 1.004186]);

        PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits> set =
            Curve(rpi, context, null).SetSeasonality(seasonality, context);
        PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits> fresh = Curve(rpi, context, seasonality);

        Assert.True(set.Nodes().SequenceEqual(fresh.Nodes()));
        Date fiveYears = Today + new Period(5, TimeUnit.Years);
        Assert.Equal(fresh.ZeroRate(fiveYears), set.ZeroRate(fiveYears));
    }

    private static PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits> Curve(
        UKRPI rpi, EvaluationContext context, Seasonality? seasonality)
    {
        Calendar uk = new UnitedKingdom();
        DayCounter dc = new Thirty360(Thirty360Convention.BondBasis);
        var helpers = new List<BootstrapHelper<ZeroInflationTermStructure>>();
        foreach ((int years, double quote) in new[]
        {
            (1, 0.0290), (2, 0.0295), (3, 0.0300), (5, 0.0305), (7, 0.0310), (10, 0.0315), (15, 0.0320), (20, 0.0322),
            (30, 0.0325),
        })
        {
            Date maturity = uk.Advance(Today, years, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing);
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
            Today, rpi.LastFixingDate(context), Frequency.Monthly, dc, helpers, context, seasonality);
    }
}
