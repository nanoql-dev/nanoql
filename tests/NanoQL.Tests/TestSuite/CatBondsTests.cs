// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/catbonds.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

/// <summary>QuantLib's cat bond tests, <c>test-suite/catbonds.cpp</c>.</summary>
/// <remarks>
/// <c>testBetaRisk</c> runs on the Poisson process: its counts at QuantLib's 2% and 5%, its loss mean and variance
/// within three standard errors of their estimators, computed from the sample (the mean's s/√n; the variance's
/// fourth-moment form √((m₄ − s⁴)/n), the loss being compound Poisson), where QuantLib's checks them at 1%, which its
/// geometric process passes on its seed and the fixed one misses by 2.22% on the variance
/// (<c>Divergences/BetaRiskTests.cs</c>).
/// </remarks>
[Trait("Oracle", "QuantLib")]
public class CatBondsTests
{
    private static readonly KeyValuePair<Date, double>[] SampleEvents =
    [
        new(new Date(1, February, 2012), 100),
        new(new Date(1, July, 2013), 150),
        new(new Date(5, January, 2014), 50),
    ];

    private static readonly Date EventsStart = new(1, January, 2011);
    private static readonly Date EventsEnd = new(31, December, 2014);

    private const double FaceAmount = 1000000.0;

    [Fact]
    public void TestEventSetForWholeYears()
    {
        // Testing that catastrophe events are split correctly for periods of whole years...
        var catRisk = new EventSet(SampleEvents, EventsStart, EventsEnd);
        CatSimulation simulation = catRisk.NewSimulation(new Date(1, January, 2015), new Date(31, December, 2015));

        var path = new List<KeyValuePair<Date, double>>();

        Assert.True(simulation.NextPath(path));
        Assert.Empty(path);

        Assert.True(simulation.NextPath(path));
        Assert.Single(path);
        Assert.Equal(new Date(1, February, 2015), path[0].Key);
        Assert.Equal(100, path[0].Value);

        Assert.True(simulation.NextPath(path));
        Assert.Single(path);
        Assert.Equal(new Date(1, July, 2015), path[0].Key);
        Assert.Equal(150, path[0].Value);

        Assert.True(simulation.NextPath(path));
        Assert.Single(path);
        Assert.Equal(new Date(5, January, 2015), path[0].Key);
        Assert.Equal(50, path[0].Value);

        Assert.False(simulation.NextPath(path));
    }

    [Fact]
    public void TestEventSetForIrregularPeriods()
    {
        // Testing that catastrophe events are split correctly for irregular periods...
        var catRisk = new EventSet(SampleEvents, EventsStart, EventsEnd);
        CatSimulation simulation = catRisk.NewSimulation(new Date(2, January, 2015), new Date(5, January, 2016));

        var path = new List<KeyValuePair<Date, double>>();

        Assert.True(simulation.NextPath(path));
        Assert.Empty(path);

        Assert.True(simulation.NextPath(path));
        Assert.Equal(2, path.Count);
        Assert.Equal(new Date(1, July, 2015), path[0].Key);
        Assert.Equal(150, path[0].Value);
        Assert.Equal(new Date(5, January, 2016), path[1].Key);
        Assert.Equal(50, path[1].Value);

        Assert.False(simulation.NextPath(path));
    }

    [Fact]
    public void TestEventSetForNoEvents()
    {
        // Testing that catastrophe events are split correctly when there are no simulated events...
        var catRisk = new EventSet([], EventsStart, EventsEnd);
        CatSimulation simulation = catRisk.NewSimulation(new Date(2, January, 2015), new Date(5, January, 2016));

        var path = new List<KeyValuePair<Date, double>>();

        Assert.True(simulation.NextPath(path));
        Assert.Empty(path);

        Assert.True(simulation.NextPath(path));
        Assert.Empty(path);

        Assert.False(simulation.NextPath(path));
    }

