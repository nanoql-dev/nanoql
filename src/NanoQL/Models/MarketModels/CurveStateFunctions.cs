// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/curvestate.hpp ql/models/marketmodels/curvestate.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// The curve-state conversions of <c>curvestate.hpp</c>: forward, coterminal swap and constant-maturity swap rates
/// from discount ratios, each written into the caller's buffers from the first valid index on.
/// </summary>
public static class CurveStateFunctions
{
    /// <summary>The forward rates of discount ratios: fᵢ = (dᵢ − dᵢ₊₁)/(dᵢ₊₁ τᵢ) from the first valid index on.</summary>
    /// <param name="firstValidIndex">The first rate computed.</param>
    /// <param name="ds">The discount ratios, one more than the rates.</param>
    /// <param name="taus">The accrual periods, one per rate.</param>
    /// <param name="fwds">The forward rates written, one per rate.</param>
    /// <exception cref="ArgumentException">The sizes don't match.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="firstValidIndex"/> is negative.</exception>
    public static void ForwardsFromDiscountRatios(
        int firstValidIndex, ReadOnlySpan<double> ds, ReadOnlySpan<double> taus, Span<double> fwds)
    {
        if (!(taus.Length == fwds.Length))
        {
            throw new ArgumentException("taus.size()!=fwds.size()", nameof(taus));
        }

        if (!(ds.Length == fwds.Length + 1))
        {
            throw new ArgumentException("ds.size()!=fwds.size()+1", nameof(ds));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(firstValidIndex);
        for (int i = firstValidIndex; i < fwds.Length; ++i)
        {
            fwds[i] = (ds[i] - ds[i + 1]) / (ds[i + 1] * taus[i]);
        }
    }

    /// <summary>
    /// The coterminal swap rates and annuities of discount ratios, from the last swap back to the first valid index.
    /// </summary>
    /// <param name="firstValidIndex">The first swap computed.</param>
    /// <param name="discountFactors">The discount ratios, one more than the swaps.</param>
    /// <param name="taus">The accrual periods, one per swap.</param>
    /// <param name="cotSwapRates">The swap rates written, at least one.</param>
    /// <param name="cotSwapAnnuities">The annuities written, one per swap.</param>
    /// <exception cref="ArgumentException">The sizes don't match, or there is no swap.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="firstValidIndex"/> is negative.</exception>
    public static void CoterminalFromDiscountRatios(
        int firstValidIndex,
        ReadOnlySpan<double> discountFactors,
        ReadOnlySpan<double> taus,
        Span<double> cotSwapRates,
        Span<double> cotSwapAnnuities)
    {
        int nCotSwapRates = cotSwapRates.Length;
        if (!(taus.Length == nCotSwapRates))
        {
            throw new ArgumentException("taus.size()!=cotSwapRates.size()", nameof(taus));
        }

        if (!(cotSwapAnnuities.Length == nCotSwapRates))
        {
            throw new ArgumentException("cotSwapAnnuities.size()!=cotSwapRates.size()", nameof(cotSwapAnnuities));
        }

        if (!(discountFactors.Length == nCotSwapRates + 1))
        {
            throw new ArgumentException("discountFactors.size()!=cotSwapRates.size()+1", nameof(discountFactors));
        }

        // QuantLib writes the last swap's annuity first, before its vectors where there is no swap: refused.
        if (!(nCotSwapRates > 0))
        {
            throw new ArgumentException("at least one coterminal swap rate is required", nameof(cotSwapRates));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(firstValidIndex);
        cotSwapAnnuities[nCotSwapRates - 1] = taus[nCotSwapRates - 1] * discountFactors[nCotSwapRates];
        cotSwapRates[nCotSwapRates - 1] =
            (discountFactors[nCotSwapRates - 1] - discountFactors[nCotSwapRates]) / cotSwapAnnuities[nCotSwapRates - 1];

        for (int i = nCotSwapRates - 1; i > firstValidIndex; --i)
        {
            cotSwapAnnuities[i - 1] = cotSwapAnnuities[i] + (taus[i - 1] * discountFactors[i]);
            cotSwapRates[i - 1] = (discountFactors[i - 1] - discountFactors[nCotSwapRates]) / cotSwapAnnuities[i - 1];
        }
    }

    /// <summary>
    /// The constant-maturity swap rates and annuities of discount ratios, each swap spanning
    /// <paramref name="spanningForwards"/> forwards (fewer near the end), from the first valid index on.
    /// </summary>
    /// <param name="spanningForwards">The forwards each swap spans.</param>
    /// <param name="firstValidIndex">The first swap computed, below the number of swaps.</param>
    /// <param name="ds">The discount ratios, one more than the swaps.</param>
    /// <param name="taus">The accrual periods, one per swap.</param>
    /// <param name="constMatSwapRates">The swap rates written.</param>
    /// <param name="constMatSwapAnnuities">The annuities written, one per swap.</param>
    /// <exception cref="ArgumentException">The sizes don't match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="spanningForwards"/> is negative, or <paramref name="firstValidIndex"/> is negative or not below
    /// the number of swaps.
    /// </exception>
    public static void ConstantMaturityFromDiscountRatios(
        int spanningForwards,
        int firstValidIndex,
        ReadOnlySpan<double> ds,
        ReadOnlySpan<double> taus,
        Span<double> constMatSwapRates,
        Span<double> constMatSwapAnnuities)
    {
        int nConstMatSwapRates = constMatSwapRates.Length;
        if (!(taus.Length == nConstMatSwapRates))
        {
            throw new ArgumentException("taus.size()!=nConstMatSwapRates", nameof(taus));
        }

        if (!(constMatSwapAnnuities.Length == nConstMatSwapRates))
        {
            throw new ArgumentException("constMatSwapAnnuities.size()!=nConstMatSwapRates", nameof(constMatSwapAnnuities));
        }

        if (!(ds.Length == nConstMatSwapRates + 1))
        {
            throw new ArgumentException("ds.size()!=nConstMatSwapRates+1", nameof(ds));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(spanningForwards);

        // QuantLib writes the first valid swap's annuity, past its vectors at a first valid index at or past the
        // swaps: refused.
        ArgumentOutOfRangeException.ThrowIfNegative(firstValidIndex);
        if (!(firstValidIndex < nConstMatSwapRates))
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstValidIndex),
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"first valid index ({firstValidIndex}) must be less than the number of swap rates ({nConstMatSwapRates})"));
        }

