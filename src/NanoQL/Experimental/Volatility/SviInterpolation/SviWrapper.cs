// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/sviinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// QuantLib's <c>SviWrapper</c>, a name for <see cref="SviSmileSection"/>: the model instance SVI's fit builds at
/// every evaluation, a section at the trial parameters, which checks them.
/// </summary>
[Experimental("NQL0001")]
internal sealed class SviWrapper : IXabrModelInstance
{
    private readonly SviSmileSection section;

    /// <summary>Initializes a new instance of the <see cref="SviWrapper"/> class.</summary>
    /// <param name="t">The time to expiry.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="parameters">a, b, σ, ρ and m, copied.</param>
    /// <exception cref="ArgumentException">The section refuses them.</exception>
    public SviWrapper(double t, double forward, double[] parameters) =>
        section = new SviSmileSection(t, forward, parameters);

    /// <inheritdoc/>
    public double Volatility(double x, VolatilityType volatilityType) => section.Volatility(x, volatilityType);
}
