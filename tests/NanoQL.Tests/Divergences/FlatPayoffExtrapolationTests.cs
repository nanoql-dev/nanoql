// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Flat payoff extrapolation's right tail (the S-g1d plan's G1R6 (xxi)): with <c>flatPayoffExtrapolation</c>,
/// QuantLib's Gaussian1d engines integrate past the grid's last node the constant <c>p[n − 2]</c>, the second-last
/// node's value (the swaption engine <c>gaussian1dswaptionengine.cpp:166-171</c>, the nonstandard, float-float and
/// cap/floor engines,
/// <c>Gaussian1dModel::zerobondOption</c> and the Markov-functional model alike), where the left tail takes
/// <c>p[0]</c>, its boundary node. On the wheel, a 5Y into 10Y payer at 2% on a GSR model (a = 0.02, σ = 0.01) on a
/// flat 3% curve, on a grid of 3 standard deviations and 16 points a side, is worth 0.102661800916881; on a grid of 12
/// standard deviations and 256 points, where the tail's weight vanishes, 0.10415806152703168; a put struck at 0.8 on
/// the 10-year bond from the same exercise, on the model's <c>zerobondOption</c>, 0.07911771510819356 against
/// 0.08048473087856346. nanoql takes <c>p[n − 1]</c>. For a payoff that increases past the grid, the fixed tail lies
/// between QuantLib's and the true one, the one assertion the two values can't both pass. No QuantLib case sets the
/// flag. QuantLib's check: the same engine on the widened grid.
/// </summary>
public class FlatPayoffExtrapolationTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void TheSwaptionEnginesRightTailTakesTheLastNode()
    {
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.02, 0.01);
        Date exercise = new TARGET().Advance(Today, 5, TimeUnit.Years);
        var swaption = new Swaption(Gaussian1dInputs.SwapOf(new Euribor6M(Curve), exercise, 10, 0.02, SwapType.Payer, Context), new EuropeanExercise(exercise));
        double flat = Gaussian1dSwaptionEngine.Calculate(swaption, Context, model, 16, 3.0, true, true).NPV!.Value;
        double widened = Gaussian1dSwaptionEngine.Calculate(swaption, Context, model, 256, 12.0, true, true).NPV!.Value;
        Assert.True(Math.Abs((widened / 0.10415806152703168) - 1.0) <= 1e-12, $"{widened}");
        Assert.True(0.102661800916881 < flat && flat < widened, $"{flat} against QuantLib's 0.102661800916881 and the widened grid's {widened}");
    }

    [Fact]
    public void TheZerobondOptionsRightTailTakesTheLastNode()
    {
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.02, 0.01);
        var calendar = new TARGET();
        Date expiry = calendar.Advance(Today, 5, TimeUnit.Years);
        Date valueDate = calendar.Advance(expiry, 2, TimeUnit.Days);
        Date maturity = valueDate + new Period(10, TimeUnit.Years);

        // a put on the bond, whose payoff increases with the state
        double flat = model.ZerobondOption(OptionType.Put, expiry, valueDate, maturity, 0.8, null, 0.0, null, 3.0, 16, true, true);
        double widened = model.ZerobondOption(OptionType.Put, expiry, valueDate, maturity, 0.8, null, 0.0, null, 12.0, 256, true, true);
        Assert.True(Math.Abs((widened / 0.08048473087856346) - 1.0) <= 1e-12, $"{widened}");
        Assert.True(0.07911771510819356 < flat && flat < widened, $"{flat} against QuantLib's 0.07911771510819356 and the widened grid's {widened}");
    }
}
