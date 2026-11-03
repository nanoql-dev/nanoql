// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A double-barrier binary knock-in already knocked in (fixed). It pays its cash at expiry, where the payoff is in the
/// money, as QuantLib's own header says ("the Double Knock In pays at end",
/// <c>analyticdoublebarrierbinaryengine.hpp:38-41</c>); QuantLib's degenerate branch pays the cash undiscounted, 10.0
/// at a spot beyond the barrier where its formula's limit just inside is cash × D(T), and whatever the payoff's strike
/// and type (<c>.cpp:237-246</c>). nanoql prices it as the European cash-or-nothing it is, at the payoff's strike and
/// type: cash × D(T) at QuantLib's strike-0 call. QuantLib's check: its analytic European engine on the payoff, and the
/// engine's own value a hair inside the corridor. QuantLib's <c>testHaugValues</c> checks at lines 171-172 move here.
/// </summary>
public class DoubleBinaryKnockedInTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly EuropeanExercise Exercise = new(Today + 90);

    [Fact]
    public void QuantLibsStrikeZeroCallPaysTheCashDiscounted()
    {
        // testHaugValues' degenerate knock-ins: QuantLib 10.0 at spots 80 and 110 on [95, 105]
        foreach (double spot in new[] { 80.0, 110.0 })
        {
            GeneralizedBlackScholesProcess process = Process(spot);
            var payoff = new CashOrNothingPayoff(OptionType.Call, 0.0, 10.0);
            var option = new DoubleBarrierOption(DoubleBarrierType.KnockIn, 95.0, 105.0, 0.0, payoff, Exercise);
            double calculated = AnalyticDoubleBarrierBinaryEngine.Calculate(option, Context, process).NPV!.Value;
            double european = AnalyticEuropeanEngine.Calculate(
                new EuropeanOption(payoff, Exercise), Context, process).NPV!.Value;
            Assert.Equal(10.0 * process.RiskFreeRate.Discount(Exercise.LastDate()), calculated, 1e-14);
            Assert.Equal(european, calculated, 1e-14);
        }

        // continuity: a hair inside the corridor the formula gives the same, 9.8758 at 0.25 years (QuantLib's own
        // checks a knock-in at spot 100 on [95, 105] at volatility 0.5 to 9.8758)
        var inside = new DoubleBarrierOption(
            DoubleBarrierType.KnockIn, 95.0, 105.0, 0.0, new CashOrNothingPayoff(OptionType.Call, 0.0, 10.0), Exercise);
        double hair = AnalyticDoubleBarrierBinaryEngine.Calculate(inside, Context, Process(95.0 * (1 + 1e-12))).NPV!.Value;
        Assert.Equal(10.0 * Process(95.0).RiskFreeRate.Discount(Exercise.LastDate()), hair, 1e-9);
    }

    [Fact]
    public void AStruckPutKnockedInIsItsEuropean()
    {
        // QuantLib pays 10.0 for a put struck at 100 knocked in at 110, which ends out of the money with probability
        // above one half
        GeneralizedBlackScholesProcess process = Process(110.0);
        var payoff = new CashOrNothingPayoff(OptionType.Put, 100.0, 10.0);
        var option = new DoubleBarrierOption(DoubleBarrierType.KnockIn, 95.0, 105.0, 0.0, payoff, Exercise);
        double calculated = AnalyticDoubleBarrierBinaryEngine.Calculate(option, Context, process).NPV!.Value;
        double european = AnalyticEuropeanEngine.Calculate(new EuropeanOption(payoff, Exercise), Context, process).NPV!.Value;
        Assert.Equal(european, calculated, 1e-14);
        Assert.True(calculated < 5.0, $"{calculated}");
    }

    private static GeneralizedBlackScholesProcess Process(double spot) => new BlackScholesMertonProcess(
        spot,
        new FlatForward(Today, 0.02, A360),
        new FlatForward(Today, 0.05, A360),
        new BlackConstantVol(Today, new NullCalendar(), 0.10, A360));
}
