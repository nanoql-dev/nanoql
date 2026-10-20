// SPDX-FileCopyrightText: Copyright (C) 2012 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/ode/adaptiverungekutta.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;

namespace NanoQL.Numerics;

/// <summary>
/// The adaptive Runge-Kutta integrator of an ordinary differential equation f'(x) = F(x, f(x)), F from ℝ × Kⁿ to Kⁿ,
/// K the real or the complex numbers: Cash and Karp's embedded fifth-order step, whose fourth-order error estimate
/// sets the next step's size (Numerical Recipes in C, chapter 16.2).
/// </summary>
/// <typeparam name="T">The values' type: <see cref="double"/>, or <see cref="Complex"/> for a complex equation.</typeparam>
/// <remarks>
/// <para>
/// QuantLib's real coefficients multiply the values as values of <typeparamref name="T"/>, and a value's magnitude is
/// <c>T.Abs</c>'s, as a real. For finite values that is C++'s arithmetic on <c>std::complex</c> too.
/// </para>
/// <para>
/// An equation whose function returns a vector of another length than its argument's is refused, where QuantLib reads
/// past the shorter one.
/// </para>
/// <para>
/// Immutable and thread-safe: it holds only its settings, and each integration works on its own state. Unsealed for its
/// named class, QuantLib-Python's <see cref="RungeKutta"/>.
/// </para>
/// </remarks>
public class AdaptiveRungeKutta<T>
    where T : INumberBase<T>
{
    private const double A2 = 0.2, A3 = 0.3, A4 = 0.6, A5 = 1.0, A6 = 0.875, B21 = 0.2, B31 = 3.0 / 40.0,
        B32 = 9.0 / 40.0, B41 = 0.3, B42 = -0.9, B43 = 1.2, B51 = -11.0 / 54.0, B52 = 2.5, B53 = -70.0 / 27.0,
        B54 = 35.0 / 27.0, B61 = 1631.0 / 55296.0, B62 = 175.0 / 512.0, B63 = 575.0 / 13824.0,
        B64 = 44275.0 / 110592.0, B65 = 253.0 / 4096.0, C1 = 37.0 / 378.0, C3 = 250.0 / 621.0, C4 = 125.0 / 594.0,
        C6 = 512.0 / 1771.0, DC1 = C1 - (2825.0 / 27648.0), DC3 = C3 - (18575.0 / 48384.0),
        DC4 = C4 - (13525.0 / 55296.0), DC5 = -277.0 / 14336.0, DC6 = C6 - 0.25;

    private const double MaxSteps = 10000, Tiny = 1.0E-30, Safety = 0.9, PGrow = -0.2, PShrink = -0.25,
        ErrCon = 1.89E-4;

    private readonly double eps;
    private readonly double h1;
    private readonly double hmin;

    /// <summary>Initializes a new instance of the <see cref="AdaptiveRungeKutta{T}"/> class.</summary>
    /// <param name="eps">The error prescribed for the solution.</param>
    /// <param name="h1">The first step's size.</param>
    /// <param name="hmin">The smallest step size allowed.</param>
    public AdaptiveRungeKutta(double eps = 1.0e-6, double h1 = 1.0e-4, double hmin = 0.0)
    {
        this.eps = eps;
        this.h1 = h1;
        this.hmin = hmin;
    }

    /// <summary>
    /// The solution at <paramref name="x2"/> of f'(x) = F(x, f(x)) with f(<paramref name="x1"/>) = <paramref name="y1"/>:
    /// QuantLib's <c>operator()</c>.
    /// </summary>
    /// <param name="ode">F, returning a vector of its argument's length.</param>
    /// <param name="y1">The initial value.</param>
    /// <param name="x1">The initial point.</param>
    /// <param name="x2">The point of the solution, before or after <paramref name="x1"/>.</param>
    /// <returns>f(<paramref name="x2"/>).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ode"/> is null.</exception>
    /// <exception cref="NanoQLException"><paramref name="ode"/> returned a vector of another length, the step size
    /// fell to the smallest allowed or underflowed, or the integration took too many steps.</exception>
    public ImmutableArray<T> Evaluate(Func<double, ImmutableArray<T>, ImmutableArray<T>> ode, ReadOnlySpan<T> y1, double x1, double x2)
    {
        ArgumentNullException.ThrowIfNull(ode);
        return [.. Integrate(ode, y1, x1, x2)];
    }

    /// <summary>
    /// The solution at <paramref name="x2"/> of the one-dimensional f'(x) = F(x, f(x)) with
    /// f(<paramref name="x1"/>) = <paramref name="y1"/>: QuantLib's <c>operator()</c> on a scalar function.
    /// </summary>
    /// <param name="ode">F.</param>
    /// <param name="y1">The initial value.</param>
    /// <param name="x1">The initial point.</param>
    /// <param name="x2">The point of the solution, before or after <paramref name="x1"/>.</param>
    /// <returns>f(<paramref name="x2"/>).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ode"/> is null.</exception>
    /// <exception cref="NanoQLException">The step size fell to the smallest allowed or underflowed, or the integration
    /// took too many steps.</exception>
    public T Evaluate(Func<double, T, T> ode, T y1, double x1, double x2)
    {
        ArgumentNullException.ThrowIfNull(ode);
        return Integrate((x, y) => [ode(x, y[0])], [y1], x1, x2)[0];
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    // QuantLib's real coefficient, as a T.
    private static T Real(double a) => T.CreateChecked(a);

    // std::abs, a real for a real or a complex value.
    private static double Magnitude(T value) => double.CreateChecked(T.Abs(value));

    // The function's value at (x, y), copied from the caller's working vector, its length checked.
    private static ImmutableArray<T> Derivatives(Func<double, ImmutableArray<T>, ImmutableArray<T>> ode, double x, T[] y)
    {
        ImmutableArray<T> dydx = ode(x, [.. y]);
        int returned = dydx.IsDefault ? 0 : dydx.Length;
        if (returned != y.Length)
        {
            throw new NanoQLException($"the ODE function returned {returned} values for {y.Length}");
        }

        return dydx;
    }

    private T[] Integrate(Func<double, ImmutableArray<T>, ImmutableArray<T>> ode, ReadOnlySpan<T> y1, double x1, double x2)
    {
        int n = y1.Length;
        T[] y = y1.ToArray();
        double[] yScale = new double[n];
        double x = x1;
        double h = h1 * (x1 <= x2 ? 1 : -1);
        double hnext;

        for (int nstp = 1; nstp <= MaxSteps; nstp++)
        {
            ImmutableArray<T> dydx = Derivatives(ode, x, y);
            for (int i = 0; i < n; i++)
            {
                yScale[i] = Magnitude(y[i]) + Magnitude(dydx[i] * Real(h)) + Tiny;
            }

            if ((x + h - x2) * (x + h - x1) > 0.0)
            {
                h = x2 - x;
            }

            Rkqs(y, dydx, ref x, h, eps, yScale, out hnext, ode);

            if ((x - x2) * (x2 - x1) >= 0.0)
            {
                return y;
            }

            if (Math.Abs(hnext) <= hmin)
            {
                throw new NanoQLException($"Step size ({Show(hnext)}) too small ({Show(hmin)} min) in AdaptiveRungeKutta");
            }

            h = hnext;
        }

        throw new NanoQLException($"Too many steps ({Show(MaxSteps)}) in AdaptiveRungeKutta");
    }

    private static void Rkqs(
        T[] y,
        ImmutableArray<T> dydx,
        ref double x,
        double htry,
        double eps,
        double[] yScale,
        out double hnext,
        Func<double, ImmutableArray<T>, ImmutableArray<T>> derivs)
    {
        int n = y.Length;
        double errmax, xnew;
        T[] yerr = new T[n], ytemp = new T[n];

        double h = htry;

        for (; ;)
        {
            Rkck(y, dydx, x, h, ytemp, yerr, derivs);
            errmax = 0.0;
            for (int i = 0; i < n; i++)
            {
                // std::max(errmax, e), which keeps errmax where e is NaN
                double e = Magnitude(yerr[i] / Real(yScale[i]));
                errmax = errmax < e ? e : errmax;
            }

            errmax /= eps;
            if (errmax > 1.0)
            {
                double htemp1 = Safety * h * Math.Pow(errmax, PShrink);
                double htemp2 = h / 10;

                // These would be std::min and std::max, of course, but VC++14 had problems inlining them and caused
                // the wrong results to be calculated. The problem seems to be fixed in update 3, but let's keep this
                // implementation for compatibility.
                double maxPositive = htemp1 > htemp2 ? htemp1 : htemp2;
                double maxNegative = htemp1 < htemp2 ? htemp1 : htemp2;
                h = (h >= 0.0) ? maxPositive : maxNegative;
                xnew = x + h;
                if (xnew == x)
                {
                    throw new NanoQLException($"Stepsize underflow ({Show(h)} at x = {Show(x)}) in AdaptiveRungeKutta::rkqs");
                }

                continue;
            }
            else
            {
                if (errmax > ErrCon)
                {
                    hnext = Safety * h * Math.Pow(errmax, PGrow);
                }
                else
                {
                    hnext = 5.0 * h;
                }

                x += h;
                for (int i = 0; i < n; i++)
                {
                    y[i] = ytemp[i];
                }

                break;
            }
        }
    }

    private static void Rkck(
        T[] y,
        ImmutableArray<T> dydx,
        double x,
        double h,
        T[] yout,
        T[] yerr,
        Func<double, ImmutableArray<T>, ImmutableArray<T>> derivs)
    {
        int n = y.Length;
        T[] ytemp = new T[n];

        // first step
        for (int i = 0; i < n; i++)
        {
            ytemp[i] = y[i] + (Real(B21 * h) * dydx[i]);
        }

        // second step
        ImmutableArray<T> ak2 = Derivatives(derivs, x + (A2 * h), ytemp);
        for (int i = 0; i < n; i++)
        {
            ytemp[i] = y[i] + (Real(h) * ((Real(B31) * dydx[i]) + (Real(B32) * ak2[i])));
        }

        // third step
        ImmutableArray<T> ak3 = Derivatives(derivs, x + (A3 * h), ytemp);
        for (int i = 0; i < n; i++)
        {
            ytemp[i] = y[i] + (Real(h) * ((Real(B41) * dydx[i]) + (Real(B42) * ak2[i]) + (Real(B43) * ak3[i])));
        }

        // fourth step
        ImmutableArray<T> ak4 = Derivatives(derivs, x + (A4 * h), ytemp);
        for (int i = 0; i < n; i++)
        {
            ytemp[i] = y[i]
                + (Real(h) * ((Real(B51) * dydx[i]) + (Real(B52) * ak2[i]) + (Real(B53) * ak3[i]) + (Real(B54) * ak4[i])));
        }

        // fifth step
        ImmutableArray<T> ak5 = Derivatives(derivs, x + (A5 * h), ytemp);
        for (int i = 0; i < n; i++)
        {
            ytemp[i] = y[i]
                + (Real(h) * ((Real(B61) * dydx[i]) + (Real(B62) * ak2[i]) + (Real(B63) * ak3[i]) + (Real(B64) * ak4[i])
                    + (Real(B65) * ak5[i])));
        }

        // sixth step
        ImmutableArray<T> ak6 = Derivatives(derivs, x + (A6 * h), ytemp);
        for (int i = 0; i < n; i++)
        {
            yout[i] = y[i] + (Real(h) * ((Real(C1) * dydx[i]) + (Real(C3) * ak3[i]) + (Real(C4) * ak4[i]) + (Real(C6) * ak6[i])));
            yerr[i] = Real(h)
                * ((Real(DC1) * dydx[i]) + (Real(DC3) * ak3[i]) + (Real(DC4) * ak4[i]) + (Real(DC5) * ak5[i])
                    + (Real(DC6) * ak6[i]));
        }
    }
}
