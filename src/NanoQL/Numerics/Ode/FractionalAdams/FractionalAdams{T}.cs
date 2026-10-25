// SPDX-FileCopyrightText: Copyright (C) 2026 Colin Alberts
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/ode/fractionaladams.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using System.Numerics;

namespace NanoQL.Numerics;

/// <summary>
/// The fractional Adams-Bashforth-Moulton predictor-corrector scheme for the fractional initial value problem
/// D^α y(t) = f(t, y(t)), y(0) = y₀, α in (0, 1], D^α the Caputo derivative; at α = 1, the classical one-step
/// Adams-Bashforth-Moulton method.
/// </summary>
/// <typeparam name="T">The solution's type: <see cref="double"/>, or <see cref="Complex"/> for a complex equation.</typeparam>
/// <remarks>
/// <para>
/// QuantLib's real weights multiply the values componentwise, as C++ multiplies a <c>std::complex</c> by a real (the
/// S-hes plan's HD16). The kernel's memory makes the cost O(N²) in the number of steps.
/// </para>
/// <para>
/// Immutable and thread-safe: it holds only its order, and each solve works on its own arrays.
/// </para>
/// <para>
/// Reference: K. Diethelm, N. J. Ford, A. D. Freed, A Predictor-Corrector Approach for the Numerical Solution of
/// Fractional Differential Equations, Nonlinear Dynamics 29, 3-22 (2002).
/// </para>
/// </remarks>
public sealed class FractionalAdams<T>
    where T : INumberBase<T>
{
    private readonly double alpha;

    /// <summary>Initializes a new instance of the <see cref="FractionalAdams{T}"/> class.</summary>
    /// <param name="alpha">The fractional order, in (0, 1].</param>
    /// <exception cref="ArgumentException"><paramref name="alpha"/> isn't in (0, 1].</exception>
    public FractionalAdams(double alpha)
    {
        if (!(alpha > 0.0 && alpha <= 1.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"fractional order alpha ({alpha}) must be in (0, 1]"),
                nameof(alpha));
        }

        this.alpha = alpha;
    }

    /// <summary>The solution's values on the uniform grid {0, Δt, …, t_max}.</summary>
    /// <param name="ode">The right-hand side f(t, y).</param>
    /// <param name="y0">The initial value.</param>
    /// <param name="tMax">The grid's end.</param>
    /// <param name="steps">The number of steps.</param>
    /// <returns>The <paramref name="steps"/> + 1 values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ode"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tMax"/> or <paramref name="steps"/> isn't positive.</exception>
    public T[] Solve(Func<double, T, T> ode, T y0, double tMax, int steps)
    {
        ArgumentNullException.ThrowIfNull(ode);
        if (!(tMax > 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"tMax ({tMax}) must be positive"), nameof(tMax));
        }

        if (!(steps > 0))
        {
            throw new ArgumentException("at least one step required", nameof(steps));
        }

        double dt = tMax / steps;
        double dtAlpha = Math.Pow(dt, alpha);
        double gamma1 = new GammaFunction().Value(alpha + 1.0);
        double gamma2 = new GammaFunction().Value(alpha + 2.0);

        // powers k ^ alpha and k ^ (alpha + 1) for the quadrature weights
        double[] alphaPowers = new double[steps + 2], alphaPlusOnePowers = new double[steps + 2];
        for (int k = 0; k <= steps + 1; ++k)
        {
            alphaPowers[k] = Math.Pow(k, alpha);
            alphaPlusOnePowers[k] = alphaPowers[k] * k;
        }

        // predictor weights b_k = (k + 1) ^ alpha - k ^ alpha and corrector
        // weights c_k = (k + 2) ^ (alpha + 1) + k ^ (alpha + 1) - 2(k + 1)^(alpha + 1),
        // both depending only on the distance k = n - j to the current step
        double[] predictorWeights = new double[steps], correctorWeights = new double[steps];
        for (int k = 0; k < steps; ++k)
        {
            predictorWeights[k] = alphaPowers[k + 1] - alphaPowers[k];
            correctorWeights[k] = alphaPlusOnePowers[k + 2] + alphaPlusOnePowers[k] - (2.0 * alphaPlusOnePowers[k + 1]);
        }

        T[] y = new T[steps + 1], f = new T[steps + 1];
        y[0] = y0;
        f[0] = ode(0.0, y0);

        for (int n = 0; n < steps; ++n)
        {
            double tNext = (n + 1) * dt;

            // corrector weight of f_0: n ^ (alpha + 1) - (n - alpha)(n + 1) ^ alpha
            double a0 = alphaPlusOnePowers[n] - ((n - alpha) * alphaPowers[n + 1]);

            T predictorSum = Scale(predictorWeights[n], f[0]);
            T correctorSum = Scale(a0, f[0]);
            for (int j = 1; j <= n; ++j)
            {
                T fj = f[j];
                predictorSum += Scale(predictorWeights[n - j], fj);
                correctorSum += Scale(correctorWeights[n - j], fj);
            }

            T yP = y0 + Scale(dtAlpha / gamma1, predictorSum);
            correctorSum += ode(tNext, yP);
            y[n + 1] = y0 + Scale(dtAlpha / gamma2, correctorSum);
            f[n + 1] = ode(tNext, y[n + 1]);
        }

        return y;
    }

    // A real times a value, componentwise for a complex one, as std::complex's real-scalar product (HD16)
    internal static T Scale(double w, T value)
    {
        if (typeof(T) == typeof(Complex))
        {
            var c = (Complex)(object)value;
            return (T)(object)new Complex(w * c.Real, w * c.Imaginary);
        }

        return T.CreateChecked(w) * value;
    }
}
