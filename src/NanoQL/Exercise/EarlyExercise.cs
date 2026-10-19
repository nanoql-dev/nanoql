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

/// <summary>The early-exercise base class: the payoff can be at exercise (the default) or at expiry.</summary>
/// <remarks>
/// Constructible, as QuantLib's is, with no dates; the derived exercises give theirs to the protected constructor. An
/// extension point.
/// </remarks>
public class EarlyExercise : Exercise
{
    /// <summary>Initializes a new instance of the <see cref="EarlyExercise"/> class, with no dates.</summary>
    /// <param name="type">The exercise's type; QuantLib doesn't check that it is American or Bermudan.</param>
    /// <param name="payoffAtExpiry">Whether the payoff is paid at expiry rather than at exercise.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public EarlyExercise(ExerciseType type, bool payoffAtExpiry = false)
        : this(type, payoffAtExpiry, [])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EarlyExercise"/> class with its dates, for a derived exercise.</summary>
    /// <param name="type">The exercise's type.</param>
    /// <param name="payoffAtExpiry">Whether the payoff is paid at expiry rather than at exercise.</param>
    /// <param name="dates">The exercise dates, in the order the exercise keeps them.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    protected EarlyExercise(ExerciseType type, bool payoffAtExpiry, ImmutableArray<Date> dates)
        : base(type, dates)
    {
        PayoffAtExpiry = payoffAtExpiry;
    }

    /// <summary>Gets a value indicating whether the payoff is paid at expiry rather than at exercise.</summary>
    public bool PayoffAtExpiry { get; }
}
