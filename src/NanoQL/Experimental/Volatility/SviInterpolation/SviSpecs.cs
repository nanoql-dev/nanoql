// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/sviinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Experimental;

/// <summary>
/// SVI's specification for XABR's fit: five parameters a, b, σ, ρ and m, their defaults and Halton guesses, and the
/// transforms between them and the optimizer's unconstrained parameters.
/// </summary>
/// <remarks>
/// <para>
/// The direct transform keeps σ above 1e-6, |ρ| below 0.999999, a free b inside b(1+|ρ|) &lt; 4 and a free a inside a +
/// bσ√(1−ρ²) &gt; 0. Two departures from QuantLib, each a fix of its defect.
/// </para>
/// <para>
/// QuantLib's inverse doesn't clamp: a ρ beyond 0.999999 in size takes <c>asin</c> of a value beyond 1, a σ below 1e-6
/// or an a with a + bσ√(1−ρ²) below 1e-6 the square root of a negative number, and a b with b(1+|ρ|) above
/// 4·0.999999 a <c>tan</c> argument past π/2, which wraps. Here <c>asin</c>'s argument is clamped to [−1, 1], each
/// square root's floored at 0 and the <c>tan</c> argument capped at π/2, so such an input comes back as the nearest
/// value the transform reaches.
/// </para>
/// <para>
/// QuantLib's direct transform copies a fixed b or a as given while ρ, σ and a free b move, so a trial can fail
/// <see cref="SviInterpolationFunctions.CheckSviParameters"/> and the fit throws midway. Here, where a trial would fail
/// the check, the free parameters move onto the set it admits, each rounded inward so the check passes: ρ first, its
/// size capped at the tightest bound the fixed parameters set (4/b − 1 for a fixed b; √(1 − (a/(bσ))²) for a fixed a,
/// b and σ; (16σ² − a²)/(16σ² + a²), where the largest b meets a's constraint, for a fixed a and σ with b free), a free
/// b and a recomputed from it; then, for a fixed a, σ rises to −a/(b√(1−ρ²)), or with σ fixed b rises to
/// −a/(σ√(1−ρ²)). A trial the check passes is QuantLib's, bit for bit. A fixed set no ρ admits fails the check at
/// the guesses already, where the fit's construction refuses it with QuantLib's text.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
internal readonly struct SviSpecs : IXabrSpecs<SviWrapper>
{
    private const double Eps1 = 0.000001;
    private const double Eps2 = 0.999999;

    // The most one-ulp steps a moved value takes inward before the check passes.
    private const int MaxSteps = 16;

    /// <inheritdoc/>
    public static int Dimension => 5;

    /// <inheritdoc/>
    public static void DefaultValues(
        double?[] parameters, bool[] paramIsFixed, double forward, double expiryTime, ReadOnlySpan<double> addParams)
    {
        parameters[2] ??= 0.1;
        parameters[3] ??= -0.4;
        parameters[4] ??= 0.0;
        double sigma = parameters[2]!.Value, rho = parameters[3]!.Value, m = parameters[4]!.Value;
        parameters[1] ??= 2.0 / (1.0 + Math.Abs(rho));
        double b = parameters[1]!.Value;
        if (parameters[0] is null)
        {
            double level = (
                0.20 * 0.20 * expiryTime) - (b * ((rho * (-m)) + Math.Sqrt(((-m) * (-m)) + (sigma * sigma))));
            double floor = (-b * sigma * Math.Sqrt(1.0 - (rho * rho))) + Eps1;
            parameters[0] = level < floor ? floor : level;
        }
    }

    /// <inheritdoc/>
    public static void Guess(
        Vector values,
        ReadOnlySpan<bool> paramIsFixed,
        double forward,
        double expiryTime,
        ReadOnlySpan<double> r,
        ReadOnlySpan<double> addParams)
    {
        int j = 0;
        if (!paramIsFixed[2])
        {
            values[2] = r[j++] + Eps1;
        }

        if (!paramIsFixed[3])
        {
            values[3] = ((2.0 * r[j++]) - 1.0) * Eps2;
        }

        if (!paramIsFixed[4])
        {
            values[4] = (2.0 * r[j++]) - 1.0;
        }

        if (!paramIsFixed[1])
        {
            values[1] = r[j++] * 4.0 / (1.0 + Math.Abs(values[3])) * Eps2;
        }

        if (!paramIsFixed[0])
        {
            values[0] = (
                r[j++] * expiryTime) - (Eps2 * (values[1] * values[2] * Math.Sqrt(1.0 - (values[3] * values[3]))));
        }
    }

    /// <inheritdoc/>
    public static Vector Inverse(
        Vector y, ReadOnlySpan<bool> paramIsFixed, ReadOnlySpan<double> parameters, double forward)
    {
        var x = new Vector(5);
        x[2] = Math.Sqrt(NonNegative(y[2] - Eps1));
        x[3] = Math.Asin(WithinOne(y[3] / Eps2));
        x[4] = y[4];
        double angle = (y[1] / 4.0 * (1.0 + Math.Abs(y[3])) / Eps2 * Math.PI) - (Math.PI / 2.0);
        x[1] = Math.Tan(angle > Math.PI / 2.0 ? Math.PI / 2.0 : angle);
        x[0] = Math.Sqrt(NonNegative(y[0] - Eps1 + (y[1] * y[2] * Math.Sqrt(NonNegative(1.0 - (y[3] * y[3]))))));
        return x;
    }

    /// <inheritdoc/>
    public static Vector Direct(
        Vector x, ReadOnlySpan<bool> paramIsFixed, ReadOnlySpan<double> parameters, double forward)
    {
        var y = new Vector(5);
        y[2] = (x[2] * x[2]) + Eps1;
        y[3] = Math.Sin(x[3]) * Eps2;
        y[4] = x[4];
        y[1] = paramIsFixed[1] ? parameters[1] : FreeB(x[1], y[3]);
        y[0] = paramIsFixed[0] ? parameters[0] : FreeA(x[0], y[1], y[2], y[3]);
        if (!Admitted(y[0], y[1], y[2], y[3]))
        {
            MoveOntoAdmittedSet(x, y, paramIsFixed);
        }

        return y;
    }

    /// <inheritdoc/>
    public static double Weight(double strike, double forward, double stdDev, ReadOnlySpan<double> addParams) =>
        BlackFormulaStdDevDerivative(strike, forward, stdDev, 1.0);

    /// <inheritdoc/>
    public static SviWrapper Instance(double t, double forward, double[] parameters, ReadOnlySpan<double> addParams) =>
        new(t, forward, parameters);

    // The square root's argument floored at 0: the direct transform's floors, inverted.
    private static double NonNegative(double a) => a < 0.0 ? 0.0 : a;

    // asin's argument clamped to [-1, 1]: the direct transform's ±0.999999, inverted.
    private static double WithinOne(double a) => a > 1.0 ? 1.0 : (a < -1.0 ? -1.0 : a);

    // QuantLib's transforms of a free b and a free a.
    private static double FreeB(double x1, double rho) =>
        (Math.Atan(x1) + (Math.PI / 2.0)) / Math.PI * Eps2 * 4.0 / (1.0 + Math.Abs(rho));

    private static double FreeA(double x0, double b, double sigma, double rho) =>
        Eps1 + (x0 * x0) - (b * sigma * Math.Sqrt(1.0 - (rho * rho)));

    // CheckSviParameters' conditions, its expressions as written, without the throw.
    private static bool Admitted(double a, double b, double sigma, double rho) =>
        b >= 0.0
        && Math.Abs(rho) < 1.0
        && sigma > 0.0
        && a + (b * sigma * Math.Sqrt(1.0 - (rho * rho))) >= 0.0
        && b * (1.0 + Math.Abs(rho)) <= 4.0;

    private static bool AConstraint(double a, double b, double sigma, double rho) =>
        a + (b * sigma * Math.Sqrt(1.0 - (rho * rho))) >= 0.0;

    private static bool BConstraint(double b, double rho) => b * (1.0 + Math.Abs(rho)) <= 4.0;

    // The free parameters moved onto the set CheckSviParameters admits, where a trial fails it. A NaN or an infinite
    // trial is left to the check.
    private static void MoveOntoAdmittedSet(Vector x, Vector y, ReadOnlySpan<bool> paramIsFixed)
    {
        bool aFixed = paramIsFixed[0], bFixed = paramIsFixed[1];
        bool sigmaFixed = paramIsFixed[2], rhoFixed = paramIsFixed[3];
        double a = y[0], b = y[1], sigma = y[2], rho = y[3];
        if (!(double.IsFinite(a) && double.IsFinite(b) && double.IsFinite(sigma) && double.IsFinite(rho)))
        {
            return;
        }

        // ρ first, capped at the tightest bound the fixed parameters set.
        if (!rhoFixed)
        {
            double bound = 1.0;
            if (bFixed && b > 0.0)
            {
                bound = Math.Min(bound, (4.0 / b) - 1.0);
            }

            if (aFixed && a < 0.0 && sigmaFixed)
            {
                if (bFixed && b > 0.0)
                {
                    double ratio = a / (b * sigma);
                    bound = Math.Min(bound, Math.Sqrt(NonNegative(1.0 - (ratio * ratio))));
                }
                else if (!bFixed)
                {
                    double s2 = 16.0 * sigma * sigma, a2 = a * a;
                    bound = Math.Min(bound, (s2 - a2) / (s2 + a2));
                }
            }

            bound = NonNegative(bound);
            if (Math.Abs(rho) > bound)
            {
                double sign = rho < 0.0 ? -1.0 : 1.0, size = bound;
                for (int k = 0; k < MaxSteps && !RhoInward(aFixed, bFixed, sigmaFixed, a, b, sigma, sign * size); ++k)
                {
                    size = Math.BitDecrement(size);
                }

                rho = sign * size;
                if (!bFixed)
                {
                    b = FreeB(x[1], rho);
                }

                if (!aFixed)
                {
                    a = FreeA(x[0], b, sigma, rho);
                }
            }
        }

        // Then, for a fixed a, σ or b rises to meet a's constraint.
        if (aFixed && !AConstraint(a, b, sigma, rho))
        {
            double root = Math.Sqrt(1.0 - (rho * rho));
            if (!sigmaFixed && b > 0.0)
            {
                sigma = -a / (b * root);
                for (int k = 0; k < MaxSteps && !AConstraint(a, b, sigma, rho); ++k)
                {
                    sigma = Math.BitIncrement(sigma);
                }
            }
            else if (!bFixed && sigma > 0.0)
            {
                b = -a / (sigma * root);
                for (int k = 0; k < MaxSteps && !AConstraint(a, b, sigma, rho); ++k)
                {
                    b = Math.BitIncrement(b);
                }
            }
        }

        y[0] = a;
        y[1] = b;
        y[2] = sigma;
        y[3] = rho;
    }

    // Whether a capped ρ meets the constraints its bound was set for: b's with a fixed b; a's with a fixed a, b and
    // σ; and, for a fixed a and σ with b free, the b that a's constraint then needs within b's.
    private static bool RhoInward(
        bool aFixed, bool bFixed, bool sigmaFixed, double a, double b, double sigma, double rho)
    {
        if (bFixed && !BConstraint(b, rho))
        {
            return false;
        }

        if (aFixed && a < 0.0 && sigmaFixed)
        {
            if (bFixed)
            {
                return AConstraint(a, b, sigma, rho);
            }

            double needed = -a / (sigma * Math.Sqrt(1.0 - (rho * rho)));
            for (int k = 0; k < MaxSteps && !AConstraint(a, needed, sigma, rho); ++k)
            {
                needed = Math.BitIncrement(needed);
            }

            return BConstraint(needed, rho);
        }

        return true;
    }
}