    // Adapted: the loss moments are checked at three standard errors of their estimators (the class remarks).
    [Fact]
    public void TestBetaRisk()
    {
        // Testing that beta risk gives correct terminal distribution...
        const int paths = 1000000;
        var catRisk = new BetaRisk(100.0, 100.0, 10.0, 15.0);
        CatSimulation simulation = catRisk.NewSimulation(new Date(2, January, 2015), new Date(2, January, 2018));

        var path = new List<KeyValuePair<Date, double>>();
        Real sum = 0.0;
        Real sumSquares = 0.0;
        Real sumCubes = 0.0;
        Real sumFourths = 0.0;
        Real poissonSum = 0.0;
        Real poissonSumSquares = 0.0;

        for (int i = 0; i < paths; ++i)
        {
            Assert.True(simulation.NextPath(path), "No next path available");
            Real processValue = 0.0;
            foreach (KeyValuePair<Date, double> j in path)
            {
                processValue += j.Value;
            }

            sum += processValue;
            sumSquares += processValue * processValue;
            sumCubes += processValue * processValue * processValue;
            sumFourths += processValue * processValue * processValue * processValue;
            poissonSum += path.Count;
            poissonSumSquares += path.Count * path.Count;
        }

        Real poissonMean = poissonSum / paths;
        CheckClose(3.0 / 100.0, poissonMean, 2);
        Real poissonVar = (poissonSumSquares / paths) - (poissonMean * poissonMean);
        CheckClose(3.0 / 100.0, poissonVar, 5);

        Real expectedMean = 3.0 * 10.0 / 100.0;
        Real actualMean = sum / paths;
        Real actualVar = (sumSquares / paths) - (actualMean * actualMean);
        Real meanError = Math.Sqrt(actualVar / paths);
        Assert.True(
            Math.Abs(actualMean - expectedMean) <= 3.0 * meanError,
            $"loss mean {actualMean}, expected {expectedMean}, standard error {meanError}");

        Real expectedVar = 3.0 * ((15.0 * 15.0) + (10 * 10)) / 100.0;

        // the fourth central moment, from the raw moments
        Real m = actualMean;
        Real m4 = (sumFourths / paths) - (4 * m * sumCubes / paths) + (6 * m * m * sumSquares / paths) - (3 * m * m * m * m);
        Real varError = Math.Sqrt((m4 - (actualVar * actualVar)) / paths);
        Assert.True(
            Math.Abs(actualVar - expectedVar) <= 3.0 * varError,
            $"loss variance {actualVar}, expected {expectedVar}, standard error {varError}");
    }

