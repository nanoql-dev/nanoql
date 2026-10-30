// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Pearson's spread engine where the conditional strike, the second forward at a node plus the strike, is zero or
/// less. QuantLib's integrand takes the call's intrinsic value there whatever the type
/// (<c>pearsonspreadengine.cpp:73-75</c>), so a put prices that intrinsic (136.84 on the plan's market where Choi's
/// engine gives 0.00304, Deng-Li-Zhou's 0.00303 and a quadrature 0.00313). nanoql's put takes its conditional value,
/// 0 there (the S-exo plan's ER5 (ix)): Choi's engine and the put-call parity, which QuantLib's put breaks, are the
/// checks. QuantLib's master makes the same fix after the pin (#2955, <c>8d98cbd3c</c>), its cases below as a check of
/// nanoql's.
/// </summary>
public class PearsonNegativeStrikePutTests
{
    private const Rate R = 0.05;

    [Fact]
    public void APutAtNegativeConditionalStrikesMeetsChoi()
    {
        // the second asset above the first, the strike below minus the second forward over much of its range
        var p1 = Process(50.0, 0.0, R, 0.3);
        var p2 = Process(60.0, 0.0, R, 0.6);
        BasketOption put = Spread(OptionType.Put, -40.0);
        double pearson = PearsonSpreadEngine.Calculate(put, Context, p1, p2, 0.3).NPV!.Value;
        double choi = ChoiBasketEngine.Calculate(put, Context, Both(p1, p2), Rho(0.3)).NPV!.Value;
        Assert.Equal(choi, pearson, 2e-4);
    }

    [Fact]
    public void PutCallParityHoldsAtNegativeConditionalStrikes()
    {
        var p1 = Process(50.0, 0.0, R, 0.3);
        var p2 = Process(60.0, 0.0, R, 0.6);
        double call = PearsonSpreadEngine.Calculate(Spread(OptionType.Call, -40.0), Context, p1, p2, 0.3).NPV!.Value;
        double put = PearsonSpreadEngine.Calculate(Spread(OptionType.Put, -40.0), Context, p1, p2, 0.3).NPV!.Value;
        double df = Flat(R).Discount(Today + 365);
        double f1 = 50.0 / df, f2 = 60.0 / df;
        Assert.Equal(df * (f1 - f2 + 40.0), call - put, 1e-8);
    }

    [Fact]
    public void TestPearsonSpreadEngineNegativeStrikes()
    {
        // Upstream: QuantLib's master's case for #2955 (basketoption.cpp's testPearsonSpreadEngineNegativeStrikes)
        var today = new Date(25, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        Date maturity = today + 365;
        var exercise = new EuropeanExercise(maturity);
        var r = new FlatForward(today, 0.05, dc);
        const double f1 = 100.0, f2 = 100.0;
        const double vol = 0.3;
        BlackProcess MakeProcess(double forward) => new(forward, r, new BlackConstantVol(today, new NullCalendar(), vol, dc));
        BlackProcess process1 = MakeProcess(f1), process2 = MakeProcess(f2);
        double df = r.Discount(maturity);
        double stdDev = vol * Math.Sqrt(dc.YearFraction(today, maturity));

        // Allow for quadrature error and normal-tail truncation at eight standard deviations.
        const double tolerance = 1e-8;

        foreach (double strike in new[] { -5.0, -20.0, -50.0, -80.0, -130.0 })
        {
            var call = new BasketOption(new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, strike)), exercise);
            var put = new BasketOption(new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Put, strike)), exercise);
            double callNPV = PearsonSpreadEngine.Calculate(call, context, process1, process2, 0.5).NPV!.Value;
            double putNPV = PearsonSpreadEngine.Calculate(put, context, process1, process2, 0.5).NPV!.Value;
            double parityPut = callNPV - (df * (f1 - f2 - strike));
            Assert.True(
                Math.Abs(putNPV - parityPut) <= tolerance,
                $"put-call parity failed for strike {strike}: put {putNPV}, expected {parityPut}");

            // (K + F2 - F1)^+ <= (F2 - |K|)^+ since F1 is positive and K is negative.
            double upperBound = df * BlackFormulaFunctions.BlackFormula(OptionType.Call, -strike, f2, stdDev);
            Assert.True(
                putNPV <= upperBound + tolerance,
                $"put exceeds upper bound for strike {strike}: put {putNPV}, upper bound {upperBound}");
        }
    }

    [Fact]
    public void TestPearsonSpreadEngineVanillaLimit()
    {
        // Upstream: QuantLib's master's case for #2955 (basketoption.cpp's testPearsonSpreadEngineVanillaLimit)
        var today = new Date(25, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        Date maturity = today + 365;
        var exercise = new EuropeanExercise(maturity);
        var r = new FlatForward(today, 0.05, dc);
        const double f1 = 100.0, f2 = 100.0;
        const double vol = 0.3;
        BlackProcess MakeProcess(double forward, double volatility) =>
            new(forward, r, new BlackConstantVol(today, new NullCalendar(), volatility, dc));
        BlackProcess process1 = MakeProcess(f1, vol), process2 = MakeProcess(f2, 0.0);
        double df = r.Discount(maturity);
        double stdDev = vol * Math.Sqrt(dc.YearFraction(today, maturity));
        const double tolerance = 1e-8;

        // A deterministic F2 reduces the spread payoff to a vanilla payoff on F1.
        foreach (double strike in new[] { -130.0, -100.0, -80.0, 5.0, 80.0 })
        {
            foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
            {
                var option = new BasketOption(new SpreadBasketPayoff(new PlainVanillaPayoff(type, strike)), exercise);
                double vanillaStrike = f2 + strike;
                double expected = vanillaStrike > 0.0
                    ? df * BlackFormulaFunctions.BlackFormula(type, vanillaStrike, f1, stdDev)
                    : (type == OptionType.Call ? df * (f1 - vanillaStrike) : 0.0);
                double calculated = PearsonSpreadEngine.Calculate(option, context, process1, process2, 0.5).NPV!.Value;
                Assert.True(
                    Math.Abs(calculated - expected) <= tolerance,
                    $"vanilla limit failed for {type} with strike {strike}: calculated {calculated}, expected {expected}");
            }
        }
    }
}
