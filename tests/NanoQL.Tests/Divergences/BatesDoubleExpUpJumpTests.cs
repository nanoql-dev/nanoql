// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The double-exponential Bates models where the up-jumps' mean size η_u reaches 1 (a refusal). The up-jumps'
/// exponential moment, which the model's drift compensates, is finite for η_u below 1 only ("the condition η1 &gt; 1 is
/// necessary and sufficient for the random variable J_t to have finite expectation", Gulisashvili and Vives, η1 =
/// 1/η_u), yet QuantLib's engine prices beyond it: a 100 call 3,040,812 at η_u = 1.01, a plausible 26.39 at 1.5, NaN at
/// 1 (QuantLib 1.44). nanoql's engines refuse a model at η_u ≥ 1, the models' constraint on η_u is (0, 1), so a
/// construction or a calibration step there is refused, and below 1 QuantLib's values stand.
/// </summary>
public class BatesDoubleExpUpJumpTests
{
    private static readonly Date Today = new(15, March, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    private static readonly HestonProcess Heston = new(
        new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.3, -0.5);

    private static readonly VanillaOption Call =
        new(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));

    [Fact]
    public void AnUpJumpMeanOfOneOrMoreIsRefused()
    {
        const string Message = "the double exponential model's up-jump mean must be below 1";
        foreach (double nuUp in new[] { 1.0, 1.01, 1.5 })
        {
            // arguments: theta, kappa, sigma, rho, v0, p, nuDown, nuUp, lambda (and kappaLambda, thetaLambda)
            var model = (BatesDoubleExpModel)new BatesDoubleExpModel(Heston, 0.3, 0.1, 0.1, 0.4)
                .WithParams(new Vector([0.04, 1.5, 0.3, -0.5, 0.04, 0.4, 0.1, nuUp, 0.3]));
            ArgumentException refused = Assert.Throws<ArgumentException>(() => BatesDoubleExpEngine.Calculate(Call, Context, model));
            Assert.Equal("model", refused.ParamName);
            Assert.StartsWith(Message, refused.Message, StringComparison.Ordinal);

            var detJump = (BatesDoubleExpDetJumpModel)new BatesDoubleExpDetJumpModel(Heston, 0.3, 0.1, 0.1, 0.4, 1.0, 0.2)
                .WithParams(new Vector([0.04, 1.5, 0.3, -0.5, 0.04, 0.4, 0.1, nuUp, 0.3, 1.0, 0.2]));
            refused = Assert.Throws<ArgumentException>(() => BatesDoubleExpDetJumpEngine.Calculate(Call, Context, detJump));
            Assert.Equal("model", refused.ParamName);
            Assert.StartsWith(Message, refused.Message, StringComparison.Ordinal);

            ArgumentException constructed = Assert.Throws<ArgumentException>(() => new BatesDoubleExpModel(Heston, 0.3, nuUp));
            Assert.Equal("nuUp", constructed.ParamName);
        }
    }

    [Fact]
    public void TheConstraintRefusesACalibrationStepThere()
    {
        var model = new BatesDoubleExpModel(Heston, 0.3, 0.1, 0.1, 0.4);
        Assert.True(model.Constraint.Test(new Vector([0.04, 1.5, 0.3, -0.5, 0.04, 0.4, 0.1, 0.999, 0.3])));
        Assert.False(model.Constraint.Test(new Vector([0.04, 1.5, 0.3, -0.5, 0.04, 0.4, 0.1, 1.0, 0.3])));
        Assert.False(model.Constraint.Test(new Vector([0.04, 1.5, 0.3, -0.5, 0.04, 0.4, 0.1, 1.2, 0.3])));
        Assert.False(model.Constraint.Test(new Vector([0.04, 1.5, 0.3, -0.5, 0.04, 0.4, 0.1, 0.0, 0.3])));
    }

    [Theory]
    [InlineData(0.1, 9.4925616381806748, 9.4346236004554598)]
    [InlineData(0.5, 13.595710512177654, 12.970057750377094)]
    [InlineData(0.9, 60.954362277281064, 56.700684218982886)]
    public void BelowOneQuantLibsValuesStand(double nuUp, double quantLib, double quantLibDetJump)
    {
        // QuantLib's engines, Gauss-Laguerre 144; lambda 0.3, nuDown 0.1, p 0.4; the det-jump kappaLambda 1, thetaLambda 0.2
        double npv = BatesDoubleExpEngine.Calculate(Call, Context, new BatesDoubleExpModel(Heston, 0.3, nuUp, 0.1, 0.4)).NPV!.Value;
        Assert.True(Math.Abs(npv - quantLib) <= 1e-10 * quantLib, $"{npv} against QuantLib's {quantLib}");
        double detJump = BatesDoubleExpDetJumpEngine.Calculate(
            Call, Context, new BatesDoubleExpDetJumpModel(Heston, 0.3, nuUp, 0.1, 0.4, 1.0, 0.2)).NPV!.Value;
        Assert.True(Math.Abs(detJump - quantLibDetJump) <= 1e-10 * quantLibDetJump, $"{detJump} against QuantLib's {quantLibDetJump}");
    }
}
