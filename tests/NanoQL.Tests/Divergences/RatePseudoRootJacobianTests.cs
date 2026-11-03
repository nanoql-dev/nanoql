// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The analytic pseudo-root Jacobians. The derivative of an Euler step's end rate j along rate k's root carries the
/// drift's sum e_j = Σ_{k&lt;j} τ_k (f_k + d_k) P_k a_k and the displaced rate f_j + d_j; QuantLib's sums the terms
/// without τ_k and multiplies by τ_j, right on equal periods only, and carries f_j
/// (<c>ratepseudorootjacobian.cpp:183</c>, <c>:191</c>, <c>:197</c>, <c>:280</c>, <c>:294</c>, <c>:298</c>). On six
/// unequal periods, FlatVol 20%, 3 factors, displacement 2%, the money-market measure, step 0 from the forwards with
/// Gaussians 0.3, −0.5, 1.1, every element bumped 1e-6: nanoql's analytic Jacobians meet the numerical within
/// QuantLib's own test's 5e-11; QuantLib's misses by 1.98e-4. Checks: QuantLib's
/// <c>RatePseudoRootJacobianNumerical</c>, its bits.
/// </summary>
public class RatePseudoRootJacobianTests
{
    [Fact]
    public void TheAnalyticJacobiansAreTheNumerical()
    {
        double[] rateTimes = [0.25, 0.75, 1.0, 1.75, 2.0, 2.6, 3.0];
        int k = rateTimes.Length - 1;
        double[] forwards = [.. Enumerable.Range(0, k).Select(i => 0.03 + (0.001 * i))];
        var evolution = new EvolutionDescription(rateTimes, rateTimes[..^1]);
        Matrix corr = ExpCorrelationsFunctions.ExponentialCorrelations(rateTimes, 0.5, 0.2);
        var model = new FlatVol(
            [.. Enumerable.Repeat(0.20, k)], new TimeHomogeneousForwardCorrelation(corr, rateTimes), evolution, 3, forwards,
            [.. Enumerable.Repeat(0.02, k)]);
        int numeraire = EvolutionDescriptionFunctions.MoneyMarketMeasure(evolution)[0];

        const double h = 1e-6;
        var up = new List<Matrix>();
        var down = new List<Matrix>();
        for (int r = 0; r < k; ++r)
        {
            for (int f = 0; f < 3; ++f)
            {
                var b = new Matrix(k, 3, 0.0);
                b[r, f] = h;
                up.Add(b.Clone());
                b[r, f] = -h;
                down.Add(b);
            }
        }

        Matrix a = model.PseudoRoot(0);
        double[] taus = [.. evolution.RateTaus];
        double[] displacements = [.. model.Displacements];
        double[] g = [0.3, -0.5, 1.1];
        double[] dfs = new double[k + 1];
        dfs[0] = 1.0;
        for (int i = 1; i <= k; ++i)
        {
            dfs[i] = 1.0 / (1.0 + (forwards[i - 1] * taus[i - 1]));
        }

        // the step's end rates: the numerical form with a zero bump from zero rates
        var b0 = new Matrix(1, k);
        new RatePseudoRootJacobianNumerical(a, 0, numeraire, taus, [new Matrix(k, 3, 0.0)], displacements)
            .GetBumps(forwards, dfs, new double[k], g, b0);
        double[] end = [.. b0.Row(0)];
        double[] quantLibEnd = [0.03126507973061643, 0.031594969384296748, 0.032430523110104567, 0.033938009844948838, 0.035550262418937995, 0.038360841259904063];
        Assert.Equal(quantLibEnd, end);

        var bu = new Matrix(up.Count, k);
        var bd = new Matrix(up.Count, k);
        var ba = new Matrix(up.Count, k);
        new RatePseudoRootJacobianNumerical(a, 0, numeraire, taus, up, displacements).GetBumps(forwards, dfs, end, g, bu);
        new RatePseudoRootJacobianNumerical(a, 0, numeraire, taus, down, displacements).GetBumps(forwards, dfs, end, g, bd);
        new RatePseudoRootJacobian(a, 0, numeraire, taus, up, displacements).GetBumps(forwards, dfs, end, g, ba);
        Matrix[] all = [.. Enumerable.Range(0, k).Select(_ => new Matrix(k, 3))];
        new RatePseudoRootJacobianAllElements(a, 0, numeraire, taus, displacements).GetBumps(forwards, dfs, end, g, all);

        for (int i = 0; i < up.Count; ++i)
        {
            for (int j = 0; j < k; ++j)
            {
                double numerical = (bu[i, j] - bd[i, j]) / (2 * h);
                Assert.True(Math.Abs((ba[i, j] / h) - numerical) <= 5e-11, $"bump {i}, rate {j}: {ba[i, j] / h} against {numerical}");
                Assert.True(Math.Abs(all[j][i / 3, i % 3] - numerical) <= 5e-11, $"element {i}, rate {j}: {all[j][i / 3, i % 3]} against {numerical}");
            }
        }

        // QuantLib's analytic Jacobian, 1.98e-4 from the numerical at worst; its entry for bump 0, rate 5
        const double quantLibWorst = 0.00019803039541817745, quantLibEntry = 8.8660794940209994e-05;
        double entry = (bu[0, 5] - bd[0, 5]) / (2 * h);
        Assert.True(quantLibWorst > 1e6 * 5e-11 && Math.Abs(quantLibEntry - entry) > 1e-6, $"{quantLibEntry} against {entry}");
    }
}
