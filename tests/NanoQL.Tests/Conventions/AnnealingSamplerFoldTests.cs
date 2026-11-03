// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The annealing's ring and mirror samplers on narrow and zero-width dimensions. QuantLib's folds never return on a
/// zero width and take about |step|/width passes on a narrow one, none moving the point once the width is below its
/// last bit. nanoql's hold a zero-width dimension at its bound, its draw still taken, so the other dimensions draw as a
/// run whose zero-width dimension is unbounded; and after 10,000 passes fold in one step from the drawn point, inside
/// the box and equal to the exact fold of the same double point within one rounding of x − lower, at widths 1e-6
/// (QuantLib's own loop ends 6.0e-12 off the fold for the ring, 4.6e-14 for the mirror) to 1e-20; an annealing with a
/// dimension narrower than its step finishes; and an annealing holding a parameter on a zero-width dimension, started
/// on its bound, reaches the other parameter's minimum.
/// </summary>
public class AnnealingSamplerFoldTests
{
    [Theory]
    [InlineData("ring")]
    [InlineData("mirror")]
    public void AZeroWidthDimensionHoldsItsBound(string kind)
    {
        Vector held = Draw(kind, new Vector(2, 0.0) { [1] = -5.0 }, new Vector(2, 0.0) { [1] = 5.0 }, 42);
        Vector free = Draw(kind, new Vector(2, -1e300) { [1] = -5.0 }, new Vector(2, 1e300) { [1] = 5.0 }, 42);
        Assert.Equal(0.0, held[0]);
        Assert.Equal(free[1], held[1]);
    }

    [Theory]
    [InlineData("ring", 1e-6)]
    [InlineData("ring", 1e-9)]
    [InlineData("ring", 1e-12)]
    [InlineData("ring", 1e-15)]
    [InlineData("ring", 1e-20)]
    [InlineData("mirror", 1e-6)]
    [InlineData("mirror", 1e-9)]
    [InlineData("mirror", 1e-12)]
    [InlineData("mirror", 1e-15)]
    [InlineData("mirror", 1e-20)]
    public void ANarrowWidthFoldsInOneStep(string kind, double w)
    {
        const double lower = 0.0;
        double upper = lower + w;
        for (long seed = 1; seed <= 8; seed++)
        {
            // the drawn point on [0, w]: the Gaussian sampler's on the same seed, from 0 at temperature 1
            var g = new SamplerGaussian(seed);
            var x = new Vector(1, 0.0);
            g.Sample(x, new Vector(1, lower), new Vector(1, 1.0));
            if (x[0] >= lower && x[0] <= upper)
            {
                continue;
            }

            double y = Draw(kind, new Vector(1, lower), new Vector(1, upper), seed, lower)[0];
            Assert.InRange(y, lower, upper);
            double exact = ExactFold(kind, x[0], lower, upper - lower);
            double tolerance = Ulp(x[0] - lower) + Ulp(exact);
            Assert.True(Math.Abs(y - exact) <= tolerance, $"{kind} at {w}, seed {seed}: {y:R} against {exact:R}");
        }
    }

    [Fact]
    public void AnAnnealingWithANarrowDimensionFinishes()
    {
        var sampler = new SamplerMirrorGaussian(new Vector(2, -5.0) { [1] = 0.25 }, new Vector(2, 5.0) { [1] = 0.25 + 1e-12 }, 42);
        var annealing = new MirrorGaussianSimulatedAnnealing(
            sampler,
            new ProbabilityBoltzmannDownhill(43),
            new TemperatureExponential(1.0, 2),
            optimizeScheme: HybridSimulatedAnnealingLocalOptimizeScheme.NoLocalOptimize);
        var problem = new Problem(new Sphere(), new NoConstraint(), new Vector(2, 0.25).AsSpan());
        ProblemResults r = annealing.Minimize(problem, new EndCriteria(200, 100, 1e-8, 1e-8, 1e-8));
        Assert.InRange(r.CurrentValue[1], 0.25, 0.25 + 1e-12);
    }

