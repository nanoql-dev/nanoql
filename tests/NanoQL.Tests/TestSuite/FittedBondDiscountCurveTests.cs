// SPDX-FileCopyrightText: Copyright (C) 2019 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/fittedbonddiscountcurve.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class FittedBondDiscountCurveTests
{
    [Fact]
    public void TestEvaluation()
    {
        // Testing that fitted bond curves work as evaluators...

        var context = new EvaluationContext { EvaluationDate = Date.TodaysDate() };
        Date today = context.EvaluationDate;
        Date maxDate = today + new Period(10, TimeUnit.Years);

        var fittingMethod = new ExponentialSplinesFitting();

        double[] parameters =
        [
            -51293.44,
            -212240.36,
            168668.51,
            88792.74,
            120712.13,
            -34332.83,
            -66479.66,
            13605.17,
            0.0,
        ];

        var curve1 = new FittedBondDiscountCurve(today, fittingMethod, parameters, maxDate, new Actual365Fixed());
        FittedBondDiscountCurve curve2 =
            FittedBondDiscountCurve.Create(0, new TARGET(), fittingMethod, parameters, maxDate, new Actual365Fixed(), context);

        // they work...
        _ = curve1.Discount(3.0);
        _ = curve2.Discount(3.0);

        // ...but not after the max date
        Assert.Contains("past max curve time", Assert.Throws<NanoQLException>(() => curve1.Discount(12.0)).Message);
        Assert.Contains("past max curve time", Assert.Throws<NanoQLException>(() => curve2.Discount(12.0)).Message);
    }

    [Fact]
    public void TestFlatExtrapolation()
    {
        // Testing fitted bond curve with flat extrapolation...

        var asof = new Date(15, Month.July, 2019);
        var context = new EvaluationContext { EvaluationDate = asof };

        // market quotes for bonds below
        Real[] quotes = [101.2100, 100.6270, 99.9210, 101.6700];

        var bonds = new List<Bond>
        {
            // EJ5346956
            new FixedRateBond(
                2,
                100.0,
                new Schedule(
                    new Date(1, Month.February, 2013), new Date(3, Month.February, 2020), new Period(6, TimeUnit.Months),
                    new Canada(), BusinessDayConvention.Following, BusinessDayConvention.Following,
                    DateGenerationRule.Forward, false, context, new Date(3, Month.August, 2013)),
                [0.046],
                new ActualActual(ActualActualConvention.ISDA)),

            // EK9689119
            new FixedRateBond(
                2,
                100.0,
                new Schedule(
                    new Date(12, Month.June, 2015), new Date(12, Month.June, 2020), new Period(6, TimeUnit.Months),
                    new Canada(), BusinessDayConvention.Following, BusinessDayConvention.Following,
                    DateGenerationRule.Forward, false, context, new Date(12, Month.December, 2015)),
                [0.0295],
                new ActualActual(ActualActualConvention.ISDA)),

            // AQ1410069
            new FixedRateBond(
                2,
                100.0,
                new Schedule(
                    new Date(24, Month.November, 2017), new Date(24, Month.November, 2020), new Period(6, TimeUnit.Months),
                    new Canada(), BusinessDayConvention.Following, BusinessDayConvention.Following,
                    DateGenerationRule.Forward, false, context, new Date(24, Month.May, 2018)),
                [0.02689],
                new ActualActual(ActualActualConvention.ISDA)),

            // AM5387676
            new FixedRateBond(
                2,
                100.0,
                new Schedule(
                    new Date(21, Month.February, 2017), new Date(21, Month.February, 2022), new Period(6, TimeUnit.Months),
                    new Canada(), BusinessDayConvention.Following, BusinessDayConvention.Following,
                    DateGenerationRule.Forward, false, context, new Date(21, Month.August, 2017)),
                [0.0338],
                new ActualActual(ActualActualConvention.ISDA)),
        };

        var helpers = new List<BondHelper>(bonds.Count);
        for (int i = 0; i < bonds.Count; ++i)
        {
            helpers.Add(new BondHelper(quotes[i], bonds[i], context));
        }

        // method1 with the usual extrapolation
        var method1 = new NelsonSiegelFitting();

        // method2 extrapoates flat before the first and after the last bond maturity
        var method2 = new NelsonSiegelFitting(
            null,
            null,
            null,
            new Actual365Fixed().YearFraction(asof, helpers[0].Bond.MaturityDate()),
            new Actual365Fixed().YearFraction(asof, helpers[^1].Bond.MaturityDate()));

        // Set a guess that will provoke a "bad" calibration for method1, actually this result was observed as a real
        // calibration outcome given the default guess. The setup was more elaborate though and we do not aim to
        // replicate that here.
        double[] guess = [0.0317, 5.0, -3.6796, 24.1703];

        // build the fitted bond curves
        FittedBondDiscountCurve curve1 =
            new FittedBondDiscountCurve(asof, helpers, new Actual365Fixed(), method1, context, 1E-10, 10000, guess)
                .EnableExtrapolation();
        FittedBondDiscountCurve curve2 =
            new FittedBondDiscountCurve(asof, helpers, new Actual365Fixed(), method2, context, 1E-10, 10000, guess)
                .EnableExtrapolation();

        // extract the model prices using the two curves
        var modelPrices1 = new List<BondPrice>();
        var modelPrices2 = new List<BondPrice>();

        foreach (Bond bond in bonds)
        {
            modelPrices1.Add(new BondPrice(
                bond.CleanPrice(DiscountingBondEngine.Calculate(bond, context, curve1), context), BondPriceType.Clean));
            modelPrices2.Add(new BondPrice(
                bond.CleanPrice(DiscountingBondEngine.Calculate(bond, context, curve2), context), BondPriceType.Clean));
        }

        Assert.Equal(EndCriteriaType.MaxIterations, curve1.FitResults.ErrorCode);

        // Unfortunately, the error code for curve2 depends on the floating-point flags used during compilation so we
        // can't have a corresponding check. We can check that the costs for the two fits are similar, though:
        Real cost1 = Math.Sqrt(curve1.FitResults.MinimumCostValue!.Value);
        Real cost2 = Math.Sqrt(curve2.FitResults.MinimumCostValue!.Value);

        Assert.True(Math.Abs(cost1 - cost2) <= 1e-3, $"cost1 - cost2 = {cost1 - cost2}");

        // the resulting cost values are similar for both approaches, i.e. the fit has a similar quality, I get for
        // example:
        // fitted curve cost1 = 0.0921232
        // fitted curve cost2 = 0.0919438

        // It turns out that the model yields are quite close for model1 and model2 while the curve yields are hugely
        // different: for model1 the yields are completely off (>> 100%) while for model2 they are close to the bond
        // model yields, as it should be.
        //
        // The reason why model1 produces reasonable bond yields is that the compounding from the evaluation date to the
        // settlement date of the bonds compensates for the discounting of the bond flows in the "right way", although
        // the level of the curve yields is completely off. I get these results:
        //
        // helper  maturity  market yield model yield 1 model yield 2 curve yield 1 curve yield 2
        //  0      0.556164     0.0235711     0.0235647     0.0235709       8.69643     0.0235709
        //  1      0.912329     0.0222977     0.0231515     0.0231468       5.31326     0.0231466
        //  2       1.36438     0.0272363     0.0254977     0.0255014       3.56288      0.025524
        //  3       2.61096     0.0268932     0.0277398     0.0277418       1.87629     0.0278147
        for (int i = 0; i < helpers.Count; ++i)
        {
            double t = curve1.TimeFromReference(helpers[i].Bond.MaturityDate());
            Real modelYield2 = bonds[i].Yield(
                modelPrices2[i], new Actual365Fixed(), Compounding.Continuous, Frequency.NoFrequency, context);
            Real curveYield1 = curve1.ZeroRate(t, Compounding.Continuous).Rate;
            Real curveYield2 = curve2.ZeroRate(t, Compounding.Continuous).Rate;

            if (curveYield1 < 1.0)
            {
                Assert.Fail("Expecting huge yield; the test premise might be outdated");
            }

            CheckClose(modelYield2, curveYield2, 1.0); // 1.0 percent relative tolerance
        }

        // resetting the guess changes the calibration
        curve1 = curve1.ResetGuess([0.02, 0.0, 0.0, 0.0], context);
        Assert.Equal(EndCriteriaType.StationaryPoint, curve1.FitResults.ErrorCode);

        for (int i = 0; i < helpers.Count; ++i)
        {
            double t = curve1.TimeFromReference(helpers[i].Bond.MaturityDate());
            Real modelYield1 = bonds[i].Yield(
                modelPrices1[i], new Actual365Fixed(), Compounding.Continuous, Frequency.NoFrequency, context);
            Real curveYield1 = curve1.ZeroRate(t, Compounding.Continuous).Rate;

            CheckClose(modelYield1, curveYield1, 6); // somewhat better, within a dozen bps
        }
    }

    [Fact]
    public void TestRequiredGuess()
    {
        // Testing that fitted bond curves require a guess when given an L2 penalty...

        var context = new EvaluationContext { EvaluationDate = Date.TodaysDate() };
        Date today = context.EvaluationDate;
        List<BondHelper> helpers = ZeroCouponHelpers(today, context, [99.0, 98.0, 95.0, 90.0]);

        double[] weights = [];
        OptimizationMethod? optimizer = null;
        double[] l2 = [0.25, 0.25, 0.25, 0.25];
        var fittingMethod = new NelsonSiegelFitting(weights, optimizer, l2);

        Real accuracy = 1e-10;
        Size maxIterations = 10000;

        // the curve fits when it is built, where QuantLib's fits at its first read
        Assert.Contains(
            "L2 penalty requires a guess",
            Assert.Throws<ArgumentException>(() => FittedBondDiscountCurve.Create(
                0, new TARGET(), helpers, new Actual365Fixed(), fittingMethod, context, accuracy, maxIterations)).Message);
    }

    [Fact]
    public void TestGuessSize()
    {
        // Testing that fitted bond curves check the guess size when given...

        var context = new EvaluationContext { EvaluationDate = Date.TodaysDate() };
        Date today = context.EvaluationDate;
        List<BondHelper> helpers = ZeroCouponHelpers(today, context, [99.0, 98.0, 95.0, 90.0]);

        var fittingMethod = new NelsonSiegelFitting();

        Real accuracy = 1e-10;
        Size maxIterations = 10000;
        double[] guess = [0.01, 0.0, 0.0]; // too few

        // the curve fits when it is built, where QuantLib's fits at its first read
        Assert.Contains(
            "wrong size for guess",
            Assert.Throws<ArgumentException>(() => FittedBondDiscountCurve.Create(
                0, new TARGET(), helpers, new Actual365Fixed(), fittingMethod, context, accuracy, maxIterations, guess))
                .Message);
    }

    [Fact]
    public void TestConstraint()
    {
        // Testing that fitted bond curves respect passed constraint...

        var context = new EvaluationContext { EvaluationDate = Date.TodaysDate() };
        Date today = context.EvaluationDate;
        List<BondHelper> helpers = ZeroCouponHelpers(today, context, [101.0, 102.0]);

        Real accuracy = 1e-10; // default value
        Size maxIterations = 10000; // default value
        double[] guess = [0.01]; // something positive so that initial value is in feasible region

        var unconstrainedMethod = new FlatZero();
        FittedBondDiscountCurve unconstrainedCurve = FittedBondDiscountCurve.Create(
            0, new TARGET(), helpers, new Actual365Fixed(), unconstrainedMethod, context, accuracy, maxIterations, guess);
        Assert.True(unconstrainedCurve.FitResults.Solution[0] < 0.0);

        var positiveMethod = new FlatZero(new PositiveConstraint());
        FittedBondDiscountCurve positiveCurve = FittedBondDiscountCurve.Create(
            0, new TARGET(), helpers, new Actual365Fixed(), positiveMethod, context, accuracy, maxIterations, guess);
        Assert.True(positiveCurve.FitResults.Solution[0] > 0.0);
    }

    // The test cases' zero-coupon bonds, at 1, 2, 5 and 10 years from today, and their helpers at the given prices.
    private static List<BondHelper> ZeroCouponHelpers(Date today, EvaluationContext context, double[] prices)
    {
        Period[] maturities = [new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(5, TimeUnit.Years), new(10, TimeUnit.Years)];
        var helpers = new List<BondHelper>(prices.Length);
        for (int i = 0; i < prices.Length; ++i)
        {
            var bond = new ZeroCouponBond(3, new TARGET(), 100.0, today + maturities[i]);
            helpers.Add(new BondHelper(prices[i], bond, context));
        }

        return helpers;
    }

    // QL_CHECK_CLOSE, BOOST_CHECK_CLOSE: the difference within the tolerance, in percent, of both values.
    private static void CheckClose(Real a, Real b, Real percent)
    {
        Real diff = Math.Abs(a - b);
        Assert.True(
            diff <= Math.Abs(a) * percent / 100.0 && diff <= Math.Abs(b) * percent / 100.0,
            $"difference between {a} and {b} exceeds {percent}%");
    }

    private sealed class FlatZero(Constraint? constraint = null) : FittedBondDiscountCurveFittingMethod(
        true, // constrainAtZero
        null, // weights
        null,
        null, // l2
        0.0, // minCutoffTime
        double.MaxValue, // maxCutoffTime
        constraint ?? new NoConstraint())
    {
        public override int Size => 1;

        protected override double DiscountFunction(ReadOnlySpan<double> x, double t)
        {
            Real zeroRate = x[0];
            DiscountFactor d = Math.Exp(-zeroRate * t);
            return d;
        }
    }
}
