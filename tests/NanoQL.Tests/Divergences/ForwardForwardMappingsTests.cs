// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The forward-forward Jacobian and Y matrix (fixed). QuantLib's <c>ForwardForwardJacobian</c> is 1/τ_long minus the
/// long forward's derivative (<c>value = df*tau[m]*P(m+1)/P(m) - 1</c>, then <c>-value/bigTau</c>,
/// <c>forwardforwardmappings.cpp:51-53</c>), and <c>YMatrix</c>'s long forward is (P_end/P_start − 1)/τ_long, the
/// negative of g/(1 + τ_long g) (<c>:86</c>): on twelve semi-annual forwards at 5% + 0.1% i in periods of 2, QuantLib's
/// J[0][0] is 0.48725 where the derivative is 0.5125, and its Y[0][0] −0.50077 where the elasticity is +0.5013; on
/// quarterly forwards in periods of 4, J[0][0] 0.74012 where the derivative is 0.2599. nanoql's derivative is
/// (P_start/P_end) τ_m (P_{m+1}/P_m)/τ_long and its long forward (P_start/P_end − 1)/τ_long, QuantLib's own
/// <c>RestrictCurveState</c> forward. Checks: central bumps of that forward, and of its logarithm against the short
/// forwards' for Y, undisplaced and with long displacements other than the short ones, Y's scaling by (f_j + d_j)/(g_l
/// + D_l).
/// </summary>
public class ForwardForwardMappingsTests
{
    [Theory]
    [InlineData(0.5, 2, 0.48724999999999996)]
    [InlineData(0.25, 4, 0.74012271657812501)]
    public void TheJacobianIsTheLongForwardsDerivative(double tau, int multiplier, double quantLibJ00)
    {
        (double[] times, double[] f, LMMCurveState cs) = Curve(tau);
        Matrix j = ForwardForwardMappings.ForwardForwardJacobian(cs, multiplier, 0);
        Matrix bumped = Bumped(times, f, multiplier, logs: false);
        for (int l = 0; l < j.Rows; ++l)
        {
            for (int m = 0; m < j.Columns; ++m)
            {
                Assert.Equal(bumped[l, m], j[l, m], 1e-8);
            }
        }

        // QuantLib's entry is 1/τ_long minus the derivative.
        double bigTau = times[multiplier] - times[0];
        Assert.Equal(quantLibJ00, (1.0 / bigTau) - j[0, 0], 1e-14);
        Assert.NotEqual(quantLibJ00, j[0, 0], 1e-2);
    }

    [Theory]
    [InlineData(0.5, 2, -0.50077413529699333)]
    [InlineData(0.25, 4, -0.74184466578437502)]
    public void TheYMatrixIsTheLongForwardsElasticity(double tau, int multiplier, double quantLibY00)
    {
        (double[] times, double[] f, LMMCurveState cs) = Curve(tau);
        int k = f.Length / multiplier;
        Matrix y = ForwardForwardMappings.YMatrix(cs, new double[f.Length], new double[k], multiplier, 0);
        Matrix bumped = Bumped(times, f, multiplier, logs: true);
        for (int l = 0; l < y.Rows; ++l)
        {
            for (int m = 0; m < y.Columns; ++m)
            {
                Assert.Equal(bumped[l, m], y[l, m], 1e-7);
            }
        }

        Assert.True(y[0, 0] > 0.0, $"{y[0, 0]}");
        Assert.NotEqual(quantLibY00, y[0, 0], 0.5);
    }

    [Theory]
    [InlineData(0.5, 2)]
    [InlineData(0.25, 4)]
    public void TheDisplacedYMatrixIsTheDisplacedLongForwardsElasticity(double tau, int multiplier)
    {
        (double[] times, double[] f, LMMCurveState cs) = Curve(tau);
        int k = f.Length / multiplier;
        double[] d = [.. Enumerable.Range(0, f.Length).Select(j => 0.01 + (0.001 * j))];
        double[] longD = [.. Enumerable.Range(0, k).Select(l => 0.03 - (0.002 * l))];
        Matrix y = ForwardForwardMappings.YMatrix(cs, d, longD, multiplier, 0);
        Matrix bumped = Bumped(times, f, multiplier, logs: true, d, longD);
        Matrix undisplaced = ForwardForwardMappings.YMatrix(cs, new double[f.Length], new double[k], multiplier, 0);
        for (int l = 0; l < y.Rows; ++l)
        {
            for (int m = 0; m < y.Columns; ++m)
            {
                Assert.Equal(bumped[l, m], y[l, m], 1e-7);
            }
        }

        Assert.NotEqual(undisplaced[0, 0], y[0, 0], 1e-3);
    }

    private static (double[] Times, double[] F, LMMCurveState Cs) Curve(double tau)
    {
        const int n = 12;
        double[] times = [.. Enumerable.Range(0, n + 1).Select(i => tau * (i + 1))];
        double[] f = [.. Enumerable.Range(0, n).Select(i => 0.05 + (0.001 * i))];
        var cs = new LMMCurveState(times);
        cs.SetOnForwardRates(f);
        return (times, f, cs);
    }

    // Central bumps of QuantLib's restricted state's forwards in each short forward, or of their displaced logs in the
    // short forwards' displaced logs.
    private static Matrix Bumped(
        double[] times, double[] f, int multiplier, bool logs, double[]? d = null, double[]? longD = null)
    {
        const double h = 1e-7;
        int k = f.Length / multiplier;
        var result = new Matrix(k, f.Length);
        for (int j = 0; j < f.Length; ++j)
        {
            double[] up = (double[])f.Clone(), down = (double[])f.Clone();
            double dj = d?[j] ?? 0.0;
            up[j] = logs ? ((f[j] + dj) * Math.Exp(h)) - dj : f[j] + h;
            down[j] = logs ? ((f[j] + dj) * Math.Exp(-h)) - dj : f[j] - h;
            LMMCurveState ru = Restricted(times, up, multiplier), rd = Restricted(times, down, multiplier);
            for (int l = 0; l < k; ++l)
            {
                double dl = longD?[l] ?? 0.0;
                result[l, j] = logs
                    ? (Math.Log(ru.ForwardRate(l) + dl) - Math.Log(rd.ForwardRate(l) + dl)) / (2 * h)
                    : (ru.ForwardRate(l) - rd.ForwardRate(l)) / (2 * h);
            }

        }

        return result;
    }

    private static LMMCurveState Restricted(double[] times, double[] f, int multiplier)
    {
        var cs = new LMMCurveState(times);
        cs.SetOnForwardRates(f);
        return ForwardForwardMappings.RestrictCurveState(cs, multiplier, 0);
    }
}
