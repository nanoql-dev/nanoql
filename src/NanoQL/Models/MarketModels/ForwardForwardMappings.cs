// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/forwardforwardmappings.hpp ql/models/marketmodels/forwardforwardmappings.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The mappings between forward rates of a multiple tenor and the forward rates of the curve state's: the Jacobian of
/// the long forwards in the short ones, the Y matrix that switches a model's base from short to long forwards, and the
/// curve state restricted to the long forwards' times.
/// </summary>
/// <remarks>
/// The k-th long forward runs from rate time offset + k × multiplier over multiplier short forwards. QuantLib's
/// Jacobian is 1/τ_long minus the long forward's derivative, and its Y matrix's long forward the negative of g/(1 + τ_long
/// g); both are fixed here (the S-mm plan's MR3 (i); <c>Divergences/ForwardForwardMappingsTests.cs</c>): the derivative
/// (P_start/P_end) τ_m (P_{m+1}/P_m)/τ_long, and the long forward (P_start/P_end − 1)/τ_long, QuantLib's own
/// <see cref="RestrictCurveState"/> forward, which central bumps of it check. A multiplier or an offset QuantLib's
/// unsigned arithmetic wraps to an endless allocation (an offset past the rates) is refused.
/// </remarks>
public static class ForwardForwardMappings
{
    /// <summary>The Jacobian dgᵢ/dfⱼ of the long forwards in the short ones.</summary>
    /// <param name="cs">The curve state, set from the first rate.</param>
    /// <param name="multiplier">The short forwards each long one spans.</param>
    /// <param name="offset">The first long forward's first short one, below the multiplier and at most the rates.</param>
    /// <returns>A new matrix, long forwards by short ones.</returns>
    /// <exception cref="ArgumentException">The offset isn't below the multiplier.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The offset is negative or past the rates.</exception>
    public static Matrix ForwardForwardJacobian(CurveState cs, int multiplier, int offset)
    {
        ArgumentNullException.ThrowIfNull(cs);
        int n = cs.NumberOfRates;
        int k = LongForwards(n, multiplier, offset);

        System.Collections.Immutable.ImmutableArray<double> tau = cs.RateTaus;
        var jacobian = new Matrix(k, n, 0.0);

        int m = offset;
        for (int l = 0; l < k; ++l)
        {
            double df = cs.DiscountRatio(m, m + multiplier);
            double bigTau = cs.RateTimes[m + multiplier] - cs.RateTimes[m];

            for (int r = 0; r < multiplier; ++r, ++m)
            {
                // dg/df_m = (P_start/P_end) τ_m (P_{m+1}/P_m) / τ_long; QuantLib's is 1/τ_long minus it (MR3 (i))
                double value = df * tau[m] * cs.DiscountRatio(m + 1, m);
                value /= bigTau;
                jacobian[l, m] = value;
            }
        }

        return jacobian;
    }

    /// <summary>The Y matrix switching a model's base from short to long forwards: the Jacobian in displaced logs.</summary>
    /// <param name="cs">The curve state, set from the first rate.</param>
    /// <param name="shortDisplacements">The short forwards' displacements, one per rate.</param>
    /// <param name="longDisplacements">The long forwards' displacements, one per long forward.</param>
    /// <param name="multiplier">The short forwards each long one spans.</param>
    /// <param name="offset">The first long forward's first short one, below the multiplier and at most the rates.</param>
    /// <returns>A new matrix, long forwards by short ones.</returns>
    /// <exception cref="ArgumentException">The offset isn't below the multiplier, or a displacement count is wrong.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The offset is negative or past the rates.</exception>
    public static Matrix YMatrix(
        CurveState cs,
        ReadOnlySpan<double> shortDisplacements,
        ReadOnlySpan<double> longDisplacements,
        int multiplier,
        int offset)
    {
        ArgumentNullException.ThrowIfNull(cs);
        int n = cs.NumberOfRates;
        int k = LongForwards(n, multiplier, offset);

        if (!(shortDisplacements.Length == n))
        {
            throw new ArgumentException("shortDisplacements must be of size equal to number of rates", nameof(shortDisplacements));
        }

        if (!(longDisplacements.Length == k))
        {
            throw new ArgumentException(
                "longDisplacements must be of size equal to (number of rates minus offset) divided by multiplier",
                nameof(longDisplacements));
        }

        Matrix jacobian = ForwardForwardJacobian(cs, multiplier, offset);

        for (int i = 0; i < k; ++i)
        {
            double tau = cs.RateTimes[((i + 1) * multiplier) + offset] - cs.RateTimes[(i * multiplier) + offset];

            // g = (P_start/P_end − 1)/τ_long; QuantLib's ratio is P_end/P_start (MR3 (i))
            double longForward = (cs.DiscountRatio((i * multiplier) + offset, ((i + 1) * multiplier) + offset) - 1.0) / tau;
            double longForwardDisplaced = longForward + longDisplacements[i];
            for (int j = 0; j < n; ++j)
            {
                double shortForward = cs.ForwardRate(j);
                double shortForwardDisplaced = shortForward + shortDisplacements[j];
                jacobian[i, j] *= shortForwardDisplaced / longForwardDisplaced;
            }
        }

        return jacobian;
    }

    /// <summary>The curve state on the long forwards' times: every multiplier-th rate time from the offset.</summary>
    /// <param name="cs">The curve state, set from the first rate.</param>
    /// <param name="multiplier">The short forwards each long one spans.</param>
    /// <param name="offset">The first long forward's first short one, below the multiplier and at most the rates.</param>
    /// <returns>A new state, set on the restricted discount ratios.</returns>
    /// <exception cref="ArgumentException">The offset isn't below the multiplier, or no long forward fits.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The offset is negative or past the rates.</exception>
    public static LMMCurveState RestrictCurveState(CurveState cs, int multiplier, int offset)
    {
        ArgumentNullException.ThrowIfNull(cs);
        int n = cs.NumberOfRates;
        int k = LongForwards(n, multiplier, offset);

        double[] times = new double[k + 1];
        double[] discRatios = new double[k + 1];

        for (int i = 0; i < k + 1; ++i)
        {
            times[i] = cs.RateTimes[(i * multiplier) + offset];
            discRatios[i] = cs.DiscountRatio((i * multiplier) + offset, 0);
        }

        var newState = new LMMCurveState(times);
        newState.SetOnDiscountRatios(discRatios);
        return newState;
    }

    // QuantLib's (n − offset)/multiplier, after its check; an offset past the rates wraps QuantLib's unsigned
    // difference to an allocation that throws, refused here.
    private static int LongForwards(int n, int multiplier, int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (!(offset < multiplier))
        {
            throw new ArgumentException("offset  must be less than period in  forward forward mappings", nameof(offset));
        }

        if (!(offset <= n))
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                string.Create(CultureInfo.InvariantCulture, $"offset ({offset}) must be at most the number of rates ({n})"));
        }

        return (n - offset) / multiplier;
    }
}
