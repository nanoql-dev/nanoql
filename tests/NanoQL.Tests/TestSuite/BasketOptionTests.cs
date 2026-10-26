// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/basketoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

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
    [Fact]
    public void TestEuroTwoValues()
    {
        /*
            Data from:
            Excel spreadsheet www.maths.ox.ac.uk/~firth/computing/excel.shtml
            and
            "Option pricing formulas", E.G. Haug, McGraw-Hill 1998 pag 56-58
            European two asset max basket options
        */
        BasketOptionTwoData[] values =
        [
            // basketType,   optionType, strike,    s1,    s2,   q1,   q2,    r,    t,   v1,   v2,  rho, result, tol
            // data from http://www.maths.ox.ac.uk/~firth/computing/excel.shtml
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Call, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.90, 10.898, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Call, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.70, 8.483, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Call, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.50, 6.844, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Call, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.30, 5.531, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Call, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.10, 4.413, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Call, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.50, 0.70, 0.00, 4.981, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Call, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.50, 0.30, 0.00, 4.159, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Call, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.50, 0.10, 0.00, 2.597, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Call, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.50, 0.10, 0.50, 4.030, 1.0e-3),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Call, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.90, 17.565, 1.0e-3),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Call, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.70, 19.980, 1.0e-3),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Call, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.50, 21.619, 1.0e-3),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Call, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.30, 22.932, 1.0e-3),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Call, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.10, 24.049, 1.1e-3),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Call, 100.0, 80.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.30, 16.508, 1.0e-3),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Call, 100.0, 80.0, 80.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.30, 8.049, 1.0e-3),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Call, 100.0, 80.0, 120.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.30, 30.141, 1.0e-3),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Call, 100.0, 120.0, 120.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.30, 42.889, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Put, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.90, 11.369, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Put, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.70, 12.856, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Put, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.50, 13.890, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Put, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.30, 14.741, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Put, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.10, 15.485, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Put, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 0.50, 0.30, 0.30, 0.10, 11.893, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Put, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 0.25, 0.30, 0.30, 0.10, 8.881, 1.0e-3),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Put, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 2.00, 0.30, 0.30, 0.10, 19.268, 1.0e-3),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Put, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.90, 7.339, 1.0e-3),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Put, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.70, 5.853, 1.0e-3),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Put, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.50, 4.818, 1.0e-3),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Put, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.30, 3.967, 1.1e-3),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Put, 100.0, 100.0, 100.0, 0.00, 0.00, 0.05, 1.00, 0.30, 0.30, 0.10, 3.223, 1.0e-3),
            // basketType,   optionType, strike,    s1,    s2,   q1,   q2,    r,    t,   v1,   v2,  rho,  result, tol
            // data from "Option pricing formulas" VB code + spreadsheet
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Call, 98.0, 100.0, 105.0, 0.00, 0.00, 0.05, 0.50, 0.11, 0.16, 0.63, 4.8177, 1.0e-4),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Call, 98.0, 100.0, 105.0, 0.00, 0.00, 0.05, 0.50, 0.11, 0.16, 0.63, 11.6323, 1.0e-4),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Put, 98.0, 100.0, 105.0, 0.00, 0.00, 0.05, 0.50, 0.11, 0.16, 0.63, 2.0376, 1.0e-4),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Put, 98.0, 100.0, 105.0, 0.00, 0.00, 0.05, 0.50, 0.11, 0.16, 0.63, 0.5731, 1.0e-4),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Call, 98.0, 100.0, 105.0, 0.06, 0.09, 0.05, 0.50, 0.11, 0.16, 0.63, 2.9340, 1.0e-4),
            new BasketOptionTwoData(BasketType.MinBasket, OptionType.Put, 98.0, 100.0, 105.0, 0.06, 0.09, 0.05, 0.50, 0.11, 0.16, 0.63, 3.5224, 1.0e-4),
            // data from "Option pricing formulas", E.G. Haug, McGraw-Hill 1998 pag 58
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Call, 98.0, 100.0, 105.0, 0.06, 0.09, 0.05, 0.50, 0.11, 0.16, 0.63, 8.0701, 1.0e-4),
            new BasketOptionTwoData(BasketType.MaxBasket, OptionType.Put, 98.0, 100.0, 105.0, 0.06, 0.09, 0.05, 0.50, 0.11, 0.16, 0.63, 1.2181, 1.0e-4),
            // "Option pricing formulas", E.G. Haug, McGraw-Hill 1998 pag 59-60
            // Kirk approx. for a european spread option on two futures
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.1, 0.20, 0.20, -0.5, 4.7530, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.1, 0.20, 0.20, 0.0, 3.7970, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.1, 0.20, 0.20, 0.5, 2.5537, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.1, 0.25, 0.20, -0.5, 5.4275, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.1, 0.25, 0.20, 0.0, 4.3712, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.1, 0.25, 0.20, 0.5, 3.0086, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.1, 0.20, 0.25, -0.5, 5.4061, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.1, 0.20, 0.25, 0.0, 4.3451, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.1, 0.20, 0.25, 0.5, 2.9723, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.5, 0.20, 0.20, -0.5, 10.7517, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.5, 0.20, 0.20, 0.0, 8.7020, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.5, 0.20, 0.20, 0.5, 6.0257, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.5, 0.25, 0.20, -0.5, 12.1941, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.5, 0.25, 0.20, 0.0, 9.9340, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.5, 0.25, 0.20, 0.5, 7.0067, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.5, 0.20, 0.25, -0.5, 12.1483, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.5, 0.20, 0.25, 0.0, 9.8780, 1.0e-3),
            new BasketOptionTwoData(BasketType.SpreadBasket, OptionType.Call, 3.0, 122.0, 120.0, 0.0, 0.0, 0.10, 0.5, 0.20, 0.25, 0.5, 6.9284, 1.0e-3),
        ];

        DayCounter dc = new Actual360();

        // QuantLib's today, Date::todaysDate(): the context's date (D13)
        Date today = Context.EvaluationDate;

        const Real mcRelativeErrorTolerance = 0.01;
        const Real fdRelativeErrorTolerance = 0.01;

        foreach (BasketOptionTwoData value in values)
        {
            var payoff = new PlainVanillaPayoff(value.Type, value.Strike);

            Date exDate = today + TimeToDays(value.T);
            var exercise = new EuropeanExercise(exDate);

            YieldTermStructure qTS1 = FlatRate(today, value.Q1, dc);
            YieldTermStructure qTS2 = FlatRate(today, value.Q2, dc);
            YieldTermStructure rTS = FlatRate(today, value.R, dc);
            BlackVolTermStructure volTS1 = FlatVol(today, value.V1, dc);
            BlackVolTermStructure volTS2 = FlatVol(today, value.V2, dc);

            GeneralizedBlackScholesProcess p1, p2;
            Func<BasketOption, EvaluationContext, MultiAssetOptionResults> analyticEngine;
            switch (value.BasketType)
            {
                case BasketType.MaxBasket:
                case BasketType.MinBasket:
                    p1 = new BlackScholesMertonProcess(value.S1, qTS1, rTS, volTS1);
                    p2 = new BlackScholesMertonProcess(value.S2, qTS2, rTS, volTS2);
                    analyticEngine = (o, c) => StulzEngine.Calculate(o, c, p1, p2, value.Rho);
                    break;
                case BasketType.SpreadBasket:
                    p1 = new BlackProcess(value.S1, rTS, volTS1);
                    p2 = new BlackProcess(value.S2, rTS, volTS2);
                    analyticEngine = (o, c) => KirkEngine.Calculate(o, c, p1, p2, value.Rho);
                    break;
                default:
                    throw new NanoQLException("unknown basket type");
            }

            var correlationMatrix = new Matrix(2, 2, value.Rho);
            for (int j = 0; j < 2; j++)
            {
                correlationMatrix[j, j] = 1.0;
            }

            var process = new StochasticProcessArray([p1, p2], correlationMatrix);

            Func<BasketOption, EvaluationContext, MultiAssetOptionResults> mcEngine =
                new MakeMCEuropeanBasketEngine<PseudoRandom, GeneralStatistics>(process, 42)
                .WithStepsPerYear(1)
                .WithSamples(10000)
                .ToEngine();

            var basketOption = new BasketOption(BasketTypeToPayoff(value.BasketType, payoff), exercise);

            // analytic engine
            Real calculated = analyticEngine(basketOption, Context).NPV!.Value;
            Real expected = value.Result;
            Real error = Math.Abs(calculated - expected);
            Assert.True(error <= value.Tol, Failure("value", value, expected, calculated, error, value.Tol));

            // fd engine
            calculated = Fd2dBlackScholesVanillaEngine.Calculate(
                basketOption, Context, p1, p2, value.Rho, 50, 50, 15).NPV!.Value;
            Real relError = RelativeError(calculated, expected, expected);
            Assert.True(
                relError <= mcRelativeErrorTolerance,
                Failure("FD value", value, expected, calculated, relError, fdRelativeErrorTolerance));

            // mc engine
            calculated = mcEngine(basketOption, Context).NPV!.Value;
            relError = RelativeError(calculated, expected, value.S1);
            Assert.True(
                relError <= mcRelativeErrorTolerance,
                Failure("MC value", value, expected, calculated, relError, mcRelativeErrorTolerance));
        }
    }

    [Fact]
    public void TestBarraquandThreeValues()
    {
        /*
            Data from:
            "Numerical Valuation of High Dimensional American Securities"
            Barraquand, J. and Martineau, D.
            Journal of Financial and Quantitative Analysis 1995 3(30) 383-405
        */
        BasketOptionThreeData[] values =
        [
            // time in months is with 30 days to the month..
            // basketType, optionType,       strike,    s1,    s2,   s3,    r,    t,   v1,   v2,  v3,  rho, euro, american,
            // Table 2
            // not using 4 month case to speed up test
            // Table 3
            new BasketOptionThreeData(BasketType.MaxBasket, OptionType.Put, 35.0, 40.0, 40.0, 40.0, 0.05, 1.00, 0.20, 0.30, 0.50, 0.0, 0.00, 0.00),
            new BasketOptionThreeData(BasketType.MaxBasket, OptionType.Put, 40.0, 40.0, 40.0, 40.0, 0.05, 1.00, 0.20, 0.30, 0.50, 0.0, 0.13, 0.23),
            new BasketOptionThreeData(BasketType.MaxBasket, OptionType.Put, 45.0, 40.0, 40.0, 40.0, 0.05, 1.00, 0.20, 0.30, 0.50, 0.0, 2.26, 5.00),
            // {MaxBasket, Option::Put,  35.0,  40.0,  40.0, 40.0, 0.05, 4.00, 0.20, 0.30, 0.50, 0.0, 0.01, 0.01},
            new BasketOptionThreeData(BasketType.MaxBasket, OptionType.Put, 40.0, 40.0, 40.0, 40.0, 0.05, 4.00, 0.20, 0.30, 0.50, 0.0, 0.25, 0.44),
            new BasketOptionThreeData(BasketType.MaxBasket, OptionType.Put, 45.0, 40.0, 40.0, 40.0, 0.05, 4.00, 0.20, 0.30, 0.50, 0.0, 1.55, 5.00),
            // {MaxBasket, Option::Put,  35.0,  40.0,  40.0, 40.0, 0.05, 7.00, 0.20, 0.30, 0.50, 0.0, 0.03, 0.04},
            // {MaxBasket, Option::Put,  40.0,  40.0,  40.0, 40.0, 0.05, 7.00, 0.20, 0.30, 0.50, 0.0, 0.31, 0.57},
            new BasketOptionThreeData(BasketType.MaxBasket, OptionType.Put, 45.0, 40.0, 40.0, 40.0, 0.05, 7.00, 0.20, 0.30, 0.50, 0.0, 1.41, 5.00),
            new BasketOptionThreeData(BasketType.MaxBasket, OptionType.Put, 40.0, 40.0, 40.0, 40.0, 0.05, 7.00, 0.20, 0.30, 0.50, 0.5, 0.91, 1.19),
        ];

        DayCounter dc = new Actual360();
        Date today = Context.EvaluationDate;

        YieldTermStructure qTS = FlatRate(today, 0.0, dc);

        foreach (BasketOptionThreeData value in values)
        {
            var payoff = new PlainVanillaPayoff(value.Type, value.Strike);

            Date exDate = today + ((Integer)value.T * 30);
            var exercise = new EuropeanExercise(exDate);
            var amExercise = new AmericanExercise(today, exDate);

            YieldTermStructure rTS = FlatRate(today, value.R, dc);

            StochasticProcess1D stochProcess1 =
                new BlackScholesMertonProcess(value.S1, qTS, rTS, FlatVol(today, value.V1, dc));
            StochasticProcess1D stochProcess2 =
                new BlackScholesMertonProcess(value.S2, qTS, rTS, FlatVol(today, value.V2, dc));
            StochasticProcess1D stochProcess3 =
                new BlackScholesMertonProcess(value.S3, qTS, rTS, FlatVol(today, value.V3, dc));

            var correlation = new Matrix(3, 3, value.Rho);
            for (int j = 0; j < 3; j++)
            {
                correlation[j, j] = 1.0;
            }

            var process = new StochasticProcessArray([stochProcess1, stochProcess2, stochProcess3], correlation);

            // use a 3D sobol sequence...
            // Think long and hard before moving to more than 1 timestep....
            Func<BasketOption, EvaluationContext, MultiAssetOptionResults> mcQuasiEngine =
                new MakeMCEuropeanBasketEngine<LowDiscrepancy, GeneralStatistics>(process, 42)
                .WithStepsPerYear(1)
                .WithSamples(8091)
                .ToEngine();

            var euroBasketOption = new BasketOption(BasketTypeToPayoff(value.BasketType, payoff), exercise);

            Real expected = value.EuroValue;
            Real calculated = mcQuasiEngine(euroBasketOption, Context).NPV!.Value;
            Real relError = RelativeError(calculated, expected, value.S1);
            Real mcRelativeErrorTolerance = 0.01;
            Assert.True(
                relError <= mcRelativeErrorTolerance,
                Failure("MC Quasi value", value, expected, calculated, relError, mcRelativeErrorTolerance));

            int requiredSamples = 1000;
            int timeSteps = 500;
            long seed = 1;
            Func<BasketOption, EvaluationContext, MultiAssetOptionResults> mcLSMCEngine =
                new MakeMCAmericanBasketEngine<PseudoRandom, GeneralStatistics>(process, seed)
                .WithSteps(timeSteps)
                .WithAntitheticVariate()
                .WithSamples(requiredSamples)
                .WithCalibrationSamples(requiredSamples / 4)
                .ToEngine();

            var amBasketOption = new BasketOption(BasketTypeToPayoff(value.BasketType, payoff), amExercise);

            expected = value.AmValue;
            calculated = mcLSMCEngine(amBasketOption, Context).NPV!.Value;
            relError = RelativeError(calculated, expected, value.S1);
            Real mcAmericanRelativeErrorTolerance = 0.01;
            Assert.True(
                relError <= mcAmericanRelativeErrorTolerance,
                Failure("MC LSMC Value", value, expected, calculated, relError, mcRelativeErrorTolerance));
        }
    }

    [Fact]
    public void TestTavellaValues()
    {
        /*
            Data from:
            "Quantitative Methods in Derivatives Pricing"
            Tavella, D. A.   -   Wiley (2002)
        */
        BasketOptionThreeData[] values =
        [
            // time in months is with 30 days to the month..
            // basketType, optionType,       strike,    s1,    s2,   s3,    r,    t,   v1,   v2,  v3,  rho, euroValue, american Value,
            new BasketOptionThreeData(BasketType.MaxBasket, OptionType.Call, 100.0, 100.0, 100.0, 100.0, 0.05, 3.00, 0.20, 0.20, 0.20, 0.0, -999.0, 18.082),
        ];

        DayCounter dc = new Actual360();
        Date today = Context.EvaluationDate;

        YieldTermStructure qTS = FlatRate(today, 0.1, dc);
        YieldTermStructure rTS = FlatRate(today, 0.05, dc);

        Real mcRelativeErrorTolerance = 0.01;
        int requiredSamples = 10000;
        int timeSteps = 20;

        // QuantLib passes seed 0, a seed from the clock, which its tolerance must meet on any draw; a fixed seed here
        // (S2R4, the S-exo plan's ED13)
        long seed = 42;

        var payoff = new PlainVanillaPayoff(values[0].Type, values[0].Strike);

        Date exDate = today + TimeToDays(values[0].T);
        var exercise = new AmericanExercise(today, exDate);

        StochasticProcess1D stochProcess1 =
            new BlackScholesMertonProcess(values[0].S1, qTS, rTS, FlatVol(today, values[0].V1, dc));
        StochasticProcess1D stochProcess2 =
            new BlackScholesMertonProcess(values[0].S2, qTS, rTS, FlatVol(today, values[0].V2, dc));
        StochasticProcess1D stochProcess3 =
            new BlackScholesMertonProcess(values[0].S3, qTS, rTS, FlatVol(today, values[0].V3, dc));

        var correlation = new Matrix(3, 3, 0.0);
        for (int j = 0; j < 3; j++)
        {
            correlation[j, j] = 1.0;
        }

        correlation[1, 0] = -0.25;
        correlation[0, 1] = -0.25;
        correlation[2, 0] = 0.25;
        correlation[0, 2] = 0.25;
        correlation[2, 1] = 0.3;
        correlation[1, 2] = 0.3;

        var process = new StochasticProcessArray([stochProcess1, stochProcess2, stochProcess3], correlation);
        Func<BasketOption, EvaluationContext, MultiAssetOptionResults> mcLSMCEngine =
            new MakeMCAmericanBasketEngine<PseudoRandom, GeneralStatistics>(process, seed)
            .WithSteps(timeSteps)
            .WithAntitheticVariate()
            .WithSamples(requiredSamples)
            .WithCalibrationSamples(requiredSamples / 4)
            .ToEngine();

        var basketOption = new BasketOption(BasketTypeToPayoff(values[0].BasketType, payoff), exercise);

        MultiAssetOptionResults results = mcLSMCEngine(basketOption, Context);
        Real calculated = results.NPV!.Value;
        Real expected = values[0].AmValue;
        Real errorEstimate = results.ErrorEstimate!.Value;
        Real relError = RelativeError(calculated, expected, values[0].S1);
        Assert.True(
            relError <= mcRelativeErrorTolerance,
            Failure("MC LSMC Tavella value", values[0], expected, calculated, errorEstimate, mcRelativeErrorTolerance));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 11)]
    [InlineData(11, 17)]
    [InlineData(17, 23)]
    [InlineData(23, 29)]
    public void TestOneDAmericanValues(int from, int to)
    {
        DayCounter dc = new Actual360();
        Date today = Context.EvaluationDate;

        int requiredSamples = 10000;
        int timeSteps = 52;

        // QuantLib passes seed 0, a seed from the clock, which its tolerance must meet on any draw; a fixed seed here
        // (S2R4, the S-exo plan's ED13)
        long seed = 42;

        for (int i = from; i < to; i++)
        {
            BasketOptionOneData value = OneDataValues[i];
            var stochProcess1 = new BlackScholesMertonProcess(
                value.S, FlatRate(today, value.Q, dc), FlatRate(today, value.R, dc), FlatVol(today, value.V, dc));

            var correlation = new Matrix(1, 1, 1.0);
            var process = new StochasticProcessArray([stochProcess1], correlation);

            Func<BasketOption, EvaluationContext, MultiAssetOptionResults> mcLSMCEngine =
                new MakeMCAmericanBasketEngine<PseudoRandom, GeneralStatistics>(process, seed)
                .WithSteps(timeSteps)
                .WithAntitheticVariate()
                .WithSamples(requiredSamples)
                .WithCalibrationSamples(requiredSamples / 4)
                .ToEngine();

            var payoff = new PlainVanillaPayoff(value.Type, value.Strike);

            Date exDate = today + TimeToDays(value.T);
            var exercise = new AmericanExercise(today, exDate);

            var basketOption = new BasketOption(BasketTypeToPayoff(BasketType.MaxBasket, payoff), exercise);

            Real calculated = mcLSMCEngine(basketOption, Context).NPV!.Value;
            Real expected = value.Result;
            Real relError = RelativeError(calculated, expected, value.S);
            Assert.True(
                relError <= value.Tol,
                $"American option with max basket payoff on one asset: spot {value.S}, strike {value.Strike}, "
                + $"expected {expected}, calculated {calculated}, error {relError}, tolerance {value.Tol}");
        }
    }

    // This unit test is a regression test to check for a crash in monte carlo if the required sample is odd.  The
    // crash occurred because the samples array size was off by one when antithetic paths were added.
    [Fact]
    public void TestOddSamples()
    {
        int requiredSamples = 10001; // The important line
        int timeSteps = 53;
        BasketOptionOneData[] values =
        [
            //        type, strike,   spot,    q,    r,    t,  vol,   value, tol
            new BasketOptionOneData(OptionType.Put, 100.00, 80.00, 0.0, 0.06, 0.5, 0.4, 21.6059, 1e-2),
        ];

        DayCounter dc = new Actual360();
        Date today = Context.EvaluationDate;

        // QuantLib passes seed 0, a seed from the clock (S2R4, the S-exo plan's ED13)
        long seed = 42;

        foreach (BasketOptionOneData value in values)
        {
            var stochProcess1 = new BlackScholesMertonProcess(
                value.S, FlatRate(today, value.Q, dc), FlatRate(today, value.R, dc), FlatVol(today, value.V, dc));

            var correlation = new Matrix(1, 1, 1.0);
            var process = new StochasticProcessArray([stochProcess1], correlation);

            Func<BasketOption, EvaluationContext, MultiAssetOptionResults> mcLSMCEngine =
                new MakeMCAmericanBasketEngine<PseudoRandom, GeneralStatistics>(process, seed)
                .WithSteps(timeSteps)
                .WithAntitheticVariate()
                .WithSamples(requiredSamples)
                .WithCalibrationSamples(requiredSamples / 4)
                .ToEngine();

            var payoff = new PlainVanillaPayoff(value.Type, value.Strike);

            Date exDate = today + TimeToDays(value.T);
            var exercise = new AmericanExercise(today, exDate);

            var basketOption = new BasketOption(BasketTypeToPayoff(BasketType.MaxBasket, payoff), exercise);

            Real calculated = mcLSMCEngine(basketOption, Context).NPV!.Value;
            Real expected = value.Result;
            Real relError = RelativeError(calculated, expected, value.S);
            Assert.True(
                relError <= value.Tol,
                $"odd samples: expected {expected}, calculated {calculated}, error {relError}, tolerance {value.Tol}");
        }
    }

    [Fact]
    public void Test2DPDEGreeks()
    {
        const Real s1 = 100;
        const Real s2 = 100;
        const Real r = 0.013;
        const Volatility v = 0.2;
        const Real rho = 0.5;
        const Real strike = s1 - s2;
        const int maturityInDays = 1095;

        DayCounter dc = new Actual365Fixed();
        Date today = Context.EvaluationDate;
        Date maturity = today + maturityInDays;

        YieldTermStructure rTS = FlatRate(today, r, dc);
        BlackVolTermStructure vTS = FlatVol(today, v, dc);

        var option = new BasketOption(
            new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, strike)), new EuropeanExercise(maturity));

        MultiAssetOptionResults fd = Fd2dBlackScholesVanillaEngine.Calculate(
            option, Context, new BlackProcess(s1, rTS, vTS), new BlackProcess(s2, rTS, vTS), rho);
        Real calculatedDelta = fd.Delta!.Value;
        Real calculatedGamma = fd.Gamma!.Value;

        Real Kirk(Real spot1, Real spot2) => KirkEngine.Calculate(
            option, Context, new BlackProcess(spot1, rTS, vTS), new BlackProcess(spot2, rTS, vTS), rho).NPV!.Value;

        const Real eps = 1.0;
        Real npv = Kirk(s1, s2);
        Real npvUp = Kirk(s1 + eps, s2 + eps);
        Real npvDown = Kirk(s1 - eps, s2 - eps);

        Real expectedDelta = (npvUp - npvDown) / (2 * eps);
        Real expectedGamma = (npvUp + npvDown - (2 * npv)) / (eps * eps);

        const Real tol = 0.0005;
        Assert.True(
            Math.Abs(expectedDelta - calculatedDelta) <= tol,
            $"failed to reproduce delta with 2dim PDE\n    PDE delta: {F8(calculatedDelta)}\n"
            + $"    numerical delta: {F8(expectedDelta)}");
        Assert.True(
            Math.Abs(expectedGamma - calculatedGamma) <= tol,
            $"failed to reproduce delta with 2dim PDE\n    PDE gamma: {F8(calculatedGamma)}\n"
            + $"    numerical gamma: {F8(expectedGamma)}");
    }

    [Fact]
    public void TestBjerksundStenslandSpreadEngine()
    {
        DayCounter dc = new Actual365Fixed();
        var today = new Date(1, Month.March, 2024);
        Date maturity = today + new Period(12, TimeUnit.Months);
        var exercise = new EuropeanExercise(maturity);

        const Real rho = 0.75;
        const Real f1 = 100, f2 = 110;

        YieldTermStructure r = FlatRate(today, 0.05, dc);

        var p1 = new BlackProcess(f1, r, FlatVol(today, 0.25, dc));
        var p2 = new BlackProcess(f2, r, FlatVol(today, 0.35, dc));

        const Real strike = 5;

        var callOption = new BasketOption(
            new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, strike)), exercise);
        Real callNPV = BjerksundStenslandSpreadEngine.Calculate(callOption, Context, p1, p2, rho).NPV!.Value;

        // reference value was calculated with python packages PyFENG 0.2.6
        const Real expectedPutNPV = 17.850835947276213;

        var putOption = new BasketOption(
            new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Put, strike)), exercise);
        Real putNPV = BjerksundStenslandSpreadEngine.Calculate(putOption, Context, p1, p2, rho).NPV!.Value;

        const double tol = QL_EPSILON * 100;
        Real diff = Math.Abs(putNPV - expectedPutNPV);
        Assert.True(
            diff <= tol,
            "failed to reproduce reference put price using the Bjerksund-Stensland spread engine."
            + $"\n    calculated: {F8(putNPV)}\n    expected  : {F8(expectedPutNPV)}\n    diff      : {F8(diff)}");

        double df = r.Discount(maturity);
        Real fwd = (callNPV - putNPV) / df;

        diff = Math.Abs(fwd - (f1 - f2 - strike));
        Assert.True(
            diff <= tol,
            "failed to reproduce call-put parity using the Bjerksund-Stensland spread engine."
            + $"\n    calculated fwd: {F8(fwd)}\n    expected fwd  : {F8(f1 - f2 - strike)}\n    diff          : {F8(diff)}");
    }

    [Fact]
    public void TestOperatorSplittingSpreadEngine()
    {
        // Example taken from
        // Chi-Fai Lo, Pricing Spread Options by the Operator Splitting Method,
        // https://papers.ssrn.com/sol3/papers.cfm?abstract_id=2429696
        DayCounter dc = new Actual365Fixed();
        var today = new Date(1, Month.March, 2025);
        Date maturity = YearFractionToDateFunctions.RoundToDayStart(
            YearFractionToDateFunctions.YearFractionToDate(dc, today, 1.0));

        YieldTermStructure r = FlatRate(today, 0.05, dc);
        double df = r.Discount(maturity);
        double dq1 = FlatRate(today, 0.03, dc).Discount(maturity);
        double dq2 = FlatRate(today, 0.02, dc).Discount(maturity);

        Real f1 = 110 * dq1 / df, f2 = 90 * dq2 / df;

        var p1 = new BlackProcess(f1, r, FlatVol(today, 0.3, dc));
        var p2 = new BlackProcess(f2, r, FlatVol(today, 0.2, dc));

        var option = new BasketOption(
            new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 20.0)), new EuropeanExercise(maturity));

        Real[][] testData =
        [
            [-0.9, 18.9323, 18.9361],
            [-0.7, 18.0092, 18.012],
            [-0.5, 17.0325, 17.0344],
            [-0.4, 16.5211, 16.5227],
            [-0.3, 15.9925, 15.9937],
            [-0.2, 15.4449, 15.4458],
            [-0.1, 14.8762, 14.8768],
            [0.0, 14.284, 14.2843],
            [0.1, 13.6651, 13.6654],
            [0.2, 13.016, 13.0161],
            [0.3, 12.3319, 12.3319],
            [0.4, 11.6067, 11.6067],
            [0.5, 10.8323, 10.8323],
            [0.7, 9.0863, 9.0862],
            [0.9, 6.9148, 6.9134],
        ];

        foreach (Real[] i in testData)
        {
            Real rho = i[0];
            Real expected = i[1];

            Real npv = OperatorSplittingSpreadEngine.Calculate(
                option, Context, p1, p2, rho, OperatorSplittingSpreadEngineOrder.First).NPV!.Value;
            Real diff = Math.Abs(npv - expected);
            Real tol = 0.0001;
            Assert.True(
                diff <= tol,
                "failed to reproduce reference values using the first order operator splitting spread engine."
                + $"\n    calculated: {F5(npv)}\n    expected  : {F5(expected)}\n    diff          : {F5(diff)}");

            expected = i[2];
            npv = OperatorSplittingSpreadEngine.Calculate(
                option, Context, p1, p2, rho, OperatorSplittingSpreadEngineOrder.Second).NPV!.Value;
            diff = Math.Abs(npv - expected);
            tol = 0.0005;
            Assert.True(
                diff <= tol,
                "failed to reproduce reference values using the second order operator splitting spread engine."
                + $"\n    calculated: {F5(npv)}\n    expected  : {F5(expected)}\n    diff          : {F5(diff)}");
        }
    }

    [Fact]
    public void TestStrangSplittingSpreadEngineVsMathematica()
    {
        // Example taken from
        // Chi-Fai Lo, Pricing Spread Options by the Operator Splitting Method,
        // https://papers.ssrn.com/sol3/papers.cfm?abstract_id=2429696
        // Reference results have been calculated with a Mathematica script.
        DayCounter dc = new Actual365Fixed();
        var today = new Date(27, Month.May, 2024);

        YieldTermStructure rTS = FlatRate(today, 0.05, dc);
        BlackVolTermStructure volTS2 = FlatVol(today, 0.2, dc);

        StrangData[] testCases =
        [
            new StrangData(5.0, 20.0, 0.1, 0.6, 15.39520956886349, 15.39641179190707, 15.41992212706643),
            new StrangData(10.0, 20.0, 0.1, 0.6, 22.91537136258191, 22.89480115264337, 22.95919510928365),
            new StrangData(20.0, 20.0, 0.1, 0.6, 33.69859018569740, 33.59697949481467, 33.73582501903848),
            new StrangData(1.0, 20.0, 0.3, 0.6, 10.9751711157804, 10.97662152028116, 10.97661321814579),
            new StrangData(2.0, 20.0, 0.3, 0.6, 15.68896063758723, 15.69277461480688, 15.69275497617036),
            new StrangData(3.0, 20.0, 0.3, 0.6, 19.33110275816226, 19.33760645637910, 19.33758123861756),
            new StrangData(4.0, 20.0, 0.3, 0.6, 22.40185479100672, 22.41113452093983, 22.41111679131122),
            new StrangData(5.0, 20.0, 0.3, 0.6, 25.09737848235137, 25.10937922536118, 25.10938819057636),
            new StrangData(1.0, 10.0, 0.3, 0.6, 16.10447007803242, 16.10494344785443, 16.10494658134660),
            new StrangData(1.0, 40.0, 0.3, 0.6, 4.657519189575983, 4.657079657030094, 4.656973008981588),
            new StrangData(1.0, 60.0, 0.3, 0.6, 1.837359067901817, 1.831230481909945, 1.831241843743509),
            new StrangData(1.0, 20.0, 0.5, 0.6, 18.79838447214884, 18.79674735337080, 18.79654551825391),
            new StrangData(1.0, 20.0, 0.3, -0.9, 20.17112122874686, 20.14780367419582, 20.15151348149147),
            new StrangData(1.0, 20.0, 0.3, 0.0, 15.38036208157481, 15.37697052349819, 15.37728179978961),
            new StrangData(2.0, 20.0, 0.3, -0.5, 25.80847626931109, 25.77323435009942, 25.77810550213640),
        ];

        const Real s1 = 110.0, s2 = 90.0;
        const double tol = 100 * QL_EPSILON;

        foreach (StrangData testCase in testCases)
        {
            Real rho = testCase.Rho;
            Real strike = testCase.K;
            Date maturityDate = YearFractionToDateFunctions.RoundToDayStart(
                YearFractionToDateFunctions.YearFractionToDate(dc, today, testCase.T));

            BlackVolTermStructure volTS1 = FlatVol(today, testCase.Vol1, dc);

            var option = new BasketOption(
                new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, strike)),
                new EuropeanExercise(maturityDate));

            double dr = rTS.Discount(maturityDate);
            Real bigF1 = s1 / dr;
            Real bigF2 = s2 / dr;

            var p1 = new BlackProcess(bigF1, rTS, volTS1);
            var p2 = new BlackProcess(bigF2, rTS, volTS2);

            Real kirkCalculated = KirkEngine.Calculate(option, Context, p1, p2, rho).NPV!.Value;
            Real diff = Math.Abs(testCase.KirkNPV - kirkCalculated);
            Assert.True(
                diff <= tol * testCase.KirkNPV,
                $"failed to reproduce Kirk reference values\n    calculated: {F16(kirkCalculated)}\n"
                + $"    expected  : {F16(testCase.KirkNPV)}\n    diff      : {F16(diff)}");

            Real strang1Calculated = OperatorSplittingSpreadEngine.Calculate(
                option, Context, p1, p2, rho, OperatorSplittingSpreadEngineOrder.First).NPV!.Value;
            diff = Math.Abs(testCase.Strang1 - strang1Calculated);
            Assert.True(
                diff <= tol * testCase.Strang1,
                $"failed to reproduce Operator Splitting reference values\n    calculated: {F16(strang1Calculated)}\n"
                + $"    expected  : {F16(testCase.Strang1)}\n    diff      : {F16(diff)}");

            Real strang2Calculated = OperatorSplittingSpreadEngine.Calculate(
                option, Context, p1, p2, rho, OperatorSplittingSpreadEngineOrder.Second).NPV!.Value;
            diff = Math.Abs(testCase.Strang2 - strang2Calculated);
            Assert.True(
                diff <= tol * testCase.Strang2,
                $"failed to reproduce Operator Splitting reference values\n    calculated: {F16(strang2Calculated)}\n"
                + $"    expected  : {F16(testCase.Strang2)}\n    diff      : {F16(diff)}");
        }
    }

    [Fact]
    public void TestPDEvsApproximations()
    {
        DayCounter dc = new Actual365Fixed();
        var today = new Date(5, Month.February, 2024);
        Date maturity = today + new Period(6, TimeUnit.Months);

        const Real strike = 5;

        var statKirk = new IncrementalStatistics();
        var statBS2014 = new IncrementalStatistics();
        var statOs1 = new IncrementalStatistics();
        var statOs2 = new IncrementalStatistics();
        var statPearson = new IncrementalStatistics();
        var statGaussianCopula = new IncrementalStatistics();

        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            var option = new BasketOption(
                new SpreadBasketPayoff(new PlainVanillaPayoff(type, strike)), new EuropeanExercise(maturity));

            foreach (Real rho in new[] { -0.75, 0.0, 0.9 })
            {
                foreach (Real rate in new[] { 0.0, 0.05, 0.2 })
                {
                    // The spread option has a single discount.
                    YieldTermStructure rTS = FlatRate(today, rate, dc);
                    var p1 = new BlackProcess(100, rTS, FlatVol(today, 0.25, dc));

                    foreach (Real spot in new Real[] { 75, 90, 100, 105, 175 })
                    {
                        var p2 = new BlackProcess(spot, rTS, FlatVol(today, 0.4, dc));

                        Real fdNPV = Fd2dBlackScholesVanillaEngine.Calculate(
                            option, Context, p1, p2, rho, 50, 50, 15).NPV!.Value;

                        statKirk.Add(KirkEngine.Calculate(option, Context, p1, p2, rho).NPV!.Value - fdNPV);
                        statBS2014.Add(
                            BjerksundStenslandSpreadEngine.Calculate(option, Context, p1, p2, rho).NPV!.Value - fdNPV);
                        statOs1.Add(
                            OperatorSplittingSpreadEngine.Calculate(
                                option, Context, p1, p2, rho, OperatorSplittingSpreadEngineOrder.First).NPV!.Value
                            - fdNPV);
                        statOs2.Add(
                            OperatorSplittingSpreadEngine.Calculate(
                                option, Context, p1, p2, rho, OperatorSplittingSpreadEngineOrder.Second).NPV!.Value
                            - fdNPV);
                        statPearson.Add(PearsonSpreadEngine.Calculate(option, Context, p1, p2, rho).NPV!.Value - fdNPV);
                        statGaussianCopula.Add(
                            GaussianCopulaSpreadEngine.Calculate(option, Context, p1, p2, rho).NPV!.Value - fdNPV);
                    }
                }
            }
        }

        AssertStdev(statKirk, 0.03, "failed to reproduce PDE spread option prices with Kirk engine.");
        AssertStdev(
            statBS2014, 0.02, "failed to reproduce PDE spread option prices with Bjerksund-Stensland engine.");
        AssertStdev(
            statOs1,
            0.02,
            "failed to reproduce PDE spread option prices with Operator-Splitting engine (first order).");
        AssertStdev(
            statOs2,
            0.02,
            "failed to reproduce PDE spread option prices with Operator-Splitting engine (second order).");
        AssertStdev(statPearson, 0.02, "failed to reproduce PDE spread option prices with Pearson engine.");
        AssertStdev(
            statGaussianCopula, 0.02, "failed to reproduce PDE spread option prices with Gaussian Copula spread engine.");
    }

    [Fact]
    public void TestDengLiZhouVsPDE()
    {
        DayCounter dc = new Actual365Fixed();
        var today = new Date(25, Month.March, 2024);
        Date maturity = today + new Period(6, TimeUnit.Months);

        Real[] underlyings = [50, 11, 55, 200];
        Real[] volatilities = [0.2, 0.6, 0.4, 0.3];
        Real[] q = [0.075, 0.05, 0.08, 0.04];

        YieldTermStructure rTS = FlatRate(today, 0.05, dc);

        var exercise = new EuropeanExercise(maturity);

        var processes = ImmutableArray.CreateBuilder<GeneralizedBlackScholesProcess>(4);
        for (int d = 0; d < 4; ++d)
        {
            processes.Add(new BlackScholesMertonProcess(
                underlyings[d], FlatRate(today, q[d], dc), rTS, FlatVol(today, volatilities[d], dc)));
        }

        var rho = new Matrix(4, 4);
        for (int i = 0; i < 4; ++i)
        {
            for (int j = i; j < 4; ++j)
            {
                rho[i, j] = rho[j, i] =
                    Math.Exp((-0.5 * Math.Abs((Real)i - (Real)j)) - ((i != j) ? 0.02 * (i + j) : 0.0));
            }
        }

        const Real strike = 5.0;
        var option = new BasketOption(
            new AverageBasketPayoff(new PlainVanillaPayoff(OptionType.Put, strike), [-1.0, -5.0, -2.0, 1.0]),
            exercise);

        Real calculated = DengLiZhouBasketEngine.Calculate(option, Context, processes.ToImmutable(), rho).NPV!.Value;

        Real expected = FdndimBlackScholesVanillaEngine.Calculate(
            option, Context, processes.ToImmutable(), rho, 20, 10).NPV!.Value;

        Real diff = Math.Abs(calculated - expected);
        const Real tol = 0.05;
        Assert.True(
            diff <= tol,
            $"failed to compare basket option prices\n    Deng-Li-Zhou: {F5(calculated)}\n    PDE:          {F5(expected)}"
            + $"\n    diff:         {F5(diff)}\n    tolerance:    {F5(tol)}");
    }

    [Fact]
    public void TestDengLiZhouWithNegativeStrike()
    {
        DayCounter dc = new Actual365Fixed();
        var today = new Date(27, Month.May, 2024);
        Date maturity = today + new Period(6, TimeUnit.Months);

        Real[] underlyings = [220, 105, 45, 1e-12];
        Real[] volatilities = [0.4, 0.25, 0.3, 0.25];
        Real[] q = [0.04, 0.075, 0.05, 0.1];

        YieldTermStructure rTS = FlatRate(today, 0.03, dc);

        var exercise = new EuropeanExercise(maturity);

        var processes = ImmutableArray.CreateBuilder<GeneralizedBlackScholesProcess>(4);
        for (int d = 0; d < 4; ++d)
        {
            processes.Add(new BlackScholesMertonProcess(
                underlyings[d], FlatRate(today, q[d], dc), rTS, FlatVol(today, volatilities[d], dc)));
        }

        var rho = new Matrix(4, 4, 0.0);
        rho[0, 1] = rho[1, 0] = 0.8;
        rho[0, 2] = rho[2, 0] = -0.2;
        rho[1, 2] = rho[2, 1] = 0.3;
        rho[0, 0] = rho[1, 1] = rho[2, 2] = rho[3, 3] = 1.0;
        rho[1, 3] = rho[3, 1] = 0.3;

        const Real strike = -2.0;
        var option = new BasketOption(
            new AverageBasketPayoff(new PlainVanillaPayoff(OptionType.Call, strike), [0.5, -2.0, 2.0, -0.75]),
            exercise);

        Real calculated = DengLiZhouBasketEngine.Calculate(option, Context, processes.ToImmutable(), rho).NPV!.Value;
        const Real expected = 3.34412;
        const Real tol = 1e-5;

        Real diff = Math.Abs(calculated - expected);
        Assert.True(
            diff <= tol,
            $"failed to reproduce Deng-Li-Zhou value with negative strike\n    Deng-Li-Zhou: {F5(calculated)}"
            + $"\n    Expected:     {F5(expected)}\n    diff:         {F5(diff)}\n    tolerance:    {F5(tol)}");
    }

    [Fact]
    public void TestNoDivByZeroOperatorSplitting()
    {
        DayCounter dc = new Actual365Fixed();
        var today = new Date(5, Month.December, 2024);
        Date maturity = today + new Period(18, TimeUnit.Months);

        YieldTermStructure zeroFlat = FlatRate(today, 0.0, dc);

        GeneralizedBlackScholesProcess ProcessGen(Real spot, Volatility vol) =>
            new(spot, zeroFlat, zeroFlat, FlatVol(today, vol, dc));

        var basketOption = new BasketOption(
            new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Put, 50.0)), new EuropeanExercise(maturity));

        const Real eps = 1e-5;
        Real Npv(Volatility vol) => OperatorSplittingSpreadEngine.Calculate(
            basketOption,
            Context,
            ProcessGen(160, 0.25 * 3),
            ProcessGen(100, vol * 150.0 / 100.0),
            1 / 3.0,
            OperatorSplittingSpreadEngineOrder.Second).NPV!.Value;

        Real lNpv = Npv(0.25 - eps);
        Real rNpv = Npv(0.25 + eps);
        Real expected = 0.5 * (rNpv + lNpv);

        Real calculated = Npv(0.25);
        Real diff = Math.Abs(calculated - expected);
        const Real tol = 5e-8;
        Assert.True(
            !double.IsNaN(calculated) && diff <= tol,
            $"failed to reproduce spread option price with OperatorSplittingEngine\n    calculated:  {F8(calculated)}"
            + $"\n    expected:    {F8(expected)}\n    diff:        {F8(diff)}\n    tolerance:   {F8(tol)}");
    }

    [Fact]
    public void TestPearsonSpreadEngine()
    {
        DayCounter dc = new Actual365Fixed();
        var today = new Date(1, Month.March, 2025);
        Date maturity = today + new Period(12, TimeUnit.Months);
        var exercise = new EuropeanExercise(maturity);

        const Real rho = 0.75;
        const Real f1 = 100, f2 = 110;

        YieldTermStructure r = FlatRate(today, 0.05, dc);

        var p1 = new BlackProcess(f1, r, FlatVol(today, 0.25, dc));
        var p2 = new BlackProcess(f2, r, FlatVol(today, 0.35, dc));

        // 1. Put-call parity: (C - P) / df = F1 - F2 - K
        const Real strike = 5;
        var callOption = new BasketOption(
            new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, strike)), exercise);
        Real callNPV = PearsonSpreadEngine.Calculate(callOption, Context, p1, p2, rho).NPV!.Value;

        var putOption = new BasketOption(
            new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Put, strike)), exercise);
        Real putNPV = PearsonSpreadEngine.Calculate(putOption, Context, p1, p2, rho).NPV!.Value;

        double df = r.Discount(maturity);
        Real fwd = (callNPV - putNPV) / df;
        Real diff = Math.Abs(fwd - (f1 - f2 - strike));
        const double tol = 1e-10;
        Assert.True(
            diff <= tol,
            "failed to reproduce call-put parity using the Pearson spread engine."
            + $"\n    calculated fwd: {F8(fwd)}\n    expected fwd  : {F8(f1 - f2 - strike)}\n    diff          : {F8(diff)}");

        // 2. Exchange option (K=0): should match Bjerksund-Stensland
        const Real exchangeStrike = 0.0;
        var exchangeOption = new BasketOption(
            new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, exchangeStrike)), exercise);
        Real pearsonExchange = PearsonSpreadEngine.Calculate(exchangeOption, Context, p1, p2, rho).NPV!.Value;
        Real bsExchange = BjerksundStenslandSpreadEngine.Calculate(exchangeOption, Context, p1, p2, rho).NPV!.Value;

        diff = Math.Abs(pearsonExchange - bsExchange);
        Assert.True(
            diff <= 1e-6,
            "failed to reproduce exchange option price using the Pearson spread engine."
            + $"\n    Pearson   : {F8(pearsonExchange)}\n    BS2014    : {F8(bsExchange)}\n    diff      : {F8(diff)}");
    }

    [Fact]
    public void TestGaussianCopulaSpreadEngineFlatVol()
    {
        DayCounter dc = new Actual365Fixed();
        var today = new Date(1, Month.March, 2025);
        Date maturity = today + new Period(12, TimeUnit.Months);
        var exercise = new EuropeanExercise(maturity);

        const Real rho = 0.5;
        const Real f1 = 100, f2 = 96;

        YieldTermStructure r = FlatRate(today, 0.05, dc);

        var p1 = new BlackProcess(f1, r, FlatVol(today, 0.20, dc));
        var p2 = new BlackProcess(f2, r, FlatVol(today, 0.25, dc));

        // 1. Put-call parity: (C - P) / df = F1 - F2 - K
        const Real strike = 3;
        var callOption = new BasketOption(
            new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, strike)), exercise);
        Real callNPV = GaussianCopulaSpreadEngine.Calculate(callOption, Context, p1, p2, rho).NPV!.Value;

        var putOption = new BasketOption(
            new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Put, strike)), exercise);
        Real putNPV = GaussianCopulaSpreadEngine.Calculate(putOption, Context, p1, p2, rho).NPV!.Value;

        double df = r.Discount(maturity);
        Real fwd = (callNPV - putNPV) / df;
        Real expectedFwd = f1 - f2 - strike;
        Real relError = RelativeError(fwd, expectedFwd, f1);
        const double parityRelTol = 1e-3;
        Assert.True(
            relError <= parityRelTol,
            "failed to reproduce call-put parity using the Gaussian Copula spread engine."
            + $"\n    calculated fwd: {F8(fwd)}\n    expected fwd : {F8(expectedFwd)}\n    rel error    : {F8(relError)}");

        // 2. Exchange option (K=0): should match Margrabe via Bjerksund-Stensland
        const Real exchangeStrike = 0.0;
        var exchangeOption = new BasketOption(
            new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, exchangeStrike)), exercise);
        Real copulaExchange = GaussianCopulaSpreadEngine.Calculate(exchangeOption, Context, p1, p2, rho).NPV!.Value;
        Real bsExchange = BjerksundStenslandSpreadEngine.Calculate(exchangeOption, Context, p1, p2, rho).NPV!.Value;

        relError = RelativeError(copulaExchange, bsExchange, f1);
        const double exchangeRelTol = 1e-3;
        Assert.True(
            relError <= exchangeRelTol,
            "failed to reproduce exchange option price using the Gaussian Copula spread engine."
            + $"\n    Copula    : {F8(copulaExchange)}\n    BS2014    : {F8(bsExchange)}\n    rel error : {F8(relError)}");
    }

    [Fact]
    public void TestGaussianCopulaSpreadEngineSVI()
    {
        DayCounter dc = new Actual365Fixed();
        var today = new Date(1, Month.March, 2025);
        Date maturity = today + new Period(12, TimeUnit.Months);
        var exercise = new EuropeanExercise(maturity);

        const Real rho = 0.5;
        const Real spot1 = 100, spot2 = 96;

        // SVI parameters: { a, b, sigma, rho_svi, m }
        Real[] sviParams1 = [0.04, 0.10, 0.30, -0.40, 0.0];
        Real[] sviParams2 = [0.02, 0.08, 0.25, -0.30, 0.0];

        YieldTermStructure rTS = FlatRate(today, 0.05, dc);
        double df = rTS.Discount(maturity);
        Real fwd1 = spot1 / df;
        Real fwd2 = spot2 / df;
        double bigT = dc.YearFraction(today, maturity);

        var sviSmile1 = new Experimental.SviSmileSection(bigT, fwd1, sviParams1);
        var sviSmile2 = new Experimental.SviSmileSection(bigT, fwd2, sviParams2);

        var sviVolTS1 = new PiecewiseBlackVarianceSurface(today, maturity, sviSmile1, dc);
        var sviVolTS2 = new PiecewiseBlackVarianceSurface(today, maturity, sviSmile2, dc);

        var p1 = new BlackProcess(spot1, rTS, sviVolTS1);
        var p2 = new BlackProcess(spot2, rTS, sviVolTS2);

        const double tol = 5e-3;
        Real f1 = fwd1;

        foreach (Real strike in new[] { -2.0, 0.0, 4.0, 8.0 })
        {
            var option = new BasketOption(
                new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, strike)), exercise);

            Real copulaNPV = GaussianCopulaSpreadEngine.Calculate(option, Context, p1, p2, rho).NPV!.Value;

            // 2D PDE with Dupire local vol
            Real pdeNPV = Fd2dBlackScholesVanillaEngine.Calculate(
                option, Context, p1, p2, rho, 100, 100, 50, 0, FdmSchemeDesc.Hundsdorfer(), true).NPV!.Value;

            Real relError = RelativeError(copulaNPV, pdeNPV, f1);
            Assert.True(
                relError <= tol,
                "failed to reproduce SVI-smile spread option price using the Gaussian Copula spread engine vs 2D PDE."
                + $"\n    strike    : {strike}\n    Copula    : {F8(copulaNPV)}\n    PDE       : {F8(pdeNPV)}"
                + $"\n    rel error : {F8(relError)}\n    tolerance : {F8(tol)}");
        }
    }

    [Fact]
    public void TestGaussianCopulaSpreadEngineParityAcrossVols()
    {
        // Parity only needs each marginal to price its own forward. A smile with fat
        // wings is where that marginal construction is actually stressed.
        DayCounter dc = new Actual365Fixed();
        var today = new Date(1, Month.March, 2025);
        Date maturity = today + new Period(12, TimeUnit.Months);
        double bigT = dc.YearFraction(today, maturity);
        var exercise = new EuropeanExercise(maturity);

        const Real rho = 0.5, strike = 20.0;
        const Real f1 = 50.0, f2 = 30.0;

        YieldTermStructure rTS = FlatRate(today, 0.0, dc);

        const double parityRelTol = 1e-4;

        foreach (Real c in new[] { 0.7, 1.0, 1.5, 2.0 })
        {
            Real[] svi1 = [c * c * 0.1125, c * c * 0.10, 0.10, 0.525, 0.0];
            Real[] svi2 = [c * c * 0.08, c * c * 0.10, 0.10, 0.18, 0.0];

            var smile1 = new Experimental.SviSmileSection(bigT, f1, svi1);
            var smile2 = new Experimental.SviSmileSection(bigT, f2, svi2);

            var volTS1 = new PiecewiseBlackVarianceSurface(today, maturity, smile1, dc);
            var volTS2 = new PiecewiseBlackVarianceSurface(today, maturity, smile2, dc);

            var p1 = new BlackProcess(f1, rTS, volTS1);
            var p2 = new BlackProcess(f2, rTS, volTS2);

            var callOption = new BasketOption(
                new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, strike)), exercise);
            var putOption = new BasketOption(
                new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Put, strike)), exercise);

            Real fwd = (GaussianCopulaSpreadEngine.Calculate(callOption, Context, p1, p2, rho).NPV!.Value
                    - GaussianCopulaSpreadEngine.Calculate(putOption, Context, p1, p2, rho).NPV!.Value)
                / rTS.Discount(maturity);
            Real expectedFwd = f1 - f2 - strike;
            Real relError = Math.Abs(fwd - expectedFwd) / f1;
            Assert.True(
                relError <= parityRelTol,
                "failed to reproduce call-put parity using the Gaussian Copula spread engine with an SVI smile."
                + $"\n    scale     : {c}\n    fwd       : {F8(fwd)}\n    expected  : {F8(expectedFwd)}"
                + $"\n    rel error : {F8(relError)}\n    tolerance : {F8(parityRelTol)}");
        }
    }

    [Fact]
    public void TestSpreadAndBasketBenchmarks()
    {
        // Benchmark set is derived from
        // "Sum of all Black-Scholes-Merton Models: An efficient Pricing Method for
        //  Spread, Basket and Asian Options", Jaehyuk Choi, 2018
        Benchmark[] benchmarks =
        [
            new Benchmark(
                [100.0, 96.0],
                [0.2, 0.1],
                [0.05, 0.05],
                0.1,
                [M([0.5])],
                [1.0, -1.0],
                [1.0],
                [0.4, 0.8, 1.2, 1.6, 2.0, 2.4, 2.8, 3.2, 3.6, 4.0],
                OptionType.Put,
                [4.86947800209290982, 5.03394599708595702, 5.20170697959426764, 5.37275466121791023, 5.54708103391874285, 5.72467640414054557, 5.90552942907314105, 6.08962715491644957, 6.27695505699956779, 6.46749708160964865]),
            new Benchmark(
                [100.0, 96.0],
                [0.2, 0.1],
                [0.05, 0.05],
                0.1,
                [M([0.5])],
                [1.0, -1.0],
                [1.0],
                [0.4, 0.8, 1.2, 1.6, 2.0, 2.4, 2.8, 3.2, 3.6, 4.0],
                OptionType.Call,
                [8.312460732881519, 8.114993760660171, 7.920819775954081, 7.729932490363331, 7.542323895849758, 7.35798429885716, 7.176902356575362, 6.999065115204262, 6.824458050072985, 6.653065107468672]),
            new Benchmark(
                [200.0, 100.0],
                [0.15, 0.3],
                [0.0, 0.0],
                0.0,
                [M([-0.9]), M([-0.7]), M([-0.5]), M([-0.3]), M([-0.1]), M([0.1]), M([0.3]), M([0.5]), M([0.7]), M([0.9])],
                [1.0, -1.0],
                [1.0],
                [100.0],
                OptionType.Call,
                [23.1398673777858619, 21.9077989170003313, 20.5982705317786383, 19.1954201364940467, 17.6770248596142956, 16.0102190445729207, 14.1425869461427691, 11.9804918293938165, 9.32094392217566181, 5.47927202785675949]),
            new Benchmark(
                [110.0, 90.0],
                [0.3, 0.2],
                [0.0, 0.0],
                0.05,
                [M([0.6])],
                [1.0, -1.0],
                [1.0, 2.0, 3.0, 4.0, 5.0],
                [10.0, 20.0, 60.0],
                OptionType.Call,
                [16.1049476565509657, 10.9766115516406035, 1.83123363415313212, 20.2228932552954817, 15.6927477228442918, 5.41564349607575757, 23.4547216033491992, 19.3375645886881351, 8.93714184429789604, 26.1938805393685357, 22.4110876517984252, 12.2205433053952373, 28.6052191202546133, 25.1093443516670014, 15.2670988551783733]),
            new Benchmark(
                [110.0, 90.0],
                [0.3, 0.2],
                [0.03, 0.02],
                0.05,
                [M([0.9]), M([0.7]), M([0.5]), M([0.3]), M([0.1]), M([-0.1]), M([-0.3]), M([-0.5]), M([-0.7]), M([-0.9])],
                [1.0, -1.0],
                [1.0],
                [20.0],
                OptionType.Put,
                [7.40655995328727013, 9.57965665828979596, 11.3257533225283407, 12.8253504634935833, 14.1588257209597579, 15.3703653131104065, 16.4873731315140475, 17.5282444792535337, 18.5060419239761167, 19.4304406116564437]),
            new Benchmark(
                [100.0, 100.0, 100.0, 100.0],
                [0.4, 0.4, 0.4, 0.4, 0.4],
                [0.0, 0.0, 0.0, 0.0, 0.0],
                0.0,
                [M([0.5])],
                [0.25, 0.25, 0.25, 0.25],
                [5.0],
                [50.0, 60.0, 70.0, 80.0, 90.0, 100.0, 110.0, 120.0, 130.0, 140.0, 150.0],
                OptionType.Call,
                [54.3101760503818554, 47.4811264983728805, 41.5225192321721579, 36.3517843455707421, 31.8768031971830865, 28.0073695445039341, 24.6605295130931736, 21.7625788671709337, 19.2493294434234272, 17.0655419939919533, 15.1640102889333352]),
            new Benchmark(
                [100.0, 100.0, 100.0, 100.0],
                [0.4, 0.4, 0.4, 0.4],
                [0.0, 0.0, 0.0, 0.0],
                0.0,
                [M([-0.1]), M([0.1]), M([0.3]), M([0.5]), M([0.8]), M([0.95])],
                [0.25, 0.25, 0.25, 0.25],
                [5.0],
                [100.0],
                OptionType.Call,
                [17.756916333753729, 21.6920964834602223, 25.029299237118412, 28.0073695445038631, 32.0412264523680363, 33.9186874338078042]),
            new Benchmark(
                [100.0, 100.0, 100.0, 100.0],
                [0.05, 0.05, 0.05, 1.0],
                [0.0, 0.0, 0.0, 0.0],
                0.0,
                [M([0.5])],
                [0.25, 0.25, 0.25, 0.25],
                [5.0],
                [100.0],
                OptionType.Call,
                [19.4590949762084549]),
            new Benchmark(
                [100.0, 100.0, 100.0, 100.0],
                [0.4, 0.4, 0.4, 1.0],
                [0.0, 0.0, 0.0, 0.0],
                0.0,
                [M([0.5])],
                [0.25, 0.25, 0.25, 0.25],
                [5.0],
                [100.0],
                OptionType.Call,
                [36.048540687480191]),
            new Benchmark(
                [100.0, 100.0, 100.0, 100.0],
                [0.8, 0.8, 0.8, 1.0],
                [0.0, 0.0, 0.0, 0.0],
                0.0,
                [M([0.5])],
                [0.25, 0.25, 0.25, 0.25],
                [5.0],
                [100.0],
                OptionType.Put,
                [56.7772198387342684]),
            new Benchmark(
                [100.0, 100.0, 100.0, 100.0, 100.0, 100.0, 100.0],
                [0.1155, 0.2068, 0.1453, 0.1799, 0.1559, 0.1462, 0.1568],
                [0.0169, 0.0239, 0.0136, 0.0192, 0.0081, 0.0362, 0.0166],
                0.063,
                [M([1.00, 0.35, 0.10, 0.27, 0.04, 0.17, 0.71], [0.35, 1.00, 0.39, 0.27, 0.50, -0.08, 0.15], [0.10, 0.39, 1.00, 0.53, 0.70, -0.23, 0.09], [0.27, 0.27, 0.53, 1.00, 0.46, -0.22, 0.32], [0.04, 0.50, 0.70, 0.46, 1.00, -0.29, 0.13], [0.17, -0.08, -0.23, -0.22, -0.29, 1.00, -0.03], [0.71, 0.15, 0.09, 0.32, 0.13, -0.03, 1.00])],
                [0.10, 0.15, 0.15, 0.05, 0.20, 0.10, 0.25],
                [0.5, 1.0, 2.0, 3.0],
                [80.0, 100.0, 120.0],
                OptionType.Call,
                [21.6065524428379092, 3.88986167789384707, 0.0238386363683683114, 23.1411626921050093, 6.2216810431377656, 0.353558402011174056, 26.0424328294544232, 10.2156011934593263, 2.05700439027528237, 28.6992602369071967, 13.7425580125613358, 4.45783894060629216]),
            new Benchmark(
                [150.0, 60.0, 50.0],
                [0.3, 0.3, 0.3],
                [0.0, 0.0, 0.0],
                0.05,
                [M([1.0, 0.2, 0.8], [0.2, 1.0, 0.4], [0.8, 0.4, 1.0])],
                [1.0, -1.0, -1.0],
                [0.25],
                [30.0, 35.0, 40.0, 45.0, 50.0],
                OptionType.Call,
                [13.5670355467464869, 10.3469714924350296, 7.65022045034505815, 5.48080150445291903, 3.80525160380840344]),
            new Benchmark(
                [150.0, 60.0, 50.0],
                [0.6, 0.6, 0.6],
                [0.0, 0.0, 0.0],
                0.05,
                [M([1.0, 0.2, 0.8], [0.2, 1.0, 0.4], [0.8, 0.4, 1.0])],
                [1.0, -1.0, -1.0],
                [0.25],
                [30.0, 35.0, 40.0, 45.0, 50.0],
                OptionType.Call,
                [20.187167856927644, 17.4567855185085179, 15.0073026904179034, 12.8307539528848373, 10.9140154840369128]),
            new Benchmark(
                [10.0, 10.0, 10.0, 10.0, 10.0, 10.0, 10.0, 10.0, 10.0, 10.0, 10.0],
                [0.3, 0.3, 0.3, 0.3, 0.3, 0.3, 0.3, 0.3, 0.3, 0.3, 0.3],
                [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0],
                0.05,
                [M([0.4])],
                [11.0, -1.0, -1.0, -1.0, -1.0, -1.0, -1.0, -1.0, -1.0, -1.0, -1.0],
                [0.25],
                [0.0, 5.0, 10.0, 15.0, 20.0],
                OptionType.Call,
                [11.5795246248372834, 8.11486124233140238, 5.36890684802773066, 3.35146299782513601, 1.97711593318812251]),
            new Benchmark(
                [80.0, 120.0, 100.0, 100.0],
                [0.3, 0.4, 0.2, 0.35],
                [0.01, 0.03, 0.07, 0.04],
                0.03,
                [M([1.0, 0.5, 0.35, 0.35], [0.5, 1.0, 0.5, 0.6], [0.35, 0.5, 1.0, -0.1], [0.35, 0.6, -0.1, 1.0])],
                [2.0, 1.0, -1.0, -1.5],
                [1.5],
                [-10.0, -5.0, 5.0, 10.0, 15.0, 20.0, 25.0, 30.0, 35.0, 40.0, 45.0],
                OptionType.Put,
                [8.261706095014931, 9.48942603546257, 12.36147376566713, 14.01364513745725, 15.81293112893055, 17.75999586876829, 19.85432452565376, 22.09433488973327, 24.47750315548787, 27.00049629189819, 29.65930486322155]),
        ];

        DayCounter dc = new Actual365Fixed();
        var today = new Date(26, Month.September, 2024);

        (string Name, BenchmarkEngine Engine, Real ExpectedRmse)[] engines =
        [
            ("Choi", new((o, c, p, rho) => ChoiBasketEngine.Calculate(o, c, p, rho, 20, 2 << 12)), 0.000182177),
            ("Choi Control Variate",
                new((o, c, p, rho) => ChoiBasketEngine.Calculate(o, c, p, rho, 20, 2 << 12, true, true)),
                0.000228738),
            ("Deng-Li-Zhou", new((o, c, p, rho) => DengLiZhouBasketEngine.Calculate(o, c, p, rho), IsDengLiZhou: true), 0.0629703),
            ("Pearson",
                new((o, c, p, rho) => PearsonSpreadEngine.Calculate(o, c, p[0], p[1], rho[0, 1]), IsSpread: true),
                0.0005),
            ("Kirk", new((o, c, p, rho) => KirkEngine.Calculate(o, c, p[0], p[1], rho[0, 1]), IsSpread: true), 0.030673),
            ("Quasi-Monte-Carlo", new((o, c, p, rho) => QmcEngine(p, rho)(o, c)), 0.28862),
            ("Bjerksund-Stensland",
                new((o, c, p, rho) => BjerksundStenslandSpreadEngine.Calculate(o, c, p[0], p[1], rho[0, 1]), IsSpread: true),
                0.0222423),
            ("Operator Splitting first order",
                new(
                    (o, c, p, rho) => OperatorSplittingSpreadEngine.Calculate(
                        o, c, p[0], p[1], rho[0, 1], OperatorSplittingSpreadEngineOrder.First),
                    IsSpread: true),
                0.00406318),
            ("Operator Splitting second order",
                new(
                    (o, c, p, rho) => OperatorSplittingSpreadEngine.Calculate(
                        o, c, p[0], p[1], rho[0, 1], OperatorSplittingSpreadEngineOrder.Second),
                    IsSpread: true),
                0.000317259),
        ];

        foreach ((string name, BenchmarkEngine engine, Real expectedRmse) in engines)
        {
            var diff = new List<Real>();
            var relDiff = new List<Real>();
            foreach (Benchmark b in benchmarks)
            {
                var calculated = new List<Real>();
                int n = b.Underlyings.Length;

                YieldTermStructure rTS = FlatRate(today, b.R, dc);
                var processes = ImmutableArray.CreateBuilder<GeneralizedBlackScholesProcess>(n);
                for (int i = 0; i < n; ++i)
                {
                    processes.Add(new BlackScholesMertonProcess(
                        b.Underlyings[i], FlatRate(today, b.Q[i], dc), rTS, FlatVol(today, b.Volatilities[i], dc)));
                }

                foreach (Matrix cor in b.Rhos)
                {
                    var rho = new Matrix(n, n);
                    if (cor.Rows == n && cor.Columns == n)
                    {
                        rho = cor;
                    }
                    else
                    {
                        for (int i = 0; i < n; ++i)
                        {
                            for (int j = 0; j < n; ++j)
                            {
                                rho[i, j] = (i == j) ? 1.0 : cor[0, 0];
                            }
                        }
                    }

                    foreach (Real t in b.Maturities)
                    {
                        Date maturityDate = YearFractionToDateFunctions.RoundToDayStart(
                            YearFractionToDateFunctions.YearFractionToDate(dc, today, t));
                        var exercise = new EuropeanExercise(maturityDate);

                        if (engine.IsSpread && !b.Weights.SequenceEqual([1.0, -1.0]))
                        {
                            // benchmark not suitable for a two asset spread engine
                            continue;
                        }

                        if (engine.IsDengLiZhou && (!b.Weights.Any(x => x < 0.0) || !b.Weights.Any(x => x > 0.0)))
                        {
                            continue;
                        }

                        foreach (Real k in b.Strikes)
                        {
                            var payoff = new PlainVanillaPayoff(b.OptionType, k);
                            BasketPayoff basketPayoff = engine.IsSpread
                                ? new SpreadBasketPayoff(payoff)
                                : new AverageBasketPayoff(payoff, [.. b.Weights]);

                            var option = new BasketOption(basketPayoff, exercise);
                            calculated.Add(engine.Price(option, Context, processes.ToImmutable(), rho).NPV!.Value);
                        }
                    }
                }

                for (int i = 0; i < calculated.Count; ++i)
                {
                    diff.Add(b.ReferenceNPVs[i] - calculated[i]);
                    relDiff.Add(diff[^1] / b.ReferenceNPVs[i]);
                }
            }

            if (diff.Count != 0)
            {
                var d = new Vector([.. diff]);
                Real calculatedRmse = Math.Sqrt(ArrayFunctions.DotProduct(d, d) / diff.Count);
                const Real relTol = 5;
                Assert.True(
                    calculatedRmse / expectedRmse <= relTol,
                    "failed to reproduce basket- and spread-option benchmark prices"
                    + $"\n    Engine         : {name}\n    expected RMSE  : {expectedRmse}"
                    + $"\n    calculated RMSE: {calculatedRmse}");
            }
        }
    }

    // QuantLib's MakeMCEuropeanBasketEngine<SobolBrownianBridgeRsgType> with one step, 4,095 samples and seed 12345.
    private static Func<BasketOption, EvaluationContext, MultiAssetOptionResults> QmcEngine(
        ImmutableArray<GeneralizedBlackScholesProcess> p, Matrix rho) =>
        new MakeMCEuropeanBasketEngine<SobolBrownianBridgeRsgType, GeneralStatistics>(
            new StochasticProcessArray([.. p], rho), 12345)
        .WithSteps(1)
        .WithSamples(4096 - 1)
        .ToEngine();

    private static string F16(double x) => x.ToString("F16", CultureInfo.InvariantCulture);

    private static void AssertStdev(IncrementalStatistics stat, Real tolerance, string what) =>
        Assert.True(
            stat.StandardDeviation() <= tolerance,
            $"{what}\n    stdev     : {F5(stat.StandardDeviation())}\n    tolerance : {F5(tolerance)}");

    private readonly record struct StrangData(Real T, Real K, Real Vol1, Real Rho, Real KirkNPV, Real Strang1, Real Strang2);

    // A matrix of rows.
    private static Matrix M(params double[][] rows)
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

    private sealed record Benchmark(
        Real[] Underlyings,
        Volatility[] Volatilities,
        Rate[] Q,
        Rate R,
        Matrix[] Rhos,
        Real[] Weights,
        Real[] Maturities,
        Real[] Strikes,
        OptionType OptionType,
        Real[] ReferenceNPVs);

    // A benchmark engine: QuantLib's factory, and its dynamic casts to a spread engine and to Deng-Li-Zhou's.
    private sealed record BenchmarkEngine(
        Func<BasketOption, EvaluationContext, ImmutableArray<GeneralizedBlackScholesProcess>, Matrix, MultiAssetOptionResults> Price,
        bool IsSpread = false,
        bool IsDengLiZhou = false);

    // The test's SobolBrownianBridgeRsgType: the Sobol Brownian-bridge sequence of the dimension's factors over one step.
    private sealed class SobolBrownianBridgeRsgType : IRngTraits
    {
        public static bool AllowsErrorEstimate => false;

        public static IRsg MakeSequenceGenerator(int dimension, long seed) => new SobolBrownianBridgeRsg(
            dimension, 1, SobolBrownianGeneratorBaseOrdering.Diagonal, seed, SobolRsgDirectionIntegers.JoeKuoD7);
    }


    private static readonly BasketOptionOneData[] OneDataValues =
    [
        // type, strike,   spot,    q,    r,    t,  vol,   value, tol
        new BasketOptionOneData(OptionType.Put, 100.00, 80.00, 0.0, 0.06, 0.5, 0.4, 21.6059, 1e-2),
        new BasketOptionOneData(OptionType.Put, 100.00, 85.00, 0.0, 0.06, 0.5, 0.4, 18.0374, 1e-2),
        new BasketOptionOneData(OptionType.Put, 100.00, 90.00, 0.0, 0.06, 0.5, 0.4, 14.9187, 1e-2),
        new BasketOptionOneData(OptionType.Put, 100.00, 95.00, 0.0, 0.06, 0.5, 0.4, 12.2314, 1e-2),
        new BasketOptionOneData(OptionType.Put, 100.00, 100.00, 0.0, 0.06, 0.5, 0.4, 9.9458, 1e-2),
        new BasketOptionOneData(OptionType.Put, 100.00, 105.00, 0.0, 0.06, 0.5, 0.4, 8.0281, 1e-2),
        new BasketOptionOneData(OptionType.Put, 100.00, 110.00, 0.0, 0.06, 0.5, 0.4, 6.4352, 1e-2),
        new BasketOptionOneData(OptionType.Put, 100.00, 115.00, 0.0, 0.06, 0.5, 0.4, 5.1265, 1e-2),
        new BasketOptionOneData(OptionType.Put, 100.00, 120.00, 0.0, 0.06, 0.5, 0.4, 4.0611, 1e-2),
        // Longstaff Schwartz 1D example
        // use constant and three Laguerre polynomials
        // 100,000 paths and 50 timesteps per year
        new BasketOptionOneData(OptionType.Put, 40.00, 36.00, 0.0, 0.06, 1.0, 0.2, 4.478, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 36.00, 0.0, 0.06, 2.0, 0.2, 4.840, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 36.00, 0.0, 0.06, 1.0, 0.4, 7.101, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 36.00, 0.0, 0.06, 2.0, 0.4, 8.508, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 38.00, 0.0, 0.06, 1.0, 0.2, 3.250, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 38.00, 0.0, 0.06, 2.0, 0.2, 3.745, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 38.00, 0.0, 0.06, 1.0, 0.4, 6.148, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 38.00, 0.0, 0.06, 2.0, 0.4, 7.670, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 40.00, 0.0, 0.06, 1.0, 0.2, 2.314, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 40.00, 0.0, 0.06, 2.0, 0.2, 2.885, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 40.00, 0.0, 0.06, 1.0, 0.4, 5.312, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 40.00, 0.0, 0.06, 2.0, 0.4, 6.920, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 42.00, 0.0, 0.06, 1.0, 0.2, 1.617, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 42.00, 0.0, 0.06, 2.0, 0.2, 2.212, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 42.00, 0.0, 0.06, 1.0, 0.4, 4.582, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 42.00, 0.0, 0.06, 2.0, 0.4, 6.248, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 44.00, 0.0, 0.06, 1.0, 0.2, 1.110, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 44.00, 0.0, 0.06, 2.0, 0.2, 1.690, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 44.00, 0.0, 0.06, 1.0, 0.4, 3.948, 1e-2),
        new BasketOptionOneData(OptionType.Put, 40.00, 44.00, 0.0, 0.06, 2.0, 0.4, 5.647, 1e-2),
    ];

    // The test suite's relativeError: the difference relative to the reference, or absolute for a zero reference.
    private static Real RelativeError(Real x1, Real x2, Real reference) =>
        reference != 0.0 ? Math.Abs(x1 - x2) / reference : Math.Abs(x1 - x2);

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static Integer TimeToDays(double t, Integer daysPerYear = 360) =>
        (Integer)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    // QuantLib's REPORT_FAILURE_2.
    private static string Failure(
        string greekName, BasketOptionTwoData value, Real expected, Real calculated, Real error, Real tolerance) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"European {value.Type} option on {value.BasketType}: s1 {value.S1}, s2 {value.S2}, strike {value.Strike}, "
            + $"q1 {value.Q1}, q2 {value.Q2}, r {value.R}, t {value.T}, v1 {value.V1}, v2 {value.V2}, rho {value.Rho}\n"
            + $"    expected   {greekName}: {expected}\n    calculated {greekName}: {calculated}\n"
            + $"    error:            {error}\n    tolerance:        {tolerance}");

    // QuantLib's REPORT_FAILURE_3.
    private static string Failure(
        string greekName, BasketOptionThreeData value, Real expected, Real calculated, Real error, Real tolerance) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{value.Type} option on {value.BasketType}: s1 {value.S1}, s2 {value.S2}, s3 {value.S3}, "
            + $"strike {value.Strike}, r {value.R}, t {value.T}, v1 {value.V1}, v2 {value.V2}, v3 {value.V3}, "
            + $"rho {value.Rho}\n    expected   {greekName}: {expected}\n    calculated {greekName}: {calculated}\n"
            + $"    error:            {error}\n    tolerance:        {tolerance}");

    private readonly record struct BasketOptionOneData(
        OptionType Type, Real Strike, Real S, Rate Q, Rate R, double T, Volatility V, Real Result, Real Tol);

    private readonly record struct BasketOptionTwoData(
        BasketType BasketType,
        OptionType Type,
        Real Strike,
        Real S1,
        Real S2,
        Rate Q1,
        Rate Q2,
        Rate R,
        double T,
        Volatility V1,
        Volatility V2,
        Real Rho,
        Real Result,
        Real Tol);

    // QuantLib's BasketOptionThreeData: t in months.
    private readonly record struct BasketOptionThreeData(
        BasketType BasketType,
        OptionType Type,
        Real Strike,
        Real S1,
        Real S2,
        Real S3,
        Rate R,
        double T,
        Volatility V1,
        Volatility V2,
        Volatility V3,
        Real Rho,
        Real EuroValue,
        Real AmValue);

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
