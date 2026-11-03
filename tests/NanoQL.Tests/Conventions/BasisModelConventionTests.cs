// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.TestSuite;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The tenor basis models' conventions: the cash-flow sets' empty forms and refusals, a seasoned swap's empty flows and
/// the tenor swaption structure's refusal of them (QuantLib reads the last of the empty flows, undefined), the tenor
/// optionlet structure's frequency check, and the structures' inspectors.
/// </summary>
public class BasisModelConventionTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.June, 2026) };

    [Fact]
    public void TheDefaultSetsHaveNoFlows()
    {
        var swaption = new SwaptionCashFlows();
        Assert.Empty(swaption.FloatLeg);
        Assert.Empty(swaption.FloatTimes);
        Assert.Empty(swaption.FloatWeights);
        Assert.Empty(swaption.FixedLeg);
        Assert.Empty(swaption.FixedTimes);
        Assert.Empty(swaption.FixedWeights);
        Assert.Empty(swaption.AnnuityWeights);
        Assert.Empty(swaption.ExerciseTimes);
        Assert.Null(swaption.Swaption);
        Assert.Empty(new SwapCashFlows().FixedLeg);
        Assert.Empty(new IborLegCashFlows().FloatLeg);
    }

    [Fact]
    public void ALegMustBeCoupons()
    {
        YieldTermStructure curve = Curve(Context);
        Assert.Throws<ArgumentException>(() => new IborLegCashFlows([], curve, Context));
        ImmutableArray<CashFlow> leg = [new SimpleCashFlow(100.0, Context.EvaluationDate + 400)];
        Assert.Equal(
            "FloatingLeg CashFlow is no Coupon.",
            Assert.Throws<NanoQLException>(() => new IborLegCashFlows(leg, curve, Context)).Message);
    }

    [Fact]
    public void ASeasonedLegHasNoFlowsAndTheTenorStructureRefusesIt()
    {
        // every floating coupon of a swap from today starts before a discount curve dated three years on
        VanillaSwap swap = Swap(Context.EvaluationDate, 2);
        var later = new EvaluationContext { EvaluationDate = Context.EvaluationDate + new Period(3, TimeUnit.Years) };
        var flows = new SwapCashFlows(swap, Curve(later), Context);
        Assert.Empty(flows.FloatLeg);
        Assert.Empty(flows.FixedLeg);

        // a 1-into-1-year swaption's swap: its last coupon starts six months before it ends, and a discount curve dated
        // in between still prices the swaps but leaves no coupon to map
        var between = new EvaluationContext { EvaluationDate = Context.EvaluationDate + new Period(20, TimeUnit.Months) };
        var vts = new TenorSwaptionVTS(
            BasisModelsTests.GetSwaptionVTS(Context), Curve(between), new Euribor6M(Curve(Context)),
            new Euribor3M(Curve(Context)), new Period(1, TimeUnit.Years), new Period(1, TimeUnit.Years),
            new Thirty360(Thirty360Convention.BondBasis), new Thirty360(Thirty360Convention.BondBasis), Context);
        var e = Assert.Throws<NanoQLException>(() => vts.Volatility(new Period(1, TimeUnit.Years), new Period(1, TimeUnit.Years), 0.01, true));
        Assert.Equal("the swap has no coupon left on or after the discount curve's reference date: no flows to map", e.Message);
    }

    [Fact]
    public void ATargetTenorMustBeAMultipleOfTheBase()
    {
        var corr = new TwoParameterCorrelation(
            default(Linear).Interpolate([0.0, 50.0], [0.3, 0.3]), default(Linear).Interpolate([0.0, 50.0], [0.9, 0.9]));
        var e = Assert.Throws<NanoQLException>(() => new TenorOptionletVTS(
            BasisModelsTests.GetOptionletTS(Context), new Euribor6M(Curve(Context)), new Euribor3M(Curve(Context)), corr,
            Context));
        Assert.Equal("Base index frequency must be a multiple of target tenor frequency", e.Message);
    }

    [Fact]
    public void TheTwoParameterCorrelationDecaysToItsLongRunLevel()
    {
        var corr = new TwoParameterCorrelation(
            default(Linear).Interpolate([0.0, 50.0], [0.3, 0.3]), default(Linear).Interpolate([0.0, 50.0], [0.9, 0.9]));
        Assert.Equal(1.0, corr.Evaluate(2.0, 2.0));
        Assert.Equal(0.3 + (0.7 * Math.Exp(-0.9 * 1.5)), corr.Evaluate(2.0, 3.5), 1e-15);
        Assert.Equal(corr.Evaluate(2.0, 3.5), corr.Evaluate(2.0, 0.5), 1e-15);
    }

    [Fact]
    public void TheStructuresAreTheirBases()
    {
        OptionletVolatilityStructure baseVTS = BasisModelsTests.GetOptionletTS(Context);
        var corr = new TwoParameterCorrelation(
            default(Linear).Interpolate([0.0, 50.0], [0.3, 0.3]), default(Linear).Interpolate([0.0, 50.0], [0.9, 0.9]));
        IborIndex base3m = new Euribor3M(Curve(Context));
        IborIndex targ6m = new Euribor6M(Curve(Context));
        var optionlet = new TenorOptionletVTS(baseVTS, base3m, targ6m, corr, Context);
        Assert.Equal((baseVTS.MaxDate, baseVTS.MinStrike(), baseVTS.MaxStrike()), (optionlet.MaxDate, optionlet.MinStrike(), optionlet.MaxStrike()));
        Assert.Equal((VolatilityType.Normal, baseVTS.ReferenceDate), (optionlet.VolatilityType, optionlet.ReferenceDate));
        Assert.Same(baseVTS, optionlet.BaseVTS);
        Assert.Same(base3m, optionlet.BaseIndex);
        Assert.Same(targ6m, optionlet.TargIndex);
        Assert.Same(corr, optionlet.Correlation);
        Assert.Same(Context, optionlet.Context);
        SmileSection section = optionlet.SmileSection(new Period(5, TimeUnit.Years));
        Assert.Equal(section.MaxStrike() - section.MinStrike(), baseVTS.SmileSection(new Period(5, TimeUnit.Years)).MaxStrike()
            - baseVTS.SmileSection(new Period(5, TimeUnit.Years)).MinStrike(), 1e-12);

        SwaptionVolatilityStructure swBase = BasisModelsTests.GetSwaptionVTS(Context);
        YieldTermStructure disc = Curve(Context);
        var bondBasis = new Thirty360(Thirty360Convention.BondBasis);
        var swaption = new TenorSwaptionVTS(
            swBase, disc, targ6m, base3m, new Period(1, TimeUnit.Years), new Period(6, TimeUnit.Months), bondBasis,
            new Actual360(), Context);
        Assert.Equal((swBase.MaxDate, swBase.MinStrike(), swBase.MaxStrike(), swBase.MaxSwapTenor),
            (swaption.MaxDate, swaption.MinStrike(), swaption.MaxStrike(), swaption.MaxSwapTenor));
        Assert.Equal(VolatilityType.Normal, swaption.VolatilityType);
        Assert.Same(swBase, swaption.BaseVTS);
        Assert.Same(disc, swaption.DiscountCurve);
        Assert.Same(targ6m, swaption.BaseIndex);
        Assert.Same(base3m, swaption.TargIndex);
        Assert.Equal((new Period(1, TimeUnit.Years), new Period(6, TimeUnit.Months)), (swaption.BaseFixedFreq, swaption.TargFixedFreq));
        Assert.Same(bondBasis, swaption.BaseFixedDC);
        Assert.IsType<Actual360>(swaption.TargFixedDC);
        Assert.Same(Context, swaption.Context);
        SmileSection swSection = swaption.SmileSection(new Period(5, TimeUnit.Years), new Period(5, TimeUnit.Years), true);
        Assert.True(swSection.MaxStrike() > swSection.MinStrike());
    }

    private static YieldTermStructure Curve(EvaluationContext context) =>
        BasisModelsTests.GetYTS(
            [new(0, TimeUnit.Days), new(5, TimeUnit.Years), new(61, TimeUnit.Years)], [0.01, 0.012, 0.015], context);

    private static VanillaSwap Swap(Date start, int years)
    {
        var target = new TARGET();
        Date end = target.Advance(start, new Period(years, TimeUnit.Years), BusinessDayConvention.Following);
        var fixedSchedule = new Schedule(
            start, end, new Period(1, TimeUnit.Years), target, BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward, false, Context);
        var floatSchedule = new Schedule(
            start, end, new Period(6, TimeUnit.Months), target, BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward, false, Context);
        var index = new Euribor6M(Curve(Context));
        return new VanillaSwap(
            SwapType.Payer, 1.0, fixedSchedule, 0.02, new Thirty360(Thirty360Convention.BondBasis), floatSchedule, index,
            0.0, index.DayCounter, Context);
    }
}