    [Fact]
    public void TestRiskFreeAgainstFloatingRateBond()
    {
        // Testing floating-rate cat bond against risk-free floating-rate bond...
        var today = new Date(22, November, 2004);
        var context = new EvaluationContext { EvaluationDate = today };
        bool usingAtParCoupons = context.UseAtParCoupons;

        Natural settlementDays = 1;

        var riskFreeRate = new FlatForward(today, 0.025, new Actual360());
        var discountCurve = new FlatForward(today, 0.03, new Actual360());

        IborIndex index = new USDLibor(6 * TimeUnit.Months, riskFreeRate);
        Natural fixingDays = 1;

        Real tolerance = 1.0e-6;

        IborCouponPricer pricer = new BlackIborCouponPricer(context);

        // plain

        Schedule sch = UsGovernmentSchedule(context);

        CatRisk noCatRisk = new EventSet([], new Date(1, January, 2000), new Date(31, December, 2010));

        EventPaymentOffset paymentOffset = new NoOffset();
        NotionalRisk notionalRisk = new DigitalNotionalRisk(paymentOffset, 100);

        var bond1 = new FloatingRateBond(
            settlementDays, FaceAmount, sch, index, new ActualActual(ActualActualConvention.ISMA), context,
            BusinessDayConvention.ModifiedFollowing, fixingDays, [], [], [], [], false, 100.0,
            new Date(30, November, 2004));

        FloatingCatBond catBond1 = CatBond(sch, index, notionalRisk, context, fixingDays, []);

        bond1 = CouponPricerFunctions.SetCouponPricer(bond1, pricer);
        catBond1 = CouponPricerFunctions.SetCouponPricer(catBond1, pricer);

        Real cachedPrice1 = usingAtParCoupons ? 99.874646 : 99.874645;

        Real price = bond1.CleanPrice(DiscountingBondEngine.Calculate(bond1, context, riskFreeRate), context);
        Real catPrice = catBond1.CleanPrice(MonteCarloCatBondEngine.Calculate(catBond1, context, noCatRisk, riskFreeRate), context);
        Assert.False(
            Math.Abs(price - cachedPrice1) > tolerance || Math.Abs(catPrice - price) > tolerance,
            $"failed to reproduce floating rate bond price: floating bond {price}, catBond bond {catPrice}, "
            + $"expected {cachedPrice1}, error {catPrice - price}");

        // different risk-free and discount curve

        var bond2 = new FloatingRateBond(
            settlementDays, FaceAmount, sch, index, new ActualActual(ActualActualConvention.ISMA), context,
            BusinessDayConvention.ModifiedFollowing, fixingDays, [], [], [], [], false, 100.0,
            new Date(30, November, 2004));

        FloatingCatBond catBond2 = CatBond(sch, index, notionalRisk, context, fixingDays, []);

        bond2 = CouponPricerFunctions.SetCouponPricer(bond2, pricer);
        catBond2 = CouponPricerFunctions.SetCouponPricer(catBond2, pricer);

        Real cachedPrice2 = 97.955904;

        price = bond2.CleanPrice(DiscountingBondEngine.Calculate(bond2, context, discountCurve), context);
        catPrice = catBond2.CleanPrice(MonteCarloCatBondEngine.Calculate(catBond2, context, noCatRisk, discountCurve), context);
        Assert.False(
            Math.Abs(price - cachedPrice2) > tolerance || Math.Abs(catPrice - price) > tolerance,
            $"failed to reproduce floating rate bond price: floating bond {price}, catBond bond {catPrice}, "
            + $"expected {cachedPrice2}, error {catPrice - price}");

        // varying spread

        Rate[] spreads = [0.001, 0.0012, 0.0014, 0.0016];

        var bond3 = new FloatingRateBond(
            settlementDays, FaceAmount, sch, index, new ActualActual(ActualActualConvention.ISMA), context,
            BusinessDayConvention.ModifiedFollowing, fixingDays, [], spreads, [], [], false, 100.0,
            new Date(30, November, 2004));

        FloatingCatBond catBond3 = CatBond(sch, index, notionalRisk, context, fixingDays, spreads);

        bond3 = CouponPricerFunctions.SetCouponPricer(bond3, pricer);
        catBond3 = CouponPricerFunctions.SetCouponPricer(catBond3, pricer);

        Real cachedPrice3 = usingAtParCoupons ? 98.495459 : 98.495458;

        price = bond3.CleanPrice(DiscountingBondEngine.Calculate(bond3, context, discountCurve), context);
        catPrice = catBond3.CleanPrice(MonteCarloCatBondEngine.Calculate(catBond3, context, noCatRisk, discountCurve), context);
        Assert.False(
            Math.Abs(price - cachedPrice3) > tolerance || Math.Abs(catPrice - price) > tolerance,
            $"failed to reproduce floating rate bond price: floating bond {price}, catBond bond {catPrice}, "
            + $"expected {cachedPrice2}, error {catPrice - price}");
    }

    [Fact]
    public void TestCatBondInDoomScenario()
    {
        // Testing floating-rate cat bond in a doom scenario (certain default)...
        (EvaluationContext context, YieldTermStructure discountCurve, IborIndex index, IborCouponPricer pricer) = Market();

        Real tolerance = 1.0e-6;

        Schedule sch = UsGovernmentSchedule(context);

        CatRisk doomCatRisk = new EventSet(
            [new(new Date(30, November, 2004), 1000)], new Date(30, November, 2004), new Date(30, November, 2008));

        NotionalRisk notionalRisk = new DigitalNotionalRisk(new NoOffset(), 100);

        FloatingCatBond catBond = CouponPricerFunctions.SetCouponPricer(CatBond(sch, index, notionalRisk, context, 1, []), pricer);

        CatBondResults results = MonteCarloCatBondEngine.Calculate(catBond, context, doomCatRisk, discountCurve);
        Real price = catBond.CleanPrice(results, context);
        Assert.Equal(0, price);

        CheckClose(1.0, results.LossProbability!.Value, tolerance);
        CheckClose(1.0, results.ExhaustionProbability!.Value, tolerance);
        CheckClose(1.0, results.ExpectedLoss!.Value, tolerance);
    }

