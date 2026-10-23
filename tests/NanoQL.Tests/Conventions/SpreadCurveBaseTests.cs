// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The piecewise spread curve on S5's bootstrap (the S-sr plan's SSD20): its maximum date is the earlier of its own and
/// its base's, as QuantLib's spread discount curve's (<c>spreaddiscountcurve.hpp:116-118</c>), and its reference date,
/// day counter and extrapolation flag are the base's, so a curve on a base dated after the evaluation date is dated
/// there and reprices its helpers.
/// </summary>
public class SpreadCurveBaseTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    private static readonly (int Years, double Rate)[] Swaps = [(2, 0.031), (5, 0.034)];

    [Fact]
    public void TheBasesMaximumDateBoundsTheSpreadCurves()
    {
        var dc = new Actual365Fixed();

        // a base to 10 years: the curve's own maximum date, its last pillar's
        YieldTermStructure longBase = new ZeroCurve(
            [Today, Today + new Period(10, TimeUnit.Years)], [0.02, 0.03], dc);
        var onLong = new PiecewiseSpreadYieldCurve<LogLinear, IterativeBootstrap>(longBase, Helpers(), Context);
        Assert.Equal(onLong.Dates()[^1], onLong.MaxDate);

        // a base to 3 years that extrapolates: the base's, before the last pillar; the flag is the base's
        YieldTermStructure shortBase = new ZeroCurve(
            [Today, Today + new Period(3, TimeUnit.Years)], [0.02, 0.03], dc).EnableExtrapolation();
        var onShort = new PiecewiseSpreadYieldCurve<LogLinear, IterativeBootstrap>(shortBase, Helpers(), Context);
        Assert.Equal(shortBase.MaxDate, onShort.MaxDate);
        Assert.True(onShort.Dates()[^1] > onShort.MaxDate);
        Assert.True(onShort.AllowsExtrapolation);
        Assert.False(onLong.AllowsExtrapolation);
    }

    [Fact]
    public void ASpreadCurveOnABaseDatedAfterTheEvaluationDateIsDatedThere()
    {
        Date settlement = new TARGET().Advance(Today, 2, TimeUnit.Days);
        YieldTermStructure baseCurve = new FlatForward(settlement, 0.025, new Actual365Fixed());
        var curve = new PiecewiseSpreadYieldCurve<LogLinear, IterativeBootstrap>(baseCurve, Helpers(), Context);
        Assert.Equal(settlement, curve.ReferenceDate);
        Assert.Equal(baseCurve.DayCounter, curve.DayCounter);
        Assert.Equal(settlement, curve.Dates()[0]);

        var index = new Euribor6M(curve);
        foreach ((int years, double rate) in Swaps)
        {
            VanillaSwap swap = new MakeVanillaSwap(new Period(years, TimeUnit.Years), index)
                .WithFixedRate(0.0)
                .WithFixedLegDayCount(new Thirty360(Thirty360Convention.BondBasis))
                .WithFixedLegTenor(new Period(1, TimeUnit.Years))
                .WithFixedLegConvention(BusinessDayConvention.Unadjusted)
                .WithFixedLegTerminationDateConvention(BusinessDayConvention.Unadjusted)
                .ToVanillaSwap(Context);
            double fair = DiscountingSwapEngine.Calculate(swap, Context, curve, includeSettlementDateFlows: false).FairRate!.Value;
            Assert.True(Math.Abs(fair - rate) <= 1e-9, $"{years} years: {fair}");
        }
    }

    private static List<RateHelper> Helpers()
    {
        var index = new Euribor6M();
        var helpers = new List<RateHelper>
        {
            new DepositRateHelper(
                0.03, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
                new Actual360(), Context),
        };
        foreach ((int years, double rate) in Swaps)
        {
            helpers.Add(new SwapRateHelper(
                rate, new Period(years, TimeUnit.Years), new TARGET(), Frequency.Annual, BusinessDayConvention.Unadjusted,
                new Thirty360(Thirty360Convention.BondBasis), index, Context));
        }

        return helpers;
    }
}
