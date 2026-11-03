// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The rough Heston engine's time form of the price. QuantLib's <c>priceVanillaPayoff(payoff, Time)</c> reads both
/// curves at the risk-free curve's time (<c>analyticroughhestonengine.cpp:594-600</c>), where its date form reads each
/// by date: on a dividend curve on another day counter the two forms of one maturity price apart. nanoql's time form is
/// on the model's clock, each curve read at the instant the time denotes, so a time and the date it denotes price
/// alike, the date form QuantLib's.
/// </summary>
public class RoughHestonTimeFormTests
{
    [Fact]
    public void ATimeAndTheDateItDenotesPriceAlike()
    {
        // the risk-free curve on Actual/360 and the dividend curve on Actual/365 (Fixed), both dated today: QuantLib
        // 1.44's date form prices 4.542308190960499 and 2.0498193225348307 (QuantLib-Python 1.44); its time form at the
        // risk-free curve's times 1.01389 and 2.02778 reads the dividend curve at those times on Actual/365 (Fixed),
        // 1.4 and 2.8 days past the instants
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        var rTS = new FlatForward(today, 0.03, new Actual360());
        var qTS = new FlatForward(today, 0.06, new Actual365Fixed());
        var engine = new AnalyticRoughHestonEngine(new RoughHestonModel(new HestonProcess(rTS, qTS, 100.0, 0.04, 0.3, 0.04, 0.4, -0.7), 0.1), 128, 256);

        foreach ((double strike, int days, double quantLib) in new[] { (100.0, 365, 4.542308190960499), (110.0, 730, 2.0498193225348307) })
        {
            var payoff = new PlainVanillaPayoff(OptionType.Call, strike);
            Date maturity = today + days;
            double byDate = engine.PriceVanillaPayoff(payoff, maturity);
            Assert.Equal(quantLib, byDate, 1e-12);
            Assert.Equal(byDate, engine.Calculate(new VanillaOption(payoff, new EuropeanExercise(maturity)), context).NPV);

            double byTime = engine.PriceVanillaPayoff(payoff, rTS.TimeFromReference(maturity));
            Assert.True(Math.Abs((byTime / byDate) - 1.0) < 1e-13, $"{strike}: {byTime} against {byDate}");
        }
    }
}
