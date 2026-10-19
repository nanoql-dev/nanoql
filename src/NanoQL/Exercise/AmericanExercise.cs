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

/// <summary>
/// American exercise: the option can be exercised at any time between two predefined dates. The first date might be
/// omitted, in which case the option can be exercised at any time before the expiry.
/// </summary>
/// <remarks>
/// Its dates are the earliest and the latest exercise dates, in that order. The inherited <see cref="Exercise.Date(int)"/>
/// is a method, so inside this class the <c>Date</c> type is written <c>NanoQL.Time.Date</c> wherever an expression
/// needs it.
/// </remarks>
public sealed class AmericanExercise : EarlyExercise
{
    /// <summary>Initializes a new instance of the <see cref="AmericanExercise"/> class between two dates.</summary>
    /// <param name="earliestDate">The earliest exercise date.</param>
    /// <param name="latestDate">The latest exercise date.</param>
    /// <param name="payoffAtExpiry">Whether the payoff is paid at expiry rather than at exercise.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="earliestDate"/> is after <paramref name="latestDate"/>, or a date is <c>default(Date)</c>.
    /// </exception>
    public AmericanExercise(Date earliestDate, Date latestDate, bool payoffAtExpiry = false)
        : base(ExerciseType.American, payoffAtExpiry, Between(earliestDate, latestDate))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AmericanExercise"/> class up to a date: its earliest date is
    /// <see cref="NanoQL.Time.Date.MinDate"/>.
    /// </summary>
    /// <param name="latestDate">The latest exercise date.</param>
    /// <param name="payoffAtExpiry">Whether the payoff is paid at expiry rather than at exercise.</param>
    /// <exception cref="ArgumentException"><paramref name="latestDate"/> is <c>default(Date)</c>.</exception>
    public AmericanExercise(Date latestDate, bool payoffAtExpiry = false)
        : base(ExerciseType.American, payoffAtExpiry, UpTo(latestDate))
    {
    }

    private static ImmutableArray<Date> Between(Date earliestDate, Date latestDate)
    {
        NanoQL.Time.Date.ThrowIfDefault(earliestDate, nameof(earliestDate));
        NanoQL.Time.Date.ThrowIfDefault(latestDate, nameof(latestDate));
        if (!(earliestDate <= latestDate))
        {
            throw new ArgumentException("earliest > latest exercise date", nameof(earliestDate));
        }

        return [earliestDate, latestDate];
    }

    private static ImmutableArray<Date> UpTo(Date latestDate)
    {
        NanoQL.Time.Date.ThrowIfDefault(latestDate, nameof(latestDate));
        return [NanoQL.Time.Date.MinDate, latestDate];
    }
}
