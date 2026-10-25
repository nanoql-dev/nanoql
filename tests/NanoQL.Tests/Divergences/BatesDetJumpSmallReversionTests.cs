// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The deterministic-intensity Bates engines at small κ_λ (the S-hes plan's HR7 (iv)). Their term weights the
/// constant-intensity term by the intensity's average, (x − 1 + e^{−x})θ_λ/(xλ) + (1 − e^{−x})/x at x = κ_λt, which
/// QuantLib writes as differences that cancel as x falls (<c>batesengine.cpp:80</c>, <c>:140</c>): with θ_λ 0.6 beside
/// λ 0.3 a call prices 9.8118999810864693 at κ_λ = 1e-12 where the constant-intensity engine, its limit, gives
/// 9.8119169388898158 [executed: <c>scratchpad\cxxprobe\src\c1oracle.cpp</c>]. Below x = 1e-2 nanoql writes the weights
/// as x·φ₂(−x) and −expm1(−x)/x, which don't cancel, QuantLib's expressions and bits at and above it. Checks: the limit,
/// and, since the term is linear in the intensity, the price equal to the constant-intensity engine's at
/// λ_eff = θ_λ·w₁ + λ·w₂, the weights w₁ = (x − 1 + e^{−x})/x and w₂ = (1 − e^{−x})/x evaluated in 50 digits
/// (Python's <c>decimal</c>), either side of the cut.
/// </summary>
public class BatesDetJumpSmallReversionTests
{
    private static readonly Date Today = new(15, March, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    // a year on Act/365, so x = kappaLambda
    private static readonly VanillaOption Call =
        new(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));

    [Fact]
    public void TheDetJumpPriceTendsToTheConstantIntensityOne()
    {
        double constant = BatesEngine.Calculate(Call, Context, new BatesModel(Process(0.3))).NPV!.Value;
        foreach (double kappaLambda in new[] { 1e-12, 1e-15 })
        {
            double detJump = BatesDetJumpEngine.Calculate(Call, Context, new BatesDetJumpModel(Process(0.3), kappaLambda, 0.6))
                .NPV!.Value;
            Assert.True(
                Math.Abs(detJump - constant) < 1e-11 * constant, $"kappaLambda {kappaLambda}: {detJump} against {constant}");
        }
    }

    [Theory]
    [InlineData(1e-9, 0.30000000015)]
    [InlineData(1e-6, 0.30000014999995)]
    [InlineData(1e-3, 0.3001499500124975)]
    [InlineData(0.00999, 0.30149352243267874)]
    [InlineData(0.01, 0.3014950124750416)]
    [InlineData(0.1, 0.3145122541078787)]
    public void TheWeightsAreTheirFiftyDigitValues(double kappaLambda, double effectiveLambda)
    {
        // lambda_eff = 0.6·w1 + 0.3·w2 from the 50-digit weights
        double detJump = BatesDetJumpEngine.Calculate(Call, Context, new BatesDetJumpModel(Process(0.3), kappaLambda, 0.6))
            .NPV!.Value;
        double constant = BatesEngine.Calculate(Call, Context, new BatesModel(Process(effectiveLambda))).NPV!.Value;
        Assert.True(Math.Abs(detJump - constant) < 1e-12 * constant, $"kappaLambda {kappaLambda}: {detJump} against {constant}");
    }

    // r 5%, q 2%, v0 = theta 0.04, kappa 1.5, sigma 0.3, rho -0.5; nu -0.1, delta 0.15
    private static BatesProcess Process(double lambda) =>
        new(new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.3, -0.5, lambda, -0.1, 0.15);
}
