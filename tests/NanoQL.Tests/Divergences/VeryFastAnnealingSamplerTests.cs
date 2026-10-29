// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The very fast annealing's sampler, fixed under R8 beside the S-xrt code review's B4-2. QuantLib's starts each
/// coordinate's loop from lower − 1, which rounds to the lower bound itself at every magnitude from 2⁵⁴, so it returns
/// that bound with no draw (<c>hybridsimulatedannealingfunctors.hpp:190-198</c>); nanoql's draws each coordinate at least
/// once. QuantLib's step T·((1 + 1/T)^e − 1) is infinite where 1/T overflows, below about 5.6e-309, so its loop never
/// lands in the box, and NaN at T = 0, which its loop returns; nanoql's takes the same value as T^(1 − e)·(1 + T)^e − T,
/// which agrees with QuantLib's form where both are finite, so at T = 0 it holds the point; and an annealing on the very
/// fast temperature with an end temperature of 0 runs past the overflow to its iteration limit. Above T 100 QuantLib's
/// 1 + 1/T loses the digits of 1/T, its step 2.3e-2 off at 1e14 and 0 from about 1e16, where every draw holds the
/// point (the S-xrt code review's F4-7): nanoql's, T·expm1(e·log1p(1/T)) there, is within 4 ulps of a 50-digit
/// reference from 1e3 to 1e20 (mpmath on the exact doubles, the S-xrt run's <c>c4\f47_refs.py</c>), and QuantLib's bits
/// at 100 and below.
/// </summary>
public class VeryFastAnnealingSamplerTests
{
    [Fact]
    public void ABoxPast2To54IsDrawnNotItsLowerBound()
    {
        double lower = Math.ScaleB(1.0, 60), upper = lower + Math.ScaleB(1.0, 20);

        // QuantLib's start of the loop: lower − 1 is the lower bound, inside the box
        Assert.Equal(lower, lower - 1.0);
        var sampler = new SamplerVeryFastAnnealing(new Vector(1, lower), new Vector(1, upper), 42);
        var point = new Vector(1, 0.0);
        int atLower = 0;
        for (int k = 0; k < 64; k++)
        {
            sampler.Sample(point, new Vector(1, lower + Math.ScaleB(1.0, 19)), new Vector(1, 1.0));
            Assert.InRange(point[0], lower, upper);
            atLower += point[0] == lower ? 1 : 0;
        }

        Assert.True(atLower < 8, $"{atLower} of 64 draws at the lower bound");
    }

    [Theory]
    [InlineData(1e-300)]
    [InlineData(1e-305)]
    [InlineData(1e-308)]
    [InlineData(6e-309)]
    public void TheStepsRewrittenFormIsQuantLibsWhereBothAreFinite(double t)
    {
        foreach (double e in new[] { 0.0, 0.1, 0.25, 0.5, 0.75, 0.9, 0.99, 0.999999, 1.0 })
        {
            double quantLib = t * (Math.Pow(1.0 + (1.0 / t), e) - 1.0);
            double rewritten = (Math.Pow(t, 1.0 - e) * Math.Pow(1.0 + t, e)) - t;
            Assert.True(double.IsFinite(quantLib));
            Assert.True(Math.Abs(quantLib - rewritten) <= 1e-12 * Math.Max(Math.Abs(quantLib), double.Epsilon), $"T {t}, e {e}: {quantLib:R} against {rewritten:R}");
        }
    }

    [Theory]
    [InlineData(1e-310)]
    [InlineData(5e-324)]
    [InlineData(0.0)]
    public void ATemperatureWhoseReciprocalOverflowsDraws(double t)
    {
        // QuantLib's step there: infinite, or NaN at 0
        double quantLib = t * (Math.Pow(1.0 + (1.0 / t), 0.5) - 1.0);
        Assert.True(t == 0.0 ? double.IsNaN(quantLib) : double.IsPositiveInfinity(quantLib));
        var sampler = new SamplerVeryFastAnnealing(new Vector(1, 0.0), new Vector(1, 1.0), 42);
        var point = new Vector(1, double.NaN);
        for (int k = 0; k < 64; k++)
        {
            Assert.True(ReturnsWithin(() => sampler.Sample(point, new Vector(1, 0.5), new Vector(1, t))), $"T {t}: no return in 10 s");
            Assert.InRange(point[0], 0.0, 1.0);
            if (t == 0.0)
            {
                // the distribution's limit: the point held
                Assert.Equal(0.5, point[0]);
            }
        }
    }

