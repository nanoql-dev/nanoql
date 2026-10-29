// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The particle swarm's adaptive and decreasing inertias (the S-xrt plan's XRR5 (xxxi), fixed under R8). QuantLib's
/// adaptive counter is unsigned, so its first improvement wraps it to 2⁶⁴ − 1 and halves the inertia as if the swarm had
/// stalled; its decreasing inertia never sets its swarm size, which it loops over, and never advances its iteration, so
/// it never decreases. On the inertia form with c₁ = c₂ = 0 each velocity changes only by the inertia's factor, so a
/// recording topology reads each iteration's factor from the velocities, on a linear cost whose leading particle's drift
/// improves the swarm, an improvement at a net count of 0 among them: nanoql's are each inertia's formula.
/// </summary>
public class ParticleSwarmInertiaTests
{
    private const double Omega = 0.3;

    [Fact]
    public void TheDecreasingInertiaFallsLinearlyToTheThreshold()
    {
        const int K = 20;
        const double threshold = 0.3;
        List<double[][]> v = Velocities(new DecreasingInertia(threshold), K);

        // the factor at the k-th iteration, k from 1, threshold at the last
        for (int k = 1; k < v.Count; k++)
        {
            double expected = Omega * (threshold + ((1.0 - threshold) * (K - k) / K));
            Assert.Equal(expected, Factor(v[k - 1], v[k]), 1e-13);
        }

        Assert.True(v.Count > 10, $"{v.Count} iterations");
    }

    [Fact]
    public void TheAdaptiveInertiaCountsImprovementsLessStalls()
    {
        const int K = 25;
        var bests = new List<double>();
        List<double[][]> v = Velocities(new AdaptiveInertia(0.1, 0.6, 2, 1), K, bests);

        // the rule with a signed counter: halved above sh net stalls, doubled below sl, the first iteration unchanged
        double c0 = Omega, best = 0.0;
        long counter = 0;
        int wraps = 0;
        for (int k = 1; k < v.Count; k++)
        {
            double currBest = bests[k - 1];
            if (k == 1)
            {
                best = currBest;
            }
            else
            {
                if (currBest < best)
                {
                    best = currBest;
                    counter--;
                }
                else
                {
                    counter++;
                }

                if (counter > 2)
                {
                    c0 = Math.Max(0.1, Math.Min(0.6, c0 * 0.5));
                }
                else if (counter < 1)
                {
                    c0 = Math.Max(0.1, Math.Min(0.6, c0 * 2.0));
                }
            }

            Assert.Equal(c0, Factor(v[k - 1], v[k]), 1e-13);
            wraps += counter == -1 && k > 1 && currBest == best ? 1 : 0;
        }

        // an improvement at a net count of 0, where QuantLib's unsigned counter wraps
        Assert.True(wraps > 0, "no improvement at a net count of 0");
    }

    // Each particle's velocity ratio between two iterations, the inertia's factor: one value across the swarm.
    private static double Factor(double[][] before, double[][] after)
    {
        double? factor = null;
        for (int i = 0; i < before.Length; i++)
        {
            for (int j = 0; j < before[i].Length; j++)
            {
                // an entry the bounds clamped is 0 from then on
                if (before[i][j] != 0.0 && after[i][j] != 0.0)
                {
                    double f = after[i][j] / before[i][j];
                    factor ??= f;
                    Assert.Equal(factor.Value, f, 1e-13);
                }
            }
        }

        Assert.True(factor.HasValue, "every velocity clamped");
        return factor.Value;
    }

    // The velocities each FindSocialBest sees, on the inertia form with c₁ = c₂ = 0.
    private static List<double[][]> Velocities(ParticleSwarmOptimizationInertia inertia, int iterations, List<double>? bests = null)
    {
        var velocities = new List<double[][]>();
        var pso = new ParticleSwarmOptimization(20, new RecordingTopology(velocities, bests ?? []), inertia, Omega, 0.0, 0.0, 42);
        var problem = new Problem(
            new Linear(), new NonhomogeneousBoundaryConstraint([-1e3, -1e3], [1e3, 1e3]), new Vector(2, 0.0).AsSpan());
        pso.Minimize(problem, new EndCriteria(iterations, iterations - 1, 1e-8, 1e-8, 1e-8));
        return velocities;
    }

    // The global topology, recording the velocities and the best personal best at each call.
    private sealed class RecordingTopology(List<double[][]> velocities, List<double> bests) : ParticleSwarmOptimizationTopology
    {
        private int M;

        public override void SetSize(int M) => this.M = M;

        public override void FindSocialBest()
        {
            velocities.Add([.. V.Select(v => v.ToArray())]);
            int bestP = 0;
            for (int i = 1; i < M; i++)
            {
                if (PersonalBestF[i] < PersonalBestF[bestP])
                {
                    bestP = i;
                }
            }

            bests.Add(PersonalBestF[bestP]);
            for (int i = 0; i < M; i++)
            {
                SetSocialBest(i, bestP, PersonalBestF[bestP]);
            }
        }

        public override ParticleSwarmOptimizationTopology Clone() => new RecordingTopology(velocities, bests);
    }

    private sealed class Linear : CostFunction
    {
        // linear, so the leading particle's drift improves the swarm's best
        public override double Value(Vector x) => -(x[0] + x[1]);

        public override Vector Values(Vector x) => new(1, Value(x));
    }
}
