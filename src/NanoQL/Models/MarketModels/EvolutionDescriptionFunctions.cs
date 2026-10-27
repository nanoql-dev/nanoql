// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2006 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/evolutiondescription.hpp ql/models/marketmodels/evolutiondescription.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Utilities;

namespace NanoQL.Models;

/// <summary>The numeraire functions of an evolution: the measures and the numeraires' checks.</summary>
public static class EvolutionDescriptionFunctions
{
    /// <summary>
    /// Checks that there is one numeraire for each evolution time, and that each step's numeraire bond hasn't expired
    /// at the step's end, as QuantLib checks: at every step but the last.
    /// </summary>
    /// <param name="evolution">The evolution.</param>
    /// <param name="numeraires">The numeraires, one per step, each the index of a rate time.</param>
    /// <exception cref="ArgumentException">
    /// The numeraires aren't one per step, or a step's numeraire before the last has expired before the step's end.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A numeraire QuantLib reads isn't the index of a rate time.</exception>
    /// <remarks>
    /// <para>
    /// QuantLib's loop stops before the last step, so a last step's numeraire may mature inside that step, which its
    /// documentation's contract ("each numeraire must not have expired before the end of the step") excludes. It is kept
    /// (the S-mm plan's MR4 (c), as the user re-ruled it): the models cut each forward's covariance at its own fixing,
    /// so the drifts under such a numeraire are those under the bond rolled into the next at its maturity, and the curve
    /// state's discount ratio to the matured bond reads its frozen forward, the rolled position's value, which prices
    /// correctly (bonds stay martingales and caplets within their Monte Carlo errors of Black's).
    /// </para>
    /// <para>
    /// A numeraire past the rate times at a step QuantLib checks is read past its vector, and refused (MR3 (xx);
    /// <c>Divergences/MarketModelUndefinedReadsTests.cs</c>); the last step's numeraire isn't read here, as in QuantLib.
    /// </para>
    /// </remarks>
    public static void CheckCompatibility(EvolutionDescription evolution, ReadOnlySpan<int> numeraires)
    {
        ArgumentNullException.ThrowIfNull(evolution);
        ImmutableArray<double> evolutionTimes = evolution.EvolutionTimes;
        int n = evolutionTimes.Length;
        if (!(numeraires.Length == n))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Size mismatch between numeraires ({numeraires.Length}) and evolution times ({n})"),
                nameof(numeraires));
        }

        CheckNonNegative(numeraires);
        ImmutableArray<double> rateTimes = evolution.RateTimes;
        for (int i = 0; i < n - 1; i++)
        {
            int numeraire = numeraires[i];
            if (!(numeraire < rateTimes.Length))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(numeraires),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{DataFormattersFunctions.Ordinal(i + 1)} step: the numeraire ({numeraire}) is not the index of a rate time (0 to {rateTimes.Length - 1})"));
            }

            if (!(rateTimes[numeraire] >= evolutionTimes[i]))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{DataFormattersFunctions.Ordinal(i + 1)} step, evolution time {evolutionTimes[i]}: the numeraire ({numeraire}), corresponding to rate time {rateTimes[numeraire]}, is expired"),
                    nameof(numeraires));
            }
        }
    }

    /// <summary>Whether numeraires are the terminal measure's: the smallest of them is the last bond.</summary>
    /// <param name="evolution">The evolution.</param>
    /// <param name="numeraires">The numeraires, at least one.</param>
    /// <returns>Whether the smallest numeraire is the last rate time's index.</returns>
    /// <exception cref="ArgumentException">There is no numeraire.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A numeraire is negative.</exception>
    /// <remarks>
    /// QuantLib takes the smallest of no numeraires by dereferencing an empty range's end, undefined, which is refused
    /// (the S-mm plan's MR3 (xx)).
    /// </remarks>
    public static bool IsInTerminalMeasure(EvolutionDescription evolution, ReadOnlySpan<int> numeraires)
    {
        ArgumentNullException.ThrowIfNull(evolution);
        if (numeraires.IsEmpty)
        {
            throw new ArgumentException("no numeraires given", nameof(numeraires));
        }

        CheckNonNegative(numeraires);
        int min = numeraires[0];
        for (int i = 1; i < numeraires.Length; ++i)
        {
            if (numeraires[i] < min)
            {
                min = numeraires[i];
            }
        }

        return min == evolution.RateTimes.Length - 1;
    }

    /// <summary>
    /// Whether numeraires are the offset money-market measure's: at each step the offset-th unexpired bond.
    /// </summary>
    /// <param name="evolution">The evolution.</param>
    /// <param name="numeraires">The numeraires, at least one per step.</param>
    /// <param name="offset">The offset, at most the last rate time's index.</param>
    /// <returns>Whether each step's numeraire is the measure's.</returns>
    /// <exception cref="ArgumentException">The offset is past the last numeraire, or there are fewer numeraires than steps.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The offset or a numeraire is negative.</exception>
    /// <remarks>
    /// QuantLib reads a numeraire per step, past the vector where there are fewer, undefined, which is refused (the
    /// S-mm plan's MR3 (xx)).
    /// </remarks>
    public static bool IsInMoneyMarketPlusMeasure(EvolutionDescription evolution, ReadOnlySpan<int> numeraires, int offset = 1)
    {
        ArgumentNullException.ThrowIfNull(evolution);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        bool res = true;
        ImmutableArray<double> rateTimes = evolution.RateTimes;
        int maxNumeraire = rateTimes.Length - 1;
        if (!(offset <= maxNumeraire))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"offset ({offset}) is greater than the max allowed value for numeraire ({maxNumeraire})"),
                nameof(offset));
        }

        ImmutableArray<double> evolutionTimes = evolution.EvolutionTimes;
        if (!(numeraires.Length >= evolutionTimes.Length))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"fewer numeraires ({numeraires.Length}) than evolution times ({evolutionTimes.Length})"),
                nameof(numeraires));
        }

        CheckNonNegative(numeraires);
        for (int i = 0, j = 0; i < evolutionTimes.Length; ++i)
        {
            while (rateTimes[j] < evolutionTimes[i])
            {
                j++;
            }

            res = (numeraires[i] == Math.Min(j + offset, maxNumeraire)) && res;
        }

        return res;
    }

    /// <summary>Whether numeraires are the money-market measure's: at each step the first unexpired bond.</summary>
    /// <param name="evolution">The evolution.</param>
    /// <param name="numeraires">The numeraires, at least one per step.</param>
    /// <returns>Whether each step's numeraire is the measure's.</returns>
    /// <exception cref="ArgumentException">There are fewer numeraires than steps.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A numeraire is negative.</exception>
    public static bool IsInMoneyMarketMeasure(EvolutionDescription evolution, ReadOnlySpan<int> numeraires) =>
        IsInMoneyMarketPlusMeasure(evolution, numeraires, 0);

    /// <summary>The terminal measure: the last bond as every step's numeraire.</summary>
    /// <param name="evolution">The evolution.</param>
    /// <returns>The numeraires, one per step.</returns>
    public static ImmutableArray<int> TerminalMeasure(EvolutionDescription evolution)
    {
        ArgumentNullException.ThrowIfNull(evolution);
        int[] numeraires = new int[evolution.EvolutionTimes.Length];
        Array.Fill(numeraires, evolution.RateTimes.Length - 1);
        return ImmutableArray.Create(numeraires);
    }

    /// <summary>
    /// The offset discretely compounded money-market measure: at each step the offset-th unexpired bond, the usual
    /// money-market measure at offset 0.
    /// </summary>
    /// <param name="evolution">The evolution.</param>
    /// <param name="offset">The offset, at most the last rate time's index.</param>
    /// <returns>The numeraires, one per step.</returns>
    /// <exception cref="ArgumentException">The offset is past the last numeraire.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The offset is negative.</exception>
    public static ImmutableArray<int> MoneyMarketPlusMeasure(EvolutionDescription evolution, int offset = 1)
    {
        ArgumentNullException.ThrowIfNull(evolution);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ImmutableArray<double> rateTimes = evolution.RateTimes;
        int maxNumeraire = rateTimes.Length - 1;
        if (!(offset <= maxNumeraire))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"offset ({offset}) is greater than the max allowed value for numeraire ({maxNumeraire})"),
                nameof(offset));
        }

        ImmutableArray<double> evolutionTimes = evolution.EvolutionTimes;
        int n = evolutionTimes.Length;
        int[] numeraires = new int[n];
        for (int i = 0, j = 0; i < n; ++i)
        {
            while (rateTimes[j] < evolutionTimes[i])
            {
                j++;
            }

            numeraires[i] = Math.Min(j + offset, maxNumeraire);
        }

        return ImmutableArray.Create(numeraires);
    }

    /// <summary>The discretely compounded money-market measure: at each step the first unexpired bond.</summary>
    /// <param name="evolution">The evolution.</param>
    /// <returns>The numeraires, one per step.</returns>
    public static ImmutableArray<int> MoneyMarketMeasure(EvolutionDescription evolution) => MoneyMarketPlusMeasure(evolution, 0);

    // QuantLib's numeraires are unsigned.
    private static void CheckNonNegative(ReadOnlySpan<int> numeraires)
    {
        foreach (int numeraire in numeraires)
        {
            if (numeraire < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(numeraires), string.Create(CultureInfo.InvariantCulture, $"negative numeraire ({numeraire})"));
            }
        }
    }
}
