// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/discretizedasset.hpp ql/discretizedasset.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL;

/// <summary>A discretized option on a given asset.</summary>
/// <remarks>
/// <para>
/// The option rolls its underlying back with itself: at each time it is adjusted at, it rolls the underlying to its
/// own time, pre-adjusts it, applies the exercise condition where an exercise time falls, then post-adjusts it. The
/// underlying must be initialized on the option's lattice before the option's <see cref="Reset"/>; as QuantLib advises,
/// a derived class creates and initializes the underlying itself.
/// </para>
/// <para>An extension point, as in QuantLib: the discretized swaption derives from it.</para>
/// </remarks>
public class DiscretizedOption : DiscretizedAsset
{
    /// <summary>Initializes a new instance of the <see cref="DiscretizedOption"/> class.</summary>
    /// <param name="underlying">The asset the option is exercised into.</param>
    /// <param name="exerciseType">The exercise's type.</param>
    /// <param name="exerciseTimes">
    /// The exercise times: for an American exercise, the first and last times of its window; otherwise each exercise time.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="underlying"/> or <paramref name="exerciseTimes"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exerciseType"/> isn't a member of its type.</exception>
    public DiscretizedOption(DiscretizedAsset underlying, ExerciseType exerciseType, IEnumerable<double> exerciseTimes)
    {
        ArgumentNullException.ThrowIfNull(underlying);
        ArgumentNullException.ThrowIfNull(exerciseTimes);

        // QuantLib refuses another value where the post-adjustment switches on it.
        if (exerciseType is < ExerciseType.American or > ExerciseType.European)
        {
            throw new ArgumentOutOfRangeException(nameof(exerciseType), exerciseType, "invalid exercise type");
        }

        Underlying = underlying;
        ExerciseType = exerciseType;
        ExerciseTimes = [.. exerciseTimes];
    }

    /// <summary>Gets the asset the option is exercised into.</summary>
    protected DiscretizedAsset Underlying { get; }

    /// <summary>Gets the exercise's type.</summary>
    protected ExerciseType ExerciseType { get; }

    /// <summary>Gets the exercise times.</summary>
    protected ImmutableArray<double> ExerciseTimes { get; }

    /// <summary>Sets the values to 0 and adjusts them, the underlying having been initialized on the same lattice.</summary>
    /// <param name="size">The number of values.</param>
    /// <exception cref="NanoQLException">The option and the underlying were initialized on different lattices.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    public override void Reset(int size)
    {
        if (!ReferenceEquals(Method, Underlying.Method))
        {
            throw new NanoQLException("option and underlying were initialized on different methods");
        }

        ResetValues(size, 0.0);
        AdjustValues();
    }

    /// <summary>The underlying's mandatory times, then the exercise times from the first non-negative one on.</summary>
    /// <returns>The times, not guaranteed to be sorted.</returns>
    public override ImmutableArray<double> MandatoryTimes()
    {
        ImmutableArray<double> underlyingTimes = Underlying.MandatoryTimes();

        // discard negative times...
        int i = 0;
        while (i < ExerciseTimes.Length && !(ExerciseTimes[i] >= 0.0))
        {
            i++;
        }

        // and add the positive ones
        return underlyingTimes.AddRange(ExerciseTimes.AsSpan()[i..]);
    }

    /// <summary>
    /// Exercises the option before the underlying's own adjustment: with time flowing backward, options must be exercised
    /// before any payment is settled.
    /// </summary>
    /// <exception cref="NanoQLException">
    /// An American exercise is read past its exercise times, where QuantLib reads past its vector.
    /// </exception>
    protected override void PostAdjustValuesImpl()
    {
        // In the real world, with time flowing forward, first any payment is settled and only after options can be
        // exercised. Here, with time flowing backward, options must be exercised before performing the adjustment.
        Underlying.PartialRollback(Time);
        Underlying.PreAdjustValues();
        switch (ExerciseType)
        {
            case ExerciseType.American:
                if (ExerciseTimes.Length < 1 || (Time >= ExerciseTimes[0] && ExerciseTimes.Length < 2))
                {
                    throw new NanoQLException("American exercise needs two exercise times");
                }

                if (Time >= ExerciseTimes[0] && Time <= ExerciseTimes[1])
                {
                    ApplyExerciseCondition();
                }

                break;
            case ExerciseType.Bermudan:
            case ExerciseType.European:
                for (int i = 0; i < ExerciseTimes.Length; i++)
                {
                    double t = ExerciseTimes[i];
                    if (t >= 0.0 && IsOnTime(t))
                    {
                        ApplyExerciseCondition();
                    }
                }

                break;
            default:
                throw new NanoQLException("invalid exercise type");
        }

        Underlying.PostAdjustValues();
    }

    /// <summary>Sets each value to the larger of the underlying's and its own, as QuantLib's <c>std::max</c> keeps them.</summary>
    protected void ApplyExerciseCondition()
    {
        Span<double> values = Values;
        Span<double> underlying = Underlying.Values;
        for (int i = 0; i < values.Length; i++)
        {
            // std::max(underlying, value)
            values[i] = underlying[i] < values[i] ? values[i] : underlying[i];
        }
    }
}
