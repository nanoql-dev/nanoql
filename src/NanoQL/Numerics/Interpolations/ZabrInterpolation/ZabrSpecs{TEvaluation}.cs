// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/zabrinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Numerics;

/// <summary>
/// ZABR's specification for XABR's fit: five parameters α, β, ν, ρ, γ, their defaults and Halton guesses, and the
/// transforms between them and the optimizer's unconstrained parameters.
/// </summary>
/// <typeparam name="TEvaluation">The kernel of the model instance, a <see cref="ZabrSmileSection{TEvaluation}"/>.</typeparam>
/// <remarks>
/// The direct transform keeps α, β and ρ as SABR's does, and maps ν into [0, 5] and γ into [0, 1.9] through
/// <c>atan</c>. QuantLib's inverse doesn't clamp: α, β and ρ invert as SABR's, so a ρ above 0.9999 flips its sign and
/// an α below 1e-7 becomes a NaN; ν and γ invert through <c>tan</c>, whose period is π, so a ν above 5 or a γ above 1.9
/// comes back as another value inside the range. Here the inverse clamps its arguments to the direct transform's range:
/// SABR's for α and ρ, and the <c>tan</c> arguments to [−π/2, π/2], so a ν above 5 comes back as 5 and a γ above 1.9 as
/// 1.9. The instance is a section built from a copy of the parameters, as QuantLib's is.
/// </remarks>
internal readonly struct ZabrSpecs<TEvaluation> : IXabrSpecs<ZabrSmileSection<TEvaluation>>
    where TEvaluation : struct, IZabrEvaluation
{
    private const double Eps1 = .0000001;
    private const double Eps2 = .9999;

    /// <inheritdoc/>
    public static int Dimension => 5;

    /// <inheritdoc/>
    public static void DefaultValues(
        double?[] parameters, bool[] paramIsFixed, double forward, double expiryTime, ReadOnlySpan<double> addParams)
    {
        parameters[1] ??= 0.5;

        // adapt alpha to beta level
        parameters[0] ??= 0.2 * (parameters[1]!.Value < 0.9999 ? Math.Pow(forward, 1.0 - parameters[1]!.Value) : 1.0);
        parameters[2] ??= Math.Sqrt(0.4);
        parameters[3] ??= 0.0;
        parameters[4] ??= 1.0;
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
        if (!paramIsFixed[1])
        {
            values[1] = ((1.0 - 2E-6) * r[j++]) + 1E-6;
        }

        if (!paramIsFixed[0])
        {
            values[0] = ((1.0 - 2E-6) * r[j++]) + 1E-6; // lognormal vol guess

            // adapt this to beta level
            if (values[1] < 0.999)
            {
                values[0] *= Math.Pow(forward, 1.0 - values[1]);
            }
        }

        if (!paramIsFixed[2])
        {
            values[2] = (1.5 * r[j++]) + 1E-6;
        }

        if (!paramIsFixed[3])
        {
            values[3] = ((2.0 * r[j++]) - 1.0) * (1.0 - 1E-6);
        }

        if (!paramIsFixed[4])
        {
            values[4] = r[j++] * 2.0;
        }
    }

    /// <inheritdoc/>
    public static Vector Inverse(Vector y, ReadOnlySpan<bool> paramIsFixed, ReadOnlySpan<double> parameters, double forward)
    {
        var x = new Vector(5);
        x[0] = y[0] < 25.0 + Eps1 ? Math.Sqrt(SABRSpecs.NonNegative(y[0] - Eps1)) : (y[0] - Eps1 + 25.0) / 10.0;
        x[1] = Math.Sqrt(-Math.Log(y[1]));
        x[2] = Math.Tan(WithinHalfPi(Math.PI * ((y[2] / 5.0) - 0.5)));
        x[3] = Math.Asin(SABRSpecs.WithinOne(y[3] / Eps2));
        x[4] = Math.Tan(WithinHalfPi(Math.PI * ((y[4] / 1.9) - 0.5)));
        return x;
    }

    /// <inheritdoc/>
    public static Vector Direct(Vector x, ReadOnlySpan<bool> paramIsFixed, ReadOnlySpan<double> parameters, double forward)
    {
        var y = new Vector(5);
        y[0] = Math.Abs(x[0]) < 5.0 ? (x[0] * x[0]) + Eps1 : ((10.0 * Math.Abs(x[0])) - 25.0) + Eps1;
        y[1] = Math.Abs(x[1]) < Math.Sqrt(-Math.Log(Eps1)) ? Math.Exp(-(x[1] * x[1])) : Eps1;

        // limit nu to 5.00
        y[2] = ((Math.Atan(x[2]) / Math.PI) + 0.5) * 5.0;
        y[3] = Math.Abs(x[3]) < 2.5 * Math.PI ? Eps2 * Math.Sin(x[3]) : Eps2 * (x[3] > 0.0 ? 1.0 : (-1.0));

        // limit gamma to 1.9
        y[4] = ((Math.Atan(x[4]) / Math.PI) + 0.5) * 1.9;
        return y;
    }

    /// <inheritdoc/>
    public static double Weight(double strike, double forward, double stdDev, ReadOnlySpan<double> addParams) =>
        BlackFormulaStdDevDerivative(strike, forward, stdDev, 1.0);

    /// <inheritdoc/>
    public static ZabrSmileSection<TEvaluation> Instance(
        double t, double forward, double[] parameters, ReadOnlySpan<double> addParams) =>
        new(t, forward, parameters);

    // tan's argument clamped to [-π/2, π/2]: the direct transform's [0, 5] for ν and [0, 1.9] for γ, inverted.
    private static double WithinHalfPi(double a) => a > Math.PI * 0.5 ? Math.PI * 0.5 : (a < -(Math.PI * 0.5) ? -(Math.PI * 0.5) : a);
}
