// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/curvestate.hpp ql/models/marketmodels/curvestate.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// The curve state of a market-model simulation: the yield curve at the simulation's fixed rate times, the workhorse
/// discounting object a product reads, so that a product runs on any kind of model (LIBOR, coterminal swap or
/// constant-maturity swap).
/// </summary>
/// <remarks>
/// <para>
/// There are n + 1 rate times, the payment and reset times of n forward rates: t₀ … tₙ, the forwards f₀ … fₙ₋₁, the
/// discount bonds d₀ … dₙ, the discount ratios dᵢ/d₀, and the coterminal swap rates sr₀ … srₙ₋₁.
/// </para>
/// <para>
/// A working type (the S-mm plan's MR1 (ii)): a derived state changes in place when it is set, and computes some of its
/// reads lazily into buffers of its own, so it is owned by one computation and isn't thread-safe. Its vector reads
/// (<see cref="ForwardRates"/>, <see cref="CoterminalSwapRates"/>, <see cref="CmSwapRates"/>) are views of its own
/// buffers, valid until it is next set or the same read is made with another argument (MR1 (iii)); a caller that keeps
/// one copies it, and <see cref="Clone"/> keeps the whole state. An extension point, as in QuantLib; the setters live
/// on the derived states only.
/// </para>
/// </remarks>
[WorkingType]
public abstract class CurveState
{
    /// <summary>Initializes a new instance of the <see cref="CurveState"/> class.</summary>
    /// <param name="rateTimes">The rate times: at least two, strictly increasing, the first greater than zero.</param>
    /// <exception cref="ArgumentException">The rate times aren't two or more strictly increasing positive times.</exception>
    protected CurveState(ReadOnlySpan<double> rateTimes)
    {
        NumberOfRates = rateTimes.IsEmpty ? 0 : rateTimes.Length - 1;
        double[] taus = new double[NumberOfRates];
        UtilitiesFunctions.CheckIncreasingTimesAndCalculateTaus(rateTimes, taus);
        RateTimes = [.. rateTimes];
        RateTaus = ImmutableArray.Create(taus);
    }

    /// <summary>Initializes a new instance of the <see cref="CurveState"/> class as a copy of another's times.</summary>
    /// <param name="other">The state copied.</param>
    protected CurveState(CurveState other)
    {
        ArgumentNullException.ThrowIfNull(other);
        NumberOfRates = other.NumberOfRates;
        RateTimes = other.RateTimes;
        RateTaus = other.RateTaus;
    }

    /// <summary>Gets the number of rates, one less than the rate times.</summary>
    public int NumberOfRates { get; }

    /// <summary>Gets the rate times.</summary>
    public ImmutableArray<double> RateTimes { get; }

    /// <summary>Gets the rates' accrual periods, the differences of consecutive rate times.</summary>
    public ImmutableArray<double> RateTaus { get; }

    /// <summary>The ratio of the <paramref name="i"/>-th discount bond to the <paramref name="j"/>-th.</summary>
    /// <param name="i">The numerator's index, in [the first valid index, the number of rates].</param>
    /// <param name="j">The denominator's index, in the same range.</param>
    /// <returns>dᵢ/dⱼ.</returns>
    public abstract double DiscountRatio(int i, int j);

    /// <summary>The <paramref name="i"/>-th forward rate.</summary>
    /// <param name="i">The index, in [the first valid index, the number of rates).</param>
    /// <returns>The forward rate.</returns>
    public abstract double ForwardRate(int i);

    /// <summary>The annuity of the <paramref name="i"/>-th coterminal swap in units of a numeraire bond.</summary>
    /// <param name="numeraire">The numeraire bond's index, in [the first valid index, the number of rates].</param>
    /// <param name="i">The swap's index, in [the first valid index, the number of rates).</param>
    /// <returns>The annuity.</returns>
    public abstract double CoterminalSwapAnnuity(int numeraire, int i);

    /// <summary>The <paramref name="i"/>-th coterminal swap rate, of the swap from tᵢ to tₙ.</summary>
    /// <param name="i">The index, in [the first valid index, the number of rates).</param>
    /// <returns>The swap rate.</returns>
    public abstract double CoterminalSwapRate(int i);

