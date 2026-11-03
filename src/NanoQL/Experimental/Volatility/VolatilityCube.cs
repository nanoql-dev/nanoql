// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/volcube.hpp ql/experimental/volatility/volcube.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A volatility cube of interest-rate volatility surfaces and abcd at-the-money curves on one reference date.
/// </summary>
/// <remarks>
/// Immutable. QuantLib's <c>minIndexTenor()</c> and <c>maxIndexTenor()</c> are declared and never defined, so they
/// aren't ported.
/// </remarks>
[Experimental("NQL0001")]
public sealed class VolatilityCube
{
    /// <summary>Initializes a new instance of the <see cref="VolatilityCube"/> class.</summary>
    /// <param name="surfaces">The surfaces, at least two.</param>
    /// <param name="curves">The at-the-money curves.</param>
    /// <exception cref="ArgumentNullException">An argument or an element is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">There are fewer than two surfaces ("at least 2 surfaces are needed"), or the
    /// reference dates differ ("different reference dates").</exception>
    public VolatilityCube(IEnumerable<InterestRateVolSurface> surfaces, IEnumerable<AbcdAtmVolCurve> curves)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        ArgumentNullException.ThrowIfNull(curves);
        Surfaces = [.. surfaces];
        Curves = [.. curves];
        foreach (InterestRateVolSurface surface in Surfaces)
        {
            ArgumentNullException.ThrowIfNull(surface, nameof(surfaces));
        }

        foreach (AbcdAtmVolCurve curve in Curves)
        {
            ArgumentNullException.ThrowIfNull(curve, nameof(curves));
        }

        if (!(Surfaces.Length > 1))
        {
            throw new NanoQLException("at least 2 surfaces are needed");
        }

        Date refDate = Surfaces[0].ReferenceDate;
        if (Surfaces.Any(s => s.ReferenceDate != refDate) || Curves.Any(c => c.ReferenceDate != refDate))
        {
            throw new NanoQLException("different reference dates");
        }
    }

    /// <summary>Gets the surfaces.</summary>
    public ImmutableArray<InterestRateVolSurface> Surfaces { get; }

    /// <summary>Gets the at-the-money curves.</summary>
    public ImmutableArray<AbcdAtmVolCurve> Curves { get; }
}
