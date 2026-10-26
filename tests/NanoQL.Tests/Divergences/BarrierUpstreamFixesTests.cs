// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The fixes QuantLib's master made to the barrier family after the pin, which nanoql takes (the S-bar plan's BR6),
/// each with the master's own case. #2876 (<c>5f4811e7e</c>): a start-window knock-in put prices the European on the
/// call payoff and process the put-call symmetry builds, where 1.44 prices the put's, so in-out parity fails off the
/// money. #2880 (<c>ca8e70946</c>): Dr78's degenerate correlations, taken with S-exo's bivariate normal; the partial-time
/// engine finite where d2 is near 0 on a term-structure market. #2881 (<c>014321f39</c>): double knock-outs struck
/// outside the corridor integrate only over terminal spots inside it, and price 0 beyond the far barrier; and a
/// one-term series at low volatility. #2883 (<c>b9bb7bedb</c>): the soft barrier against the average of single-barrier
/// options with barriers in [L, U], by Simpson's rule over QuantLib's analytic barrier engine; the master refuses the
/// directions Haug's form doesn't cover, which nanoql prices by the average (BR7 (b)), so the case asserts the average
/// there too. #2878 (<c>eaeac5651</c>): the binomial barrier asset's American window bounded by <c>isOnTime</c> at both
/// ends, so the exercise at expiry isn't skipped.
/// </summary>
public class BarrierUpstreamFixesTests
{
    [Fact]
    public void TestInOutParity()
    {
        // Upstream: QuantLib's master's case for #2876 (partialtimebarrieroption.cpp's testInOutParity)
        var today = new Date(1, January, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        var exercise = new EuropeanExercise(today + 182);
        Date coverEventDate = today + 91;
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.04, dc), new FlatForward(today, 0.08, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.25, dc));

        (BarrierType KnockIn, BarrierType KnockOut, double Barrier)[] cases =
        [
            (BarrierType.DownIn, BarrierType.DownOut, 85.0),
            (BarrierType.UpIn, BarrierType.UpOut, 115.0),
        ];

