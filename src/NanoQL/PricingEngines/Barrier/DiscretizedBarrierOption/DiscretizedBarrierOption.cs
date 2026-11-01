// SPDX-FileCopyrightText: Copyright (C) 2014 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/discretizedbarrieroption.hpp ql/pricingengines/barrier/discretizedbarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;

namespace NanoQL.PricingEngines;

/// <summary>
/// A barrier option on a lattice: knocked out to its rebate, or knocked in to its vanilla, at the nodes beyond the
/// barrier.
/// </summary>
/// <remarks>
/// <para>
/// A working type, as every discretized asset is (S-bond's protocol, SBR1): its values are views of its buffers, its
/// exercise reads the lattice's grid into a buffer it reserves at its widest step, and the vanilla sub-asset a
/// knock-in rolls back beside it is kept and initialized again at each reset, so a rollback allocates nothing.
/// </para>
/// <para>
/// <b>The American window</b> is bounded by its two times compared with <c>IsOnTime</c> as well, upstream's fix after
/// v1.44 (#2878, the S-bar plan's BR6 (iv)): the grid's last time can be an ulp off the maturity the engine starts the
/// asset at, where QuantLib 1.44's plain comparison skips the exercise at expiry
/// (<c>Divergences/BarrierUpstreamFixesTests.cs</c>).
/// </para>
/// </remarks>
[WorkingType]
public sealed class DiscretizedBarrierOption : DiscretizedAsset, IDiscretizedBarrierAsset<DiscretizedBarrierOption>
{
    private readonly BarrierType barrierType;
    private readonly double barrier;
    private readonly double rebate;
    private readonly Payoff payoff;
    private readonly ExerciseType exerciseType;
    private readonly ImmutableArray<double> stoppingTimes;
    private readonly DiscretizedVanillaOption vanilla;
    private double[] grid = [];

    /// <summary>Initializes a new instance of the <see cref="DiscretizedBarrierOption"/> class.</summary>
    /// <param name="option">The barrier option.</param>
    /// <param name="process">The process, which converts the exercise dates to times.</param>
    /// <param name="grid">The time grid the exercise times are moved to the closest of, or <see langword="null"/>
    /// for none, QuantLib's empty grid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> or <paramref name="process"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The option has no payoff.</exception>
    public DiscretizedBarrierOption(BarrierOption option, StochasticProcess process, TimeGrid? grid = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(process);
        barrierType = option.BarrierType;
        barrier = option.Barrier;
        rebate = option.Rebate;
        payoff = option.Payoff ?? throw new NanoQLException("no payoff given");
        exerciseType = option.Exercise.Type;
        vanilla = new DiscretizedVanillaOption(payoff, option.Exercise, process, grid);

        // QuantLib's "specify at least one stopping date" can't fail: an exercise holds a date
        double[] times = new double[option.Exercise.Dates.Length];
        for (int i = 0; i < times.Length; ++i)
        {
            times[i] = process.Time(option.Exercise.DateAt(i));
            if (grid is not null && !grid.Empty)
            {
                // adjust to the given grid
                times[i] = grid.ClosestTime(times[i]);
            }
        }

        stoppingTimes = [.. times];
    }

    /// <summary>Gets the vanilla sub-asset's values at its time: a view, valid until its next step or reset.</summary>
    public ReadOnlySpan<double> Vanilla => vanilla.Values;

    /// <summary>Gets the option's barrier type.</summary>
    public BarrierType BarrierType => barrierType;

    /// <summary>Gets the option's barrier.</summary>
    public double Barrier => barrier;

    /// <summary>Gets the option's rebate.</summary>
    public double Rebate => rebate;

    /// <inheritdoc/>
    public static DiscretizedBarrierOption Create(BarrierOption option, StochasticProcess process, TimeGrid? grid) =>
        new(option, process, grid);

    /// <summary>Sets the values to zero at the given size, initializes the vanilla sub-asset beside them, then applies
    /// the barrier.</summary>
    /// <param name="size">The number of values.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    /// <exception cref="NanoQLException">The asset isn't on a lattice.</exception>
    public override void Reset(int size)
    {
        if (grid.Length < size)
        {
            grid = new double[size];
        }

        vanilla.Initialize(Method ?? throw new NanoQLException("asset not initialized"), Time);
        ResetValues(size, 0.0);
        AdjustValues();
    }

