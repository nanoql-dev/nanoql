// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/curvestates/cmswapcurvestate.hpp ql/models/marketmodels/curvestates/cmswapcurvestate.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Models;

/// <summary>
/// The curve state of constant-maturity swap market models: set on the swap rates spanning a fixed number of forwards,
/// from a first valid index on.
/// </summary>
/// <remarks>
/// A working type (the S-mm plan's MR1 (ii)). Its own spanning's swap rates and annuities are set with it; the forwards,
/// the coterminal swaps and another spanning's swaps are computed at each read into the state's own buffers, as
/// QuantLib's are. A rate read at the number of rates, which QuantLib's index check admits and reads past its vectors,
/// is refused with QuantLib's text (MR3 (xx)).
/// </remarks>
[WorkingType]
public sealed class CMSwapCurveState : CurveState
{
    private readonly int spanningFwds;
    private readonly double[] discRatios;
    private readonly double[] forwardRates;
    private readonly double[] cmSwapRates;
    private readonly double[] cmSwapAnnuities;
    private readonly double[] irrCMSwapRates;
    private readonly double[] irrCMSwapAnnuities;
    private readonly double[] cotSwapRates;
    private readonly double[] cotAnnuities;
    private int first;

    /// <summary>Initializes a new instance of the <see cref="CMSwapCurveState"/> class, not yet set.</summary>
    /// <param name="rateTimes">The rate times: at least two, strictly increasing, the first greater than zero.</param>
    /// <param name="spanningForwards">The forwards each of the state's swaps spans.</param>
    /// <exception cref="ArgumentException">The rate times aren't two or more strictly increasing positive times.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spanningForwards"/> is negative.</exception>
    public CMSwapCurveState(ReadOnlySpan<double> rateTimes, int spanningForwards)
        : base(rateTimes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(spanningForwards);
        int n = NumberOfRates;
        spanningFwds = spanningForwards;
        first = n;
        discRatios = new double[n + 1];
        Array.Fill(discRatios, 1.0);
        forwardRates = new double[n];
        cmSwapRates = new double[n];
        cmSwapAnnuities = new double[n];
        Array.Fill(cmSwapAnnuities, RateTaus[n - 1]);
        irrCMSwapRates = new double[n];
        irrCMSwapAnnuities = new double[n];
        Array.Fill(irrCMSwapAnnuities, RateTaus[n - 1]);
        cotSwapRates = new double[n];
        cotAnnuities = new double[n];
        Array.Fill(cotAnnuities, RateTaus[n - 1]);
    }

    private CMSwapCurveState(CMSwapCurveState other)
        : base(other)
    {
        spanningFwds = other.spanningFwds;
        discRatios = (double[])other.discRatios.Clone();
        forwardRates = (double[])other.forwardRates.Clone();
        cmSwapRates = (double[])other.cmSwapRates.Clone();
        cmSwapAnnuities = (double[])other.cmSwapAnnuities.Clone();
        irrCMSwapRates = (double[])other.irrCMSwapRates.Clone();
        irrCMSwapAnnuities = (double[])other.irrCMSwapAnnuities.Clone();
        cotSwapRates = (double[])other.cotSwapRates.Clone();
        cotAnnuities = (double[])other.cotAnnuities.Clone();
        first = other.first;
    }

