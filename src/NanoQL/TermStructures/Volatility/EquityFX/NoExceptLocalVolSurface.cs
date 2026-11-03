// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/noexceptlocalvolsurface.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// Dupire's local volatility surface that answers a given value where <see cref="LocalVolSurface"/> refuses, as where
/// the local variance turns negative.
/// </summary>
/// <remarks>
/// Immutable, as its base. It catches <see cref="NanoQLException"/>, the refusals of the surface's reads, where
/// QuantLib's catches its <c>Error</c>.
/// </remarks>
public sealed class NoExceptLocalVolSurface : LocalVolSurface
{
    private readonly double illegalLocalVolOverwrite;

    /// <summary>Initializes a new instance of the <see cref="NoExceptLocalVolSurface"/> class.</summary>
    /// <param name="blackTS">The Black volatility surface.</param>
    /// <param name="riskFreeTS">The risk-free curve.</param>
    /// <param name="dividendTS">The dividend curve.</param>
    /// <param name="underlying">The underlying's level.</param>
    /// <param name="illegalLocalVolOverwrite">The value answered where the surface refuses.</param>
    /// <exception cref="ArgumentNullException">A structure is <see langword="null"/>.</exception>
    public NoExceptLocalVolSurface(
        BlackVolTermStructure blackTS,
        YieldTermStructure riskFreeTS,
        YieldTermStructure dividendTS,
        double underlying,
        double illegalLocalVolOverwrite)
        : base(blackTS, riskFreeTS, dividendTS, underlying)
    {
        this.illegalLocalVolOverwrite = illegalLocalVolOverwrite;
    }

    /// <summary>The local volatility, or the overwrite where the surface refuses.</summary>
    /// <param name="t">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The local volatility.</returns>
    protected override double LocalVolImpl(double t, double strike)
    {
        double vol;
        try
        {
            vol = base.LocalVolImpl(t, strike);
        }
        catch (NanoQLException)
        {
            vol = illegalLocalVolOverwrite;
        }

        return vol;
    }
}
