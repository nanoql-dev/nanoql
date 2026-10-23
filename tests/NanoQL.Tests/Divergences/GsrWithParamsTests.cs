// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// GSR's parameters set on a built model: QuantLib's <c>Gsr::generateArguments</c> flushes the process core, which
/// recomputes its zero-reversion flags from the core's old reversions, before it sets the new ones
/// (<c>gsr.hpp:144-149</c>, <c>gsrprocesscore.cpp:68-75</c>, <c>:41-49</c>), so after <c>setParams</c> the flags lag one
/// parameter set, and a calibration prices each trial point with the previous point's flags wherever the two straddle
/// QuantLib's cut. On the wheel, a European 5Y into 10Y payer at 3% on a flat 3% curve with σ = 0.01, on a model built
/// at a = 0 and set to a = 0.05, is worth 0.06892731403656928, a = 0's price, where the model built at a = 0.05 gives
/// 0.04929346290668454; set from 0.05 to 0, NaN. nanoql's <c>WithParams</c> builds a new core from the new
/// reversions. QuantLib's check: the model built at the same parameters, which QuantLib's own second <c>setParams</c>
/// with the same values gives too. nanoql's prices here differ from QuantLib's in their last places only by the C
/// runtime's <c>erf</c> (the S-g1d plan's G1R3).
/// </summary>
public class GsrWithParamsTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void ParametersSetOnAModelGiveTheModelBuiltAtThem()
    {
        Gsr atZero = Gaussian1dInputs.Constant(Curve, 0.0, 0.01);
        Gsr at005 = Gaussian1dInputs.Constant(Curve, 0.05, 0.01);

        double moved = Price((Gsr)atZero.WithParams(new Vector([0.05, 0.01])));
        Assert.Equal(Price(at005), moved);
        Assert.True(Math.Abs((moved / 0.04929346290668454) - 1.0) <= 1e-13, $"{moved}");

        double back = Price((Gsr)at005.WithParams(new Vector([0.0, 0.01])));
        Assert.Equal(Price(atZero), back);

        // QuantLib's moved models: a = 0's price at a = 0.05, and NaN back at a = 0.
        Assert.True(Math.Abs((0.06892731403656928 / moved) - 1.0) > 0.2);
        Assert.False(double.IsNaN(back));
    }

    private static double Price(Gsr model)
    {
        var index = new Indexes.Euribor6M(Curve);
        Date exercise = new TARGET().Advance(Today, 5, TimeUnit.Years);
        VanillaSwap swap = Gaussian1dInputs.SwapOf(index, exercise, 10, 0.03, SwapType.Payer, Context);
        var swaption = new Swaption(swap, new EuropeanExercise(exercise));
        return Gaussian1dSwaptionEngine.Calculate(swaption, Context, model).NPV!.Value;
    }
}