        foreach (OptionType type in new[] { OptionType.Put, OptionType.Call })
        {
            foreach (double strike in new[] { 80.0, 100.0, 130.0 })
            {
                var payoff = new PlainVanillaPayoff(type, strike);
                double vanillaValue = AnalyticEuropeanEngine.Calculate(
                    new VanillaOption(payoff, exercise), context, process).NPV!.Value;

                foreach (var c in cases)
                {
                    var knockIn = new PartialTimeBarrierOption(
                        c.KnockIn, PartialBarrierRange.Start, c.Barrier, 0.0, coverEventDate, payoff, exercise);
                    var knockOut = new PartialTimeBarrierOption(
                        c.KnockOut, PartialBarrierRange.Start, c.Barrier, 0.0, coverEventDate, payoff, exercise);
                    double expected = vanillaValue
                        - AnalyticPartialTimeBarrierOptionEngine.Calculate(knockOut, context, process).NPV!.Value;
                    double calculated = AnalyticPartialTimeBarrierOptionEngine.Calculate(knockIn, context, process).NPV!.Value;
                    Assert.True(
                        Math.Abs(calculated - expected) <= 1e-10,
                        $"{BarrierTypeFunctions.ToString(c.KnockIn)} {type} strike {strike}: {calculated} for {expected}");
                }
            }
        }
    }

    [Fact]
    public void TestAnalyticEngineNearZeroD2()
    {
        // Upstream: QuantLib's master's case for #2880 (partialtimebarrieroption.cpp's testAnalyticEngineNearZeroD2)
        var today = new Date(1, January, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        int[] pillars = [91, 182, 365, 730, 1095];
        double[] rates = [0.01, 0.03, 0.05, 0.02, 0.04];
        double[] instantVols = [0.30, 0.22, 0.20, 0.25, 0.20];
        List<Date> dates = [today];
        List<Date> volDates = [];
        List<double> forwards = [rates[0]];
        List<double> yields = [rates[0] - (0.5 * instantVols[0] * instantVols[0])];
        List<double> blackVols = [];
        double variance = 0.0;
        double previous = 0.0;
        for (int i = 0; i < pillars.Length; ++i)
        {
            Date date = today + pillars[i];
            double t = dc.YearFraction(today, date);
            variance += instantVols[i] * instantVols[i] * (t - previous);
            previous = t;
            dates.Add(date);
            volDates.Add(date);
            forwards.Add(rates[i]);

            // Carry equals half the variance rate, so d2 cancels to near zero.
            yields.Add(rates[i] - (0.5 * instantVols[i] * instantVols[i]));
            blackVols.Add(Math.Sqrt(variance / t));
        }

        var process = new BlackScholesMertonProcess(
            100.0,
            new ForwardCurve(dates, yields, dc),
            new ForwardCurve(dates, forwards, dc),
            new BlackVarianceCurve(today, volDates, blackVols, dc, false));
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        var exercise = new EuropeanExercise(today + 548);
        double Price(BarrierType type, PartialBarrierRange range) => AnalyticPartialTimeBarrierOptionEngine.Calculate(
            new PartialTimeBarrierOption(type, range, 85.0, 0.0, today + 274, payoff, exercise), context, process).NPV!.Value;
        Assert.True(double.IsFinite(Price(BarrierType.DownOut, PartialBarrierRange.Start)));
        Assert.True(double.IsFinite(Price(BarrierType.DownIn, PartialBarrierRange.Start)));
        Assert.True(double.IsFinite(Price(BarrierType.DownOut, PartialBarrierRange.EndB1)));
    }

    [Fact]
    public void TestStrikesOutsideCorridor()
    {
        // Upstream: QuantLib's master's case for #2881 (doublebarrieroption.cpp's testStrikesOutsideCorridor)
        var today = new Date(1, January, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.04, dc), new FlatForward(today, 0.08, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.25, dc));
        const double lower = 80.0, upper = 120.0;
        const double tolerance = 1.0e-10;

        (OptionType Type, double Strike, int Days)[] cases =
        [
            (OptionType.Call, 70.0, 365), (OptionType.Call, 72.0, 182),
            (OptionType.Put, 132.0, 182), (OptionType.Put, 130.0, 365),
            (OptionType.Call, 132.0, 365), (OptionType.Put, 70.0, 365),
            (OptionType.Call, 120.0, 365), (OptionType.Put, 80.0, 365),
        ];

        foreach (var c in cases)
        {
            var exercise = new EuropeanExercise(today + c.Days);
            var payoff = new PlainVanillaPayoff(c.Type, c.Strike);
            double expected = 0.0;
            if ((c.Type == OptionType.Call && c.Strike < lower) || (c.Type == OptionType.Put && c.Strike > upper))
            {
                double boundary = c.Type == OptionType.Call ? lower : upper;
                var atBoundary = new DoubleBarrierOption(
                    DoubleBarrierType.KnockOut, lower, upper, 0.0, new PlainVanillaPayoff(c.Type, boundary), exercise);
                var noTouch = new DoubleBarrierOption(
                    DoubleBarrierType.KnockOut, lower, upper, 0.0, new CashOrNothingPayoff(OptionType.Call, 0.0, 1.0), exercise);

                // On survival, C(K) = C(L) + (L-K) and P(K) = P(U) + (K-U).
                expected = AnalyticDoubleBarrierEngine.Calculate(atBoundary, context, process).NPV!.Value
                    + (Math.Abs(c.Strike - boundary)
                        * AnalyticDoubleBarrierBinaryEngine.Calculate(noTouch, context, process).NPV!.Value);
            }

            var knockOut = new DoubleBarrierOption(DoubleBarrierType.KnockOut, lower, upper, 0.0, payoff, exercise);
            double calculated = AnalyticDoubleBarrierEngine.Calculate(knockOut, context, process).NPV!.Value;
            Assert.True(
                Math.Abs(calculated - expected) < tolerance,
                $"{c.Type} strike {c.Strike}, {c.Days} days: expected {expected}, calculated {calculated}");

            double vanilla = AnalyticEuropeanEngine.Calculate(new VanillaOption(payoff, exercise), context, process).NPV!.Value;
            var knockIn = new DoubleBarrierOption(DoubleBarrierType.KnockIn, lower, upper, 0.0, payoff, exercise);
            Assert.True(
                Math.Abs(AnalyticDoubleBarrierEngine.Calculate(knockIn, context, process).NPV!.Value - (vanilla - expected)) < tolerance,
                $"Knock-in parity failed for {c.Type} strike {c.Strike}");
        }
    }

    [Fact]
    public void TestLowVolatility()
    {
        // Upstream: QuantLib's master's case for #2881 (doublebarrieroption.cpp's testLowVolatility)
        var today = new Date(1, January, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        var exercise = new EuropeanExercise(today + 365);
        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            double rate = type == OptionType.Call ? 0.05 : -0.05;
            var process = new BlackScholesMertonProcess(
                100.0, new FlatForward(today, 0.0, dc), new FlatForward(today, rate, dc),
                new BlackConstantVol(today, new NullCalendar(), 0.01, dc));
            var payoff = new PlainVanillaPayoff(type, 100.0);
            var option = new DoubleBarrierOption(DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, payoff, exercise);
            double value = AnalyticDoubleBarrierEngine.Calculate(option, context, process, 1).NPV!.Value;
            double vanilla = AnalyticEuropeanEngine.Calculate(new VanillaOption(payoff, exercise), context, process).NPV!.Value;

            // Barrier-hit probabilities are negligible at this volatility, so the
            // vanilla prices (4.87705760207 and 5.12710969242) are independent references.
            Assert.True(Math.Abs(value - vanilla) < 1.0e-10, $"{type}: expected {vanilla}, calculated {value}");
        }
    }

    [Fact]
    public void TestSoftBarrierDirection()
    {
        // Upstream: QuantLib's master's case for #2883 (softbarrieroption.cpp's testSoftBarrierDirection), the
        // directions it refuses asserted as the average too (BR7 (b))
        DayCounter dc = new Actual360();
        var today = new Date(8, August, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var process = new GeneralizedBlackScholesProcess(
            100.0, new FlatForward(today, 0.04, dc), new FlatForward(today, 0.08, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.25, dc));
        var exercise = new EuropeanExercise(today + 360);

        // a soft barrier option is the average of the single-barrier
        // options with barriers in [L, U]; Simpson's rule.
        double Averaged(BarrierType barrierType, StrikedTypePayoff payoff, double l, double u)
        {
            const int n = 128;
            double h = (u - l) / n;
            double sum = 0.0;
            for (int i = 0; i <= n; ++i)
            {
                var option = new BarrierOption(barrierType, l + (i * h), 0.0, payoff, exercise);
                double weight = (i == 0 || i == n) ? 1.0 : (i % 2 == 1 ? 4.0 : 2.0);
                sum += weight * AnalyticBarrierEngine.Calculate(option, context, process).NPV!.Value;
            }

            return sum * h / 3.0 / (u - l);
        }

        (BarrierType BarrierType, double L, double U)[] cases =
        [
            (BarrierType.DownIn, 80.0, 90.0),
            (BarrierType.DownOut, 80.0, 90.0),
            (BarrierType.UpIn, 110.0, 120.0),
            (BarrierType.UpOut, 110.0, 120.0),
        ];

        const double tolerance = 1e-6;
        foreach (var c in cases)
        {
            foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
            {
                var payoff = new PlainVanillaPayoff(type, 100.0);
                var option = new SoftBarrierOption(c.BarrierType, c.L, c.U, payoff, exercise);
                double calculated = AnalyticSoftBarrierEngine.Calculate(option, context, process).NPV!.Value;
                double expected = Averaged(c.BarrierType, payoff, c.L, c.U);
                Assert.True(
                    Math.Abs(calculated - expected) <= tolerance,
                    $"the averaged value of a {BarrierTypeFunctions.ToString(c.BarrierType)} {type} option: calculated "
                    + $"{calculated}, expected {expected}");
            }
        }
    }

    [Fact]
    public void TestBinomialExerciseAtMaturity()
    {
        // Upstream: QuantLib's master's case for #2878 (barrieroption.cpp's testBinomialExerciseAtMaturity)
        var today = new Date(21, September, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        Date maturity = today + 91;
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.0, dc), new FlatForward(today, 0.05, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.15, dc));
        var payoff = new PlainVanillaPayoff(OptionType.Call, 85.0);
        var europeanExercise = new EuropeanExercise(maturity);
        var americanExercise = new AmericanExercise(today, maturity);
        const int timeSteps = 51;
        double expected = BinomialVanillaEngine.Calculate<CoxRossRubinstein>(
            new EuropeanOption(payoff, europeanExercise), context, process, timeSteps).NPV!.Value;
        const double tolerance = 1.0e-10;

        // All stock nodes lie between 58 and 171, so neither barrier can knock out.
        foreach (BarrierType type in new[] { BarrierType.DownOut, BarrierType.UpOut })
        {
            double barrier = type == BarrierType.DownOut ? 1.0 : 1000.0;

            // Disable Boyle-Lau adjustment to keep the same tree as the vanilla reference.
            double europeanValue = BinomialBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedBarrierOption>(
                new BarrierOption(type, barrier, 0.0, payoff, europeanExercise), context, process, timeSteps, timeSteps).NPV!.Value;
            double americanValue = BinomialBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedBarrierOption>(
                new BarrierOption(type, barrier, 0.0, payoff, americanExercise), context, process, timeSteps, timeSteps).NPV!.Value;
            Assert.True(Math.Abs(europeanValue - expected) <= tolerance, $"{type}: European {europeanValue} for {expected}");
            Assert.True(americanValue >= expected - tolerance, $"{type}: American {americanValue} below {expected}");
            Assert.True(Math.Abs(americanValue - expected) <= tolerance, $"{type}: American {americanValue} for {expected}");
        }
    }
}