    [Theory]
    [InlineData("ring")]
    [InlineData("mirror")]
    public void AnAnnealingHoldingAParameterReachesTheOthersMinimum(string kind)
    {
        // (x0 − 1)² + (x1 − 2)² on [−5, 5] × [3, 3]: the held minimum at (1, 3), its value 1
        var lower = new Vector(2, -5.0) { [1] = 3.0 };
        var upper = new Vector(2, 5.0) { [1] = 3.0 };
        var problem = new Problem(new Held(), new NoConstraint(), new Vector(2, 0.0) { [1] = 3.0 }.AsSpan());
        var criteria = new EndCriteria(5000, 2000, 1e-8, 1e-8, 1e-8);
        const HybridSimulatedAnnealingLocalOptimizeScheme none = HybridSimulatedAnnealingLocalOptimizeScheme.NoLocalOptimize;
        ProblemResults r = kind == "ring"
            ? new HybridSimulatedAnnealing<SamplerRingGaussian, ProbabilityBoltzmannDownhill, TemperatureExponential, ReannealingTrivial>(
                new SamplerRingGaussian(lower, upper, 42), new ProbabilityBoltzmannDownhill(43), new TemperatureExponential(1.0, 2),
                new ReannealingTrivial(), endTemperature: 1e-12, optimizeScheme: none).Minimize(problem, criteria)
            : new MirrorGaussianSimulatedAnnealing(
                new SamplerMirrorGaussian(lower, upper, 42), new ProbabilityBoltzmannDownhill(43), new TemperatureExponential(1.0, 2),
                endTemperature: 1e-12, optimizeScheme: none).Minimize(problem, criteria);
        Assert.Equal(3.0, r.CurrentValue[1]);
        Assert.True(Math.Abs(r.CurrentValue[0] - 1.0) < 1e-4, $"{kind}: x0 {r.CurrentValue[0]:R}");
        Assert.True(r.FunctionValue - 1.0 < 1e-8, $"{kind}: {r.FunctionValue:R}");
    }

    private static Vector Draw(string kind, Vector lower, Vector upper, long seed, double from = 0.0)
    {
        var point = new Vector(lower.Size, 0.0);
        var current = new Vector(lower.Size, from);
        var temp = new Vector(lower.Size, 1.0);
        if (kind == "ring")
        {
            new SamplerRingGaussian(lower, upper, seed).Sample(point, current, temp);
        }
        else
        {
            new SamplerMirrorGaussian(lower, upper, seed).Sample(point, current, temp);
        }

        return point;
    }

    // The fold of the double x into [lower, lower + w] in exact arithmetic, rounded once.
    private static double ExactFold(string kind, double x, double lower, double w)
    {
        int e = Math.Min(Exponent(x), Math.Min(Exponent(lower), Exponent(w)));
        System.Numerics.BigInteger X = Scaled(x, e), L = Scaled(lower, e), W = Scaled(w, e);
        System.Numerics.BigInteger r;
        if (kind == "ring")
        {
            // from below lower + ((x − lower) mod w); from above upper − ((upper − x) mod w)
            r = x < lower ? Mod(X - L, W) : W - Mod(L + W - X, W);
        }
        else
        {
            System.Numerics.BigInteger m = Mod(X - L, 2 * W);
            r = m <= W ? m : (2 * W) - m;
        }

        return Math.ScaleB((double)(L + r), e);
    }

    private static System.Numerics.BigInteger Mod(System.Numerics.BigInteger a, System.Numerics.BigInteger m) => ((a % m) + m) % m;

    // the exponent of the double's last bit
    private static int Exponent(double d) => d == 0.0 ? 0 : Math.ILogB(d) - 52;

    private static System.Numerics.BigInteger Scaled(double d, int e) => new System.Numerics.BigInteger(Math.ScaleB(d, -Exponent(d))) << (Exponent(d) - e);

    private static double Ulp(double d) => Math.BitIncrement(Math.Abs(d)) - Math.Abs(d);

    private sealed class Held : CostFunction
    {
        public override double Value(Vector x) => ((x[0] - 1.0) * (x[0] - 1.0)) + ((x[1] - 2.0) * (x[1] - 2.0));

        public override Vector Values(Vector x) => new(1, Value(x));
    }

    private sealed class Sphere : CostFunction
    {
        public override double Value(Vector x) => (x[0] * x[0]) + (x[1] * x[1]);

        public override Vector Values(Vector x) => new(1, Value(x));
    }
}
