// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2006 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/curvestates/lmmcurvestate.hpp ql/models/marketmodels/curvestates/lmmcurvestate.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Models;

/// <summary>
/// The curve state of LIBOR market models: set on forward rates or discount ratios, from a first valid index on.
/// </summary>
/// <remarks>
/// A working type (the S-mm plan's MR1 (ii)). The coterminal swap annuities are computed lazily, from the last back to
/// the one asked for, as QuantLib's are, and the constant-maturity swap rates at each read; both into the state's own
/// buffers, reset at each set, so every read is QuantLib's arithmetic in QuantLib's order. A rate read at the number of
/// rates, which QuantLib's index check admits and reads past its vectors, is refused with QuantLib's text (MR3 (xx)).
/// </remarks>
public sealed class LMMCurveState : CurveState
{
    private readonly double[] discRatios;
    private readonly double[] forwardRates;
    private readonly double[] cmSwapRates;
    private readonly double[] cmSwapAnnuities;
    private readonly double[] cotSwapRates;
    private readonly double[] cotAnnuities;
    private int first;
    private int firstCotAnnuityComped;

    /// <summary>Initializes a new instance of the <see cref="LMMCurveState"/> class, not yet set.</summary>
    /// <param name="rateTimes">The rate times: at least two, strictly increasing, the first greater than zero.</param>
    /// <exception cref="ArgumentException">The rate times aren't two or more strictly increasing positive times.</exception>
    public LMMCurveState(ReadOnlySpan<double> rateTimes)
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
        firstCotAnnuityComped = n;
    }

    private LMMCurveState(LMMCurveState other)
        : base(other)
    {
        discRatios = (double[])other.discRatios.Clone();
        forwardRates = (double[])other.forwardRates.Clone();
        cmSwapRates = (double[])other.cmSwapRates.Clone();
        cmSwapAnnuities = (double[])other.cmSwapAnnuities.Clone();
        cotSwapRates = (double[])other.cotSwapRates.Clone();
        cotAnnuities = (double[])other.cotAnnuities.Clone();
        first = other.first;
        firstCotAnnuityComped = other.firstCotAnnuityComped;
    }

    /// <summary>Sets the state on forward rates, those from the first valid index on.</summary>
    /// <param name="fwdRates">The forward rates, one per rate; those below the first valid index aren't read.</param>
    /// <param name="firstValidIndex">The first valid index, below the number of rates.</param>
    /// <exception cref="ArgumentException">The number of forwards isn't the number of rates.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="firstValidIndex"/> is outside the rates.</exception>
    public void SetOnForwardRates(ReadOnlySpan<double> fwdRates, int firstValidIndex = 0)
    {
        int n = NumberOfRates;
        if (!(fwdRates.Length == n))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"rates mismatch: {n} required, {fwdRates.Length} provided"),
                nameof(fwdRates));
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
        fwdRates[first..].CopyTo(forwardRates.AsSpan(first));

        // ...then calculate discount ratios; the last, 1, is set at construction
        for (int i = first; i < n; ++i)
        {
            discRatios[i + 1] = discRatios[i] / (1.0 + (forwardRates[i] * RateTaus[i]));
        }

        // lazy evaluation of the coterminal swap rates and annuities and the constant maturity swap rates and annuities
        firstCotAnnuityComped = n;
    }

    /// <summary>Sets the state on discount ratios, those from the first valid index on.</summary>
    /// <param name="discRatios">The discount ratios, one more than the rates; those below the first valid index aren't read.</param>
    /// <param name="firstValidIndex">The first valid index, below the number of rates.</param>
    /// <exception cref="ArgumentException">The number of ratios isn't one more than the number of rates.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="firstValidIndex"/> is outside the rates.</exception>
    /// <remarks>
    /// QuantLib's refusal of a first valid index names one more than the number of rates as its bound while checking
    /// against the number of rates; the text here names the bound it checks (the S-mm plan's MR3 (xv);
    /// <c>Divergences/MarketModelTextTests.cs</c>).
    /// </remarks>
    public void SetOnDiscountRatios(ReadOnlySpan<double> discRatios, int firstValidIndex = 0)
    {
        int n = NumberOfRates;
        if (!(discRatios.Length == n + 1))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture, $"too many discount ratios: {n + 1} required, {discRatios.Length} provided"),
                nameof(discRatios));
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
        discRatios[first..].CopyTo(this.discRatios.AsSpan(first));

        // ...then calculate forwards
        for (int i = first; i < n; ++i)
        {
            forwardRates[i] = ((this.discRatios[i] / this.discRatios[i + 1]) - 1.0) / RateTaus[i];
        }

        // lazy evaluation of the coterminal swap rates and annuities and the constant maturity swap rates and annuities
        firstCotAnnuityComped = n;
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
        return CotAnnuity(numeraire, i);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The state isn't set yet.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is outside the valid rates.</exception>
    public override double CoterminalSwapRate(int i)
    {
        CheckInitialized();
        CheckRateIndex(i, first, NumberOfRates);
        int n = NumberOfRates;
        return ((discRatios[i] / discRatios[n]) - 1.0) / CotAnnuity(n, i);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The state isn't set yet.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The numeraire, <paramref name="i"/> or the forwards is outside its range.</exception>
    public override double CmSwapAnnuity(int numeraire, int i, int spanningForwards)
    {
        CheckInitialized();
        CheckNumeraire(numeraire, first, NumberOfRates);
        CheckRateIndex(i, first, NumberOfRates);

        // consider lazy evaluation here
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

        // consider lazy evaluation here
        CurveStateFunctions.ConstantMaturityFromDiscountRatios(
            spanningForwards, first, discRatios, RateTaus.AsSpan(), cmSwapRates, cmSwapAnnuities);
        return cmSwapRates[i];
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The state isn't set yet.</exception>
    public override ReadOnlySpan<double> ForwardRates()
    {
        CheckInitialized();
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
        CurveStateFunctions.ConstantMaturityFromDiscountRatios(
            spanningForwards, first, discRatios, RateTaus.AsSpan(), cmSwapRates, cmSwapAnnuities);
        return cmSwapRates;
    }

    /// <inheritdoc/>
    public override CurveState Clone() => new LMMCurveState(this);

    // QuantLib's lazy coterminal annuity: the annuities from the last computed back to i, each from the one after it.
    private double CotAnnuity(int numeraire, int i)
    {
        int n = NumberOfRates;
        if (firstCotAnnuityComped <= i)
        {
            return cotAnnuities[i] / discRatios[numeraire];
        }

        if (firstCotAnnuityComped == n)
        {
            cotAnnuities[n - 1] = RateTaus[n - 1] * discRatios[n];
            --firstCotAnnuityComped;
        }

        for (int j = firstCotAnnuityComped - 1; j >= i; --j)
        {
            cotAnnuities[j] = cotAnnuities[j + 1] + (RateTaus[j] * discRatios[j + 1]);
        }

        firstCotAnnuityComped = i;
        return cotAnnuities[i] / discRatios[numeraire];
    }

    private void CheckInitialized()
    {
        if (!(first < NumberOfRates))
        {
            throw new NanoQLException("curve state not initialized yet");
        }
    }
}
