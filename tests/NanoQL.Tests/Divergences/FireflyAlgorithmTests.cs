// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The firefly algorithm (fixed). QuantLib's attraction loop stops at k &lt; i − 1, so each firefly misses the next
/// brighter one and the second brightest is attracted by none; its moved fireflies' new values go to the sorted
/// population's place their index names, another firefly's, so values and positions part after the first move; and pure
/// differential evolution assigns its random base through a reference, overwriting the best firefly. nanoql attracts
/// each firefly to every brighter one, which a recording intensity counts, i(i − 1)/2 of M brightest a round; keeps
/// each value with its firefly, which the intensity checks against the cost at every round; and copies the base, so
/// pure differential evolution converges on the shifted sphere, where QuantLib's population of six collapses onto
/// copies of its members and stalls at 1.2e-3 (6.0e-16 for the copy). The decreasing walk's δ squares at each round's
/// first walk from the second round: each walk's scale, read against a walk at δ 1 on the same seed, is δ₀^(2^(r − 1))
/// in round r, where QuantLib's count squares once every Mfa + 1 walks, so with one firefly δ₀² still in the third
/// round.
/// </summary>
public class FireflyAlgorithmTests
{
    private static readonly double[] Center = [1.2, -0.7, 0.3];

    [Fact]
    public void EachFireflyIsAttractedByEveryBrighterOne()
    {
        var rounds = new List<List<(double ValueX, double ValueY)>>();
        Run(new RecordingIntensity(rounds, null), 8, 0, 20);
        Assert.NotEmpty(rounds);
        foreach (List<(double ValueX, double ValueY)> calls in rounds)
        {
            // fireflies 1 to 7 of the sorted eight, each by the i brighter ones
            Assert.Equal(28, calls.Count);
            Assert.All(calls, c => Assert.True(c.ValueY <= c.ValueX, $"{c.ValueY} brighter than {c.ValueX}"));
        }
    }

    [Fact]
    public void ThePopulationKeepsEachFireflysValue()
    {
        var mismatches = new List<string>();
        Run(new RecordingIntensity([], mismatches), 10, 4, 30);
        Assert.Empty(mismatches);
    }

    [Fact]
    public void PureDifferentialEvolutionConverges()
    {
        // six members: QuantLib's overwritten best stalls at 1.2e-3 by 400 iterations
        ProblemResults r = Run(new ExponentialIntensity(1.0, 0.2, 1.0), 6, 6, 400);
        Assert.True(r.FunctionValue < 1e-12, $"{r.FunctionValue}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void TheDecreasingWalksDeltaSquaresEachRound(int Mfa)
    {
        const double delta0 = 0.9;
        var walk = new DecreasingGaussianWalk(0.1, delta0, 23);
        var unit = new GaussianWalk(0.1, 1.0, 23);
        Vector[] xRW = Bind(walk, Mfa), xUnit = Bind(unit, Mfa);
        for (int r = 1; r <= 6; r++)
        {
            walk.Walk();
            unit.Walk();
            double expected = Math.Pow(delta0, Math.Pow(2.0, r - 1));
            for (int k = 0; k < Mfa; k++)
            {
                // the walk's draws don't read δ, so the ratio is the walk's scale
                int j = Math.Abs(xUnit[k][0]) > Math.Abs(xUnit[k][1]) ? 0 : 1;
                double delta = xRW[k][j] / xUnit[k][j];
                Assert.True(Math.Abs(delta - expected) <= 1e-12 * expected, $"Mfa {Mfa}, round {r}, walk {k}: {delta:R} against {expected:R}");
            }
        }
    }

    // A walk bound to a population of its own, through reflection: Mfa fireflies in two dimensions within [−5, 5]².
    private static Vector[] Bind(FireflyAlgorithmRandomWalk walk, int Mfa)
    {
        Type type = typeof(FireflyAlgorithm).GetNestedType("Population", BindingFlags.NonPublic)!;
        object pop = Activator.CreateInstance(type)!;
        Vector[] xRW = [.. Enumerable.Range(0, Mfa).Select(_ => new Vector(2, 0.0))];
        type.GetField("Mfa")!.SetValue(pop, Mfa);
        type.GetField("N")!.SetValue(pop, 2);
        type.GetField("X")!.SetValue(pop, Enumerable.Range(0, Mfa).Select(_ => new Vector(2, 0.0)).ToArray());
        type.GetField("XRW")!.SetValue(pop, xRW);
        type.GetField("Values")!.SetValue(pop, Enumerable.Range(0, Mfa).Select(k => (0.0, k)).ToArray());
        type.GetField("LowerBound")!.SetValue(pop, new Vector(2, -5.0));
        type.GetField("UpperBound")!.SetValue(pop, new Vector(2, 5.0));
        typeof(FireflyAlgorithmRandomWalk).GetMethod("Bind", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(walk, [pop]);
        return xRW;
    }

    private static ProblemResults Run(FireflyAlgorithmIntensity intensity, int M, int Mde, int iterations)
    {
        var fa = new FireflyAlgorithm(M, intensity, new GaussianWalk(0.1, 0.9, 17), Mde, 0.8, 0.9, 42);
        var problem = new Problem(
            new ShiftedSphere(), new NonhomogeneousBoundaryConstraint([-5.0, -5.0, -5.0], [5.0, 5.0, 5.0]), Center);
        return fa.Minimize(problem, new EndCriteria(iterations, iterations - 1, 1e-8, 1e-8, 1e-8));
    }

    private static double Sphere(Vector x)
    {
        double s = 0.0;
        for (int i = 0; i < x.Size; i++)
        {
            s += (x[i] - Center[i]) * (x[i] - Center[i]);
        }

        return s;
    }

    // The exponential intensity, recording each round's attractions and checking each value against its firefly.
    private sealed class RecordingIntensity(List<List<(double ValueX, double ValueY)>> rounds, List<string>? mismatches)
        : FireflyAlgorithmIntensity
    {
        private List<(double, double)> current = [];
        private string? lastPopulation;

        public override FireflyAlgorithmIntensity Clone() => new RecordingIntensity(rounds, mismatches);

        // QuantLib's exponential intensity; a round starts where the population differs from the last call's
        protected override double IntensityImpl(double valueX, double valueY, double distance)
        {
            string population = string.Join(",", Values.Select(v => $"{v.Index}:{v.Value:R}"));
            if (population != lastPopulation)
            {
                lastPopulation = population;
                current = [];
                rounds.Add(current);
                Check();
            }

            current.Add((valueX, valueY));
            return ((1.0 - 0.2) * Math.Exp(-1.0 * distance)) + 0.2;
        }

        private void Check()
        {
            if (mismatches is null)
            {
                return;
            }

            for (int k = 0; k < Values.Count; k++)
            {
                double f = Sphere(X[Values[k].Index]);
                if (f != Values[k].Value)
                {
                    mismatches.Add($"place {k}: firefly {Values[k].Index}'s value {Values[k].Value} for its cost {f}");
                }
            }
        }
    }

    private sealed class ShiftedSphere : CostFunction
    {
        public override double Value(Vector x) => Sphere(x);

        public override Vector Values(Vector x) => new(1, Value(x));
    }
}