    [Fact]
    public void TestCatBondWithDoomOnceInTenYears()
    {
        // Testing floating-rate cat bond in a doom once in 10 years scenario...
        (EvaluationContext context, YieldTermStructure discountCurve, IborIndex index, IborCouponPricer pricer) = Market();

        Real tolerance = 1.0e-6;

        Schedule sch = UsGovernmentSchedule(context);

        CatRisk doomCatRisk = new EventSet(
            [new(new Date(30, November, 2008), 1000)], new Date(30, November, 2004), new Date(30, November, 2044));

        CatRisk noCatRisk = new EventSet([], new Date(1, January, 2000), new Date(31, December, 2010));

        NotionalRisk notionalRisk = new DigitalNotionalRisk(new NoOffset(), 100);

        FloatingCatBond catBond = CouponPricerFunctions.SetCouponPricer(CatBond(sch, index, notionalRisk, context, 1, []), pricer);

        var dc = new ActualActual(ActualActualConvention.ISMA);
        CatBondResults results = MonteCarloCatBondEngine.Calculate(catBond, context, doomCatRisk, discountCurve);
        Real price = catBond.CleanPrice(results, context);
        Real yield = catBond.Yield(results, dc, Compounding.Simple, Frequency.Annual, context);

        CheckClose(0.1, results.LossProbability!.Value, tolerance);
        CheckClose(0.1, results.ExhaustionProbability!.Value, tolerance);
        CheckClose(0.1, results.ExpectedLoss!.Value, tolerance);

        CatBondResults riskFree = MonteCarloCatBondEngine.Calculate(catBond, context, noCatRisk, discountCurve);
        Real riskFreePrice = catBond.CleanPrice(riskFree, context);
        Real riskFreeYield = catBond.Yield(riskFree, dc, Compounding.Simple, Frequency.Annual, context);

        CheckClose(0.0, riskFree.LossProbability!.Value, tolerance);
        CheckClose(0.0, riskFree.ExhaustionProbability!.Value, tolerance);
        Assert.True(Math.Abs(riskFree.ExpectedLoss!.Value) < tolerance);

        CheckClose(riskFreePrice * 0.9, price, tolerance);
        Assert.True(riskFreeYield < yield);
    }

    [Fact]
    public void TestCatBondWithDoomOnceInTenYearsProportional()
    {
        // Testing floating-rate cat bond in a doom once in 10 years scenario with proportional notional reduction...
        (EvaluationContext context, YieldTermStructure discountCurve, IborIndex index, IborCouponPricer pricer) = Market();

        Real tolerance = 1.0e-6;

        Schedule sch = UsGovernmentSchedule(context);

        CatRisk doomCatRisk = new EventSet(
            [new(new Date(30, November, 2008), 1000)], new Date(30, November, 2004), new Date(30, November, 2044));

        CatRisk noCatRisk = new EventSet([], new Date(1, January, 2000), new Date(31, December, 2010));

        NotionalRisk notionalRisk = new ProportionalNotionalRisk(new NoOffset(), 500, 1500);

        FloatingCatBond catBond = CouponPricerFunctions.SetCouponPricer(CatBond(sch, index, notionalRisk, context, 1, []), pricer);

        var dc = new ActualActual(ActualActualConvention.ISMA);
        CatBondResults results = MonteCarloCatBondEngine.Calculate(catBond, context, doomCatRisk, discountCurve);
        Real price = catBond.CleanPrice(results, context);
        Real yield = catBond.Yield(results, dc, Compounding.Simple, Frequency.Annual, context);

        CheckClose(0.1, results.LossProbability!.Value, tolerance);
        CheckClose(0.0, results.ExhaustionProbability!.Value, tolerance);
        CheckClose(0.05, results.ExpectedLoss!.Value, tolerance);

        CatBondResults riskFree = MonteCarloCatBondEngine.Calculate(catBond, context, noCatRisk, discountCurve);
        Real riskFreePrice = catBond.CleanPrice(riskFree, context);
        Real riskFreeYield = catBond.Yield(riskFree, dc, Compounding.Simple, Frequency.Annual, context);

        CheckClose(0.0, riskFree.LossProbability!.Value, tolerance);
        Assert.True(Math.Abs(riskFree.ExpectedLoss!.Value) < tolerance);

        CheckClose(riskFreePrice * 0.95, price, tolerance);
        Assert.True(riskFreeYield < yield);
    }

