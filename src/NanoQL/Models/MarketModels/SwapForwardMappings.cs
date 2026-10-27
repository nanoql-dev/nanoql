// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2006 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/swapforwardmappings.hpp ql/models/marketmodels/swapforwardmappings.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;
using NanoQL.Utilities;

namespace NanoQL.Models;

/// <summary>
/// The mappings between swap rates and forward rates: annuities, the swap rates' derivatives and Jacobians in the
/// forwards, the Z matrices that switch a model's base from forwards to swap rates, and a swaption's implied volatility
/// by frozen coefficients.
/// </summary>
/// <remarks>
/// Three departures from QuantLib: the coinitial Z matrix scales every entry of its rows, where QuantLib's scales from
/// the diagonal rightward, as the coterminal one does, though a coinitial row is nonzero left of it (the S-mm plan's
/// MR3 (xxii); <c>Divergences/CoinitialZedMatrixTests.cs</c>); and the swaption's implied volatility refuses an
/// evolution whose times don't hold the swaption's expiry, and unequal displacements, as QuantLib's frozen-coefficient
/// adapters refuse them, since it sums whole steps up to the expiry and reads the first displacement alone (MR4 (a);
/// <c>Divergences/SwaptionImpliedVolatilityGridTests.cs</c>).
/// </remarks>
public static class SwapForwardMappings
{
    /// <summary>The annuity of the swap from the start index to the end index, in units of a numeraire bond.</summary>
    /// <param name="cs">The curve state.</param>
    /// <param name="startIndex">The swap's first rate.</param>
    /// <param name="endIndex">The swap's end, after its last rate, at most the number of rates.</param>
    /// <param name="numeraireIndex">The numeraire bond.</param>
    /// <returns>The annuity.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index is outside the rates.</exception>
    /// <remarks>QuantLib reads the accrual periods past their vector at an end past the rates, refused (MR3 (xx)).</remarks>
    public static double Annuity(CurveState cs, int startIndex, int endIndex, int numeraireIndex)
    {
        ArgumentNullException.ThrowIfNull(cs);
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        CheckEnd(cs, endIndex);
        double annuity = 0.0;
        for (int i = startIndex; i < endIndex; ++i)
        {
            annuity += cs.RateTaus[i] * cs.DiscountRatio(i + 1, numeraireIndex);
        }

        return annuity;
    }

    /// <summary>The derivative of the swap rate from the start index to the end index in a forward rate.</summary>
    /// <param name="cs">The curve state.</param>
    /// <param name="startIndex">The swap's first rate.</param>
    /// <param name="endIndex">The swap's end, after its last rate.</param>
    /// <param name="forwardIndex">The forward.</param>
    /// <returns>The derivative, 0 for a forward outside the swap.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An index the derivative reads is outside the rates.</exception>
    public static double SwapDerivative(CurveState cs, int startIndex, int endIndex, int forwardIndex)
    {
        ArgumentNullException.ThrowIfNull(cs);
        if (forwardIndex < startIndex)
        {
            return 0.0;
        }

        if (forwardIndex >= endIndex)
        {
            return 0.0;
        }

        double numerator = cs.DiscountRatio(startIndex, endIndex) - 1;
        double swapAnnuity = Annuity(cs, startIndex, endIndex, endIndex);

        double ratio = cs.RateTaus[forwardIndex] / (1 + (cs.RateTaus[forwardIndex] * cs.ForwardRate(forwardIndex)));

        double part1 = ratio * (numerator + 1) / swapAnnuity;
        double part2 = numerator / (swapAnnuity * swapAnnuity);

        if (forwardIndex >= 1)
        {
            part2 *= ratio * Annuity(cs, startIndex, forwardIndex, endIndex);
        }
        else
        {
            part2 = 0.0;
        }

        return part1 - part2;
    }

    /// <summary>The Jacobian dsrᵢ/dfⱼ of the coterminal swap rates in the forwards.</summary>
    /// <param name="cs">The curve state, set from the first rate.</param>
    /// <returns>A new matrix, swap rates by forwards.</returns>
    public static Matrix CoterminalSwapForwardJacobian(CurveState cs)
    {
        ArgumentNullException.ThrowIfNull(cs);
        int n = cs.NumberOfRates;
        ReadOnlySpan<double> f = cs.ForwardRates();
        ImmutableArray<double> tau = cs.RateTaus;

        // coterminal floating leg values
        double[] a = new double[n];
        for (int k = 0; k < n; ++k)
        {
            a[k] = cs.DiscountRatio(k, n) - 1.0;
        }

        var jacobian = new Matrix(n, n, 0.0);
        for (int i = 0; i < n; ++i)
        {
            // i = swap rate index
            for (int j = i; j < n; ++j)
            {
                // j = forward rate index
                double bi = cs.CoterminalSwapAnnuity(n, i);
                double bj = cs.CoterminalSwapAnnuity(n, j);
                jacobian[i, j] = (tau[j] / cs.CoterminalSwapAnnuity(j + 1, i))
                    + (tau[j] / (1.0 + (f[j] * tau[j])) * ((-a[j] * bi) + (a[i] * bj)) / (bi * bi));
            }
        }

        return jacobian;
    }

