// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/exercise.hpp ql/exercise.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL;

/// <summary>Bermudan exercise: the option can only be exercised at a set of fixed dates.</summary>
/// <remarks>
/// Its dates are the given ones, sorted, duplicates kept. An extension point, as in QuantLib: swing exercises derive
/// from it. The inherited <see cref="Exercise.Date(int)"/> is a method, so inside this class the <c>Date</c> type is
/// written <c>NanoQL.Time.Date</c> wherever an expression needs it.
/// </remarks>
public class BermudanExercise : EarlyExercise
{
    /// <summary>Initializes a new instance of the <see cref="BermudanExercise"/> class.</summary>
    /// <param name="dates">The exercise dates, in any order.</param>
    /// <param name="payoffAtExpiry">Whether the payoff is paid at expiry rather than at exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dates"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="dates"/> is empty, or holds <c>default(Date)</c>.</exception>
    public BermudanExercise(IEnumerable<Date> dates, bool payoffAtExpiry = false)
        : base(ExerciseType.Bermudan, payoffAtExpiry, Sorted(dates))
    {
    }

    private static ImmutableArray<Date> Sorted(IEnumerable<Date> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        Date[] sorted = [.. dates];
        foreach (Date d in sorted)
        {
            NanoQL.Time.Date.ThrowIfDefault(d, nameof(dates));
        }

        if (sorted.Length == 0)
        {
            throw new ArgumentException("no exercise date given", nameof(dates));
        }

        Array.Sort(sorted);
        return [.. sorted];
    }
}
