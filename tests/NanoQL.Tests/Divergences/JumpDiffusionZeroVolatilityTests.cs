// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The jump-diffusion engine's vega and theta at zero diffusive volatility. For the no-jump term v₀ = 0, so its vega
/// weight √(variance/t)/v₀ and its theta's jump term i·jumpSquareVol/(2v₀t²) are 0/0
/// (<c>jumpdiffusionengine.cpp:110</c>, <c>:123-127</c>): QuantLib's vega and theta are NaN at σ = 0 (the C++ harness).
/// nanoql takes their limits, 1 and 0, the values they take at any positive volatility. QuantLib's check: its own
/// Greeks at σ = 1e-12, the sum taken to 1e-12, and the limit they approach from σ = 1e-8.
/// </summary>
public class JumpDiffusionZeroVolatilityTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AtZeroVolatilityTheVegaAndThetaAreTheirLimits()
    {
        // q 2%, r 5%, one jump a year of mean log -0.1 and volatility 0.25, a one-year call at 100, the sum to 1e-12;
        // QuantLib at σ = 0: NPV 10.527076279129314, vega and theta NaN; at σ = 1e-12: NPV 10.527076279129318, vega
        // 8.07152874082114e-11 and theta -6.9167245456958035, at σ = 1e-8 vega 8.071528740821137e-07 and theta
        // -6.916724545695806 (the no-jump term's vega is of the order of σ)
        var process = new Merton76Process(
            100.0,
            new FlatForward(Today, 0.02, A365),
            new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.0, A365),
            1.0,
            -0.1,
            0.25);
        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        OneAssetOptionResults results = JumpDiffusionEngine.Calculate(option, Context, process, 1e-12, 200);
        Assert.True(Math.Abs(results.Vega!.Value) <= 1e-10, $"vega {results.Vega}");
        Assert.Equal(0.0, (results.Theta!.Value / -6.9167245456958035) - 1.0, 1e-12);
        Assert.Equal(0.0, (results.NPV!.Value / 10.527076279129314) - 1.0, 1e-14);
    }
}
