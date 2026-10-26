// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/basketoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testEuroTwoValues, testBarraquandThreeValues, testTavellaValues, testOneDAmericanValues, testOddSamples, test2DPDEGreeks, testBjerksundStenslandSpreadEngine, testOperatorSplittingSpreadEngine, testStrangSplittingSpreadEngineVsMathematica, testPDEvsApproximations, testDengLiZhouVsPDE, testDengLiZhouWithNegativeStrike, testSpreadAndBasketBenchmarks, testNoDivByZeroOperatorSplitting, testPearsonSpreadEngine, testGaussianCopulaSpreadEngineFlatVol, testGaussianCopulaSpreadEngineSVI, testGaussianCopulaSpreadEngineParityAcrossVols, which wait for S-exo's spread, two-asset and American Monte Carlo engines

using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class BasketOptionTests
{
    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    // The test runner's evaluation date (quantlibglobalfixture.cpp): the cases set none of their own.
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(16, Month.September, 2015) };

    [Fact]
    public void TestLocalVolatilitySpreadOption()
    {
        // Testing 2D local-volatility spread-option pricing...

        DayCounter dc = new Actual360();
        var today = new Date(21, Month.September, 2017);
        Date maturity = today + new Period(3, TimeUnit.Months);

        YieldTermStructure riskFreeRate = FlatRate(today, 0.07, dc);
        YieldTermStructure dividendYield = FlatRate(today, 0.03, dc);

        const Real s1 = 100;
        const Real s2 = 110;

        var hm1 = new HestonModel(new HestonProcess(riskFreeRate, dividendYield, s1, 0.09, 1.0, 0.06, 0.6, -0.75));

        var hm2 = new HestonModel(new HestonProcess(riskFreeRate, dividendYield, s2, 0.1, 2.0, 0.07, 0.8, 0.85));

        BlackVolTermStructure vol1 = new HestonBlackVolSurface(hm1);

        BlackVolTermStructure vol2 = new HestonBlackVolSurface(hm2);

        var basketOption = new BasketOption(
            BasketTypeToPayoff(BasketType.SpreadBasket, new PlainVanillaPayoff(OptionType.Call, s2 - s1)),
            new EuropeanExercise(maturity));

        const Real rho = -0.6;

        var bs2 = new GeneralizedBlackScholesProcess(s2, dividendYield, riskFreeRate, vol2);

        var bs1 = new GeneralizedBlackScholesProcess(s1, dividendYield, riskFreeRate, vol1);

        const Real tolerance = 0.01;
        const Real expected = 2.561;
        Real calculated = Fd2dBlackScholesVanillaEngine.Calculate(
            basketOption, Context, bs1, bs2, rho, 11, 11, 6, 0, FdmSchemeDesc.Hundsdorfer(), true, 0.25).NPV!.Value;

        if (Math.Abs(expected - calculated) > tolerance)
        {
            Assert.Fail(
                "Failed to reproduce expected local volatility price"
                + $"\n    calculated: {calculated}\n    expected:   {expected}\n    tolerance:  {tolerance}");
        }
    }

    [Fact]
    public void TestNdimPDEvs2dimPDE()
    {
        // Testing n-dimensional PDE engine vs two dimensional engine...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(25, Month.February, 2024);
        Date maturity = today + new Period(6, TimeUnit.Months);

        const Real s1 = 110;
        const Real s2 = 100;

        YieldTermStructure rTS = FlatRate(today, 0.1, dc);

        const Real v1 = 0.5;
        const Real v2 = 0.3;

        var p1 = new BlackProcess(s1, rTS, FlatVol(today, v1, dc));

        YieldTermStructure qTS = FlatRate(today, 0.075, dc);

        var p2 = new GeneralizedBlackScholesProcess(s2, qTS, rTS, FlatVol(today, v2, dc));

        const Real rho = 0.75;

        MultiAssetOptionResults TwoDimEngine(BasketOption o) =>
            Fd2dBlackScholesVanillaEngine.Calculate(o, Context, p1, p2, rho, 25, 25, 100);

        MultiAssetOptionResults NDimEngine(BasketOption o) =>
            FdndimBlackScholesVanillaEngine.Calculate(
                o, Context, [p1, p2], Matrix2([[1, rho], [rho, 1]]), [25, 25], 100);

        const Real tol = 0.2;
        foreach (Exercise exercise in new Exercise[]
        {
            new EuropeanExercise(maturity),
            new AmericanExercise(today, maturity),
        })
        {
            foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
            {
                var option = new BasketOption(new SpreadBasketPayoff(new PlainVanillaPayoff(type, 5)), exercise);

                Real sb2dNPV = TwoDimEngine(option).NPV!.Value;

                Real sbndNPV = NDimEngine(option).NPV!.Value;

                if (Math.Abs(sb2dNPV - sbndNPV) > tol)
                {
                    Assert.Fail(
                        "failed to reproduce spread option prices with multidimensional PDE engine."
                        + $"\n    calculated: {F5(sbndNPV)}\n    expected:   {F5(sb2dNPV)}"
                        + $"\n    diff:       {F5(Math.Abs(sb2dNPV - sbndNPV))}\n    tolerance : {F5(tol)}");
                }

                var avgOption = new BasketOption(
                    new AverageBasketPayoff(new PlainVanillaPayoff(type, 200), [1.5, 0.5]), exercise);
                Real avg2dNPV = TwoDimEngine(avgOption).NPV!.Value;

                Real avgndNPV = NDimEngine(avgOption).NPV!.Value;

                if (Math.Abs(avg2dNPV - avgndNPV) > tol)
                {
                    Assert.Fail(
                        "failed to reproduce average option prices with multidimensional PDE engine."
                        + $"\n    calculated: {F5(avgndNPV)}\n    expected:   {F5(avg2dNPV)}"
                        + $"\n    diff:       {F5(Math.Abs(avg2dNPV - avgndNPV))}\n    tolerance : {F5(tol)}");
                }
            }
        }
    }

    [Fact]
    public void TestNdimPDEinDifferentDims()
    {
        // Testing n-dimensional PDE engine in different dimensions...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(25, Month.February, 2024);
        Date maturity = today + new Period(6, TimeUnit.Months);

        Real[] underlyings = [100, 50, 75, 120];
        Real[] volatilities = [0.3, 0.2, 0.6, 0.4];

        Real strike = 5.0;

        YieldTermStructure rTS = FlatRate(today, 0.05, dc);
        Exercise exercise = new EuropeanExercise(maturity);

        List<GeneralizedBlackScholesProcess> processes = [];
        for (Size d = 1; d <= 4; ++d)
        {
            processes.Add(
                BlackScholesProcess.Create(underlyings[d - 1], rTS, FlatVol(today, volatilities[d - 1], dc), Context));

            strike += underlyings[d - 1];

            var rho = new Matrix(d, d);
            for (Size i = 0; i < d; ++i)
            {
                for (Size j = 0; j < d; ++j)
                {
                    rho[i, j] = rho[j, i] = Math.Exp(-0.5 * Math.Abs((Real)(i - j)));
                }
            }

            var option = new BasketOption(
                new AverageBasketPayoff(new PlainVanillaPayoff(OptionType.Call, strike), [.. Enumerable.Repeat(1.0, d)]),
                exercise);

            ImmutableArray<GeneralizedBlackScholesProcess> ps = [.. processes];
            Real expected = d > 1
                ? ChoiBasketEngine.Calculate(option, Context, ps, rho, 15).NPV!.Value
                : SingleFactorBsmBasketEngine.Calculate(option, Context, ps).NPV!.Value;

            Real calculated = FdndimBlackScholesVanillaEngine.Calculate(option, Context, ps, rho, 30, 7).NPV!.Value;
            Real diff = Math.Abs(calculated - expected);
            const Real tol = 0.05;

            if (diff > tol)
            {
                Assert.Fail(
                    $"failed to reproduce precalculated {d}-dim option price"
                    + $"\n    calculated: {F5(calculated)}\n    expected:   {F5(expected)}"
                    + $"\n    diff:       {F5(diff)}\n    tolerance : {F5(tol)}");
            }
        }
    }

    [Fact]
    public void TestRootOfSumExponentials()
    {
        // Testing the root of a sum of exponentials...

        Assert.Throws<NanoQLException>(() => RootSolver([2.0, 3.0, 4.0], [0.2, 0.4, -0.1], 0.0).GetRoot());
        Assert.Throws<NanoQLException>(() => RootSolver([2.0, -3.0, 4.0], [0.2, -0.4, -0.1], 0.0).GetRoot());

        var mt = new MersenneTwisterUniformRng(42);

        foreach ((string Name, int Strategy) strategy in new[] { ("Brent", 2), ("Newton", 1), ("Ridder", 0), ("Halley", 3) })
        {
            Size fCtr = 0;
            const Size n = 10000;
            const double tol = 1e8 * QL_EPSILON;
            const Real acc = 1e-4 * tol;
            var stats = new IncrementalStatistics();

            for (Size i = 0; i < n; ++i)
            {
                Size m = (Size)(mt.NextInt32() % 10) + 1;
                Real[] a = new Real[m], sig = new Real[m];
                Real offset = (mt.Next().Value < 0.3) ? -1.0 : 0.0;
                for (Size j = 0; j < m; ++j)
                {
                    a[j] = mt.Next().Value + offset;
                    sig[j] = Math.CopySign(1.0, a[j]) * mt.Next().Value;
                }

                Real kMin = RootSolver(a, sig, 0.0).Evaluate(-10.0);
                Real kMax = RootSolver(a, sig, 0.0).Evaluate(10.0);
                Real k = ((kMax - kMin) * mt.Next().Value) + kMin;

                Real xValue = RootSolver(a, sig, k).GetRoot(acc, 2).Root;

                RootSolverHandle solver = RootSolver(a, sig, k);
                (Real xRoot, int f, int fPrime, int fSecond) = solver.GetRoot(tol, strategy.Strategy);

                stats.Add(xValue - xRoot);
                fCtr += f + fPrime + fSecond;
            }

            if (fCtr > 15 * n)
            {
                Assert.Fail($"too many function calls needed for solver {strategy.Name}");
            }

            if (stats.StandardDeviation() > 10 * tol)
            {
                Assert.Fail(
                    "failed to find root of sum of exponentials"
                    + $"\n    solver   : {strategy.Name}"
                    + $"\n    stdev    : {stats.StandardDeviation().ToString("F15", CultureInfo.InvariantCulture)}"
                    + $"\n    tolerance: {tol.ToString("F15", CultureInfo.InvariantCulture)}");
            }
        }
    }

    [Fact]
    public void TestSingleFactorBsmBasketEngine()
    {
        // Testing single factor BSM basket engine against reference results...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(3, Month.July, 2024);
        Date maturity = today + new Period(18, TimeUnit.Months);
        double deltaT = dc.YearFraction(today, maturity);
        Real sqrtDeltaT = Math.Sqrt(deltaT);

        (Real[] Underlyings, Real[] Volatilities, Real[] Q, Rate R, Real[] Weights, OptionType OptionType)[] testCases =
        [
            ([200, 50, -125], [0.4, 0.3, -0.5], [0.03, 0.075, 0.04], 0.05, [0.5, 0.25, 1.0], OptionType.Call),
            ([200, 50, -125], [0.4, 0.3, -0.5], [0.03, 0.075, 0.04], 0.05, [0.5, 0.25, 1.0], OptionType.Put),
            ([100, 50], [0.4, -0.3], [0.03, 0.075], 0.025, [1.0, -2.0], OptionType.Put),
            ([100, 50], [0.4, -0.3], [0.03, 0.075], 0.025, [1.0, -2.0], OptionType.Call),
            ([100], [0.4], [0.03], 0.045, [1.0], OptionType.Call),
            ([100, 50, 100, 150], [0.4, 0.0, 0.2, 0.1], [0.03, 0.05, 0.02, 0], 0.045, [1.0, 2.0, 1.0, 1.0], OptionType.Call),
            ([100, 50], [0.0, 0.0], [0.03, 0.05], 0.055, [1.0, 1.95], OptionType.Call),
        ];

        foreach ((Real[] Underlyings, Real[] Volatilities, Real[] Q, Rate R, Real[] Weights, OptionType OptionType) t in testCases)
        {
            YieldTermStructure rTS = FlatRate(today, t.R, dc);

            List<GeneralizedBlackScholesProcess> processes = [];
            for (Size d = 0; d < t.Underlyings.Length; ++d)
            {
                processes.Add(
                    new BlackScholesMertonProcess(
                        t.Underlyings[d], FlatRate(today, t.Q[d], dc), rTS, FlatVol(today, t.Volatilities[d], dc)));
            }

            // std::inner_product from 0
            Real strike = 0.0;
            for (Size d = 0; d < t.Weights.Length; ++d)
            {
                strike = strike + (t.Weights[d] * t.Underlyings[d]);
            }

            var payoff = new PlainVanillaPayoff(t.OptionType, strike);

            var option = new BasketOption(new AverageBasketPayoff(payoff, t.Weights), new EuropeanExercise(maturity));

            Real calculated = SingleFactorBsmBasketEngine.Calculate(option, Context, [.. processes]).NPV!.Value;

            Real[] f = new Real[processes.Count];
            for (Size i = 0; i < f.Length; ++i)
            {
                f[i] = t.Weights[i]
                    * processes[i].X0
                    * processes[i].DividendYield.Discount(maturity)
                    / rTS.Discount(maturity)
                    * Math.Exp(-0.5 * processes[i].BlackVolatility.BlackVariance(maturity, 0));
            }

            // QuantLib's SobolRsg(1) draws its seed from the clock; one dimension's directions don't read it
            var rsg = new SobolRsg(1, 1);
            var invCumNormal = new InverseCumulativeNormal();
            var stats = new IncrementalStatistics();

            const Size nPath = 10000;
            DiscountFactor df = rTS.Discount(maturity);
            for (Size i = 0; i < nPath; ++i)
            {
                Real z = sqrtDeltaT * invCumNormal.Evaluate(rsg.NextSequence().Value.Span[0]);
                Real s = 0.0;
                for (Size j = 0; j < processes.Count; ++j)
                {
                    s += f[j] * Math.Exp(t.Volatilities[j] * z);
                }

                stats.Add(df * payoff.Evaluate(s));
            }

            Real expected = stats.Mean();
            Real diff = Math.Abs(expected - calculated);

            Real errorEstimate = stats.ErrorEstimate();
            Real tol = Math.Max(1e-10, 0.1 * errorEstimate);

            if (diff > tol)
            {
                Assert.Fail(
                    "failed to reproduce single factor basket prices"
                    + $"\n    calculated: {F8(calculated)}\n    expected:   {F8(expected)}"
                    + $"\n    diff:       {F8(diff)}\n    tolerance:  {F8(tol)}");
            }
        }
    }

    [Fact]
    public void TestGoldenChoiBasketEngineExample()
    {
        // Testing BSM Choi basket engine against reference results...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(26, Month.September, 2024);

        YieldTermStructure rTS = FlatRate(today, 0.05, dc);

        const Real strike = 20;
        Date maturity = today + new Period(18, TimeUnit.Months);

        Real[] spots = [100, 50, 75, 25];

        GeneralizedBlackScholesProcess ProcessGen(Real spot, Rate q, Volatility vol) =>
            new(spot, FlatRate(today, q, dc), rTS, FlatVol(today, vol, dc));

        (Rate Q, Volatility Vol)[] parameters = [(0.075, 0.45), (0.035, 0.4), (0.08, 0.35), (0.02, 0.2)];

        // QuantLib's processes read their spots' quotes, which the case bumps; here each bump builds the processes
        // again
        ImmutableArray<GeneralizedBlackScholesProcess> Processes() =>
            [.. spots.Select((s, i) => ProcessGen(s, parameters[i].Q, parameters[i].Vol))];

        ImmutableArray<GeneralizedBlackScholesProcess> processes = Processes();

        Matrix rho = Matrix2(
        [
            [1.0, 0.2, 0.3, 0.0],
            [0.2, 1.0, -0.3, 0.1],
            [0.3, -0.3, 1.0, 0.7],
            [0.0, 0.1, 0.7, 1.0],
        ]);

        MultiAssetOptionResults Engine(BasketOption o, ImmutableArray<GeneralizedBlackScholesProcess> ps) =>
            ChoiBasketEngine.Calculate(o, Context, ps, rho, 7.0, 10000, true, true);

        Real[] expected = [15.92008513388834, 22.36122704630282];
        OptionType[] optionTypes = [OptionType.Put, OptionType.Call];

        for (Size i = 0; i < expected.Length; ++i)
        {
            var option = new BasketOption(
                new AverageBasketPayoff(new PlainVanillaPayoff(optionTypes[i], strike), [1, -2, -1, 4]),
                new EuropeanExercise(maturity));

            MultiAssetOptionResults results = Engine(option, processes);
            Real calculated = results.NPV!.Value;
            Real npvDiff = Math.Abs(expected[i] - calculated);
            const Real npvTol = 1e-5;

            if (npvDiff > npvTol)
            {
                Assert.Fail(
                    "failed to reproduce reference price with Choi engine"
                    + $"\n    option type: {optionTypes[i]}\n    calculated:  {F8(calculated)}"
                    + $"\n    expected:    {F8(expected[i])}\n    diff:        {F8(npvDiff)}\n    tolerance:   {F8(npvTol)}");
            }

            for (Size k = 0; k < processes.Length; ++k)
            {
                Real baseSpot = spots[k];

                spots[k] = baseSpot * 1.001;
                Real up = Engine(option, Processes()).NPV!.Value;
                spots[k] = baseSpot * 0.999;
                Real down = Engine(option, Processes()).NPV!.Value;

                spots[k] = baseSpot;

                Real expectedDeltaSpot = (up - down) / (0.002 * baseSpot);
                Real expectedDeltaFwd = expectedDeltaSpot
                    / processes[k].DividendYield.Discount(maturity)
                    * processes[0].RiskFreeRate.Discount(maturity);

                string deltaName = "forwardDelta " + k.ToString(CultureInfo.InvariantCulture);
                Real deltaDiff = Math.Abs(expectedDeltaFwd - results.Result<Real>(deltaName));
                const Real deltaTol = 5e-5;

                if (deltaDiff > deltaTol)
                {
                    Assert.Fail(
                        "failed to reproduce forward delta with Choi engine"
                        + $"\n    option type: {optionTypes[i]}\n    underlying:  {i}"
                        + $"\n    calculated:  {F8(calculated)}\n    expected:    {F8(expected[i])}"
                        + $"\n    diff:        {F8(npvDiff)}\n    tolerance:   {F8(npvTol)}");
                }
            }
        }
    }

    [Fact]
    public void TestFdmAmericanBasketOptions()
    {
        // Testing American Basket and Spread Options using FDM...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(28, Month.October, 2024);
        Date maturity = today + new Period(9, TimeUnit.Months);

        YieldTermStructure rTS = FlatRate(today, 0.1, dc);

        GeneralizedBlackScholesProcess ProcessGen(Real spot, Rate q, Volatility vol) =>
            new(spot, FlatRate(today, q, dc), rTS, FlatVol(today, vol, dc));

        ImmutableArray<GeneralizedBlackScholesProcess> processes =
        [
            ProcessGen(100, 0.02, 0.4),
            ProcessGen(25, 0.035, 0.5),
            ProcessGen(90, 0.08, 0.25),
        ];

        Matrix rho = Matrix2(
        [
            [1.0, 0.2, 0.6],
            [0.2, 1.0, -0.3],
            [0.6, -0.3, 1.0],
        ]);

        var option = new BasketOption(
            new AverageBasketPayoff(new PlainVanillaPayoff(OptionType.Put, -30), [1, -2, -1]),
            new AmericanExercise(today, maturity));

        // QuantLib sets Choi's engine here and prices nothing with it
        const Real expected = 15.1858;

        Real calculated = FdndimBlackScholesVanillaEngine.Calculate(
            option, Context, processes, rho, [.. Enumerable.Repeat(20, processes.Length)], 15).NPV!.Value;
        Real diff = Math.Abs(calculated - expected);
        const Real tol = 0.01;
        if (diff > tol)
        {
            Assert.Fail(
                "failed to reproduce american spread-basket option price"
                + $"\n    calculated:  {F8(calculated)}\n    expected:    {F8(expected)}"
                + $"\n    diff:        {F8(diff)}\n    tolerance:   {F8(tol)}");
        }
    }

    [Fact]
    public void TestAccurateAmericanBasketOptions()
    {
        // Testing high precision American Options Pricing using multi-dim FDM...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(28, Month.October, 2024);
        Date maturity = today + new Period(18, TimeUnit.Months);

        var p = new GeneralizedBlackScholesProcess(
            100,
            new ZeroCurve(
                [today, today + new Period(1, TimeUnit.Months), today + new Period(2, TimeUnit.Years)],
                [0.05, 0.075, 0.02],
                dc,
                null),
            new ZeroCurve(
                [today, today + new Period(3, TimeUnit.Months), today + new Period(2, TimeUnit.Years)],
                [0.15, 0.1, 0.2],
                dc),
            FlatVol(today, 0.25, dc));

        Exercise exercise = new AmericanExercise(today, maturity);

        var payoff = new PlainVanillaPayoff(OptionType.Put, 120);

        var vanillaOption = new VanillaOption(payoff, exercise);

        var basketOption = new BasketOption(new AverageBasketPayoff(payoff, [1.0]), exercise);

        Real expected = FdBlackScholesVanillaEngine.Calculate(vanillaOption, Context, p, tGrid: 200, xGrid: 800).NPV!.Value;
        Real calculated = FdndimBlackScholesVanillaEngine.Calculate(
            basketOption, Context, [p], Matrix2([[1]]), 200, 800).NPV!.Value;
        Real diff = Math.Abs(expected - calculated);
        const Real tol = 0.02;
        if (diff > tol)
        {
            Assert.Fail(
                "failed to reproduce american vanilla option price with multi-dim FDM engine"
                + $"\n    calculated:  {F8(calculated)}\n    expected:    {F8(expected)}"
                + $"\n    diff:        {F8(diff)}\n    tolerance:   {F8(tol)}");
        }
    }

    // The test suite's basketTypeToPayoff.
    internal static BasketPayoff BasketTypeToPayoff(BasketType basketType, Payoff p) => basketType switch
    {
        BasketType.MinBasket => new MinBasketPayoff(p),
        BasketType.MaxBasket => new MaxBasketPayoff(p),
        BasketType.SpreadBasket => new SpreadBasketPayoff(p),
        _ => throw new NanoQLException("unknown basket option type"),
    };

    // The test suite's flatRate: a flat continuously compounded curve from the reference date.
    private static YieldTermStructure FlatRate(Date today, double forward, DayCounter dc) =>
        new FlatForward(today, forward, dc);

    // The test suite's flatVol: a constant volatility from the reference date, on the null calendar.
    private static BlackVolTermStructure FlatVol(Date today, double vol, DayCounter dc) =>
        new BlackConstantVol(today, new NullCalendar(), vol, dc);

    // QuantLib's Matrix from nested initializer lists.
    private static Matrix Matrix2(double[][] rows)
    {
        var m = new Matrix(rows.Length, rows[0].Length);
        for (int i = 0; i < rows.Length; ++i)
        {
            for (int j = 0; j < rows[i].Length; ++j)
            {
                m[i, j] = rows[i][j];
            }
        }

        return m;
    }

    // std::fixed with std::setprecision(5) and (8).
    private static string F5(double x) => x.ToString("F5", CultureInfo.InvariantCulture);

    private static string F8(double x) => x.ToString("F8", CultureInfo.InvariantCulture);

    // QuantLib's detail::SumExponentialsRootSolver, internal here as in QuantLib's detail namespace, read through
    // reflection.
    private static RootSolverHandle RootSolver(Real[] a, Real[] sig, Real k) => new(a, sig, k);

    internal enum BasketType
    {
        MinBasket,
        MaxBasket,
        SpreadBasket,
    }

    private sealed class RootSolverHandle
    {
        private static readonly Type SolverType =
            typeof(ChoiBasketEngine).Assembly.GetType("NanoQL.PricingEngines.SumExponentialsRootSolver", true)!;

        private static readonly Type StrategyType = SolverType.GetNestedType("Strategy", BindingFlags.NonPublic)!;

        private static readonly ConstructorInfo Ctor = SolverType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, [typeof(double[]), typeof(double[]), typeof(double)])!;

        private static readonly MethodInfo EvaluateMethod =
            SolverType.GetMethod("Evaluate", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly MethodInfo GetRootMethod =
            SolverType.GetMethod("GetRoot", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private readonly object solver;

        public RootSolverHandle(Real[] a, Real[] sig, Real k)
        {
            try
            {
                solver = Ctor.Invoke([a, sig, k]);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException!;
            }
        }

        public Real Evaluate(Real x) => (Real)EvaluateMethod.Invoke(solver, [x])!;

        // getRoot(xTol = 1e6*QL_EPSILON, Strategy = Brent)
        public (Real Root, int FCtr, int DerivativeCtr, int SecondDerivativeCtr) GetRoot(
            Real xTol = 1e6 * QL_EPSILON, int strategy = 2)
        {
            object result;
            try
            {
                result = GetRootMethod.Invoke(solver, [xTol, Enum.ToObject(StrategyType, strategy)])!;
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException!;
            }

            Type r = result.GetType();
            return (
                (Real)r.GetProperty("Root")!.GetValue(result)!,
                (int)r.GetProperty("FCtr")!.GetValue(result)!,
                (int)r.GetProperty("DerivativeCtr")!.GetValue(result)!,
                (int)r.GetProperty("SecondDerivativeCtr")!.GetValue(result)!);
        }
    }
}
