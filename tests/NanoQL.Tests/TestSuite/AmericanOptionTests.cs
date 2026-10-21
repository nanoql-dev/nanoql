// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/americanoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testBaroneAdesiWhaleyValues, testBaroneAdesiWhaleyValuesAtNonPositiveRate, testBjerksundStenslandValues, testJuValues, testJuValuesAtZeroRate, testBaroneAdesiWhaleyAndJuAtLowVolatility, testJuNotBelowExerciseValue, testBjerksundStenslandAtUnitBeta, testQdPlusBoundaryValues, testAndersenLakeHighPrecisionExample, testQdEngineStandardExample, testBulkQdFpAmericanEngine, testQdEngineWithLobattoIntegral, testQdEnginesNotBelowExerciseValue, testQdNegativeDividendYield, testBjerksundStenslandEuropeanGreeks, testBjerksundStenslandAmericanGreeks, testSingleBjerksundStenslandGreeks, testBaroneAdesiWhaleyNegativeRates, testQdFpIterationScheme (B4.5's analytic and QD engines); testFdShoutGreeks, testFDShoutNPV, testZeroVolFDShoutNPV, testLargeDividendShoutNPV (B4.3's shout engine); testQdPlusBoundaryConvergence, testQdAmericanEngines (B4.7's Halley solver)

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class AmericanOptionTests
{
    // The data below are from
    // An Approximate Formula for Pricing American Options
    // Journal of Derivatives Winter 1999
    // Ju, N.
    private static readonly AmericanOptionData[] JuValues =
    [
        //        type, strike,   spot,    q,    r,    t,     vol,   value, tol
        // These values are from Exhibit 3 - Short dated Put Options
        new(OptionType.Put, 35.00, 40.00, 0.0, 0.0488, 0.0833, 0.2, 0.006),
        new(OptionType.Put, 35.00, 40.00, 0.0, 0.0488, 0.3333, 0.2, 0.201),
        new(OptionType.Put, 35.00, 40.00, 0.0, 0.0488, 0.5833, 0.2, 0.433),

        new(OptionType.Put, 40.00, 40.00, 0.0, 0.0488, 0.0833, 0.2, 0.851),
        new(OptionType.Put, 40.00, 40.00, 0.0, 0.0488, 0.3333, 0.2, 1.576),
        new(OptionType.Put, 40.00, 40.00, 0.0, 0.0488, 0.5833, 0.2, 1.984),

        new(OptionType.Put, 45.00, 40.00, 0.0, 0.0488, 0.0833, 0.2, 5.000),
        new(OptionType.Put, 45.00, 40.00, 0.0, 0.0488, 0.3333, 0.2, 5.084),
        new(OptionType.Put, 45.00, 40.00, 0.0, 0.0488, 0.5833, 0.2, 5.260),

        new(OptionType.Put, 35.00, 40.00, 0.0, 0.0488, 0.0833, 0.3, 0.078),
        new(OptionType.Put, 35.00, 40.00, 0.0, 0.0488, 0.3333, 0.3, 0.697),
        new(OptionType.Put, 35.00, 40.00, 0.0, 0.0488, 0.5833, 0.3, 1.218),

        new(OptionType.Put, 40.00, 40.00, 0.0, 0.0488, 0.0833, 0.3, 1.309),
        new(OptionType.Put, 40.00, 40.00, 0.0, 0.0488, 0.3333, 0.3, 2.477),
        new(OptionType.Put, 40.00, 40.00, 0.0, 0.0488, 0.5833, 0.3, 3.161),

        new(OptionType.Put, 45.00, 40.00, 0.0, 0.0488, 0.0833, 0.3, 5.059),
        new(OptionType.Put, 45.00, 40.00, 0.0, 0.0488, 0.3333, 0.3, 5.699),
        new(OptionType.Put, 45.00, 40.00, 0.0, 0.0488, 0.5833, 0.3, 6.231),

        new(OptionType.Put, 35.00, 40.00, 0.0, 0.0488, 0.0833, 0.4, 0.247),
        new(OptionType.Put, 35.00, 40.00, 0.0, 0.0488, 0.3333, 0.4, 1.344),
        new(OptionType.Put, 35.00, 40.00, 0.0, 0.0488, 0.5833, 0.4, 2.150),

        new(OptionType.Put, 40.00, 40.00, 0.0, 0.0488, 0.0833, 0.4, 1.767),
        new(OptionType.Put, 40.00, 40.00, 0.0, 0.0488, 0.3333, 0.4, 3.381),
        new(OptionType.Put, 40.00, 40.00, 0.0, 0.0488, 0.5833, 0.4, 4.342),

        new(OptionType.Put, 45.00, 40.00, 0.0, 0.0488, 0.0833, 0.4, 5.288),
        new(OptionType.Put, 45.00, 40.00, 0.0, 0.0488, 0.3333, 0.4, 6.501),
        new(OptionType.Put, 45.00, 40.00, 0.0, 0.0488, 0.5833, 0.4, 7.367),

        // Type in Exhibits 4 and 5 if you have some spare time ;-)

        //        type, strike,   spot,    q,    r,    t,     vol,   value, tol
        // values from Exhibit 6 - Long dated Call Options with dividends
        new(OptionType.Call, 100.00, 80.00, 0.07, 0.03, 3.0, 0.2, 2.605),
        new(OptionType.Call, 100.00, 90.00, 0.07, 0.03, 3.0, 0.2, 5.182),
        new(OptionType.Call, 100.00, 100.00, 0.07, 0.03, 3.0, 0.2, 9.065),
        new(OptionType.Call, 100.00, 110.00, 0.07, 0.03, 3.0, 0.2, 14.430),
        new(OptionType.Call, 100.00, 120.00, 0.07, 0.03, 3.0, 0.2, 21.398),

        new(OptionType.Call, 100.00, 80.00, 0.07, 0.03, 3.0, 0.4, 11.336),
        new(OptionType.Call, 100.00, 90.00, 0.07, 0.03, 3.0, 0.4, 15.711),
        new(OptionType.Call, 100.00, 100.00, 0.07, 0.03, 3.0, 0.4, 20.760),
        new(OptionType.Call, 100.00, 110.00, 0.07, 0.03, 3.0, 0.4, 26.440),
        new(OptionType.Call, 100.00, 120.00, 0.07, 0.03, 3.0, 0.4, 32.709),

        new(OptionType.Call, 100.00, 80.00, 0.07, 0.00001, 3.0, 0.3, 5.552),
        new(OptionType.Call, 100.00, 90.00, 0.07, 0.00001, 3.0, 0.3, 8.868),
        new(OptionType.Call, 100.00, 100.00, 0.07, 0.00001, 3.0, 0.3, 13.158),
        new(OptionType.Call, 100.00, 110.00, 0.07, 0.00001, 3.0, 0.3, 18.458),
        new(OptionType.Call, 100.00, 120.00, 0.07, 0.00001, 3.0, 0.3, 24.786),

        new(OptionType.Call, 100.00, 80.00, 0.03, 0.07, 3.0, 0.3, 12.177),
        new(OptionType.Call, 100.00, 90.00, 0.03, 0.07, 3.0, 0.3, 17.411),
        new(OptionType.Call, 100.00, 100.00, 0.03, 0.07, 3.0, 0.3, 23.402),
        new(OptionType.Call, 100.00, 110.00, 0.03, 0.07, 3.0, 0.3, 30.028),
        new(OptionType.Call, 100.00, 120.00, 0.03, 0.07, 3.0, 0.3, 37.177),
    ];

    // Adapted: the quotes changed in place become a process built for each row.
    [Fact]
    public void TestFdValues()
    {
        // Testing finite-difference and QR+ engine for American options...

        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual360();

        Real tolerance = 8.0e-2;

        foreach (AmericanOptionData juValue in JuValues)
        {
            StrikedTypePayoff payoff = new PlainVanillaPayoff(juValue.Type, juValue.Strike);

            Date exDate = today + TimeToDays(juValue.T);
            Exercise exercise = new AmericanExercise(today, exDate);

            GeneralizedBlackScholesProcess stochProcess = new BlackScholesMertonProcess(
                juValue.S,
                new FlatForward(today, juValue.Q, dc),
                new FlatForward(today, juValue.R, dc),
                new BlackConstantVol(today, new NullCalendar(), juValue.V, dc));

            var option = new VanillaOption(payoff, exercise);

            Real pdeCalculated = FdBlackScholesVanillaEngine.Calculate(
                option, context, stochProcess, tGrid: 100, xGrid: 400).NPV!.Value;
            Real error = Math.Abs(pdeCalculated - juValue.Result);
            if (error > tolerance)
            {
                Assert.Fail(
                    $"American {payoff.OptionType} option: spot {juValue.S}, strike {payoff.Strike}, q {juValue.Q}, "
                    + $"r {juValue.R}, vol {juValue.V}: expected value {juValue.Result}, calculated {pdeCalculated}, "
                    + $"error {error}, tolerance {tolerance}");
            }

            Real qrPlusCalculated = FdBlackScholesVanillaEngine.Calculate(option, context, stochProcess).NPV!.Value;
            if (Math.Abs(pdeCalculated - qrPlusCalculated) > 2e-2)
            {
                Assert.Fail(
                    $"QR+ boundary approximation failed to reproduce PDE value for {juValue.Type}, spot {juValue.S}, "
                    + $"strike {juValue.Strike}, r {juValue.R}, q {juValue.Q}, vol {juValue.V}: "
                    + $"PDE value {pdeCalculated}, QR+ value {qrPlusCalculated}");
            }
        }
    }

    [Fact]
    public void TestFdAmericanGreeks()
    {
        // Testing finite-differences American option greeks...
        TestFdGreeks();
    }

    // Adapted: the quotes changed in place become a process built for each value.
    [Fact]
    public void TestEscrowedVsSpotAmericanOption()
    {
        // Testing escrowed vs spot dividend model for American options...

        DayCounter dc = new Actual360();
        var today = new Date(27, Month.February, 2021);
        var context = new EvaluationContext { EvaluationDate = today };

        GeneralizedBlackScholesProcess Process(Volatility vol) => new BlackScholesMertonProcess(
            100,
            FlatRate(0.08, dc, context),
            FlatRate(0.04, dc, context),
            FlatVol(vol, dc, context));

        Date maturityDate = today + new Period(12, TimeUnit.Months);
        Date[] dividendDates = [today + new Period(10, TimeUnit.Months)];
        Real[] dividendAmounts = [10.0];
        ImmutableArray<Dividend> dividends = DividendFunctions.DividendVector(dividendDates, dividendAmounts);

        Real strike = 100.0;
        var option = new VanillaOption(
            new PlainVanillaPayoff(OptionType.Call, strike),
            new AmericanExercise(today, maturityDate));

        OneAssetOptionResults spot = FdBlackScholesVanillaEngine.Calculate(
            option, context, Process(0.3), dividends, tGrid: 100, xGrid: 400);

        Real spotNpv = spot.NPV!.Value;
        Real spotDelta = spot.Delta!.Value;

        OneAssetOptionResults escrowed = new MakeFdBlackScholesVanillaEngine(Process(100 / 90.0 * 0.3))
            .WithTGrid(100)
            .WithXGrid(400)
            .WithCashDividends(dividendDates, dividendAmounts)
            .WithCashDividendModel(FdBlackScholesVanillaEngineCashDividendModel.Escrowed)
            .ToEngine()(option, context);

        Real escrowedNpv = escrowed.NPV!.Value;
        Real escrowedDelta = escrowed.Delta!.Value;

        Real diffNpv = Math.Abs(escrowedNpv - spotNpv);
        Real tol = 1e-2;

        if (diffNpv > tol)
        {
            Assert.Fail(
                "failed to compare American option NPV with escrowed and spot dividend model "
                + $"\n    escrowed div: {escrowedNpv}\n    spot div    : {spotNpv}\n    difference: {diffNpv}"
                + $"\n    tolerance:  {tol}");
        }

        Real diffDelta = Math.Abs(escrowedDelta - spotDelta);

        if (diffDelta > tol)
        {
            Assert.Fail(
                "failed to compare American option Delta with escrowed and spot dividend model "
                + $"\n    escrowed div: {escrowedDelta}\n    spot div    : {spotDelta}\n    difference: {diffDelta}"
                + $"\n    tolerance:  {tol}");
        }
    }

    // Adapted: the quotes changed in place become a process built for each value; QuantLib's theta() throws on its
    // null theta, which is a null result here.
    [Fact]
    public void TestTodayIsDividendDate()
    {
        // Testing escrowed vs spot dividend model on dividend dates for American options...

        DayCounter dc = new Actual360();
        var today = new Date(27, Month.February, 2021);
        var context = new EvaluationContext { EvaluationDate = today };

        GeneralizedBlackScholesProcess Process(Volatility vol) => new BlackScholesMertonProcess(
            100,
            FlatRate(0.05, dc, context),
            FlatRate(0.07, dc, context),
            FlatVol(vol, dc, context));

        Date maturityDate = today + new Period(12, TimeUnit.Months);
        Date[] dividendDates = [today, today + new Period(11, TimeUnit.Months)];
        Real[] dividendAmounts = [5.0, 5.0];

        Func<VanillaOption, EvaluationContext, OneAssetOptionResults> Engine(
            Volatility vol, FdBlackScholesVanillaEngineCashDividendModel model) =>
            new MakeFdBlackScholesVanillaEngine(Process(vol))
                .WithTGrid(100)
                .WithXGrid(400)
                .WithCashDividends(dividendDates, dividendAmounts)
                .WithCashDividendModel(model)
                .ToEngine();

        Real strike = 90.0;
        var option = new VanillaOption(
            new PlainVanillaPayoff(OptionType.Put, strike),
            new AmericanExercise(today, maturityDate));

        OneAssetOptionResults spot = Engine(0.3, FdBlackScholesVanillaEngineCashDividendModel.Spot)(option, context);

        Real spotNpv = spot.NPV!.Value;
        Real spotDelta = spot.Delta!.Value;
        Assert.Null(spot.Theta);

        OneAssetOptionResults escrowed =
            Engine(100 / 95.0 * 0.3, FdBlackScholesVanillaEngineCashDividendModel.Escrowed)(option, context);

        Real escrowedNpv = escrowed.NPV!.Value;
        Real escrowedDelta = escrowed.Delta!.Value;
        Assert.Null(escrowed.Theta);

        Real diffNpv = Math.Abs(escrowedNpv - spotNpv);
        Real tol = 5e-2;

        if (diffNpv > tol)
        {
            Assert.Fail(
                "failed to compare American option NPV with escrowed and spot dividend model "
                + $"\n    escrowed div: {escrowedNpv}\n    spot div    : {spotNpv}\n    difference:   {diffNpv}"
                + $"\n    tolerance:    {tol}");
        }

        Real diffDelta = Math.Abs(escrowedDelta - spotDelta);

        if (diffDelta > tol)
        {
            Assert.Fail(
                "failed to compare American option Delta with escrowed and spot dividend model "
                + $"\n    escrowed div: {escrowedDelta}\n    spot div    : {spotDelta}\n    difference:   {diffDelta}"
                + $"\n    tolerance:    {tol}");
        }

        dividendDates[0] = today + 1;

        spotNpv = Engine(0.3, FdBlackScholesVanillaEngineCashDividendModel.Spot)(option, context).NPV!.Value;

        escrowed = Engine(100 / 95.0 * 0.3, FdBlackScholesVanillaEngineCashDividendModel.Escrowed)(option, context);
        escrowedNpv = escrowed.NPV!.Value;
        Assert.NotNull(escrowed.Theta);

        diffNpv = Math.Abs(escrowedNpv - spotNpv);

        if (diffNpv > tol)
        {
            Assert.Fail(
                "failed to compare American option NPV with escrowed and spot dividend model "
                + $"\n    escrowed div: {escrowedNpv}\n    spot div    : {spotNpv}\n    difference:   {diffNpv}"
                + $"\n    tolerance:    {tol}");
        }
    }

    [Fact]
    public void TestCallPutParity()
    {
        // Testing call/put parity for American options...

        // R.L. McDonald, M.D. Schroder: A parity result for American option

        DayCounter dc = new Actual365Fixed();
        var today = new Date(8, Month.April, 2022);
        var context = new EvaluationContext { EvaluationDate = today };

        GeneralizedBlackScholesProcess BuildStochProcess(OptionSpec testCase) => new BlackScholesMertonProcess(
            testCase.Spot,
            FlatRate(testCase.Q, dc, context),
            FlatRate(testCase.R, dc, context),
            FlatVol(testCase.Volatility, dc, context));

        OptionSpec[] testCaseSpecs =
        [
            new(100.0, 100.0, 365, 0.5, 0.15, 0.02),
            new(100.0, 90.0, 365, 0.5, 0.15, 0.02),
            new(100.0, 125.0, 730, 0.4, 0.15, 0.05),
            new(100.0, 125.0, 730, 0.4, 0.06, 0.05),
        ];

        Size xGrid = 400;
        Size timeStepsPerYear = 50;

        foreach (OptionSpec testCaseSpec in testCaseSpecs)
        {
            Date maturityDate = today + new Period(testCaseSpec.MaturityInDays, TimeUnit.Days);
            double maturityTime = dc.YearFraction(today, maturityDate);
            Size tGrid = (Size)(maturityTime * timeStepsPerYear);

            Exercise exercise = new AmericanExercise(today, maturityDate);

            var putOption = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, testCaseSpec.Strike), exercise);
            Real putNpv = FdBlackScholesVanillaEngine.Calculate(
                putOption, context, BuildStochProcess(testCaseSpec), tGrid: tGrid, xGrid: xGrid).NPV!.Value;

            var callOptionSpec = new OptionSpec(
                testCaseSpec.Strike,
                testCaseSpec.Spot,
                testCaseSpec.MaturityInDays,
                testCaseSpec.Volatility,
                testCaseSpec.Q,
                testCaseSpec.R);
            var callOption = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, callOptionSpec.Strike), exercise);
            Real callNpv = FdBlackScholesVanillaEngine.Calculate(
                callOption, context, BuildStochProcess(callOptionSpec), tGrid: tGrid, xGrid: xGrid).NPV!.Value;

            Real diff = Math.Abs(putNpv - callNpv);
            Real tol = 0.001;

            if (diff > tol)
            {
                Assert.Fail(
                    "failed to reproduce American call/put parity"
                    + $"\n    Put NPV   : {putNpv}\n    Call NPV  : {callNpv}\n    difference: {diff}\n    tolerance : {tol}");
            }
        }
    }

    [Fact]
    public void TestFdEarliestExerciseDate()
    {
        // Testing that the FD engine respects the earliest date for American exercise...

        // A deep ITM American put where early exercise is valuable.
        // Restricting the exercise window should reduce the price toward
        // the European value.

        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();

        Real s0 = 80.0;
        Real k = 100.0;
        Volatility sigma = 0.25;
        Rate r = 0.05;
        Rate q = 0.0;

        GeneralizedBlackScholesProcess bsmProcess = new BlackScholesMertonProcess(
            s0,
            new FlatForward(today, q, dc),
            new FlatForward(today, r, dc),
            new BlackConstantVol(today, new NullCalendar(), sigma, dc));

        Date maturity = today + new Period(1, TimeUnit.Years);
        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Put, k);

        Real Fd(Exercise exercise) => FdBlackScholesVanillaEngine.Calculate(
            new VanillaOption(payoff, exercise), context, bsmProcess, tGrid: 200, xGrid: 200, dampingSteps: 0).NPV!.Value;

        // Full American exercise
        Real fullPrice = Fd(new AmericanExercise(today, maturity));

        // European benchmark
        Real euroPrice = AnalyticEuropeanEngine.Calculate(
            new VanillaOption(payoff, new EuropeanExercise(maturity)), context, bsmProcess).NPV!.Value;

        Real earlyExPremium = fullPrice - euroPrice;

        // Sanity: the early exercise premium should be significant
        // for this deep ITM put with 5% rates
        Assert.True(earlyExPremium > 1.0);

        // Restricted exercise: only last 3 months
        Date lateStart = maturity - new Period(3, TimeUnit.Months);
        Real latePrice = Fd(new AmericanExercise(lateStart, maturity));

        // The restricted option should be worth less than full American
        Assert.True(
            fullPrice - latePrice > 0.01,
            $"Restricting exercise window should reduce price: full={fullPrice} late={latePrice}");

        // The restricted option should be worth more than European
        // (it still has some early exercise value in the last 3 months)
        Assert.True(
            latePrice > euroPrice + 0.01, $"Restricted American should exceed European: late={latePrice} euro={euroPrice}");

        // Monotonicity: longer exercise window -> higher price
        Date midStart = maturity - new Period(6, TimeUnit.Months);
        Real midPrice = Fd(new AmericanExercise(midStart, maturity));

        Assert.True(midPrice >= latePrice - 1e-8, $"Wider window should give higher price: 6M={midPrice} 3M={latePrice}");
        Assert.True(fullPrice >= midPrice - 1e-8, $"Full window should give highest price: full={fullPrice} 6M={midPrice}");
    }

    // Adapted: the quotes changed in place become a process built for each value; the engine is the
    // finite-difference engine, QuantLib's template argument.
    private static void TestFdGreeks()
    {
        Dictionary<string, Real> calculated = [], expected = [], tolerance = [];
        tolerance["delta"] = 7.0e-4;
        tolerance["gamma"] = 2.0e-4;

        // tolerance["theta"]  = 1.0e-4;
        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] strikes = [50.0, 99.5, 100.0, 100.5, 150.0];
        Real[] underlyings = [100.0];
        Rate[] qRates = [0.04, 0.05, 0.06];
        Rate[] rRates = [0.01, 0.05, 0.15];
        Integer[] years = [1, 2];
        Volatility[] vols = [0.11, 0.50, 1.20];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (OptionType type in types)
        {
            foreach (Real strike in strikes)
            {
                foreach (int year in years)
                {
                    Date exDate = today + (year * TimeUnit.Years);
                    Exercise exercise = new AmericanExercise(today, exDate);
                    StrikedTypePayoff payoff = new PlainVanillaPayoff(type, strike);

                    var option = new VanillaOption(payoff, exercise);

                    foreach (Real u in underlyings)
                    {
                        foreach (Real m in qRates)
                        {
                            foreach (Real n in rRates)
                            {
                                foreach (Real v in vols)
                                {
                                    Rate q = m, r = n;
                                    OneAssetOptionResults results = Price(u);
                                    Real value = results.NPV!.Value;
                                    calculated["delta"] = results.Delta!.Value;
                                    calculated["gamma"] = results.Gamma!.Value;

                                    // calculated["theta"]  = option.theta();
                                    if (value > u * 1.0e-5)
                                    {
                                        // perturb spot and get delta and gamma
                                        Real du = u * 1.0e-4;
                                        OneAssetOptionResults p = Price(u + du);
                                        OneAssetOptionResults mm = Price(u - du);
                                        expected["delta"] = (p.NPV!.Value - mm.NPV!.Value) / (2 * du);
                                        expected["gamma"] = (p.Delta!.Value - mm.Delta!.Value) / (2 * du);

                                        // compare
                                        foreach ((string greek, Real calcl) in calculated)
                                        {
                                            Real expct = expected[greek], tol = tolerance[greek];
                                            Real error = RelativeError(expct, calcl, u);
                                            if (error > tol)
                                            {
                                                Assert.Fail(
                                                    $"American {type} option with plain vanilla payoff: spot {u}, strike "
                                                    + $"{strike}, q {q}, r {r}, maturity {exDate}, vol {v}: expected "
                                                    + $"{greek} {expct}, calculated {calcl}, error {error}, tolerance {tol}");
                                            }
                                        }
                                    }

                                    OneAssetOptionResults Price(Real s) => FdBlackScholesVanillaEngine.Calculate(
                                        option,
                                        context,
                                        new BlackScholesMertonProcess(
                                            s,
                                            FlatRate(q, dc, context),
                                            FlatRate(r, dc, context),
                                            FlatVol(v, dc, context)),
                                        tGrid: 50);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // The test suite's flatRate(forward, dc): a flat forward from the evaluation date.
    private static YieldTermStructure FlatRate(Rate forward, DayCounter dc, EvaluationContext context) =>
        FlatForward.Create(0, new NullCalendar(), forward, dc, context);

    // The test suite's flatVol(vol, dc): a constant volatility from the evaluation date.
    private static BlackVolTermStructure FlatVol(Volatility vol, DayCounter dc, EvaluationContext context) =>
        BlackConstantVol.Create(0, new NullCalendar(), vol, dc, context);

    // The test suite's relativeError: the difference relative to the reference, or absolute for a zero reference.
    private static Real RelativeError(Real x1, Real x2, Real reference) =>
        reference != 0.0 ? Math.Abs(x1 - x2) / reference : Math.Abs(x1 - x2);

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static Integer TimeToDays(double t, Integer daysPerYear = 360) =>
        (Integer)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    private readonly record struct AmericanOptionData(
        OptionType Type, Real Strike, Real S, Rate Q, Rate R, double T, Volatility V, Real Result);

    private readonly record struct OptionSpec(
        Real Spot, Real Strike, Size MaturityInDays, Real Volatility, Real R, Real Q);
}
