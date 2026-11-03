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
/// The Black-Vasicek engine's equity volatility and dividends. QuantLib reads the equity volatility at time 0
/// (<c>blackVol(t, K)</c> with t = 0, <c>analyticeuropeanvasicekengine.cpp:78</c>) and leaves the dividend curve out,
/// so a volatility curve rising from 10% to 30% prices on 10% and a dividend yield changes nothing. nanoql integrates
/// the equity's term variance over [0, T] and takes the dividend discount into the forward. QuantLib's check: the
/// engine's limit as the short rate's volatility vanishes, QuantLib's analytic European engine on the Vasicek bond's
/// flat rate with the term volatility and the dividend curve (QuantLib-Python 1.44; QuantLib's own engine through the
/// harness). On a flat volatility and no dividend, QuantLib's bits (the engine's harness vectors). QuantLib's master
/// fixes the same class after v1.44 (#2927: the volatility at maturity, the spot discounted by the dividend curve),
/// whose two cases <c>EuropeanOptionTests</c> ports as checks of this engine; it stays until the pin moves to a release
/// carrying the fix.
/// </summary>
public class BlackVasicekVolatilityTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void TheEquityVolatilityIsItsTermVariance()
    {
        // a five-year call at 100 on the curve 10%, 20%, 30% at one, three and five years; QuantLib prices on 10%,
        // 23.42105725119137
        AssertClose(35.957806538443236, Price(0.0));
    }

    [Fact]
    public void TheDividendsEnterTheForward()
    {
        // the same with a 3% dividend yield, which QuantLib drops: 23.42105725119137 again
        AssertClose(25.858781441404837, Price(0.03));
    }

    // r0 5%, a 0.1, b 5%, the short rate's volatility 1e-12, no market price of risk, no correlation
    private static double Price(double dividendYield)
    {
        var volatility = new BlackVarianceCurve(Today, [Today + 365, Today + 1095, Today + 1825], [0.10, 0.20, 0.30], A365, true);
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, dividendYield, A365), new FlatForward(Today, 0.05, A365), volatility);
        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 1825));
        return AnalyticBlackVasicekEngine.Calculate(option, Context, process, new Vasicek(0.05, 0.1, 0.05, 1e-12, 0.0), 0.0)
            .NPV!.Value;
    }

    private static void AssertClose(double limit, double calculated) =>
        Assert.True(Math.Abs(calculated - limit) <= 1e-10 * limit, $"{calculated} against QuantLib's limit {limit}");
}