    /// <summary>The Z matrix switching a model's base from forwards to coterminal swap rates.</summary>
    /// <param name="cs">The curve state, set from the first rate.</param>
    /// <param name="displacement">The rates' displacement.</param>
    /// <returns>A new matrix, swap rates by forwards.</returns>
    public static Matrix CoterminalSwapZedMatrix(CurveState cs, double displacement)
    {
        ArgumentNullException.ThrowIfNull(cs);
        int n = cs.NumberOfRates;
        Matrix zMatrix = CoterminalSwapForwardJacobian(cs);
        ReadOnlySpan<double> f = cs.ForwardRates();
        ReadOnlySpan<double> sr = cs.CoterminalSwapRates();
        for (int i = 0; i < n; ++i)
        {
            for (int j = i; j < n; ++j)
            {
                zMatrix[i, j] *= (f[j] + displacement) / (sr[i] + displacement);
            }
        }

        return zMatrix;
    }

    /// <summary>The Jacobian dsrᵢ/dfⱼ of the coinitial swap rates, from the first rate time to the (i + 1)-th, in the forwards.</summary>
    /// <param name="cs">The curve state, set from the first rate.</param>
    /// <returns>A new matrix, swap rates by forwards.</returns>
    public static Matrix CoinitialSwapForwardJacobian(CurveState cs)
    {
        ArgumentNullException.ThrowIfNull(cs);
        int n = cs.NumberOfRates;
        var jacobian = new Matrix(n, n, 0.0);
        for (int i = 0; i < n; ++i)
        {
            // i = swap rate index
            for (int j = 0; j < n; ++j)
            {
                // j = forward rate index
                jacobian[i, j] = SwapDerivative(cs, 0, i + 1, j);
            }
        }

        return jacobian;
    }

    /// <summary>The Jacobian dsrᵢ/dfⱼ of the constant-maturity swap rates in the forwards.</summary>
    /// <param name="cs">The curve state.</param>
    /// <param name="spanningForwards">The forwards each swap spans.</param>
    /// <returns>A new matrix, swap rates by forwards.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spanningForwards"/> is negative.</exception>
    public static Matrix CmSwapForwardJacobian(CurveState cs, int spanningForwards)
    {
        ArgumentNullException.ThrowIfNull(cs);
        ArgumentOutOfRangeException.ThrowIfNegative(spanningForwards);
        int n = cs.NumberOfRates;
        var jacobian = new Matrix(n, n, 0.0);
        for (int i = 0; i < n; ++i)
        {
            // i = swap rate index
            for (int j = 0; j < n; ++j)
            {
                // j = forward rate index
                jacobian[i, j] = SwapDerivative(cs, i, (int)Math.Min(n, (long)i + spanningForwards), j);
            }
        }

        return jacobian;
    }

    /// <summary>The Z matrix switching a model's base from forwards to coinitial swap rates.</summary>
    /// <param name="cs">The curve state, set from the first rate.</param>
    /// <param name="displacement">The rates' displacement.</param>
    /// <returns>A new matrix, swap rates by forwards.</returns>
    /// <remarks>
    /// Each row is scaled whole: QuantLib's scales from the diagonal rightward, leaving the entries left of it, nonzero
    /// for a coinitial swap, raw derivatives blind to the displacement (MR3 (xxii)). Each row then equals QuantLib's own
    /// <see cref="CmSwapZedMatrix"/>'s first row at a spanning of the row's swap, bit for bit.
    /// </remarks>
    public static Matrix CoinitialSwapZedMatrix(CurveState cs, double displacement)
    {
        ArgumentNullException.ThrowIfNull(cs);
        int n = cs.NumberOfRates;
        Matrix zMatrix = CoinitialSwapForwardJacobian(cs);
        ReadOnlySpan<double> f = cs.ForwardRates();
        double[] sr = new double[n];

        for (int i = 0; i < n; ++i)
        {
            sr[i] = cs.CmSwapRate(0, i + 1);
        }

        for (int i = 0; i < n; ++i)
        {
            for (int j = 0; j < n; ++j)
            {
                zMatrix[i, j] *= (f[j] + displacement) / (sr[i] + displacement);
            }
        }

        return zMatrix;
    }

