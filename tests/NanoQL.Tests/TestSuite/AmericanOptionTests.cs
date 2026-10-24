// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/americanoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testFdShoutGreeks, testFDShoutNPV, testZeroVolFDShoutNPV, testLargeDividendShoutNPV (S-van's shout engine)
// Moved: testBjerksundStenslandAmericanGreeks line 2437 (Divergences/BjerksundStenslandTriggerTests.cs)

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class AmericanOptionTests
{
    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    // testQdAmericanEngines' random cases' reference values, QuantLib's PDE values
    private static readonly double[] QdPdeValues =
    [
        581.46895, 113.78442, 581.44547, 1408.579, 49.19448, 1060.27367, 834.83366, 176.48305, 120.38008, 307.11264,
        602.7006, 233.80171, 204.74596, 0.30987, 0, 0, 5.36215, 0.01711, 0, 84.51193, 0.67131, 0.06414, 152.67188,
        54.75257, 90.31861, 168.50289, 18.38926, 0, 282.4995, 0, 0.08428, 12.30929, 42.26359, 139.87748, 0.28724,
        0.00421, 0, 0.00206, 0, 658.60427, 140.51139, 23.17387, 0.35612, 0, 909.14828, 0, 0.11549, 5.46749, 144.25428,
        2576.6754, 562.16484, 0, 122.725, 383.48463, 278.7447, 3.52566, 82.34348, 81.06139, 0, 10.42824, 4.95917,
        25.28602, 31.38869, 3.53697, 0, 0.012, 0, 0.4263, 162.16184, 0.4618, 97.714, 283.03442, 0.38176, 70.25367,
        134.94142, 2.19293, 226.4746, 76.74309, 46.03123, 15.76214, 0.01666, 1806.26208, 0, 103.93726, 6.82956,
        337.81301, 0.64236, 677.63248, 25.01763, 443.79052, 1793.78327, 118.6293, 185.79849, 11.59313, 679.01736,
        17.99005, 403.57554, 1.67418, 0, 0.03795, 3326.09089, 71.1996, 0, 485.10353, 0, 1681.25166, 0, 43.15432,
        0.75825, 0.05895, 34.71493, 0.00015, 5.58671, 115.98793, 37.7713, 399.24494, 0.00766, 445.42207, 152.65397, 0,
        47.05874, 0.96921, 14.21875, 257.84754, 109.62533, 2553.99295, 138.46663, 192.33614, 81.41877, 18.21403,
        113.926, 27.28409, 174.77093, 42.70527, 0.90326, 0, 967.9901, 616.0143, 253.56442, 0.00397, 2493.82098, 9.29406,
        11.00023, 0, 0, 234.12481, 0, 72.46356, 0, 9.00932, 48.67934, 29.42756, 13.4271, 0, 0, 0, 0, 20.71417, 48.57474,
        2.26452, 0, 109.0243, 0, 21.26801, 1.21164, 0, 86.25232, 36.00437, 4.53844, 7.40503, 313.53602, 379.76105,
        165.84347, 77.19665, 9.02466, 0.10634, 214.84982, 6.13387, 133.44645, 303.25953, 0, 134.26724, 246.89804, 0,
        123.32975, 32.83429, 9.56819, 7.42582, 0, 73.82832, 196.84831, 0.00001, 72.70391, 2173.8649, 123.00513,
        153.83539, 21.63003, 209.84752, 30.12425, 0, 197.6502, 0, 164.02863, 7.65143, 56.57631, 2392.70018, 0, 0,
        34.23457, 171.08459, 0.49387, 31.13395, 237.68801, 0.01262, 0, 0, 0, 0, 41.56635, 0, 8.41535, 55.01775,
        310.50094, 0, 14.85456, 174.34018, 7.19772, 0.00001, 0, 91.70874, 0.00001, 17.51724, 0.00587, 0, 532.24902,
        2.05553, 36.80843, 0, 33.39288, 0.00006, 0.04439, 1.3434, 0, 0.41816, 926.37642, 0, 247.61559, 151.98965,
        0.35243, 4.33198, 23294.47744, 0.00791, 12.51996, 53.47727, 167.95572, 0.0062, 6.8482, 0, 347.83408, 852.85742,
        558.21422, 0, 53.89293, 78.61011, 187.3978, 9.18927, 0.00553, 113.48101, 1467.30556, 74.82251, 94.84476, 0,
        101.3649, 59.27007, 0, 773.81251, 0, 542.7889, 0, 68.96209, 96.0435, 0.00004, 0.10738, 0.00187, 324.97758,
        245.68455, 30.52818, 129.84472, 0, 46.86288, 368.41675, 139.29763, 4.4393, 16.29594, 25.7554, 64.02621,
        89.41363, 0.62751, 219.65237, 0.26039, 0, 12.02172, 101.97733, 69.37456, 45.81122, 1263.33603, 164.31607,
        15.88788, 0, 48.77797, 0.13233, 147.16808, 10.31217, 7.50634, 7.48611, 177.95409, 225.77562, 3.56947, 0.02531,
        4.88869, 8.76632, 0, 0, 0.02214, 305.08468, 44.52185, 182.17332, 538.31458, 0, 46.97229, 0, 31.94202, 410.43038,
        0, 70.35432, 15.58346, 74.14177, 953.67663, 11.79128, 59.83061, 0, 37.86557, 1184.22731, 2411.37823, 0, 0, 0, 0,
        49.3179, 236.38654, 21.36225, 218.048, 517.57006, 0, 0, 12.52933, 256.71967, 0.00025, 1.47981, 158.19166, 0,
        1923.70709, 4.94441, 1199.81196, 45.92353, 85.73255, 14.91338, 88.81459, 21.42459, 3456.9466, 31.97838,
        233.26863, 49.34801, 2684.07758, 0, 0, 32.24149, 0, 111.79552, 0.00506, 8.77602, 0, 406.54213, 0.32974,
        365.53998, 1.49714, 19.65603, 37.33877, 205.06928, 0.01805, 589.23478, 9.58273, 0.02946, 286.48706, 463.34512,
        528.21392, 0, 47.71294, 21.0864, 114.81771, 80.489, 21.30905, 41.95873, 19.03598, 156.09295, 0, 73.6509, 0, 0,
        168.17576, 0, 32.71243, 36.75044, 177.64583, 0.05618, 156.38616, 1370.4754, 24.5976, 59.83173, 0, 354.93074,
        34.96889, 0.00532, 16.95287, 1259.72993, 241.05777, 18.9778, 0.57635, 43.98093, 25.2678, 369.39896, 0.31549, 0,
        31.95512, 101.60559, 11.22079, 970.16273, 0, 0, 1.55445, 0, 18.6067, 0, 1124.20117, 52.67762, 10.38273, 0,
        10.22588, 251.27813, 0, 431.82244, 0, 1.31252, 0, 84.72154, 100.98411, 160.95557, 129.51372, 0.00026, 103.81663,
        421.64767, 0.00031, 0, 104.48529, 162.59225, 0, 1504.0869, 88.11253, 4.14052, 0.07195, 203.78754, 0.00002,
        42.5395, 0, 17.05087, 26.89157, 64.64923, 0, 390.87453, 124.55406, 0.01018, 94.23963,
    ];

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

    // Adapted: the quotes changed in place become a process built for each row.
    [Fact]
    public void TestBaroneAdesiWhaleyValues()
    {
        // Testing Barone-Adesi and Whaley approximation for American options...

        /* The data below are from
           "Option pricing formulas", E.G. Haug, McGraw-Hill 1998
           pag 24

           The following values were replicated only up to the second digit
           by the VB code provided by Haug, which was used as base for the
           C++ implementation

        */
        AmericanOptionData[] values =
        [
            //        type, strike,   spot,    q,    r,    t,  vol,   value
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.10, 0.15, 0.0206),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.10, 0.15, 1.8771),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.10, 0.15, 10.0089),
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.10, 0.25, 0.3159),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.10, 0.25, 3.1280),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.10, 0.25, 10.3919),
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.10, 0.35, 0.9495),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.10, 0.35, 4.3777),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.10, 0.35, 11.1679),
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.50, 0.15, 0.8208),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.50, 0.15, 4.0842),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.50, 0.15, 10.8087),
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.50, 0.25, 2.7437),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.50, 0.25, 6.8015),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.50, 0.25, 13.0170),
            new(OptionType.Call, 100.00, 90.00, 0.10, 0.10, 0.50, 0.35, 5.0063),
            new(OptionType.Call, 100.00, 100.00, 0.10, 0.10, 0.50, 0.35, 9.5106),
            new(OptionType.Call, 100.00, 110.00, 0.10, 0.10, 0.50, 0.35, 15.5689),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.10, 0.15, 10.0000),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.10, 0.15, 1.8770),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.10, 0.15, 0.0410),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.10, 0.25, 10.2533),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.10, 0.25, 3.1277),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.10, 0.25, 0.4562),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.10, 0.35, 10.8787),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.10, 0.35, 4.3777),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.10, 0.35, 1.2402),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.50, 0.15, 10.5595),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.50, 0.15, 4.0842),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.50, 0.15, 1.0822),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.50, 0.25, 12.4419),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.50, 0.25, 6.8014),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.50, 0.25, 3.3226),
            new(OptionType.Put, 100.00, 90.00, 0.10, 0.10, 0.50, 0.35, 14.6945),
            new(OptionType.Put, 100.00, 100.00, 0.10, 0.10, 0.50, 0.35, 9.5104),
            new(OptionType.Put, 100.00, 110.00, 0.10, 0.10, 0.50, 0.35, 5.8823),
            new(OptionType.Put, 100.00, 100.00, 0.00, 0.00, 0.50, 0.15, 4.2294),
        ];

        Real tolerance = 3.0e-3;
        CheckValues(values, tolerance, BaroneAdesiWhaleyApproximationEngine.Calculate);
    }

    [Fact]
    public void TestBaroneAdesiWhaleyValuesAtNonPositiveRate()
    {
        // Testing Barone-Adesi and Whaley approximation for American puts at non-positive risk-free rates...

        // criticalPrice() rejects a discount factor above 1, so these puts were
        // refused.  Holding dominates; the value is European.
        var today = new Date(19, Month.August, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        DayCounter dc = new Actual360();
        Date maturity = today + new Period(1, TimeUnit.Years);
        var american = new AmericanExercise(today, maturity);
        var european = new EuropeanExercise(maturity);

        const Real tolerance = 1.0e-6;

        foreach (Real r in new[] { 0.0, -1.0e-10, -0.05 })
        {
            foreach (Real q in new[] { 0.0, 0.04 })
            {
                foreach (Real s in new[] { 70.0, 85.0, 100.0, 115.0, 130.0 })
                {
                    GeneralizedBlackScholesProcess process = new BlackScholesMertonProcess(
                        s, FlatRate(q, dc, context), FlatRate(r, dc, context), FlatVol(0.30, dc, context));
                    var payoff = new PlainVanillaPayoff(OptionType.Put, 100.0);

                    Real expected = AnalyticEuropeanEngine.Calculate(new VanillaOption(payoff, european), context, process)
                        .NPV!.Value;
                    Real calculated = BaroneAdesiWhaleyApproximationEngine.Calculate(
                        new VanillaOption(payoff, american), context, process).NPV!.Value;

                    Real error = Math.Abs(calculated - expected);
                    Assert.True(error <= tolerance, $"put at r {r}, q {q}, spot {s}: {calculated} against {expected}");
                }
            }
        }
    }

    // Adapted: the quotes changed in place become a process built for each row.
    [Fact]
    public void TestBjerksundStenslandValues()
    {
        // Testing Bjerksund and Stensland approximation for American options...
        AmericanOptionData[] values =
        [
            //      type, strike,   spot,    q,    r,    t,  vol,   value, tol
            // from "Option pricing formulas", Haug, McGraw-Hill 1998, pag 27
            new(OptionType.Call, 40.00, 42.00, 0.08, 0.04, 0.75, 0.35, 5.2704),

            // from "Option pricing formulas", Haug, McGraw-Hill 1998, VBA code
            new(OptionType.Put, 40.00, 36.00, 0.00, 0.06, 1.00, 0.20, 4.4531),

            // ATM option with very small volatility, reference value taken from R
            new(OptionType.Call, 100, 100, 0.05, 0.05, 1.0, 0.0021, 0.08032314),

            // ATM option with very small volatility,
            // reference value taken from Barone-Adesi and Whaley Approximation
            new(OptionType.Call, 100, 100, 0.05, 0.05, 1.0, 0.0001, 0.003860656),
            new(OptionType.Call, 100, 99.99, 0.05, 0.05, 1.0, 0.0001, 0.00081),

            // ITM option with a very small volatility
            new(OptionType.Call, 100, 110, 0.05, 0.05, 1.0, 0.0001, 10.0),
            new(OptionType.Put, 110, 100, 0.05, 0.05, 1.0, 0.0001, 10.0),

            // ATM option with a very large volatility
            new(OptionType.Put, 100, 110, 0.05, 0.05, 1.0, 10, 95.12289),
        ];

        Real tolerance = 5.0e-5;
        CheckValues(values, tolerance, BjerksundStenslandApproximationEngine.Calculate);
    }

    // Adapted: the quotes changed in place become a process built for each row.
    [Fact]
    public void TestJuValues()
    {
        // Testing Ju approximation for American options...
        Real tolerance = 1.0e-3;
        CheckValues(JuValues, tolerance, JuQuadraticApproximationEngine.Calculate);
    }

    [Fact]
    public void TestJuValuesAtZeroRate()
    {
        // Testing Ju approximation for American options at zero risk-free rate...

        // The approximation used to return NaN when the risk-free rate was
        // exactly 0; see <https://github.com/lballabio/QuantLib/issues/2737>.
        // The results are compared against those for a negligible but non-null
        // rate, of which they are the limit.
        var today = new Date(19, Month.August, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        DayCounter dc = new Actual360();
        var exercise = new AmericanExercise(today, today + new Period(1, TimeUnit.Years));

        const Real tolerance = 1.0e-6;

        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            foreach (Real q in new[] { 0.04, 0.0 })
            {
                foreach (Real s in new[] { 70.0, 85.0, 100.0, 115.0, 130.0 })
                {
                    var option = new VanillaOption(new PlainVanillaPayoff(type, 100.0), exercise);
                    Real Price(Rate r) => JuQuadraticApproximationEngine.Calculate(
                        option,
                        context,
                        new BlackScholesMertonProcess(
                            s, FlatRate(q, dc, context), FlatRate(r, dc, context), FlatVol(0.30, dc, context))).NPV!.Value;

                    Real calculated = Price(0.0);
                    Real expected = Price(1.0e-10);
                    Real error = Math.Abs(calculated - expected);
                    Assert.True(error <= tolerance, $"{type} at q {q}, spot {s}: {calculated} against {expected}");
                }
            }
        }
    }

    // Upstream: QuantLib's master adds the Bjerksund-Stensland engine to this case (#2854, the S-van plan's VR5 (ii)).
    [Fact]
    public void TestBaroneAdesiWhaleyAndJuAtLowVolatility()
    {
        // Testing Barone-Adesi-Whaley, Ju and Bjerksund-Stensland approximations at very low volatility...

        // Both engines used to throw an uncaught error for an American put once
        // sigma*sqrt(T) dropped below roughly 7e-4, for ordinary positive rates;
        // see <https://github.com/lballabio/QuantLib/issues/2749>. With no time
        // value the American price is the European one floored at intrinsic.
        var today = new Date(15, Month.May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        DayCounter dc = new Actual365Fixed();
        Date exDate = today + new Period(180, TimeUnit.Days);
        var exercise = new AmericanExercise(today, exDate);
        var euExercise = new EuropeanExercise(exDate);

        // as the volatility vanishes the early-exercise premium does too, so the
        // approximation should land between the floor and a small band above it
        const Real tolerance = 1.0e-6;
        const Real band = 1.0e-2;

        Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults>[] engines =
        [
            BaroneAdesiWhaleyApproximationEngine.Calculate,
            JuQuadraticApproximationEngine.Calculate,
            BjerksundStenslandApproximationEngine.Calculate,
        ];

        foreach (OptionType type in new[] { OptionType.Put, OptionType.Call })
        {
            foreach (Real s in new[] { 80.0, 100.0, 120.0 })
            {
                foreach (Real v in new[] { 1.0e-3, 5.0e-4, 0.0 })
                {
                    GeneralizedBlackScholesProcess process = new BlackScholesMertonProcess(
                        s, FlatRate(0.02, dc, context), FlatRate(0.05, dc, context), FlatVol(v, dc, context));
                    var payoff = new PlainVanillaPayoff(type, 100.0);
                    Real intrinsic = payoff.Evaluate(s);

                    Real european = AnalyticEuropeanEngine.Calculate(new VanillaOption(payoff, euExercise), context, process)
                        .NPV!.Value;
                    Real floor = Math.Max(european, intrinsic);

                    foreach (var engine in engines)
                    {
                        Real calculated = engine(new VanillaOption(payoff, exercise), context, process).NPV!.Value;
                        Assert.True(
                            calculated >= floor - tolerance && calculated <= floor + band,
                            $"{type} at spot {s}, vol {v}: {calculated} against the floor {floor}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestJuNotBelowExerciseValue()
    {
        // Testing that the Ju approximation is never worth less than immediate exercise...

        // Deep in the money the Ju-Zhong correction term can take the price
        // below the exercise value at ordinary positive rates.
        var today = new Date(15, Month.August, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        DayCounter dc = new Actual365Fixed();
        Real spot = 100.0;

        (OptionType Type, Real Strike, Rate R, Rate Q, Volatility V, Integer Years)[] cases =
        [
            (OptionType.Call, 60.0, 0.03, 0.02, 0.10, 3),
            (OptionType.Call, 60.0, 0.06, 0.04, 0.10, 10),
            (OptionType.Put, 140.0, 0.03, 0.04, 0.03, 10),
        ];

        foreach (var c in cases)
        {
            GeneralizedBlackScholesProcess process = new BlackScholesMertonProcess(
                spot, FlatRate(c.Q, dc, context), FlatRate(c.R, dc, context), FlatVol(c.V, dc, context));
            var payoff = new PlainVanillaPayoff(c.Type, c.Strike);
            var exercise = new AmericanExercise(today, today + new Period(c.Years, TimeUnit.Years));

            Real intrinsic = payoff.Evaluate(spot);
            Real calculated = JuQuadraticApproximationEngine.Calculate(new VanillaOption(payoff, exercise), context, process)
                .NPV!.Value;
            Assert.False(calculated < intrinsic, $"{c.Type} {c.Strike}: {calculated} below the exercise value {intrinsic}");
        }
    }

    // Upstream: QuantLib's master's case for #2906 (the S-van plan's VR5 (iii)).
    [Fact]
    public void TestJuNotBelowEuropeanValue()
    {
        // Testing that the Ju approximation is never worth less than the European option...

        // For short-dated, highly volatile options the Ju-Zhong correction term
        // can turn negative and take the price below the European one; see
        // <https://github.com/lballabio/QuantLib/issues/2904>. This happens both
        // when the price was floored at an exercise value lower than the European
        // price and, at the money, when the exercise floor doesn't apply at all.
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        DayCounter dc = new Actual365Fixed();
        Real spot = 100.0;

        (OptionType Type, Real Strike, Rate R, Rate Q, Volatility V, Integer Days)[] cases =
        [
            (OptionType.Put, 110.0, 0.02, 0.00, 0.80, 3),
            (OptionType.Put, 100.0, 0.05, 0.03, 1.20, 1),
            (OptionType.Call, 90.0, 0.01, 0.03, 1.20, 3),
        ];

        foreach (var c in cases)
        {
            GeneralizedBlackScholesProcess process = new BlackScholesMertonProcess(
                spot, FlatRate(c.Q, dc, context), FlatRate(c.R, dc, context), FlatVol(c.V, dc, context));
            var payoff = new PlainVanillaPayoff(c.Type, c.Strike);
            Date exDate = today + new Period(c.Days, TimeUnit.Days);
            var exercise = new AmericanExercise(today, exDate);

            Real expected = AnalyticEuropeanEngine.Calculate(new VanillaOption(payoff, new EuropeanExercise(exDate)), context, process)
                .NPV!.Value;
            Real calculated = JuQuadraticApproximationEngine.Calculate(new VanillaOption(payoff, exercise), context, process)
                .NPV!.Value;
            Assert.False(calculated < expected, $"{c.Type} {c.Strike}: {calculated} below the European {expected}");
        }
    }

    [Fact]
    public void TestBjerksundStenslandAtUnitBeta()
    {
        // Testing Bjerksund-Stensland where beta is one...

        // A call with q = 0 and -sigma^2/2 <= r < 0, or by symmetry a put with
        // r = 0 and -sigma^2/2 <= q < 0, gives beta = 1 and an infinite perpetual
        // boundary. The results there must match those for a carry a hair away.
        var today = new Date(15, Month.August, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        DayCounter dc = new Actual365Fixed();
        Real spot = 100.0;
        var exercise = new AmericanExercise(today, today + new Period(5, TimeUnit.Years));

        (OptionType Type, Rate R, Rate Q, Rate RBumped, Rate QBumped)[] cases =
        [
            (OptionType.Call, -0.005, 0.0, -0.005, 1.0e-6),
            (OptionType.Put, 0.0, -0.005, 1.0e-6, -0.005),
        ];

        foreach (var c in cases)
        {
            var option = new VanillaOption(new PlainVanillaPayoff(c.Type, 100.0), exercise);
            OneAssetOptionResults Price(Rate r, Rate q) => BjerksundStenslandApproximationEngine.Calculate(
                option,
                context,
                new BlackScholesMertonProcess(spot, FlatRate(q, dc, context), FlatRate(r, dc, context), FlatVol(0.20, dc, context)));

            OneAssetOptionResults calculated = Price(c.R, c.Q);
            OneAssetOptionResults bumped = Price(c.RBumped, c.QBumped);
            (string Name, Func<OneAssetOptionResults, Real> Get)[] results =
            [
                ("value", o => o.NPV!.Value),
                ("delta", o => o.Delta!.Value),
                ("gamma", o => o.Gamma!.Value),
                ("rho", o => o.Rho!.Value),
                ("dividend rho", o => o.DividendRho!.Value),
                ("vega", o => o.Vega!.Value),
            ];
            foreach (var (name, get) in results)
            {
                Real expected = get(bumped);
                Real error = Math.Abs(get(calculated) - expected);
                Assert.True(
                    error <= 1.0e-3 * Math.Max(1.0, Math.Abs(expected)),
                    $"{c.Type} {name}: {get(calculated)} against {expected}");
            }
        }

        // where r = -sigma^2/2 exactly, rounding can take the radicand of beta
        // below zero
        DayCounter a360 = new Actual360();
        Real kink = BjerksundStenslandApproximationEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), exercise),
            context,
            new BlackScholesMertonProcess(
                spot, FlatRate(0.0, a360, context), FlatRate(-0.02, a360, context), FlatVol(0.20, a360, context))).NPV!.Value;
        Assert.True(double.IsFinite(kink));
    }

    [Fact]
    public void TestBjerksundStenslandEuropeanGreeks()
    {
        // Testing Bjerksund-Stensland greeks when early exercise is not optimal...
        var today = new Date(5, Month.November, 2022);
        var context = new EvaluationContext { EvaluationDate = today };

        Real spot = 100;
        const Real k = 105;
        const Volatility sigma = 0.40;
        Date maturityDate = today + new Period(724, TimeUnit.Days);

        (OptionType Type, Real R, Real Q)[] testCaseSpecs =
        [
            (OptionType.Put, -0.05, 0.02),
            (OptionType.Call, 0.05, -0.025),
        ];

        var europeanExercise = new EuropeanExercise(maturityDate);
        var americanExercise = new AmericanExercise(today, maturityDate);

        foreach (var testCaseSpec in testCaseSpecs)
        {
            GeneralizedBlackScholesProcess bsProcess = new BlackScholesMertonProcess(
                spot,
                FlatRate(testCaseSpec.Q, new Actual365Fixed(), context),
                FlatRate(testCaseSpec.R, new Actual360(), context),
                new BlackConstantVol(today, new NullCalendar(), sigma, new Thirty360(Thirty360Convention.European)));

            OneAssetOptionResults americanOption = BjerksundStenslandApproximationEngine.Calculate(
                new VanillaOption(new PlainVanillaPayoff(testCaseSpec.Type, k), americanExercise), context, bsProcess);
            OneAssetOptionResults europeanOption = AnalyticEuropeanEngine.Calculate(
                new VanillaOption(new PlainVanillaPayoff(testCaseSpec.Type, k), europeanExercise), context, bsProcess);

            const double tol = 1000 * QL_EPSILON;
            CheckClose(europeanOption.NPV!.Value, americanOption.NPV!.Value, tol);
            CheckClose(europeanOption.Delta!.Value, americanOption.Delta!.Value, tol);
            CheckClose(europeanOption.StrikeSensitivity!.Value, americanOption.StrikeSensitivity!.Value, tol);
            CheckClose(europeanOption.Gamma!.Value, americanOption.Gamma!.Value, tol);
            CheckClose(europeanOption.Vega!.Value, americanOption.Vega!.Value, tol);
            CheckClose(europeanOption.Theta!.Value, americanOption.Theta!.Value, tol);
            CheckClose(europeanOption.ThetaPerDay!.Value, americanOption.ThetaPerDay!.Value, tol);
            CheckClose(europeanOption.Rho!.Value, americanOption.Rho!.Value, tol);
            CheckClose(europeanOption.DividendRho!.Value, americanOption.DividendRho!.Value, tol);
        }
    }

    // Adapted: the quotes changed in place become a process built for each bump.
    [Fact]
    public void TestBjerksundStenslandAmericanGreeks()
    {
        // Testing Bjerksund-Stensland American greeks...
        var today = new Date(5, Month.December, 2022);
        var context = new EvaluationContext { EvaluationDate = today };

        OneAssetOptionResults Price(StrikedTypePayoff payoff, Exercise exercise, Real s, Rate q, Rate r, Volatility v) =>
            BjerksundStenslandApproximationEngine.Calculate(
                new VanillaOption(payoff, exercise),
                context,
                new BlackScholesMertonProcess(
                    s,
                    FlatRate(q, new Actual360(), context),
                    FlatRate(r, new Actual365Fixed(), context),
                    new BlackConstantVol(today, new NullCalendar(), v, new Thirty360(Thirty360Convention.ISDA))));

        const Real strike = 100;
        OptionType[] types = [OptionType.Call, OptionType.Put];
        Rate[] rf = [0.0, 0.02, 0.06, 0.1, 0.2];
        Rate[] qy = [0.0, 0.08, 0.12];
        Volatility[] sig = [0.1, 0.2, 0.4, 1.0];
        Real[] spots = [25, 50, 99.9, 110, 150, 200];
        Size[] maturities = [30, 182, 365, 1825];

        const Real f_d = 1e-5;
        const Real f_g = 5e-5;
        const Real f_q = 1e-6;

        foreach (OptionType type in types)
        {
            StrikedTypePayoff Payoff(Real k) => new PlainVanillaPayoff(type, k);
            StrikedTypePayoff stdPayoff = Payoff(strike);

            foreach (Size t in maturities)
            {
                Date maturityDate = today + new Period(t, TimeUnit.Days);
                Exercise ExerciseAt(Integer offset) => new AmericanExercise(today, maturityDate + offset);
                Exercise stdExercise = ExerciseAt(0);

                foreach (Rate r in rf)
                {
                    foreach (Rate q in qy)
                    {
                        foreach (Volatility v in sig)
                        {
                            foreach (Real s in spots)
                            {
                                OneAssetOptionResults option = Price(stdPayoff, stdExercise, s, q, r, v);
                                Real npv = option.NPV!.Value;
                                Real delta = option.Delta!.Value;
                                Real gamma = option.Gamma!.Value;
                                Real strikeSensitivity = option.StrikeSensitivity!.Value;
                                Real dividendRho = option.DividendRho!.Value;
                                Real rho = option.Rho!.Value;
                                Real vega = option.Vega!.Value;
                                Real theta = option.Theta!.Value;
                                string exerciseType = (string)option.AdditionalResults["exerciseType"];
                                string where = $"{type} t {t}, r {r}, q {q}, v {v}, s {s}";

                                Real f2 = Price(stdPayoff, stdExercise, s * (1 + f_d), q, r, v).NPV!.Value;
                                Real f1 = Price(stdPayoff, stdExercise, s * (1 - f_d), q, r, v).NPV!.Value;
                                Real numericalDelta = (f2 - f1) / (2 * f_d * s);
                                Real error = Math.Abs(delta - numericalDelta);
                                Assert.False(error > 5e-6, $"delta {where}: {delta} against {numericalDelta}");

                                Real gp2 = Price(stdPayoff, stdExercise, s * (1 + (2 * f_g)), q, r, v).NPV!.Value;
                                Real gp1 = Price(stdPayoff, stdExercise, s * (1 + f_g), q, r, v).NPV!.Value;
                                Real gm1 = Price(stdPayoff, stdExercise, s * (1 - f_g), q, r, v).NPV!.Value;
                                Real gm2 = Price(stdPayoff, stdExercise, s * (1 - (2 * f_g)), q, r, v).NPV!.Value;
                                Real numericalGamma = (-gp2 + (16 * gp1) - (30 * npv) + (16 * gm1) - gm2)
                                    / (12 * (f_g * s) * (f_g * s));
                                error = Math.Abs(gamma - numericalGamma);
                                Assert.False(error > 1e-4 && t < 1000, $"gamma {where}: {gamma} against {numericalGamma}");

                                Real k2 = Price(Payoff(strike * (1 + f_d)), stdExercise, s, q, r, v).NPV!.Value;
                                Real k1 = Price(Payoff(strike * (1 - f_d)), stdExercise, s, q, r, v).NPV!.Value;
                                Real numericalStrikeSensitivity = (k2 - k1) / (2 * f_d * strike);
                                error = Math.Abs(strikeSensitivity - numericalStrikeSensitivity);
                                Assert.False(
                                    error > 5e-6,
                                    $"strikeSensitivity {where}: {strikeSensitivity} against {numericalStrikeSensitivity}");

                                if (q != 0.0)
                                {
                                    Real q2 = Price(stdPayoff, stdExercise, s, q + f_q, r, v).NPV!.Value;
                                    Real q1 = Price(stdPayoff, stdExercise, s, q - f_q, r, v).NPV!.Value;
                                    Real numericalDividendRho = (q2 - q1) / (2 * f_q);
                                    error = Math.Abs(dividendRho - numericalDividendRho);
                                    Assert.False(error > 3e-2, $"dividendRho {where}: {dividendRho} against {numericalDividendRho}");

                                    Real r2 = Price(stdPayoff, stdExercise, s, q, r + f_q, v).NPV!.Value;
                                    Real r1 = Price(stdPayoff, stdExercise, s, q, r - f_q, v).NPV!.Value;
                                    Real numericalRho = (r2 - r1) / (2 * f_q);
                                    error = Math.Abs(rho - numericalRho);
                                    Assert.False(error > 3e-2, $"rho {where}: {rho} against {numericalRho}");
                                }

                                // Moved: the check of line 2437 at the put of a year, r 0.2, q 0, v 0.1, s 99.9 and
                                // 110, whose bumps straddle the edge of the S-van plan's VR9 region (bT + 2√v = 0 at
                                // v 0.1): the trigger is QuantLib's above it and the perpetual boundary below, and the
                                // value jumps between the two (Divergences/BjerksundStenslandTriggerTests.cs).
                                bool straddlesEdge = type == OptionType.Put && t == 365 && r == 0.2 && q == 0.0
                                    && v == 0.1 && (s == 99.9 || s == 110);
                                Real v2 = Price(stdPayoff, stdExercise, s, q, r, v + f_d).NPV!.Value;
                                Real v1 = Price(stdPayoff, stdExercise, s, q, r, v - f_d).NPV!.Value;
                                Real numericalVega = (v2 - v1) / (2 * f_d);
                                error = Math.Abs(vega - numericalVega);
                                Assert.False(error > 5e-4 && !straddlesEdge, $"vega {where}: {vega} against {numericalVega}");

                                if (exerciseType == "American")
                                {
                                    Real t2 = Price(stdPayoff, ExerciseAt(1), s, q, r, v).NPV!.Value;
                                    Real t1 = Price(stdPayoff, ExerciseAt(-1), s, q, r, v).NPV!.Value;
                                    Real numericalThetaPerDay = (t1 - t2) / 2;
                                    Real numericalTheta = 365 * numericalThetaPerDay;
                                    error = Math.Abs(theta - numericalTheta);
                                    Real thetaTol = t < 60 ? 3.0 : 5e-4;
                                    Assert.False(error > thetaTol, $"theta {where}: {theta} against {numericalTheta}");
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TestSingleBjerksundStenslandGreeks()
    {
        // Testing a single Bjerksund-Stensland greeks set...
        var today = new Date(20, Month.January, 2023);
        var context = new EvaluationContext { EvaluationDate = today };

        const Real s = 100;
        const Volatility v = 0.3;
        const Rate q = 0.04;
        const Rate r = 0.07;

        GeneralizedBlackScholesProcess bsProcess = new BlackScholesMertonProcess(
            s,
            FlatRate(q, new Actual365Fixed(), context),
            FlatRate(r, new Actual365Fixed(), context),
            new BlackConstantVol(today, new NullCalendar(), v, new Actual365Fixed()));

        Date maturityDate = today + new Period(2, TimeUnit.Years);
        var exercise = new AmericanExercise(today, maturityDate);
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100);

        OneAssetOptionResults option = BjerksundStenslandApproximationEngine.Calculate(
            new VanillaOption(payoff, exercise), context, bsProcess);

        const Real expectedNpv = 17.9251834488399169;
        const Real expectedDelta = 0.590801845261082592;
        const Real expectedGamma = 0.00825347110063545664;
        const Real expectedStrikeSensitivity = -0.411550010772683383;
        const Real expectedDivRho = -114.137818682236826;
        const Real expectedRho = 80.4900013901554416;
        const Real expectedVega = 49.2906331545933227;
        const Real expectedTheta = -4.22540293840206704;

        void Report(Real value, Real expectedValue, string name)
        {
            const double tol = 1e6 * QL_EPSILON;
            Real error = Math.Abs(value - expectedValue);
            Assert.False(error > tol, $"{name}: {value} against {expectedValue}");
        }

        Report(option.NPV!.Value, expectedNpv, "npv");
        Report(option.Delta!.Value, expectedDelta, "delta");
        Report(option.Gamma!.Value, expectedGamma, "gamma");
        Report(option.StrikeSensitivity!.Value, expectedStrikeSensitivity, "strikeSensitivity");
        Report(option.DividendRho!.Value, expectedDivRho, "dividendRho");
        Report(option.Rho!.Value, expectedRho, "rho");
        Report(option.Vega!.Value, expectedVega, "vega");
        Report(option.Theta!.Value, expectedTheta, "theta");
        Report(option.ThetaPerDay!.Value, expectedTheta / 365, "thetaPerDay");

        Assert.Equal("American", (string)option.AdditionalResults["exerciseType"]);
    }

    // Adapted: the quotes changed in place become a process built for each market.
    [Fact]
    public void TestBaroneAdesiWhaleyNegativeRates()
    {
        // Testing Barone-Adesi-Whaley approximation with negative rates...

        // Issue #1291: BAW crashes with a cryptic error when rates are negative.
        // Verify that it now throws a clear error message instead.
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual360();

        GeneralizedBlackScholesProcess Process(Real spot, Rate q, Rate r, Volatility v) =>
            new BlackScholesMertonProcess(
                spot, new FlatForward(today, q, dc), new FlatForward(today, r, dc),
                new BlackConstantVol(today, new NullCalendar(), v, dc));

        GeneralizedBlackScholesProcess stochProcess = Process(36.0, 0.0, -0.012, 0.20);
        var payoff = new PlainVanillaPayoff(OptionType.Put, 40.0);
        Date exDate = today + new Period(1, TimeUnit.Years);
        var exercise = new AmericanExercise(today, exDate);

        // With r <= 0 and q >= r the put takes the European shortcut, not the
        // critical price.  Ju agrees.
        Real europeanPut = AnalyticEuropeanEngine.Calculate(
            new VanillaOption(payoff, new EuropeanExercise(exDate)), context, stochProcess).NPV!.Value;
        Assert.True(Math.Abs(BaroneAdesiWhaleyApproximationEngine.Calculate(
            new VanillaOption(payoff, exercise), context, stochProcess).NPV!.Value - europeanPut) < 1.0e-6);
        Assert.True(Math.Abs(JuQuadraticApproximationEngine.Calculate(
            new VanillaOption(payoff, exercise), context, stochProcess).NPV!.Value - europeanPut) < 1.0e-6);

        // also verify with a call and positive dividends
        var callPayoff = new PlainVanillaPayoff(OptionType.Call, 40.0);
        NanoQLException e = Assert.Throws<NanoQLException>(() => BaroneAdesiWhaleyApproximationEngine.Calculate(
            new VanillaOption(callPayoff, exercise), context, Process(36.0, 0.06, -0.012, 0.20)));
        Assert.Contains("negative interest rates", e.Message);

        // With r < q <= 0 early exercise is optimal, so the European shortcut
        // must not fire. Ju goes through the same critical price.
        GeneralizedBlackScholesProcess itmProcess = Process(100.0, 0.0, -0.05, 0.03);
        var itmCallPayoff = new PlainVanillaPayoff(OptionType.Call, 80.0);
        var longExercise = new AmericanExercise(today, today + new Period(3, TimeUnit.Years));
        e = Assert.Throws<NanoQLException>(() => BaroneAdesiWhaleyApproximationEngine.Calculate(
            new VanillaOption(itmCallPayoff, longExercise), context, itmProcess));
        Assert.Contains("negative interest rates", e.Message);
        e = Assert.Throws<NanoQLException>(() => JuQuadraticApproximationEngine.Calculate(
            new VanillaOption(itmCallPayoff, longExercise), context, itmProcess));
        Assert.Contains("negative interest rates", e.Message);
    }

    // The value cases' loop: QuantLib's engine on each row, from today's date on Actual/360.
    private static void CheckValues(
        AmericanOptionData[] values,
        Real tolerance,
        Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> engine)
    {
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual360();

        foreach (AmericanOptionData value in values)
        {
            StrikedTypePayoff payoff = new PlainVanillaPayoff(value.Type, value.Strike);
            Date exDate = today + TimeToDays(value.T);
            Exercise exercise = new AmericanExercise(today, exDate);

            GeneralizedBlackScholesProcess stochProcess = new BlackScholesMertonProcess(
                value.S,
                new FlatForward(today, value.Q, dc),
                new FlatForward(today, value.R, dc),
                new BlackConstantVol(today, new NullCalendar(), value.V, dc));

            Real calculated = engine(new VanillaOption(payoff, exercise), context, stochProcess).NPV!.Value;
            Real error = Math.Abs(calculated - value.Result);
            if (error > tolerance)
            {
                Assert.Fail(
                    $"American {payoff.OptionType} option: spot {value.S}, strike {payoff.Strike}, q {value.Q}, "
                    + $"r {value.R}, vol {value.V}: expected value {value.Result}, calculated {calculated}, "
                    + $"error {error}, tolerance {tolerance}");
            }
        }
    }

    // BOOST_CHECK_CLOSE: the difference within the tolerance, in percent, of both values.
    private static void CheckClose(Real a, Real b, Real percent)
    {
        Real diff = Math.Abs(a - b);
        Assert.True(
            diff <= Math.Abs(a) * percent / 100.0 && diff <= Math.Abs(b) * percent / 100.0,
            $"difference between {a} and {b} exceeds {percent}%");
    }

    // Adapted: QuantLib's engine is built without a process, which the boundary never reads; a placeholder here.
    [Fact]
    public void TestQdPlusBoundaryValues()
    {
        // Testing QD+ boundary approximation...
        const Real s = 100;
        const Real k = 120;
        const Rate r = 0.1;
        const Rate q = 0.03;
        const Volatility sigma = 0.25;
        const double maturity = 5.0;

        var qrPlusEngine = new QdPlusAmericanEngine(PlaceholderProcess(), 10);

        (Real Tau, Real Expected)[] testCaseSpecs =
        [
            (4.9, 87.76960949965387),
            (4.0, 88.39053003614612),
            (2.5, 90.14327315762256),
            (1.0, 94.49793803095984),
            (0.1, 106.2588964442338),
        ];

        foreach (var testCaseSpec in testCaseSpecs)
        {
            (int nrEvaluations, Real boundary) = qrPlusEngine.PutExerciseBoundaryAtTau(
                s, k, r, q, sigma, maturity, testCaseSpec.Tau);
            Real expected = testCaseSpec.Expected;
            Real diff = Math.Abs(boundary - expected);
            const Real tol = 1e-12;
            Assert.False(diff > tol, $"QR+ boundary at {testCaseSpec.Tau}: {boundary} against {expected}");
            Assert.False(nrEvaluations > 10, $"{nrEvaluations} evaluations, at most 10");
        }
    }

    // Adapted: QuantLib's engine is built without a process, which the boundary never reads; a placeholder here.
    [Fact]
    public void TestQdPlusBoundaryConvergence()
    {
        // Testing QD+ boundary convergence...
        const Real s = 100;
        const Volatility sigma = 0.25;
        const double maturity = 10.0;

        (Real R, Real Q, Real Strike, Size MaxEvaluations)[] testCases =
        [
            (0.10, 0.03, 120, 2000),
            (0.0001, 0.03, 120, 2000),
            (0.0001, 0.000002, 120, 2000),
            (0.01, 0.75, 120, 2000),
            (0.03, 0.0, 30, 2000),
            (0.03, 0.0, 1e7, 2500),
            (0.075, 0.0, 1e-8, 2000),
        ];

        QdPlusAmericanEngineSolverType[] solverTypes =
        [
            QdPlusAmericanEngineSolverType.Brent,
            QdPlusAmericanEngineSolverType.Newton,
            QdPlusAmericanEngineSolverType.Ridder,
            QdPlusAmericanEngineSolverType.Halley,
            QdPlusAmericanEngineSolverType.SuperHalley,
        ];

        foreach (var testCase in testCases)
        {
            foreach (QdPlusAmericanEngineSolverType solverType in solverTypes)
            {
                var qrPlusEngine = new QdPlusAmericanEngine(PlaceholderProcess(), int.MaxValue, solverType, 1e-8);

                Size nrEvaluations = 0;
                for (Real t = 0.0; t < maturity; t += 0.1)
                {
                    nrEvaluations += qrPlusEngine.PutExerciseBoundaryAtTau(
                        s, testCase.Strike, testCase.R, testCase.Q, sigma, maturity, t).Evaluations;
                }

                Size maxEvaluations = solverType is QdPlusAmericanEngineSolverType.Halley or QdPlusAmericanEngineSolverType.SuperHalley
                    ? 750
                    : testCase.MaxEvaluations;
                Assert.False(
                    nrEvaluations > maxEvaluations,
                    $"{solverType} at r {testCase.R}, q {testCase.Q}, K {testCase.Strike}: {nrEvaluations} evaluations, "
                    + $"at most {maxEvaluations}");
            }
        }
    }

    // Adapted: the quotes changed in place become a process built for each case.
    [Fact]
    public void TestQdAmericanEngines()
    {
        // Testing QD+ American option pricing...
        DayCounter dc = new Actual365Fixed();
        var today = new Date(1, Month.June, 2022);
        var context = new EvaluationContext { EvaluationDate = today };

        // high precision edge cases
        QdOptionSpec[] edgeTestCases =
        [
            // standard put option
            new(OptionType.Put, 100.0, 120.0, 3650, 0.25, 0.10, 0.03, 22.97383256003585, 1e-8),

            // call-put parity on standard option
            new(OptionType.Call, 120.0, 100.0, 3650, 0.25, 0.03, 0.10, 22.97383256003585, 1e-8),

            // zero strike put
            new(OptionType.Put, 100.0, 0.0, 365, 0.25, 0.02, 0.02, 0.0, 1e-14),
            new(OptionType.Put, 100.0, 1e-8, 365, 0.25, 0.02, 0.02, 0.0, 1e-14),

            // zero strike call
            new(OptionType.Call, 100.0, 0.0, 365, 0.25, 0.05, 0.01, 100.0, 1e-11),
            new(OptionType.Call, 100.0, 1e-7, 365, 0.25, 0.05, 0.01, 100.0 - 1e-7, 1e-9),

            // zero vol call
            new(OptionType.Call, 100.0, 50.0, 365, 0.0, 0.05, 0.01, 51.4435121498811085, 1e-10),
            new(OptionType.Call, 100.0, 50.0, 365, 1e-8, 0.05, 0.01, 51.4435121498811156, 1e-8),

            // zero vol put 1
            new(OptionType.Put, 100.0, 120.0, 4 * 3650, 1e-6, 0.01, 0.50, 108.980920365700442, 1e-4),
            new(OptionType.Put, 100.0, 120.0, 4 * 3650, 0.0, 0.01, 0.50, 108.980904561184602, 1e-10),

            // zero vol put 2
            new(OptionType.Put, 100.0, 120.0, 365, 1e-7, 0.05, 0.01, 20.0, 1e-9),
            new(OptionType.Put, 100.0, 120.0, 365, 0.0, 0.05, 0.01, 20.0, 1e-12),

            // zero vol put 3
            new(OptionType.Put, 100.0, 120.0, 365, 1e-7, 0.00, 0.05, 24.8770575499286082, 1e-8),
            new(OptionType.Put, 100.0, 120.0, 365, 0.0, 0.00, 0.05, 24.8770575499286082, 1e-10),

            // zero spot put
            new(OptionType.Put, 1e-6, 120.0, 365, 0.25, -0.075, 0.05, 129.346097154926355, 1e-9),
            new(OptionType.Put, 0.0, 120.0, 365, 0.25, -0.075, 0.05, 129.346098106155779, 1e-10),

            // zero spot call
            new(OptionType.Call, 1e-6, 120.0, 365, 0.25, 0.075, 0.05, 0.0, 1e-14),
            new(OptionType.Call, 0.0, 120.0, 365, 0.25, 0.075, 0.05, 0.0, 1e-14),

            // put option with one day left
            new(OptionType.Put, 100.0, 120.0, 1, 0.25, 0.05, 0.0, 20.0, 1e-10),

            // put option at maturity
            new(OptionType.Put, 100.0, 120.0, 0, 0.25, 0.05, 0.0, 0.0, 1e-14),

            // zero everything
            new(OptionType.Put, 0.0, 0.0, 365, 0.0, 0.0, 0.0, 0.0, 1e-14),

            // zero strike call with zero vol
            new(OptionType.Call, 100.0, 1e-7, 365, 1e-8, 0.05, 0.025, 100.0 - 1e-7, 1e-8),
            new(OptionType.Call, 100.0, 0.0, 365, 1e-8, 0.05, 0.025, 100.0, 1e-8),
            new(OptionType.Call, 100.0, 1e-7, 365, 0.0, 0.05, 0.025, 100.0 - 1e-7, 1e-8),
            new(OptionType.Call, 100.0, 0.0, 365, 0.0, 0.05, 0.025, 100.0, 1e-8),

            // zero spot call with zero vol
            new(OptionType.Call, 1e-8, 100, 365, 1e-8, 0.05, 0.025, 0.0, 1e-10),
            new(OptionType.Call, 0.0, 100, 365, 0.0, 0.05, 0.025, 0.0, 1e-14),

            // zero interest rate call
            new(OptionType.Call, 100, 100, 365, 0.25, 0.0, 0.025, 8.871505915120776, 1e-8),

            // zero dividend rate call
            new(OptionType.Call, 100, 100, 365, 0.25, 0.05, 0.0, 12.3359989303687243, 1e-8),

            // extreme spot call
            new(OptionType.Call, 1e10, 100, 365, 0.25, 0.01, 0.05, 1e10 - 100.0, -1),

            // extreme strike call
            new(OptionType.Call, 100, 1e10, 365, 0.25, 0.01, 0.05, 0.0, 1e-14),

            // extreme vol call
            new(OptionType.Call, 100, 100, 365, 100.0, 0.01, 0.05, 99.9874942266127, 1e-8),

            // extreme dividend yield call
            new(OptionType.Call, 100, 100, 365, 0.25, 0.10, 10.0, 0.1159627202107989, 1e-8),

            // extreme maturity call
            new(OptionType.Call, 100, 100, 170 * 365, 0.25, 0.01, 0.002, 80.37468392429741, 1e-8),
        ];

        // random test cases
        double[] pdeValues = QdPdeValues;

        var testCaseSpecs = new List<QdOptionSpec>(pdeValues.Length + edgeTestCases.Length);

        var rng = new InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>(
            new MersenneTwisterUniformRng(12345L));
        foreach (double pdeValue in pdeValues)
        {
            OptionType optionType = rng.Next().Value > 0 ? OptionType.Call : OptionType.Put;
            Real spot = 100 * Math.Exp(1.5 * rng.Next().Value);
            Real strike = 100 * Math.Exp(1.5 * rng.Next().Value);
            Size maturityInDays = (Size)(1 + (365 * Math.Exp(2 * rng.Next().Value)));
            Volatility vol = 0.5 * Math.Exp(rng.Next().Value);
            Rate r = 0.10 * Math.Exp(rng.Next().Value);
            Rate q = 0.10 * Math.Exp(rng.Next().Value);
            testCaseSpecs.Add(new QdOptionSpec(optionType, spot, strike, maturityInDays, vol, r, q, pdeValue, -1));
        }

        testCaseSpecs.AddRange(edgeTestCases);

        foreach (QdOptionSpec testCaseSpec in testCaseSpecs)
        {
            Date maturityDate = today + new Period(testCaseSpec.MaturityInDays, TimeUnit.Days);
            GeneralizedBlackScholesProcess bsProcess = new BlackScholesMertonProcess(
                testCaseSpec.Spot,
                new FlatForward(today, testCaseSpec.Q, dc),
                new FlatForward(today, testCaseSpec.R, dc),
                new BlackConstantVol(today, new NullCalendar(), testCaseSpec.Volatility, dc));

            var option = new VanillaOption(
                new PlainVanillaPayoff(testCaseSpec.OptionType, testCaseSpec.Strike), new AmericanExercise(today, maturityDate));
            Real calculated = new QdPlusAmericanEngine(bsProcess, 8, QdPlusAmericanEngineSolverType.Halley, 1e-10)
                .Calculate(option, context).NPV!.Value;
            Real expected = testCaseSpec.ExpectedValue;

            bool failed = (testCaseSpec.Precision > 0 && Math.Abs(expected - calculated) > testCaseSpec.Precision)
                || (testCaseSpec.Precision < 0 && expected > 0.1 && Math.Abs(calculated - expected) / expected > 0.005)
                || (testCaseSpec.Precision < 0 && expected <= 0.1 && Math.Abs(expected - calculated) > 5e-4);
            Assert.False(
                failed,
                $"QR+ {testCaseSpec.OptionType}: spot {testCaseSpec.Spot}, strike {testCaseSpec.Strike}, "
                + $"r {testCaseSpec.R}, q {testCaseSpec.Q}, vol {testCaseSpec.Volatility}: calculated {calculated}, "
                + $"expected {expected}");
        }
    }

    [Fact]
    public void TestQdFpIterationScheme()
    {
        // Testing Legendre and tanh-sinh iteration scheme for QD+ fixed-point American engine...
        const Real tol = 1e-8;
        const Size l = 32, m = 6, n = 18, p = 36;

        QdFpIterationScheme[] schemes =
        [
            new QdFpLegendreScheme(l, m, n, p),
            new QdFpLegendreTanhSinhScheme(l, m, n, tol),
            new QdFpTanhSinhIterationScheme(m, n, tol),
        ];

        var nd = new NormalDistribution();
        foreach (QdFpIterationScheme scheme in schemes)
        {
            Assert.Equal(n, scheme.GetNumberOfChebyshevInterpolationNodes());
            Assert.Equal(1, scheme.GetNumberOfJacobiNewtonFixedPointSteps());
            Assert.Equal(m - 1, scheme.GetNumberOfNaiveFixedPointSteps());

            Assert.True(Math.Abs(scheme.GetFixedPointIntegrator().Integrate(nd.Evaluate, -10.0, 10.0) - 1.0) < tol);
            Assert.True(Math.Abs(scheme.GetExerciseBoundaryToPriceIntegrator().Integrate(nd.Evaluate, -10.0, 10.0) - 1.0) < tol);
        }
    }

    [Fact]
    public void TestAndersenLakeHighPrecisionExample()
    {
        // Testing Andersen, Lake and Offengenden high precision example...

        // Example and results are taken from
        //    Leif Andersen, Mark Lake and Dimitri Offengenden (2015)
        //    "High Performance American Option Pricing",
        //    https://papers.ssrn.com/sol3/papers.cfm?abstract_id=2547027
        (Size L, Size M, Size N, Rate R, Real[] Expected, Real Tol)[] testCases =
        [
            (24, 3, 9, 0.05, [0.1069528125898476, 0.1069524359360852], 1e-6),
            (5, 1, 4, 0.05, [0.1070237787625299, 0.1070042740171235], 1e-3),
            (11, 2, 5, 0.05, [0.106938750864602, 0.1069479057531648], 1e-4),
            (35, 8, 16, 0.05, [0.1069527032381714, 0.106952558361499], 1e-9),
            (65, 8, 32, 0.05, [0.1069527028247546, 0.1069526779971959], 1e-11),
            (5, 1, 4, 0.075, [0.3674420299196104, 0.3674766444325588], 1e-3),
            (11, 2, 5, 0.075, [0.3671056766787473, 0.3671024005532715], 1e-4),
            (35, 8, 16, 0.075, [0.3671116758420414, 0.3671111055677869], 1e-9),
            (65, 8, 32, 0.075, [0.3671112309062572, 0.3671111267813689], 1e-11),
        ];

        DayCounter dc = new Actual365Fixed();
        var today = new Date(25, Month.July, 2022);
        var context = new EvaluationContext { EvaluationDate = today };

        const Real spot = 100.0;
        const Real strike = 100.0;
        const Rate q = 0.05;
        const Volatility vol = 0.25;
        Date maturityDate = today + new Period(1, TimeUnit.Years);
        var payoff = new PlainVanillaPayoff(OptionType.Put, strike);

        foreach (var testCase in testCases)
        {
            GeneralizedBlackScholesProcess bsProcess = new BlackScholesMertonProcess(
                spot, FlatRate(q, dc, context), FlatRate(testCase.R, dc, context), FlatVol(vol, dc, context));

            var americanOption = new VanillaOption(payoff, new AmericanExercise(today, maturityDate));
            Real europeanNPV = AnalyticEuropeanEngine.Calculate(
                new VanillaOption(payoff, new EuropeanExercise(maturityDate)), context, bsProcess).NPV!.Value;

            QdFpAmericanEngineFixedPointEquation[] schemes =
                [QdFpAmericanEngineFixedPointEquation.FpA, QdFpAmericanEngineFixedPointEquation.FpB];

            for (int i = 0; i < schemes.Length; ++i)
            {
                Real americanNPV = QdFpAmericanEngine.Calculate(
                    americanOption,
                    context,
                    bsProcess,
                    new QdFpLegendreTanhSinhScheme(testCase.L, testCase.M, testCase.N, testCase.Tol),
                    schemes[i]).NPV!.Value;
                Real americanPremium = americanNPV - europeanNPV;
                Real diff = Math.Abs(americanPremium - testCase.Expected[i]);
                Assert.False(
                    diff > testCase.Tol,
                    $"{schemes[i]} at r {testCase.R}, ({testCase.L},{testCase.M},{testCase.N}): diff {diff}, tol {testCase.Tol}");
            }
        }
    }

    [Fact]
    public void TestQdEngineStandardExample()
    {
        // Testing Andersen, Lake and Offengenden standard example...
        DayCounter dc = new Actual365Fixed();
        var today = new Date(1, Month.June, 2022);
        var context = new EvaluationContext { EvaluationDate = today };

        const Real s = 100;
        const Real k = 95;
        const Rate r = 0.075;
        const Rate q = 0.05;
        const Volatility sigma = 0.25;
        Date maturityDate = today + new Period(1, TimeUnit.Years);

        GeneralizedBlackScholesProcess bsProcess = new BlackScholesMertonProcess(
            s, FlatRate(q, dc, context), FlatRate(r, dc, context), FlatVol(sigma, dc, context));

        var payoff = new PlainVanillaPayoff(OptionType.Put, k);
        Real europeanNPV = AnalyticEuropeanEngine.Calculate(
            new VanillaOption(payoff, new EuropeanExercise(maturityDate)), context, bsProcess).NPV!.Value;
        var americanOption = new VanillaOption(payoff, new AmericanExercise(today, maturityDate));

        QdFpAmericanEngineFixedPointEquation[] schemes =
            [QdFpAmericanEngineFixedPointEquation.FpA, QdFpAmericanEngineFixedPointEquation.FpB];
        Real[] expected = [0.2386475283369327, 0.2386596962737606];

        for (int i = 0; i < schemes.Length; ++i)
        {
            Real calculated = QdFpAmericanEngine.Calculate(
                americanOption, context, bsProcess, new QdFpLegendreScheme(32, 2, 15, 48), schemes[i]).NPV!.Value
                - europeanNPV;
            const Real tol = 7e-15;
            Real diff = Math.Abs(calculated - expected[i]);
            Assert.False(diff > tol, $"{schemes[i]}: diff {diff}, tol {tol}");
        }
    }

    // Adapted: the quotes changed in place become a process built for each market.
    [Fact]
    public void TestBulkQdFpAmericanEngine()
    {
        // Testing Andersen, Lake and Offengenden bulk examples...

        // Examples are taken from
        //    Leif Andersen, Mark Lake and Dimitri Offengenden (2015)
        //    "High Performance American Option Pricing",
        //    https://papers.ssrn.com/sol3/papers.cfm?abstract_id=2547027
        DayCounter dc = new Actual365Fixed();
        var today = new Date(1, Month.June, 2022);
        var context = new EvaluationContext { EvaluationDate = today };

        // original test set from the article, takes too long
        // const Size T[] = {30, 91, 182, 273, 365};
        // const Rate rf[] = {0.02, 0.04, 0.06, 0.08, 0.1};
        // const Rate qy[] = {0, 0.04, 0.08, 0.12};
        // const Real S[] = {25, 50, 80, 90, 100, 110, 120, 150, 175, 200};
        // const Volatility sig[] = {0.1, 0.2, 0.3, 0.4, 0.5, 0.6};
        Size[] maturities = [30, 182, 365];
        Rate[] rf = [0.02, 0.04, 0.06, 0.1];
        Rate[] qy = [0, 0.04, 0.08, 0.12];
        Real[] spots = [25, 75, 100, 125, 200];
        Volatility[] sig = [0.1, 0.25, 0.6];

        var payoff = new PlainVanillaPayoff(OptionType.Put, 100);
        var statsAccurate = new IncrementalStatistics();
        var statsLobatto = new IncrementalStatistics();

        foreach (Size t in maturities)
        {
            Date maturityDate = today + new Period(t, TimeUnit.Days);
            var option = new VanillaOption(payoff, new AmericanExercise(today, maturityDate));
            foreach (Rate r in rf)
            {
                foreach (Rate q in qy)
                {
                    foreach (Volatility v in sig)
                    {
                        foreach (Real s in spots)
                        {
                            GeneralizedBlackScholesProcess bsProcess = new BlackScholesMertonProcess(
                                s, new FlatForward(today, q, dc), new FlatForward(today, r, dc),
                                new BlackConstantVol(today, new NullCalendar(), v, dc));

                            Real fast = QdFpAmericanEngine.Calculate(option, context, bsProcess, QdFpAmericanEngine.FastScheme())
                                .NPV!.Value;
                            Real accurate = QdFpAmericanEngine.Calculate(
                                option, context, bsProcess, QdFpAmericanEngine.AccurateScheme()).NPV!.Value;
                            statsAccurate.Add(Math.Abs(fast - accurate));

                            Real lobatto = QdFpAmericanEngine.Calculate(
                                option, context, bsProcess, new QdFpGaussLobattoScheme(3, 7, 1e-5)).NPV!.Value;
                            statsLobatto.Add(Math.Abs(accurate - lobatto));
                        }
                    }
                }
            }
        }

        const Real tolStdDev = 1e-4;
        Assert.False(statsAccurate.StandardDeviation() > tolStdDev, $"RMSE diff {statsAccurate.StandardDeviation()}");
        Assert.False(statsLobatto.StandardDeviation() > tolStdDev, $"Lobatto RMSE diff {statsLobatto.StandardDeviation()}");

        const Real tolMax = 2.5e-3;
        Assert.False(statsAccurate.Max() > tolMax, $"max diff {statsAccurate.Max()}");
        Assert.False(statsLobatto.Max() > tolMax, $"Lobatto max diff {statsLobatto.Max()}");
    }

    // Adapted: the quote changed in place becomes a process built for each spot.
    [Fact]
    public void TestQdEngineWithLobattoIntegral()
    {
        // Testing American engine with high precision Gauss-Lobatto integration...
        DayCounter dc = new Actual365Fixed();
        var today = new Date(5, Month.November, 2022);
        var context = new EvaluationContext { EvaluationDate = today };

        const Real k = 40;
        const Rate r = 0.075;
        const Rate q = 0.01;
        const Volatility sigma = 0.40;
        Date maturityDate = today + new Period(2, TimeUnit.Years);

        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, k), new AmericanExercise(today, maturityDate));

        QdFpAmericanEngineFixedPointEquation[] schemes =
            [QdFpAmericanEngineFixedPointEquation.FpA, QdFpAmericanEngineFixedPointEquation.FpB];

        var gaussLobattoScheme = new QdFpGaussLobattoScheme(10, 30, 1e-10);

        foreach (QdFpAmericanEngineFixedPointEquation scheme in schemes)
        {
            foreach (Real s in new[] { 36, 40 - 1e-8, 40, 40 + 1e-8, 50 })
            {
                GeneralizedBlackScholesProcess bsProcess = new BlackScholesMertonProcess(
                    s, FlatRate(q, dc, context), FlatRate(r, dc, context), FlatVol(sigma, dc, context));

                Real highPrecisionNPV = QdFpAmericanEngine.Calculate(
                    option, context, bsProcess, QdFpAmericanEngine.HighPrecisionScheme(), scheme).NPV!.Value;
                Real lobattoNPV = QdFpAmericanEngine.Calculate(option, context, bsProcess, gaussLobattoScheme, scheme)
                    .NPV!.Value;

                Real diff = Math.Abs(lobattoNPV - highPrecisionNPV);
                const Real tol = 1e-11;
                Assert.False(diff > tol || double.IsNaN(lobattoNPV), $"{scheme} at spot {s}: diff {diff}, tol {tol}");
            }
        }
    }

    [Fact]
    public void TestQdEnginesNotBelowExerciseValue()
    {
        // Testing that the QD+ and QD fixed-point engines are never worth less than immediate exercise...
        var today = new Date(15, Month.August, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        DayCounter dc = new Actual365Fixed();
        const Real spot = 100.0;

        (OptionType Type, Real Strike, Rate R, Rate Q, Volatility V, Integer Years)[] cases =
        [
            (OptionType.Call, 60.0, -0.10, 0.06, 0.40, 10),
            (OptionType.Call, 80.0, -0.05, 0.06, 0.20, 10),
            (OptionType.Put, 140.0, 0.06, 0.00, 0.20, 10),
        ];

        foreach (var c in cases)
        {
            GeneralizedBlackScholesProcess process = new BlackScholesMertonProcess(
                spot, FlatRate(c.Q, dc, context), FlatRate(c.R, dc, context), FlatVol(c.V, dc, context));
            var payoff = new PlainVanillaPayoff(c.Type, c.Strike);
            var exercise = new AmericanExercise(today, today + new Period(c.Years, TimeUnit.Years));
            Real intrinsic = payoff.Evaluate(spot);

            QdPutCallParityEngine[] engines = [new QdPlusAmericanEngine(process), new QdFpAmericanEngine(process)];
            foreach (QdPutCallParityEngine engine in engines)
            {
                Real calculated = engine.Calculate(new VanillaOption(payoff, exercise), context).NPV!.Value;
                Assert.False(calculated < intrinsic, $"{c.Type} {c.Strike}: {calculated} below the exercise value {intrinsic}");
            }
        }
    }

    // Adapted: the quotes changed in place become a process built for each rate pair.
    [Fact]
    public void TestQdNegativeDividendYield()
    {
        // Testing American engines with positive or zero interest rate and negative dividend yield...
        DayCounter dc = new Actual365Fixed();
        var today = new Date(5, Month.December, 2022);
        var context = new EvaluationContext { EvaluationDate = today };

        Date maturityDate = today + new Period(18, TimeUnit.Months);
        const Real k = 90;
        const Real spot = 100;

        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, k), new AmericanExercise(today, maturityDate));

        Rate[] rRates = [0.025, 0.5, 0.0, 1e-6];
        Rate[] qRates = [-0.05, -0.1, -0.5, 0.0];

        foreach (Rate r in rRates)
        {
            foreach (Rate q in qRates)
            {
                GeneralizedBlackScholesProcess process = new BlackScholesMertonProcess(
                    spot, FlatRate(q, dc, context), FlatRate(r, dc, context), FlatVol(0.4, dc, context));

                Real qdFpNPV = QdFpAmericanEngine.Calculate(option, context, process).NPV!.Value;
                Real qdPlusNPV = QdPlusAmericanEngine.Calculate(option, context, process).NPV!.Value;
                Real fdmNPV = FdBlackScholesVanillaEngine.Calculate(option, context, process, tGrid: 800, xGrid: 200).NPV!.Value;

                const Real tol = 1.5e-2;
                Real diffFdmQqFp = Math.Abs(fdmNPV - qdFpNPV);
                Assert.False(diffFdmQqFp > tol || double.IsNaN(diffFdmQqFp), $"QD fixed point at r {r}, q {q}: diff {diffFdmQqFp}");

                Real diffFdmQdPlus = Math.Abs(fdmNPV - qdPlusNPV);
                Assert.False(diffFdmQdPlus > 5 * tol || double.IsNaN(diffFdmQdPlus), $"QD+ at r {r}, q {q}: diff {diffFdmQdPlus}");
            }
        }
    }

    // A process the QD+ boundary never reads, where QuantLib's case passes a null one.
    private static GeneralizedBlackScholesProcess PlaceholderProcess()
    {
        var today = new Date(1, Month.June, 2022);
        DayCounter dc = new Actual365Fixed();
        return new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.0, dc), new FlatForward(today, 0.0, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.2, dc));
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

    // The QD cases' option: QuantLib's OptionSpec in testQdAmericanEngines.
    private readonly record struct QdOptionSpec(
        OptionType OptionType, Real Spot, Real Strike, Size MaturityInDays, Real Volatility, Real R, Real Q,
        Real ExpectedValue, Real Precision);

    // The test suite's QdFpGaussLobattoScheme: Gauss-Lobatto integration throughout.
    private sealed class QdFpGaussLobattoScheme(Size m, Size n, Real eps) : QdFpIterationScheme
    {
        private readonly Integrator integrator = new GaussLobattoIntegral(100000, double.MaxValue, 0.1 * eps);

        public override int GetNumberOfChebyshevInterpolationNodes() => n;

        public override int GetNumberOfNaiveFixedPointSteps() => m - 1;

        public override int GetNumberOfJacobiNewtonFixedPointSteps() => 1;

        public override Integrator GetFixedPointIntegrator() => integrator;

        public override Integrator GetExerciseBoundaryToPriceIntegrator() => integrator;
    }
}
