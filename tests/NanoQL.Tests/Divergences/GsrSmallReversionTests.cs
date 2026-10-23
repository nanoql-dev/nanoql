// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// GSR's process core at small reversions (the S-g1d plan's G1R4 (i)): QuantLib switches to its zero-reversion formulas
/// below |a| &lt; 1e-4 (<c>gsrprocesscore.cpp:72-75</c>), so its outputs jump at the cut, and just above it its two
/// terms over a² cancel to second order. On the wheel, a European 5Y into 10Y payer at 3% on a flat 3% curve with
/// σ = 0.01 is worth 0.06892731403656928 at a = 1e-4·(1 − 1e-6) and 0.068867423550291 at a = 1e-4·(1 + 1e-6), a jump
/// of 8.7e-4. nanoql computes each term below |a| &lt; 1e-2 in a form continuous in a (<c>expm1</c> and
/// φ₂(u) = (eᵘ − 1 − u)/u²), QuantLib's zero-reversion formulas at a = 0 exactly, and QuantLib's formulas and bits at
/// and above 1e-2. QuantLib's checks: its own formulas at the switch, which the forms meet at the process's members, on
/// a process with step dates as without: the measured gaps, 1.3e-12 on one piece and 2e-12 on the stepped process
/// here, are the switch's own step of a acting on the expectation's cancelling parts; each side, at its own reversion,
/// is within about 2e-13 of exact; and its own zero-reversion formulas, the forms' limit, which the process meets as
/// a → 0, each term moving in proportion to a. A price answers a change of its inputs at the 1e-12 level by about as
/// much on this swaption, and by up to 2e-9 on 10-year expiries, on either side of the switch, through the engine's
/// grid and spline, so the price fact at the switch (1.1e-12 measured) checks the engine at one point on each side, not
/// the formulas. nanoql's prices here differ from
/// QuantLib's in their last places only by the C runtime's <c>erf</c> (the plan's G1R3): with it, QuantLib's bits at
/// a = 0 and a = 1e-2.
/// </summary>
public class GsrSmallReversionTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void TheFormsMeetQuantLibsFormulasAtTheSwitch()
    {
        double below = Price(1e-2 * (1.0 - 1e-12));
        double at = Price(1e-2);
        Assert.True(Math.Abs((below / at) - 1.0) <= 2e-12, $"{below} against {at}");

        // QuantLib's formulas at the switch: its value, 0.06432904765894296 (the erf class apart).
        Assert.True(Math.Abs((at / 0.06432904765894296) - 1.0) <= 1e-13, $"{at}");
    }

    [Fact]
    public void TheSteppedProcessMeetsQuantLibsFormulasAtTheSwitchAndTheLimit()
    {
        // With step dates, the terms of the steps before and after a piece run too: at ±1e-2·(1 − 1e-12), the forms,
        // against QuantLib's formulas at ±1e-2, and at 1e-14 against QuantLib's zero-reversion formulas at 0, for a
        // constant and a piecewise reversion.
        double[] times = [0.5, 1.0, 2.0, 3.0, 5.0, 7.0, 10.0, 15.0];
        double[] vols = [0.010, 0.012, 0.009, 0.011, 0.008, 0.010, 0.013, 0.009, 0.011];
        foreach (bool piecewise in new[] { false, true })
        {
            double[] Reversions(double a) => piecewise ? [.. Enumerable.Repeat(a, times.Length + 1)] : [a];
            foreach (double a in new[] { 1e-2, -1e-2 })
            {
                AssertMembers(new GsrProcess(times, vols, Reversions(a * (1.0 - 1e-12)), 20.0), new GsrProcess(times, vols, Reversions(a), 20.0), 5e-12);
            }

            AssertMembers(new GsrProcess(times, vols, Reversions(1e-14), 20.0), new GsrProcess(times, vols, Reversions(0.0), 20.0), 1e-11);
        }
    }

    [Fact]
    public void PricesAreContinuousAcrossQuantLibsCut()
    {
        double below = Price(1e-4 * (1.0 - 1e-6));
        double above = Price(1e-4 * (1.0 + 1e-6));
        Assert.True(Math.Abs((below / above) - 1.0) <= 5e-9, $"{below} against {above}");

        // QuantLib's jump across its cut.
        Assert.True(Math.Abs((0.06892731403656928 / 0.068867423550291) - 1.0) > 8e-4);
    }

    [Fact]
    public void TheProcessIsSmoothAcrossQuantLibsCut()
    {
        // The second difference across 1e-4, where QuantLib's formulas switch, is the curvature's, about 1e-20: a jump
        // or a formula losing digits on one side shows in it.
        foreach (Func<GsrProcess, double> member in new Func<GsrProcess, double>[]
        {
            p => p.Expectation(0.0, 0.0, 5.0), p => p.Expectation(5.0, 0.01, 0.5), p => p.Variance(0.0, 0.0, 5.0), p => p.Y(5.0),
        })
        {
            double below = member(new GsrProcess([], [0.01], [1e-4 * (1.0 - 1e-6)], 60.0));
            double at = member(new GsrProcess([], [0.01], [1e-4], 60.0));
            double above = member(new GsrProcess([], [0.01], [1e-4 * (1.0 + 1e-6)], 60.0));
            Assert.True(Math.Abs(above - (2.0 * at) + below) <= 1e-14 * Math.Abs(at), $"{below}, {at}, {above}");
        }
    }

    [Fact]
    public void TheZeroReversionIsTheFormsLimit()
    {
        // The process at a = 1e-14 against QuantLib's zero-reversion formulas at a = 0: each moves in proportion to a.
        var atZero = new GsrProcess([], [0.01], [0.0], 60.0);
        var small = new GsrProcess([], [0.01], [1e-14], 60.0);
        foreach ((double x, double z) in new[]
        {
            (small.Expectation(0.0, 0.0, 5.0), atZero.Expectation(0.0, 0.0, 5.0)),
            (small.Expectation(5.0, 0.01, 0.5), atZero.Expectation(5.0, 0.01, 0.5)),
            (small.Variance(5.0, 0.0, 0.5), atZero.Variance(5.0, 0.0, 0.5)),
            (small.Y(5.0), atZero.Y(5.0)),
            (small.G(5.0, 15.0, 0.0), atZero.G(5.0, 15.0, 0.0)),
        })
        {
            Assert.True(Math.Abs((x / z) - 1.0) <= 1e-12, $"{x} against {z}");
        }

        double zero = Price(0.0);

        // QuantLib's zero-reversion formulas: its value at a = 0, which it gives below 1e-4 too (the erf class apart).
        Assert.True(Math.Abs((zero / 0.06892731403656928) - 1.0) <= 5e-13, $"{zero}");
    }

    // The expectation, variance, Y and G at nine (w, t) on the stepped process, against the reference's.
    private static void AssertMembers(GsrProcess process, GsrProcess reference, double tolerance)
    {
        foreach ((double w, double t) in new[] { (0.0, 0.3), (0.0, 2.5), (0.0, 8.0), (0.7, 4.0), (2.0, 12.0), (5.0, 19.0), (3.0, 20.0), (0.0, 20.0), (10.0, 15.0) })
        {
            foreach ((double x, double r) in new[]
            {
                (process.Expectation(w, 0.01, t - w), reference.Expectation(w, 0.01, t - w)),
                (process.Variance(w, 0.0, t - w), reference.Variance(w, 0.0, t - w)),
                (process.Y(t), reference.Y(t)),
                (process.G(w, t, 0.0), reference.G(w, t, 0.0)),
            })
            {
                Assert.True(Math.Abs((x / r) - 1.0) <= tolerance, $"({w}, {t}): {x} against {r}");
            }
        }
    }

    private static double Price(double a)
    {
        var index = new Indexes.Euribor6M(Curve);
        Date exercise = new TARGET().Advance(Today, 5, TimeUnit.Years);
        VanillaSwap swap = Gaussian1dInputs.SwapOf(index, exercise, 10, 0.03, SwapType.Payer, Context);
        var swaption = new Swaption(swap, new EuropeanExercise(exercise));
        return Gaussian1dSwaptionEngine.Calculate(swaption, Context, Gaussian1dInputs.Constant(Curve, a, 0.01)).NPV!.Value;
    }
}