    /// <summary>The option's mandatory times: its exercise times.</summary>
    /// <returns>The times.</returns>
    public override ImmutableArray<double> MandatoryTimes() => stoppingTimes;

    /// <summary>
    /// Applies the barrier at the asset's time to values on a grid: a knock-out's nodes beyond the barrier take the
    /// rebate, a knock-in's the vanilla (exercised where the time is an exercise time), and the others the exercise,
    /// or a knock-in's rebate at the end.
    /// </summary>
    /// <param name="values">The values, written in place.</param>
    /// <param name="grid">The underlying's values at the nodes.</param>
    /// <exception cref="NanoQLException">The asset isn't on a lattice, or its exercise or barrier type is invalid.</exception>
    public void CheckBarrier(Span<double> values, ReadOnlySpan<double> grid)
    {
        double now = Time;
        bool endTime = IsOnTime(stoppingTimes[^1]);
        bool stoppingTime = false;
        switch (exerciseType)
        {
            case ExerciseType.American:
                // upstream's fix after v1.44 (#2878): both bounds also on time
                if ((now <= stoppingTimes[1] || IsOnTime(stoppingTimes[1]))
                    && (now >= stoppingTimes[0] || IsOnTime(stoppingTimes[0])))
                {
                    stoppingTime = true;
                }

                break;
            case ExerciseType.European:
                if (IsOnTime(stoppingTimes[0]))
                {
                    stoppingTime = true;
                }

                break;
            case ExerciseType.Bermudan:
                foreach (double t in stoppingTimes)
                {
                    if (IsOnTime(t))
                    {
                        stoppingTime = true;
                        break;
                    }
                }

                break;
            default:
                throw new NanoQLException("invalid option type");
        }

        ReadOnlySpan<double> vanillaValues = vanilla.Values;
        for (int j = 0; j < values.Length; j++)
        {
            switch (barrierType)
            {
                case BarrierType.DownIn:
                    if (grid[j] <= barrier)
                    {
                        // knocked in
                        values[j] = stoppingTime ? Max(vanillaValues[j], payoff.Evaluate(grid[j])) : vanillaValues[j];
                    }
                    else if (endTime)
                    {
                        values[j] = rebate;
                    }

                    break;
                case BarrierType.DownOut:
                    if (grid[j] <= barrier)
                    {
                        values[j] = rebate; // knocked out
                    }
                    else if (stoppingTime)
                    {
                        values[j] = Max(values[j], payoff.Evaluate(grid[j]));
                    }

                    break;
                case BarrierType.UpIn:
                    if (grid[j] >= barrier)
                    {
                        // knocked in
                        values[j] = stoppingTime ? Max(vanillaValues[j], payoff.Evaluate(grid[j])) : vanillaValues[j];
                    }
                    else if (endTime)
                    {
                        values[j] = rebate;
                    }

                    break;
                case BarrierType.UpOut:
                    if (grid[j] >= barrier)
                    {
                        values[j] = rebate; // knocked out
                    }
                    else if (stoppingTime)
                    {
                        values[j] = Max(values[j], payoff.Evaluate(grid[j]));
                    }

                    break;
                default:
                    throw new NanoQLException("invalid barrier type");
            }
        }
    }

    /// <summary>Rolls a knock-in's vanilla sub-asset back to the asset's time, then applies the barrier on the
    /// lattice's grid there.</summary>
    protected override void PostAdjustValuesImpl()
    {
        if (barrierType is BarrierType.DownIn or BarrierType.UpIn)
        {
            vanilla.Rollback(Time);
        }

        Span<double> values = Values;
        CheckBarrier(values, GridAt(values.Length));
    }

    // the lattice's grid at the asset's time, in the held buffer
    private ReadOnlySpan<double> GridAt(int size)
    {
        if (grid.Length < size)
        {
            grid = new double[size];
        }

        Method!.Grid(Time, grid);
        return grid.AsSpan(0, size);
    }

    // std::max
    private static double Max(double a, double b) => a < b ? b : a;
}
