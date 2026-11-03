// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/curvestates/coterminalswapcurvestate.hpp ql/models/marketmodels/curvestates/coterminalswapcurvestate.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Models;

/// <summary>
/// The curve state of coterminal swap market models: set on coterminal swap rates, from a first valid index on.
/// </summary>
/// <remarks>
/// A working type. The forwards and the constant-maturity swap rates are computed at each read into the state's own
/// buffers, as QuantLib's are. A rate read at the number of rates, which QuantLib's index check admits and reads past
/// its vectors, is refused with QuantLib's text.
/// </remarks>
[WorkingType]
public sealed class CoterminalSwapCurveState : CurveState
{
    private readonly double[] discRatios;
    private readonly double[] forwardRates;
    private readonly double[] cmSwapRates;
    private readonly double[] cmSwapAnnuities;
    private readonly double[] cotSwapRates;
    private readonly double[] cotAnnuities;
    private int first;

    /// <summary>Initializes a new instance of the <see cref="CoterminalSwapCurveState"/> class, not yet set.</summary>
    /// <param name="rateTimes">The rate times: at least two, strictly increasing, the first greater than zero.</param>
    /// <exception cref="ArgumentException">The rate times aren't two or more strictly increasing positive times.</exception>
    public CoterminalSwapCurveState(ReadOnlySpan<double> rateTimes)
        : base(rateTimes)
    {
        int n = NumberOfRates;
        first = n;
        discRatios = new double[n + 1];
        Array.Fill(discRatios, 1.0);
        forwardRates = new double[n];
        cmSwapRates = new double[n];
        cmSwapAnnuities = new double[n];
        Array.Fill(cmSwapAnnuities, RateTaus[n - 1]);
        cotSwapRates = new double[n];
        cotAnnuities = new double[n];
        Array.Fill(cotAnnuities, RateTaus[n - 1]);
    }

    private CoterminalSwapCurveState(CoterminalSwapCurveState other)
        : base(other)
    {
        discRatios = (double[])other.discRatios.Clone();
        forwardRates = (double[])other.forwardRates.Clone();
        cmSwapRates = (double[])other.cmSwapRates.Clone();
        cmSwapAnnuities = (double[])other.cmSwapAnnuities.Clone();
        cotSwapRates = (double[])other.cotSwapRates.Clone();
        cotAnnuities = (double[])other.cotAnnuities.Clone();
        first = other.first;
    }

    /// <summary>Sets the state on coterminal swap rates, those from the first valid index on.</summary>
    /// <param name="swapRates">The coterminal swap rates, one per rate; those below the first valid index aren't read.</param>
    /// <param name="firstValidIndex">The first valid index, below the number of rates.</param>
    /// <exception cref="ArgumentException">The number of swap rates isn't the number of rates.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="firstValidIndex"/> is outside the rates.</exception>
    public void SetOnCoterminalSwapRates(ReadOnlySpan<double> swapRates, int firstValidIndex = 0)
    {
        int n = NumberOfRates;
        if (!(swapRates.Length == n))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"rates mismatch: {n} required, {swapRates.Length} provided"),
                nameof(swapRates));
        }

        if (!(firstValidIndex >= 0 && firstValidIndex < n))
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstValidIndex),
                string.Create(
                    CultureInfo.InvariantCulture, $"first valid index must be less than {n}: {firstValidIndex} not allowed"));
        }

        first = firstValidIndex;
        swapRates[first..].CopyTo(cotSwapRates.AsSpan(first));

        // ...then calculate discount ratios and coterminal annuities; the last ratio, 1, is set at construction
        cotAnnuities[n - 1] = RateTaus[n - 1];
        for (int i = n - 1; i > first; --i)
        {
            discRatios[i] = 1.0 + (cotSwapRates[i] * cotAnnuities[i]);
            cotAnnuities[i - 1] = cotAnnuities[i] + (RateTaus[i - 1] * discRatios[i]);
        }

        discRatios[first] = 1.0 + (cotSwapRates[first] * cotAnnuities[first]);
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
        return cotAnnuities[i] / discRatios[numeraire];
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The state isn't set yet.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is outside the valid rates.</exception>
    public override double CoterminalSwapRate(int i)
    {
        CheckInitialized();
        CheckRateIndex(i, first, NumberOfRates);
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
        CurveStateFunctions.ConstantMaturityFromDiscountRatios(
            spanningForwards, first, discRatios, RateTaus.AsSpan(), cmSwapRates, cmSwapAnnuities);
        return cmSwapAnnuities[i] / discRatios[numeraire];
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The state isn't set yet.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> or the forwards is outside its range.</exception>
    public override double CmSwapRate(int i, int spanningForwards)
    {
        CheckInitialized();
        CheckRateIndex(i, first, NumberOfRates);
        CurveStateFunctions.ConstantMaturityFromDiscountRatios(
            spanningForwards, first, discRatios, RateTaus.AsSpan(), cmSwapRates, cmSwapAnnuities);
        return cmSwapRates[i];
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
        return cotSwapRates;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The state isn't set yet.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spanningForwards"/> is negative.</exception>
    public override ReadOnlySpan<double> CmSwapRates(int spanningForwards)
    {
        CheckInitialized();
        CurveStateFunctions.ConstantMaturityFromDiscountRatios(
            spanningForwards, first, discRatios, RateTaus.AsSpan(), cmSwapRates, cmSwapAnnuities);
        return cmSwapRates;
    }

    /// <inheritdoc/>
    public override CurveState Clone() => new CoterminalSwapCurveState(this);

    private void CheckInitialized()
    {
        if (!(first < NumberOfRates))
        {
            throw new NanoQLException("curve state not initialized yet");
        }
    }
}