    [Fact]
    public void AnAnnealingRunsPastTheTemperaturesOverflow()
    {
        // the temperature exp(−ln(100)·k/100) falls below 1/double.MaxValue from step 15,413 and to 0 from 16,181
        var annealing = new HybridSimulatedAnnealing<SamplerVeryFastAnnealing, ProbabilityBoltzmannDownhill, TemperatureVeryFastAnnealing, ReannealingTrivial>(
            new SamplerVeryFastAnnealing(new Vector(1, 0.0), new Vector(1, 1.0), 42),
            new ProbabilityBoltzmannDownhill(43),
            new TemperatureVeryFastAnnealing(1.0, 0.01, 100.0, 1),
            new ReannealingTrivial(),
            startTemperature: 1.0,
            endTemperature: 0.0,
            optimizeScheme: HybridSimulatedAnnealingLocalOptimizeScheme.NoLocalOptimize);
        var problem = new Problem(new Square(), new NoConstraint(), new Vector(1, 0.5).AsSpan());
        ProblemResults? r = null;
        Assert.True(ReturnsWithin(() => r = annealing.Minimize(problem, new EndCriteria(17000, 16999, 1e-8, 1e-8, 1e-8))), "no return in 10 s");
        Assert.NotNull(r);
        Assert.Equal(EndCriteriaType.MaxIterations, r.EndCriteriaType);
        Assert.InRange(r.CurrentValue[0], 0.0, 1.0);
        Assert.True(r.FunctionValue < 1e-6, $"{r.FunctionValue}");
    }

    [Theory]
    [InlineData(1000.0, 0.5, 0.4998750624609648)]
    [InlineData(1000.0, 0.9, 0.8999550164913429)]
    [InlineData(1000.0, 1.0, 1.0)]
    [InlineData(10000.0, 0.5, 0.49998750062496095)]
    [InlineData(10000.0, 0.9, 0.8999955001649914)]
    [InlineData(10000.0, 1.0, 1.0)]
    [InlineData(100000000.0, 0.5, 0.49999999875)]
    [InlineData(100000000.0, 0.9, 0.89999999955)]
    [InlineData(100000000.0, 1.0, 1.0)]
    [InlineData(1e12, 0.5, 0.499999999999875)]
    [InlineData(1e12, 0.9, 0.8999999999999551)]
    [InlineData(1e12, 1.0, 1.0)]
    [InlineData(1e14, 0.5, 0.4999999999999987)]
    [InlineData(1e14, 0.9, 0.8999999999999996)]
    [InlineData(1e14, 1.0, 1.0)]
    [InlineData(1e16, 0.5, 0.5)]
    [InlineData(1e16, 0.9, 0.9)]
    [InlineData(1e16, 1.0, 1.0)]
    [InlineData(1e18, 0.5, 0.5)]
    [InlineData(1e18, 0.9, 0.9)]
    [InlineData(1e18, 1.0, 1.0)]
    [InlineData(1e20, 0.5, 0.5)]
    [InlineData(1e20, 0.9, 0.9)]
    [InlineData(1e20, 1.0, 1.0)]
    public void TheStepAboveTemperature100KeepsItsDigits(double t, double e, double reference)
    {
        double step = Step(t, e);
        double ulp = Math.BitIncrement(reference) - reference;
        Assert.True(Math.Abs(step - reference) <= 4 * ulp, $"T {t}, e {e}: {step:R} against {reference:R}");
    }

    [Theory]
    [InlineData(100.0)]
    [InlineData(1.0)]
    [InlineData(0.01)]
    [InlineData(1e-300)]
    public void TheStepAt100AndBelowIsQuantLibs(double t)
    {
        foreach (double e in new[] { 0.0, 0.25, 0.5, 0.9, 1.0 })
        {
            Assert.Equal(t * (Math.Pow(1.0 + (1.0 / t), e) - 1.0), Step(t, e));
        }
    }

    [Fact]
    public void AHighTemperatureMovesThePoint()
    {
        // QuantLib's step is 0 at T 1e16, so its draws all return the point
        Assert.Equal(0.0, 1e16 * (Math.Pow(1.0 + (1.0 / 1e16), 0.5) - 1.0));
        var sampler = new SamplerVeryFastAnnealing(new Vector(1, 0.0), new Vector(1, 1.0), 42);
        var point = new Vector(1, 0.0);
        int held = 0;
        for (int k = 0; k < 1000; k++)
        {
            sampler.Sample(point, new Vector(1, 0.5), new Vector(1, 1e16));
            Assert.InRange(point[0], 0.0, 1.0);
            held += point[0] == 0.5 ? 1 : 0;
        }

        Assert.True(held < 10, $"{held} of 1,000 draws hold the point");
    }

    private static double Step(double t, double e) =>
        (double)typeof(SamplerVeryFastAnnealing).GetMethod("Step", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [t, e])!;

    // Whether the action returns within ten seconds, on a background thread, its exception rethrown here: QuantLib's
    // loop never returns.
    private static bool ReturnsWithin(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                caught = e;
            }
        })
        {
            IsBackground = true,
        };
        thread.Start();
        bool returned = thread.Join(TimeSpan.FromSeconds(10));
        if (caught is not null)
        {
            throw new InvalidOperationException("the action threw", caught);
        }

        return returned;
    }

    private sealed class Square : CostFunction
    {
        public override double Value(Vector x) => (x[0] - 0.3) * (x[0] - 0.3);

        public override Vector Values(Vector x) => new(1, Value(x));
    }
}