    [Fact]
    public void TestCatBondWithGeneratedEventsProportional()
    {
        // Testing floating-rate cat bond in a generated scenario with proportional notional reduction...
        (EvaluationContext context, YieldTermStructure discountCurve, IborIndex index, IborCouponPricer pricer) = Market();

        Real tolerance = 1.0e-6;

        Schedule sch = UsGovernmentSchedule(context);

        CatRisk betaCatRisk = new BetaRisk(5000, 50, 500, 500);

        CatRisk noCatRisk = new EventSet([], new Date(1, January, 2000), new Date(31, December, 2010));

        NotionalRisk notionalRisk = new ProportionalNotionalRisk(new NoOffset(), 500, 1500);

        FloatingCatBond catBond = CouponPricerFunctions.SetCouponPricer(CatBond(sch, index, notionalRisk, context, 1, []), pricer);

        var dc = new ActualActual(ActualActualConvention.ISMA);
        CatBondResults results = MonteCarloCatBondEngine.Calculate(catBond, context, betaCatRisk, discountCurve);
        Real price = catBond.CleanPrice(results, context);
        Real yield = catBond.Yield(results, dc, Compounding.Simple, Frequency.Annual, context);
        Real lossProbability = results.LossProbability!.Value;
        Real exhaustionProbability = results.ExhaustionProbability!.Value;
        Real expectedLoss = results.ExpectedLoss!.Value;

        Assert.True(lossProbability < 1.0 && lossProbability > 0.0);
        Assert.True(exhaustionProbability < 1.0 && exhaustionProbability > 0.0);
        Assert.True(expectedLoss > 0.0);

        CatBondResults riskFree = MonteCarloCatBondEngine.Calculate(catBond, context, noCatRisk, discountCurve);
        Real riskFreePrice = catBond.CleanPrice(riskFree, context);
        Real riskFreeYield = catBond.Yield(riskFree, dc, Compounding.Simple, Frequency.Annual, context);

        CheckClose(0.0, riskFree.LossProbability!.Value, tolerance);
        Assert.True(Math.Abs(riskFree.ExpectedLoss!.Value) < tolerance);

        Assert.True(riskFreePrice > price);
        Assert.True(riskFreeYield < yield);
    }

    // The common market of the bond cases: today 22 November 2004, the index on a 2.5% curve, the bond discounted on a
    // 3% one, and the Black coupon pricer without volatility.
    private static (EvaluationContext Context, YieldTermStructure DiscountCurve, IborIndex Index, IborCouponPricer Pricer) Market()
    {
        var today = new Date(22, November, 2004);
        var context = new EvaluationContext { EvaluationDate = today };
        var riskFreeRate = new FlatForward(today, 0.025, new Actual360());
        var discountCurve = new FlatForward(today, 0.03, new Actual360());
        IborIndex index = new USDLibor(6 * TimeUnit.Months, riskFreeRate);
        return (context, discountCurve, index, new BlackIborCouponPricer(context));
    }

    private static Schedule UsGovernmentSchedule(EvaluationContext context) =>
        new(
            new Date(30, November, 2004), new Date(30, November, 2008), new Period(Frequency.Semiannual),
            new UnitedStates(UnitedStatesMarket.GovernmentBond), BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward, false, context);

    private static FloatingCatBond CatBond(
        Schedule sch, IborIndex index, NotionalRisk notionalRisk, EvaluationContext context, Natural fixingDays, Rate[] spreads) =>
        new(
            1, FaceAmount, sch, index, new ActualActual(ActualActualConvention.ISMA), notionalRisk, context,
            BusinessDayConvention.ModifiedFollowing, fixingDays, [], spreads, [], [], false, 100.0,
            new Date(30, November, 2004));

    // QL_CHECK_CLOSE: the difference within the tolerance, in percent, of both values.
    private static void CheckClose(Real a, Real b, Real percent)
    {
        Real diff = Math.Abs(a - b);
        Assert.True(
            diff <= Math.Abs(a) * percent / 100.0 && diff <= Math.Abs(b) * percent / 100.0,
            $"difference between {a} and {b} exceeds {percent}%");
    }
}
