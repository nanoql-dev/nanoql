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

using System.Globalization;
using NanoQL.TermStructures;

namespace NanoQL.Numerics;

/// <summary>
/// SABR's model instance in XABR's fit: the shifted SABR volatility at the fit's parameters, which it reads from the
/// fit's own array, as QuantLib's keeps a reference to the coefficient holder's.
/// </summary>
internal sealed class SABRWrapper : IXabrModelInstance
{
    private readonly double t;
    private readonly double forward;
    private readonly double[] parameters;
    private readonly double shift;

    /// <summary>Initializes a new instance of the <see cref="SABRWrapper"/> class.</summary>
    /// <param name="t">The time to expiry.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="parameters">The fit's parameter array: α, β, ν, ρ, kept by reference.</param>
    /// <param name="addParams">The shift, or nothing for none.</param>
    /// <exception cref="ArgumentException">The forward plus the shift isn't positive, or a parameter is out of its
    /// range.</exception>
    internal SABRWrapper(double t, double forward, double[] parameters, ReadOnlySpan<double> addParams)
    {
        this.t = t;
        this.forward = forward;
        this.parameters = parameters;
        shift = addParams.IsEmpty ? 0.0 : addParams[0];
        if (!(forward + shift > 0.0))
        {
            throw new ArgumentException(
                $"forward+shift must be positive: {Show(forward)} with shift {Show(shift)} not allowed", nameof(forward));
        }

        SabrFunctions.ValidateSabrParameters(parameters[0], parameters[1], parameters[2], parameters[3]);
    }

    /// <inheritdoc/>
    public double Volatility(double x, VolatilityType volatilityType) =>
        SabrFunctions.CheckedShiftedVolatility(
            x, forward, t, parameters[0], parameters[1], parameters[2], parameters[3], shift, volatilityType);

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);
}
