// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Monte Carlo cat bond engine's path count (QuantLib's #2839): a parameter, 10,000 by default, refused at 0
/// ("maxPaths must be positive"), and the paths simulated at most; and a cat bond refused by the plain bonds' engine,
/// whose arguments QuantLib's cat bond refuses ("wrong arguments type"); an expired cat bond's results; and the floating
/// cat bond's constructor on dates, the schedule built from them as QuantLib builds it, and its refusal of a rule that
/// takes no stub.
/// </summary>
public class MonteCarloCatBondEngineConventionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void APathCountBelowOneIsRefused(int maxPaths)
    {
        (FloatingCatBond bond, EvaluationContext context, YieldTermStructure discount) = Bond();
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => MonteCarloCatBondEngine.Calculate(
            bond, context, new BetaRisk(5000, 50, 500, 500), discount, maxPaths: maxPaths));
        Assert.Equal("maxPaths", e.ParamName);
        Assert.StartsWith("maxPaths must be positive", e.Message);
    }

    [Fact]
    public void ThePathCountCapsTheSimulation()
    {
        // one path: a loss or none, so the loss probability is 0 or 1
        (FloatingCatBond bond, EvaluationContext context, YieldTermStructure discount) = Bond();
        CatBondResults results = MonteCarloCatBondEngine.Calculate(bond, context, new BetaRisk(5000, 50, 500, 500), discount, maxPaths: 1);
        Assert.Contains(results.LossProbability!.Value, new[] { 0.0, 1.0 });
    }

    [Fact]
    public void ThePlainBondsEngineRefusesACatBond()
    {
        (FloatingCatBond bond, EvaluationContext context, YieldTermStructure discount) = Bond();
        var e = Assert.Throws<NanoQLException>(() => DiscountingBondEngine.Calculate(bond, context, discount));
        Assert.Equal("wrong arguments type", e.Message);
    }

    [Fact]
    public void AnExpiredCatBondsValueIsZeroAndItsStatisticsNull()
    {
        (FloatingCatBond bond, _, _) = Bond();
        var later = new EvaluationContext { EvaluationDate = new Date(1, December, 2008) };
        CatBondResults results = MonteCarloCatBondEngine.Calculate(
            bond, later, new BetaRisk(5000, 50, 500, 500), new FlatForward(later.EvaluationDate, 0.03, new Actual360()));
        Assert.Equal(0.0, results.NPV);
        Assert.Equal(0.0, results.SettlementValue);
        Assert.Null(results.LossProbability);
        Assert.Null(results.ExhaustionProbability);
        Assert.Null(results.ExpectedLoss);
    }

    [Fact]
    public void TheDatesConstructorIsTheScheduleConstructor()
    {
        (FloatingCatBond onSchedule, EvaluationContext context, _) = Bond();
        var riskFree = new FlatForward(context.EvaluationDate, 0.025, new Actual360());
        var onDates = CouponPricerFunctions.SetCouponPricer(
            new FloatingCatBond(
                1, 1000000.0, new Date(30, November, 2004), new Date(30, November, 2008), Frequency.Semiannual,
                new UnitedStates(UnitedStatesMarket.GovernmentBond), new USDLibor(new Period(6, TimeUnit.Months), riskFree),
                new ActualActual(ActualActualConvention.ISMA), new ProportionalNotionalRisk(new NoOffset(), 500, 1500), context,
                BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, 1, [], [], [], [], false, 100.0,
                new Date(30, November, 2004)),
            new BlackIborCouponPricer(context));
        Assert.Equal(onSchedule.Cashflows.Select(cf => cf.Date), onDates.Cashflows.Select(cf => cf.Date));
        Assert.Equal(onSchedule.Cashflows.Select(cf => cf.Amount(context)), onDates.Cashflows.Select(cf => cf.Amount(context)));
        // QuantLib's maturities: the schedule's adjusted end, and the date given
        Assert.Equal(new Date(28, November, 2008), onSchedule.MaturityDate());
        Assert.Equal(new Date(30, November, 2008), onDates.MaturityDate());
    }

    [Theory]
    [InlineData(DateGenerationRule.Zero, "stub date (null date) not allowed with Zero DateGeneration::Rule")]
    [InlineData(DateGenerationRule.CDS, "unknown DateGeneration::Rule (")]
    public void ARuleThatTakesNoStubIsRefused(DateGenerationRule rule, string message)
    {
        var context = new EvaluationContext { EvaluationDate = new Date(22, November, 2004) };
        var riskFree = new FlatForward(context.EvaluationDate, 0.025, new Actual360());
        var e = Assert.Throws<ArgumentException>(() => new FloatingCatBond(
            1, 1000000.0, new Date(30, November, 2004), new Date(30, November, 2008), Frequency.Semiannual,
            new UnitedStates(UnitedStatesMarket.GovernmentBond), new USDLibor(new Period(6, TimeUnit.Months), riskFree),
            new ActualActual(ActualActualConvention.ISMA), new DigitalNotionalRisk(new NoOffset(), 100), context, rule: rule));
        Assert.StartsWith(message, e.Message);
    }

    private static (FloatingCatBond Bond, EvaluationContext Context, YieldTermStructure Discount) Bond()
    {
        var today = new Date(22, November, 2004);
        var context = new EvaluationContext { EvaluationDate = today };
        var riskFree = new FlatForward(today, 0.025, new Actual360());
        var schedule = new Schedule(
            new Date(30, November, 2004), new Date(30, November, 2008), new Period(Frequency.Semiannual),
            new UnitedStates(UnitedStatesMarket.GovernmentBond), BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward, false, context);
        FloatingCatBond bond = CouponPricerFunctions.SetCouponPricer(
            new FloatingCatBond(
                1, 1000000.0, schedule, new USDLibor(new Period(6, TimeUnit.Months), riskFree), new ActualActual(ActualActualConvention.ISMA),
                new ProportionalNotionalRisk(new NoOffset(), 500, 1500), context, BusinessDayConvention.ModifiedFollowing, 1, [], [], [], [],
                false, 100.0, new Date(30, November, 2004)),
            new BlackIborCouponPricer(context));
        return (bond, context, new FlatForward(today, 0.03, new Actual360()));
    }
}
