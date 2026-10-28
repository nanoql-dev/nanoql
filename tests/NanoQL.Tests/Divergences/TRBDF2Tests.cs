// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The old TR-BDF2's trapezoidal stage (the S-core plan's CR7 (xvi)): QuantLib builds the stage's implicit part at
/// t − Δt, where the stage ends at t − αΔt, so a time-dependent operator steps at first order; nanoql builds it at
/// t − αΔt. On u_t = c(t)·u_xx on 101 points from sin(πx), rolled back from 1 to 0 with Dirichlet rows, sin(πx) is the
/// discrete operator's eigenvector, so the semi-discrete solution at x ½ is exp(−λ_h ∫c), λ_h = (2/h²)(1 − cos πh), and
/// the errors are the scheme's own (this run's <c>c4work\refs_c4.py</c>, <c>refs_c4.out</c>). At c = 1 nanoql's values
/// are QuantLib's, bit for bit; at c = 1 + t nanoql's errors fall fourfold per halving, QuantLib's don't. QuantLib's
/// values are its own (<c>probes\c4_facts.cpp</c>, <c>c4_facts.out</c>).
/// </summary>
public class TRBDF2Tests
{
    private static double Rollback(bool constant, int steps)
    {
        const int n = 101;
        const double h = 1.0 / (n - 1);
        var l = new HeatOperator(n, new Setter(h, constant));
        l.SetTime(1.0);
        var u = new double[n];
        for (int i = 0; i < n; ++i)
        {
            u[i] = Math.Sin(Math.PI * i * h);
        }

        var model = new FiniteDifferenceModel<TRBDF2>(new TRBDF2(l, []));
        model.Rollback(u, 1.0, 0.0, steps);
        return u[50];
    }

    [Fact]
    public void AtAConstantOperatorQuantLibsSchemeIsKept()
    {
        Assert.Equal(4.8493412235309302e-05, Rollback(true, 25));
        Assert.Equal(5.0949731362159137e-05, Rollback(true, 50));
        Assert.Equal(5.1562409466017854e-05, Rollback(true, 100));
        Assert.Equal(5.1714674840890688e-05, Rollback(true, 200));
    }

    [Fact]
    public void AtATimeDependentOperatorTheSchemeIsSecondOrder()
    {
        const double exact = 3.7244031950627975e-7;
        int[] steps = [25, 50, 100, 200];
        double[] errors = [.. steps.Select(s => Math.Abs(Rollback(false, s) - exact) / exact)];

        // QuantLib's values 2.943677698507409e-07, 3.5771195753264131e-07, 3.715035363857023e-07 and
        // 3.735660530908121e-07: errors 0.21, 0.040, 0.0025 and 0.0030
        for (int i = 1; i < errors.Length; ++i)
        {
            double ratio = errors[i - 1] / errors[i];
            Assert.True(ratio > 3.6 && ratio < 4.4, $"{steps[i]} steps: errors {string.Join(", ", errors)}");
        }

        Assert.NotEqual(2.943677698507409e-07, Rollback(false, 25));
    }

    // u_t = c(t) u_xx, its operator -c D2 with identity rows at the ends
    private sealed class Setter(double h, bool constant) : TridiagonalOperatorTimeSetter
    {
        public override void SetTime(double t, TridiagonalOperator l)
        {
            double c = constant ? 1.0 : 1.0 + t;
            double a = -c / (h * h);
            for (int i = 1; i + 1 < l.Size; ++i)
            {
                l.SetMidRow(i, a, -2 * a, a);
            }

            l.SetFirstRow(1.0, 0.0);
            l.SetLastRow(0.0, 1.0);
        }
    }

    private sealed class HeatOperator : TridiagonalOperator
    {
        public HeatOperator(int n, TridiagonalOperatorTimeSetter setter)
            : base(n) => timeSetter = setter;
    }
}