        // (the spanning bounded by the number of swaps, which changes no index below and keeps the sums in range)
        spanningForwards = Math.Min(spanningForwards, nConstMatSwapRates);

        // compute the first cmsrate and cmsannuity
        constMatSwapAnnuities[firstValidIndex] = 0.0;
        int lastIndex = Math.Min(firstValidIndex + spanningForwards, nConstMatSwapRates);
        for (int i = firstValidIndex; i < lastIndex; ++i)
        {
            constMatSwapAnnuities[firstValidIndex] += taus[i] * ds[i + 1];
        }

        constMatSwapRates[firstValidIndex] = (ds[firstValidIndex] - ds[lastIndex]) / constMatSwapAnnuities[firstValidIndex];
        int oldLastIndex = lastIndex;

        // compute all the other cms rates and cms annuities
        for (int i = firstValidIndex + 1; i < nConstMatSwapRates; ++i)
        {
            lastIndex = Math.Min(i + spanningForwards, nConstMatSwapRates);
            constMatSwapAnnuities[i] = constMatSwapAnnuities[i - 1] - (taus[i - 1] * ds[i]);
            if (lastIndex != oldLastIndex)
            {
                constMatSwapAnnuities[i] += taus[lastIndex - 1] * ds[lastIndex];
            }

            constMatSwapRates[i] = (ds[i] - ds[lastIndex]) / constMatSwapAnnuities[i];
            oldLastIndex = lastIndex;
        }
    }
}
