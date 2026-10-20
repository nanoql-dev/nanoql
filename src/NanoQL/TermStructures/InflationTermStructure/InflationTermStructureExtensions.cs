// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflationtermstructure.hpp ql/termstructures/inflationtermstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// QuantLib's <c>InflationTermStructure::setSeasonality</c>, which changes a built structure, as a copy of the
/// receiver's own type.
/// </summary>
public static class InflationTermStructureExtensions
{
    /// <summary>A copy of an inflation term structure with another seasonality.</summary>
    /// <typeparam name="T">The structure's type, which the copy keeps.</typeparam>
    /// <param name="curve">The structure; it is unchanged.</param>
    /// <param name="seasonality">The seasonality, or <see langword="null"/> for none, as QuantLib's setter always
    /// replaces it.</param>
    /// <param name="context">The evaluation context, under which a bootstrapped curve's copy is bootstrapped again,
    /// from a cold start.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="curve"/> or <paramref name="context"/> is
    /// null.</exception>
    /// <exception cref="NanoQLException">The seasonality is inconsistent with the structure: the seasonality's own
    /// refusal, as a multi-year <see cref="MultiplicativePriceSeasonality"/>'s, or QuantLib's "Seasonality inconsistent
    /// with inflation term structure" when its <see cref="Seasonality.IsConsistent"/> returns false.</exception>
    /// <remarks>
    /// QuantLib's setter changes its curve, and assigns before it checks, so a refused seasonality stays on the curve;
    /// here the receiver is unchanged whether the seasonality is accepted or refused, a documented divergence. QuantLib
    /// re-bootstraps a piecewise curve warm, from its own nodes, which differs from a fresh build by up to 7.9e-15 at
    /// QuantLib's default accuracy; here the copy is always built afresh, a documented divergence.
    /// </remarks>
    public static T SetSeasonality<T>(this T curve, Seasonality? seasonality, EvaluationContext context)
        where T : InflationTermStructure
    {
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentNullException.ThrowIfNull(context);
        return (T)curve.WithSeasonality(seasonality, context);
    }
}