    /// <summary>Sets the state on its spanning's swap rates, those from the first valid index on.</summary>
    /// <param name="cmSwapRates">The swap rates, one per rate; those below the first valid index aren't read.</param>
    /// <param name="firstValidIndex">The first valid index, below the number of rates.</param>
    /// <exception cref="ArgumentException">The number of swap rates isn't the number of rates.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="firstValidIndex"/> is outside the rates.</exception>
    public void SetOnCMSwapRates(ReadOnlySpan<double> cmSwapRates, int firstValidIndex = 0)
    {
        int n = NumberOfRates;
        if (!(cmSwapRates.Length == n))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"rates mismatch: {n} required, {cmSwapRates.Length} provided"),
                nameof(cmSwapRates));
        }

        if (!(firstValidIndex >= 0 && firstValidIndex < n))
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstValidIndex),
                string.Create(
                    CultureInfo.InvariantCulture, $"first valid index must be less than {n}: {firstValidIndex} not allowed"));
        }

        // first copy input...
        first = firstValidIndex;
        cmSwapRates[first..].CopyTo(this.cmSwapRates.AsSpan(first));

        // ...then calculate discount ratios and annuities; the last ratio, 1, and the last annuity, the last accrual
        // period, are set at construction. Assume i+1 and do i.
        // (the spanning bounded by the number of rates, which changes no index below and keeps the sums in range)
        int span = Math.Min(spanningFwds, n);
        int oldAnnuityEndIndex = n;
        for (int i = n - 1; i > first; --i)
        {
            // formula 6.1 Joshi Liesch
            int endIndex = Math.Min(i + span, n);
            int annuityEndIndex = Math.Min(i + span - 1, n);

            discRatios[i] = discRatios[endIndex] + (this.cmSwapRates[i] * cmSwapAnnuities[i]);
            cmSwapAnnuities[i - 1] = cmSwapAnnuities[i] + (discRatios[i] * RateTaus[i - 1]);

            if (annuityEndIndex < oldAnnuityEndIndex)
            {
                cmSwapAnnuities[i - 1] -= discRatios[oldAnnuityEndIndex] * RateTaus[oldAnnuityEndIndex - 1];
            }

            oldAnnuityEndIndex = annuityEndIndex;
        }

        int firstEndIndex = Math.Min(first + span, n);
        discRatios[first] = discRatios[firstEndIndex] + (this.cmSwapRates[first] * cmSwapAnnuities[first]);

        // lazy evaluation of forward and coterminal swap rates and annuities
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The state isn't set yet.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An index is outside [the first valid index, the number of rates].</exception>
    public override double DiscountRatio(int i, int j)
    {
        CheckInitialized();
        CheckBondIndex(i, first, NumberOfRates, nameof(i));
        CheckBondIndex(j, first, NumberOfRates, nameof(j));
        return discRatios[i] / discRatios[j];
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The state isn't set yet.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is outside the valid rates.</exception>
    public override double ForwardRate(int i)
    {
        CheckInitialized();
        CheckRateIndex(i, first, NumberOfRates);
        CurveStateFunctions.ForwardsFromDiscountRatios(first, discRatios, RateTaus.AsSpan(), forwardRates);
        return forwardRates[i];
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The state isn't set yet.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The numeraire or <paramref name="i"/> is outside its range.</exception>
    public override double CoterminalSwapAnnuity(int numeraire, int i)
    {
        CheckInitialized();
        CheckNumeraire(numeraire, first, NumberOfRates);
        CheckRateIndex(i, first, NumberOfRates);
        CurveStateFunctions.CoterminalFromDiscountRatios(first, discRatios, RateTaus.AsSpan(), cotSwapRates, cotAnnuities);
        return cotAnnuities[i] / discRatios[numeraire];
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The state isn't set yet.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is outside the valid rates.</exception>
    public override double CoterminalSwapRate(int i)
    {
        CheckInitialized();
        CheckRateIndex(i, first, NumberOfRates);
        CurveStateFunctions.CoterminalFromDiscountRatios(first, discRatios, RateTaus.AsSpan(), cotSwapRates, cotAnnuities);
        return cotSwapRates[i];
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The state isn't set yet.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The numeraire, <paramref name="i"/> or the forwards is outside its range.</exception>
    public override double CmSwapAnnuity(int numeraire, int i, int spanningForwards)
    {
        CheckInitialized();
        CheckNumeraire(numeraire, first, NumberOfRates);
        CheckRateIndex(i, first, NumberOfRates);
        if (spanningForwards == spanningFwds)
        {
            return cmSwapAnnuities[i] / discRatios[numeraire];
        }

        CurveStateFunctions.ConstantMaturityFromDiscountRatios(
            spanningForwards, first, discRatios, RateTaus.AsSpan(), irrCMSwapRates, irrCMSwapAnnuities);
        return irrCMSwapAnnuities[i] / discRatios[numeraire];
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The state isn't set yet.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> or the forwards is outside its range.</exception>
    public override double CmSwapRate(int i, int spanningForwards)
    {
        CheckInitialized();
        CheckRateIndex(i, first, NumberOfRates);
        if (spanningForwards == spanningFwds)
        {
            return cmSwapRates[i];
        }

        CurveStateFunctions.ConstantMaturityFromDiscountRatios(
            spanningForwards, first, discRatios, RateTaus.AsSpan(), irrCMSwapRates, irrCMSwapAnnuities);
        return irrCMSwapRates[i];
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The state isn't set yet.</exception>
    public override ReadOnlySpan<double> ForwardRates()
    {
        CheckInitialized();
        CurveStateFunctions.ForwardsFromDiscountRatios(first, discRatios, RateTaus.AsSpan(), forwardRates);
        return forwardRates;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The state isn't set yet.</exception>
    public override ReadOnlySpan<double> CoterminalSwapRates()
    {
        CheckInitialized();
        CurveStateFunctions.CoterminalFromDiscountRatios(first, discRatios, RateTaus.AsSpan(), cotSwapRates, cotAnnuities);
        return cotSwapRates;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The state isn't set yet.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spanningForwards"/> is negative.</exception>
    public override ReadOnlySpan<double> CmSwapRates(int spanningForwards)
    {
        CheckInitialized();
        if (spanningForwards == spanningFwds)
        {
            return cmSwapRates;
        }

        CurveStateFunctions.ConstantMaturityFromDiscountRatios(
            spanningForwards, first, discRatios, RateTaus.AsSpan(), irrCMSwapRates, irrCMSwapAnnuities);
        return irrCMSwapRates;
    }

    /// <inheritdoc/>
    public override CurveState Clone() => new CMSwapCurveState(this);

    private void CheckInitialized()
    {
        if (!(first < NumberOfRates))
        {
            throw new NanoQLException("curve state not initialized yet");
        }
    }
}
