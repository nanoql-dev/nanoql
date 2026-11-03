// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The jump-diffusion engine on structures dated differently. QuantLib rebuilds each term's flat curve and constant
/// volatility on the risk-free curve's reference date with the time measured from the volatility's
/// (<c>jumpdiffusionengine.cpp:62-67</c>, <c>:113-114</c>), so with no jumps it isn't the Black-Scholes price on a
/// curve settling T+2. nanoql builds both on the volatility's reference date; its rho is then per unit of the risk-free
/// curve's own rate over its own time, and theta's risk-free term runs over that time, as the analytic European
/// engine's. QuantLib's check: with no jumps, its analytic European engine on the same market, every output but the
/// theta, which is the analytic engine's (<c>Divergences/AnalyticThetaMismatchedTimesTests.cs</c>) (QuantLib-Python
/// 1.44, QuantLib's jump-diffusion engine emulated by its rebuilt structures). Where the structures share their dates
/// and counts, QuantLib's bits (the engine's harness vectors).
/// </summary>
public class JumpDiffusionReferenceDateTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void WithNoJumpsARiskFreeCurveSettlingLaterGivesTheAnalyticResults()
    {
        // q 2% and volatility 25% today, r 5% settling T+2, a one-year call at 100; QuantLib's engine: 11.020011829482998,
        // delta 0.5832814671918956, gamma 0.015279441513755852, vega 37.77998894839629, rho 46.789689575846765,
        // dividend rho -58.328146719189554
        OneAssetOptionResults results = Price(Settling(0.05), new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        double[] analytic =
            [11.097820498934109, 0.5841229496259692, 0.015187352441370719, 37.96838110342679, 46.79595967501987, -58.41229496259685];
        double[] calculated =
            [results.NPV!.Value, results.Delta!.Value, results.Gamma!.Value, results.Vega!.Value, results.Rho!.Value, results.DividendRho!.Value];
        for (int i = 0; i < analytic.Length; i++)
        {
            Assert.True(Math.Abs(calculated[i] - analytic[i]) <= 1e-13 * Math.Abs(analytic[i]), $"{i}: {calculated[i]} against {analytic[i]}");
        }

        double theta = AnalyticEuropeanEngine.Calculate(Call(), Context, BlackScholes(Settling(0.05), new BlackConstantVol(Today, new NullCalendar(), 0.25, A365)))
            .Theta!.Value;
        Assert.Equal(0.0, (results.Theta!.Value / theta) - 1.0, 1e-13);
    }

    [Fact]
    public void WithNoJumpsAVolatilitySettlingLaterGivesTheAnalyticValue()
    {
        // the volatility settling T+2 and both curves today: QuantLib's analytic engine 11.071634877423028, its
        // jump-diffusion engine 11.15002267133434
        BlackVolTermStructure vol = BlackConstantVol.Create(2, new TARGET(), 0.25, A365, Context);
        OneAssetOptionResults results = Price(new FlatForward(Today, 0.05, A365), vol);
        Assert.True(Math.Abs(results.NPV!.Value - 11.071634877423028) <= 1e-13 * 11.071634877423028, $"{results.NPV}");
        OneAssetOptionResults analytic = AnalyticEuropeanEngine.Calculate(Call(), Context, BlackScholes(new FlatForward(Today, 0.05, A365), vol));
        Assert.Equal(0.0, (results.Rho!.Value / analytic.Rho!.Value) - 1.0, 1e-13);
        Assert.Equal(0.0, (results.Theta!.Value / analytic.Theta!.Value) - 1.0, 1e-13);
    }

    private static YieldTermStructure Settling(double rate) => FlatForward.Create(2, new TARGET(), rate, A365, Context);

    private static BlackScholesMertonProcess BlackScholes(YieldTermStructure riskFree, BlackVolTermStructure vol) =>
        new(100.0, new FlatForward(Today, 0.02, A365), riskFree, vol);

    private static OneAssetOptionResults Price(YieldTermStructure riskFree, BlackVolTermStructure vol) =>
        JumpDiffusionEngine.Calculate(
            Call(), Context, new Merton76Process(100.0, new FlatForward(Today, 0.02, A365), riskFree, vol, 0.0, 0.0, 0.0));

    private static VanillaOption Call() =>
        new(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
}
