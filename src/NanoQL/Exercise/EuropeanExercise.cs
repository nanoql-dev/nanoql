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

/// <summary>European exercise: the option can only be exercised at one (expiry) date.</summary>
/// <remarks>
/// The inherited <see cref="Exercise.Date(int)"/> is a method, so inside this class the <c>Date</c> type is written
/// <c>NanoQL.Time.Date</c> wherever an expression needs it.
/// </remarks>
public sealed class EuropeanExercise : Exercise
{
    /// <summary>Initializes a new instance of the <see cref="EuropeanExercise"/> class.</summary>
    /// <param name="date">The exercise date.</param>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    public EuropeanExercise(Date date)
        : base(ExerciseType.European, Only(date))
    {
    }

    private static ImmutableArray<Date> Only(Date date)
    {
        NanoQL.Time.Date.ThrowIfDefault(date, nameof(date));
        return [date];
    }
}
