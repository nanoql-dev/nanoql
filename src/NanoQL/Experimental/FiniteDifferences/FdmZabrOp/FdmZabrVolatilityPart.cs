// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmzabrop.hpp ql/experimental/finitedifferences/fdmzabrop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>The ZABR operator's volatility part, ½ν²v<sup>2γ</sup>·∂²/∂v² on a mesher whose first direction is the
/// forward and second the volatility v.</summary>
/// <remarks>Immutable: its band is built at construction, and <see cref="SetTime"/> changes nothing, as QuantLib's.
/// Experimental, as in QuantLib.</remarks>
[Experimental("NQL0001")]
public sealed class FdmZabrVolatilityPart
{
    private readonly TripleBandLinearOp mapT;

    /// <summary>Initializes a new instance of the <see cref="FdmZabrVolatilityPart"/> class.</summary>
    /// <param name="mesher">The mesher: the forward, then the volatility.</param>
    /// <param name="beta">β, which this part doesn't read.</param>
    /// <param name="nu">ν.</param>
    /// <param name="rho">ρ, which this part doesn't read.</param>
    /// <param name="gamma">γ.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mesher"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="mesher"/> has fewer than two directions, where QuantLib reads past its layout.
    /// </exception>
    public FdmZabrVolatilityPart(FdmMesher mesher, double beta, double nu, double rho, double gamma)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        if (!(mesher.Layout.Dim.Length >= 2))
        {
            throw new ArgumentException($"a mesher of {mesher.Layout.Dim.Length} directions; the ZABR operator takes 2", nameof(mesher));
        }
        Vector volatilityValues = mesher.Locations(1);
        mapT = new SecondDerivativeOp(1, mesher).Mult(0.5 * nu * nu * FdmZabrOp.Pow(volatilityValues, 2.0 * gamma));
    }

    /// <summary>Gets the part's operator.</summary>
    public TripleBandLinearOp Map => mapT;

    /// <summary>Sets the time step: nothing changes with time.</summary>
    /// <param name="t1">The step's start.</param>
    /// <param name="t2">The step's end.</param>
    public void SetTime(double t1, double t2)
    {
    }
}