    /// <summary>The Z matrix switching a model's base from forwards to constant-maturity swap rates.</summary>
    /// <param name="cs">The curve state.</param>
    /// <param name="spanningForwards">The forwards each swap spans.</param>
    /// <param name="displacement">The rates' displacement.</param>
    /// <returns>A new matrix, swap rates by forwards.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spanningForwards"/> is negative.</exception>
    public static Matrix CmSwapZedMatrix(CurveState cs, int spanningForwards, double displacement)
    {
        ArgumentNullException.ThrowIfNull(cs);
        int n = cs.NumberOfRates;
        Matrix zMatrix = CmSwapForwardJacobian(cs, spanningForwards);
        ReadOnlySpan<double> f = cs.ForwardRates();
        double[] sr = new double[n];

        for (int i = 0; i < n; ++i)
        {
            sr[i] = cs.CmSwapRate(i, spanningForwards);
        }

        for (int i = 0; i < n; ++i)
        {
            for (int j = i; j < n; ++j)
            {
                zMatrix[i, j] *= (f[j] + displacement) / (sr[i] + displacement);
            }
        }

        return zMatrix;
    }

    /// <summary>
    /// The implied volatility of the swaption on the swap from the start index to the end index, by frozen coefficients:
    /// the model's variance of the swap rate summed over the steps to the swaption's expiry, the start index's rate time.
    /// </summary>
    /// <param name="volStructure">The model.</param>
    /// <param name="startIndex">The swap's first rate.</param>
    /// <param name="endIndex">The swap's end, after its last rate, at most the number of rates.</param>
    /// <returns>The implied volatility.</returns>
    /// <exception cref="ArgumentException">
    /// The start index isn't before the end, the model's displacements aren't equal, or its evolution times don't hold
    /// the swaption's expiry.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">An index is outside the rates.</exception>
    public static double SwaptionImpliedVolatility(MarketModel volStructure, int startIndex, int endIndex)
    {
        ArgumentNullException.ThrowIfNull(volStructure);
        if (!(startIndex < endIndex))
        {
            throw new ArgumentException("start index must be before end index in swaptionImpliedVolatility", nameof(startIndex));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        EvolutionDescription evolution = volStructure.Evolution;
        if (!(endIndex <= evolution.NumberOfRates))
        {
            throw new ArgumentOutOfRangeException(
                nameof(endIndex),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"end index ({endIndex}) must be at most the number of rates ({evolution.NumberOfRates})"));
        }

        // QuantLib reads the first displacement alone, and sums whole steps up to the expiry: unequal displacements and
        // an evolution that doesn't stop at the expiry are refused, with its adapters' texts (MR4 (a)).
        ImmutableArray<double> displacements = volStructure.Displacements;
        for (int i = 1; i < displacements.Length; ++i)
        {
            if (!(displacements[i] == displacements[0]))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{DataFormattersFunctions.Ordinal(i + 1)} displacement ({displacements[i]}) not equal to the previous ones ({displacements[0]})"),
                    nameof(volStructure));
            }
        }

        if (!evolution.EvolutionTimes.Contains(evolution.RateTimes[startIndex]))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"skipping {DataFormattersFunctions.Ordinal(startIndex + 1)} rate time"),
                nameof(volStructure));
        }

        var cs = new LMMCurveState(evolution.RateTimes.AsSpan());
        cs.SetOnForwardRates(volStructure.InitialRates.AsSpan());
        double displacement = displacements[0];

        Matrix cmsZed = CmSwapZedMatrix(cs, endIndex - startIndex, displacement);

        double variance = 0.0;
        int index = 0;
        int factors = volStructure.NumberOfFactors;

        while (index < evolution.NumberOfSteps && startIndex >= evolution.FirstAliveRate[index])
        {
            Matrix thisPseudo = volStructure.PseudoRoot(index);
            double thisVariance = 0.0;

            for (int f = 0; f < factors; ++f)
            {
                double sum = 0.0;
                for (int j = startIndex; j < endIndex; ++j)
                {
                    sum += cmsZed[startIndex, j] * thisPseudo[j, f];
                }

                thisVariance += sum * sum;
            }

            variance += thisVariance;
            ++index;
        }

        double expiry = evolution.RateTimes[startIndex];
        return Math.Sqrt(variance / expiry);
    }

    // QuantLib reads the accrual periods up to the end without a check.
    private static void CheckEnd(CurveState cs, int endIndex)
    {
        if (!(endIndex <= cs.NumberOfRates))
        {
            throw new ArgumentOutOfRangeException(
                nameof(endIndex),
                string.Create(
                    CultureInfo.InvariantCulture, $"end index ({endIndex}) must be at most the number of rates ({cs.NumberOfRates})"));
        }
    }
}
