// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/discretizedvanillaoption.hpp ql/pricingengines/vanilla/discretizedvanillaoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.PricingEngines;

/// <summary>A vanilla option on a lattice: worth the larger of its rolled-back value and its payoff at each exercise
/// time.</summary>
/// <remarks>
/// <para>
/// A working type, as every discretized asset is. Its exercise reads the lattice's grid at each step through
/// <see cref="Lattice.Grid(double, Span{double})"/> into a buffer it reserves at its widest step, where QuantLib's
/// builds an array per step, so a rollback allocates nothing.
/// </para>
/// <para>
/// <b>The American window</b> is bounded by its two times compared with <c>IsOnTime</c> as well, upstream's fix after
/// v1.44 (#2878, the S-sr plan's SSR5): the grid's last time can be an ulp off the maturity the engine starts the asset
/// at, where QuantLib 1.44's plain comparison skips the exercise at expiry, an American option then priced below the
/// European one on the same tree, or rejects a window ending there (<c>Divergences/AmericanBoundsOnTimeTests.cs</c>).
/// </para>
/// </remarks>
[WorkingType]
public sealed class DiscretizedVanillaOption : DiscretizedAsset
{
    private readonly Payoff payoff;
    private readonly ExerciseType exerciseType;
    private readonly ImmutableArray<double> stoppingTimes;
    private double[] grid = [];

    /// <summary>Initializes a new instance of the <see cref="DiscretizedVanillaOption"/> class.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <param name="process">The process, which converts the exercise dates to times.</param>
    /// <param name="grid">The time grid the exercise times are moved to the closest of, or <see langword="null"/>
    /// for none, QuantLib's empty grid.</param>
    /// <exception cref="ArgumentNullException">The payoff, the exercise or the process is
    /// <see langword="null"/>.</exception>
    public DiscretizedVanillaOption(Payoff payoff, Exercise exercise, StochasticProcess process, TimeGrid? grid = null)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(process);
        this.payoff = payoff;
        exerciseType = exercise.Type;
        double[] times = new double[exercise.Dates.Length];
        for (int i = 0; i < times.Length; ++i)
        {
            times[i] = process.Time(exercise.DateAt(i));
            if (grid is not null && !grid.Empty)
            {
                // adjust to the given grid
                times[i] = grid.ClosestTime(times[i]);
            }
        }

        stoppingTimes = [.. times];
    }

    /// <summary>Sets the values to zero at the given size, then applies the exercise.</summary>
    /// <param name="size">The number of values.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    public override void Reset(int size)
    {
        // the widest step a rollback reads the grid at is the one it starts from
        if (grid.Length < size)
        {
            grid = new double[size];
        }

        ResetValues(size, 0.0);
        AdjustValues();
    }

    /// <summary>The option's mandatory times: its exercise times.</summary>
    /// <returns>The times.</returns>
    public override ImmutableArray<double> MandatoryTimes() => stoppingTimes;

    /// <summary>Applies the exercise where the option's time is an exercise time, or within the American
    /// window.</summary>
    /// <exception cref="NanoQLException">The option's exercise is of an unknown type.</exception>
    protected override void PostAdjustValuesImpl()
    {
        double now = Time;
        switch (exerciseType)
        {
            case ExerciseType.American:
                // upstream's fix after v1.44 (#2878): both bounds also on time
                if ((now <= stoppingTimes[1] || IsOnTime(stoppingTimes[1]))
                    && (now >= stoppingTimes[0] || IsOnTime(stoppingTimes[0])))
                {
                    ApplySpecificCondition();
                }

                break;
            case ExerciseType.European:
                if (IsOnTime(stoppingTimes[0]))
                {
                    ApplySpecificCondition();
                }

                break;
            case ExerciseType.Bermudan:
                foreach (double stoppingTime in stoppingTimes)
                {
                    if (IsOnTime(stoppingTime))
                    {
                        ApplySpecificCondition();
                    }
                }

                break;
            default:
                throw new NanoQLException("invalid option type");
        }
    }

    private void ApplySpecificCondition()
    {
        Span<double> values = Values;
        if (grid.Length < values.Length)
        {
            grid = new double[values.Length];
        }

        Method!.Grid(Time, grid);
        for (int j = 0; j < values.Length; j++)
        {
            values[j] = Max(values[j], payoff.Evaluate(grid[j]));
        }
    }

    // std::max
    private static double Max(double a, double b) => a < b ? b : a;
}
