// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/exercise.hpp ql/exercise.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL;

/// <summary>The base exercise class: the exercise's type and its dates.</summary>
/// <remarks>
/// <para>
/// Constructible, as QuantLib's is: <c>new Exercise(type)</c> holds no dates, so <see cref="LastDate"/> refuses it, and
/// <see cref="Date(int)"/> and <see cref="DateAt(int)"/> refuse every index. The derived exercises give their dates to
/// the protected constructor, where QuantLib's assign its protected <c>dates_</c>.
/// </para>
/// <para>
/// An extension point, as in QuantLib: rebated and swing exercises derive from it and from
/// <see cref="BermudanExercise"/>. <see cref="Date(int)"/> is a method, so inside this class the <c>Date</c> type is
/// written <c>NanoQL.Time.Date</c>.
/// </para>
/// </remarks>
public class Exercise
{
    private readonly ImmutableArray<NanoQL.Time.Date> dates;

    /// <summary>Initializes a new instance of the <see cref="Exercise"/> class, with no dates.</summary>
    /// <param name="type">The exercise's type.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public Exercise(ExerciseType type)
        : this(type, [])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Exercise"/> class with its dates, for a derived exercise.</summary>
    /// <param name="type">The exercise's type.</param>
    /// <param name="dates">The exercise dates, in the order the exercise keeps them.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>.</exception>
    protected Exercise(ExerciseType type, ImmutableArray<NanoQL.Time.Date> dates)
    {
        EnumArguments.Check(type, nameof(type));
        foreach (NanoQL.Time.Date d in dates)
        {
            NanoQL.Time.Date.ThrowIfDefault(d, nameof(dates));
        }

        Type = type;
        this.dates = dates;
    }

    /// <summary>Gets the exercise's type.</summary>
    public ExerciseType Type { get; }

    /// <summary>Gets all the exercise dates.</summary>
    public ImmutableArray<NanoQL.Time.Date> Dates => dates;

    /// <summary>The exercise date at the given index.</summary>
    /// <param name="index">The index.</param>
    /// <returns>The date.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is out of range, where QuantLib reads past its dates.</exception>
    public NanoQL.Time.Date Date(int index) => DateAt(index);

    /// <summary>The exercise date at the given index, checked.</summary>
    /// <param name="index">The index.</param>
    /// <returns>The date.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is out of range.</exception>
    public NanoQL.Time.Date DateAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, dates.Length);
        return dates[index];
    }

    /// <summary>The last exercise date.</summary>
    /// <returns>The date.</returns>
    /// <exception cref="NanoQLException">The exercise has no dates.</exception>
    public NanoQL.Time.Date LastDate()
    {
        if (dates.IsEmpty)
        {
            throw new NanoQLException("no exercise date given");
        }

        return dates[^1];
    }
}