    /// <summary>
    /// The annuity of the <paramref name="i"/>-th constant-maturity swap, spanning
    /// <paramref name="spanningForwards"/> forwards, in units of a numeraire bond.
    /// </summary>
    /// <param name="numeraire">The numeraire bond's index, in [the first valid index, the number of rates].</param>
    /// <param name="i">The swap's index, in [the first valid index, the number of rates).</param>
    /// <param name="spanningForwards">The forwards each swap spans.</param>
    /// <returns>The annuity.</returns>
    public abstract double CmSwapAnnuity(int numeraire, int i, int spanningForwards);

    /// <summary>The <paramref name="i"/>-th constant-maturity swap rate, spanning <paramref name="spanningForwards"/> forwards.</summary>
    /// <param name="i">The index, in [the first valid index, the number of rates).</param>
    /// <param name="spanningForwards">The forwards each swap spans.</param>
    /// <returns>The swap rate.</returns>
    public abstract double CmSwapRate(int i, int spanningForwards);

    /// <summary>The forward rates: a view of the state's buffer, valid until the state is next set.</summary>
    /// <returns>The forward rates, those below the first valid index as the buffer holds them.</returns>
    public abstract ReadOnlySpan<double> ForwardRates();

    /// <summary>The coterminal swap rates: a view of the state's buffer, valid until the state is next set.</summary>
    /// <returns>The coterminal swap rates.</returns>
    public abstract ReadOnlySpan<double> CoterminalSwapRates();

    /// <summary>
    /// The constant-maturity swap rates spanning <paramref name="spanningForwards"/> forwards: a view of the state's
    /// buffer, valid until the state is next set or the read is made with another number of forwards.
    /// </summary>
    /// <param name="spanningForwards">The forwards each swap spans.</param>
    /// <returns>The swap rates.</returns>
    public abstract ReadOnlySpan<double> CmSwapRates(int spanningForwards);

    /// <summary>The rate of the swap from the <paramref name="begin"/>-th rate time to the <paramref name="end"/>-th.</summary>
    /// <param name="begin">The first rate's index.</param>
    /// <param name="end">The index after the last rate, at most the number of rates.</param>
    /// <returns>The swap rate.</returns>
    /// <exception cref="ArgumentException">The range is empty, or ends past the rates.</exception>
    public double SwapRate(int begin, int end)
    {
        if (!(end > begin))
        {
            throw new ArgumentException("empty range specified", nameof(end));
        }

        if (!(end <= NumberOfRates))
        {
            throw new ArgumentException("taus/end mismatch", nameof(end));
        }

        // QuantLib's index type is unsigned: a negative begin is outside its range.
        ArgumentOutOfRangeException.ThrowIfNegative(begin);
        double sum = 0.0;
        for (int i = begin; i < end; ++i)
        {
            sum += RateTaus[i] * DiscountRatio(i + 1, NumberOfRates);
        }

        return (DiscountRatio(begin, NumberOfRates) - DiscountRatio(end, NumberOfRates)) / sum;
    }

    /// <summary>A deep copy: the state, its buffers and its lazy reads.</summary>
    /// <returns>The copy, of the receiver's type.</returns>
    public abstract CurveState Clone();

    // The derived states' index checks, QuantLib's texts. A discount bond's index runs to the number of rates.
    private protected static void CheckBondIndex(int i, int first, int numberOfRates, string paramName)
    {
        if (!(i >= first && i <= numberOfRates))
        {
            throw new ArgumentOutOfRangeException(paramName, "invalid index");
        }
    }

    // A rate's index: QuantLib's check admits the number of rates, whose read is past its rate vectors, refused here
    // (the S-mm plan's MR3 (xx)).
    private protected static void CheckRateIndex(int i, int first, int numberOfRates)
    {
        if (!(i >= first && i < numberOfRates))
        {
            throw new ArgumentOutOfRangeException(nameof(i), "invalid index");
        }
    }

    private protected static void CheckNumeraire(int numeraire, int first, int numberOfRates)
    {
        if (!(numeraire >= first && numeraire <= numberOfRates))
        {
            throw new ArgumentOutOfRangeException(nameof(numeraire), "invalid numeraire");
        }
    }
}
