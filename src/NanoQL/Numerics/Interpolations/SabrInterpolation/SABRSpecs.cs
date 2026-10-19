// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/sabrinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Numerics;

/// <summary>
/// SABR's specification for XABR's fit: four parameters α, β, ν, ρ, their defaults and Halton guesses, and the
/// transforms between them and the optimizer's unconstrained parameters.
/// </summary>
/// <remarks>
/// The direct transform floors α and ν at 1e-7, keeps β in [1e-7, 1] and ρ in [−0.9999, 0.9999]. QuantLib's inverse
/// doesn't clamp: a ρ above 0.9999 in size takes <c>asin</c> of a value beyond 1, a NaN that the direct transform
/// turns into −0.9999, flipping a positive ρ's sign; an α or ν below 1e-7 takes the square root of a negative number,
/// a NaN the fit then refuses. Here the inverse clamps its arguments to the direct transform's range, so such an input
/// comes back as the nearest value the transform reaches. Fixed parameters go through both transforms at every
/// evaluation, as QuantLib's do, and come back changed in their last bits.
/// </remarks>
internal readonly struct SABRSpecs : IXabrSpecs<SABRWrapper>
{
    private const double Eps1 = .0000001;
    private const double Eps2 = .9999;

    /// <inheritdoc/>
    public static int Dimension => 4;

    /// <inheritdoc/>
    public static void DefaultValues(
        double?[] parameters, bool[] paramIsFixed, double forward, double expiryTime, ReadOnlySpan<double> addParams)
    {
        parameters[1] ??= 0.5;

        // adapt alpha to beta level
        parameters[0] ??= 0.2 * (parameters[1]!.Value < 0.9999
            ? Math.Pow(forward + (addParams.IsEmpty ? 0.0 : addParams[0]), 1.0 - parameters[1]!.Value)
            : 1.0);
        parameters[2] ??= Math.Sqrt(0.4);
        parameters[3] ??= 0.0;
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
                values[0] *= Math.Pow(forward + (addParams.IsEmpty ? 0.0 : addParams[0]), 1.0 - values[1]);
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
    }

    /// <inheritdoc/>
    public static Vector Inverse(Vector y, ReadOnlySpan<bool> paramIsFixed, ReadOnlySpan<double> parameters, double forward)
    {
        var x = new Vector(4);
        x[0] = y[0] < 25.0 + Eps1 ? Math.Sqrt(NonNegative(y[0] - Eps1)) : (y[0] - Eps1 + 25.0) / 10.0;
        x[1] = Math.Sqrt(-Math.Log(y[1]));
        x[2] = y[2] < 25.0 + Eps1 ? Math.Sqrt(NonNegative(y[2] - Eps1)) : (y[2] - Eps1 + 25.0) / 10.0;
        x[3] = Math.Asin(WithinOne(y[3] / Eps2));
        return x;
    }

    /// <inheritdoc/>
    public static Vector Direct(Vector x, ReadOnlySpan<bool> paramIsFixed, ReadOnlySpan<double> parameters, double forward)
    {
        var y = new Vector(4);
        y[0] = Math.Abs(x[0]) < 5.0 ? (x[0] * x[0]) + Eps1 : ((10.0 * Math.Abs(x[0])) - 25.0) + Eps1;
        y[1] = Math.Abs(x[1]) < Math.Sqrt(-Math.Log(Eps1)) ? Math.Exp(-(x[1] * x[1])) : Eps1;
        y[2] = Math.Abs(x[2]) < 5.0 ? (x[2] * x[2]) + Eps1 : ((10.0 * Math.Abs(x[2])) - 25.0) + Eps1;
        y[3] = Math.Abs(x[3]) < 2.5 * Math.PI ? Eps2 * Math.Sin(x[3]) : Eps2 * (x[3] > 0.0 ? 1.0 : (-1.0));
        return y;
    }

    /// <inheritdoc/>
    public static double Weight(double strike, double forward, double stdDev, ReadOnlySpan<double> addParams) =>
        BlackFormulaStdDevDerivative(strike, forward, stdDev, 1.0, addParams[0]);

    /// <inheritdoc/>
    public static SABRWrapper Instance(double t, double forward, double[] parameters, ReadOnlySpan<double> addParams) =>
        new(t, forward, parameters, addParams);

    // The square root's argument floored at 0, std::max(a, 0.0): the direct transform's 1e-7 floor, inverted.
    internal static double NonNegative(double a) => a < 0.0 ? 0.0 : a;

    // asin's argument clamped to [-1, 1]: the direct transform's ±0.9999, inverted.
    internal static double WithinOne(double a) => a > 1.0 ? 1.0 : (a < -1.0 ? -1.0 : a);
}
