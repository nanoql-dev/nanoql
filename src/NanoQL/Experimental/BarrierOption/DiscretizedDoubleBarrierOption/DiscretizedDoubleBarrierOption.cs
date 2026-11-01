// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/barrieroption/discretizeddoublebarrieroption.hpp ql/experimental/barrieroption/discretizeddoublebarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.PricingEngines;

namespace NanoQL.Experimental;

/// <summary>
/// A double barrier option on a lattice: knocked out to its rebate, or knocked in to its vanilla, at the nodes on or
/// beyond either barrier, as its type says of each.
/// </summary>
/// <remarks>
/// <para>
/// A working type, as every discretized asset is (S-bond's protocol, SBR1): its values are views of its buffers, its
/// exercise reads the lattice's grid into a buffer it reserves at its widest step, and the vanilla sub-asset rolled back
/// beside it is kept and initialized again at each reset, so a rollback allocates nothing.
/// </para>
/// <para>
/// <b>The American window</b> is bounded by its two times compared with <c>IsOnTime</c> as well, upstream's fix after
/// v1.44 (#2878, the S-xeq plan's XER5 (xii)): the grid's last time can be an ulp off the maturity the engine starts
/// the asset at, where QuantLib 1.44's plain comparison skips the exercise at expiry
/// (<c>Divergences/DoubleBarrierUpstreamFixesTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
[WorkingType]
public sealed class DiscretizedDoubleBarrierOption
    : DiscretizedAsset, IDiscretizedDoubleBarrierAsset<DiscretizedDoubleBarrierOption>
{
    private readonly DoubleBarrierType barrierType;
    private readonly double barrierLo;
    private readonly double barrierHi;
    private readonly double rebate;
    private readonly Payoff payoff;
    private readonly ExerciseType exerciseType;
    private readonly ImmutableArray<double> stoppingTimes;
    private readonly DiscretizedVanillaOption vanilla;
    private double[] grid = [];

    /// <summary>Initializes a new instance of the <see cref="DiscretizedDoubleBarrierOption"/> class.</summary>
    /// <param name="option">The double barrier option.</param>
    /// <param name="process">The process, which converts the exercise dates to times.</param>
    /// <param name="grid">The time grid the exercise times are moved to the closest of, or <see langword="null"/>
    /// for none, QuantLib's empty grid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> or <paramref name="process"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The option has no payoff.</exception>
    public DiscretizedDoubleBarrierOption(DoubleBarrierOption option, StochasticProcess process, TimeGrid? grid = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(process);
        barrierType = option.BarrierType;
        barrierLo = option.BarrierLo;
        barrierHi = option.BarrierHi;
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
    public DoubleBarrierType BarrierType => barrierType;

    /// <summary>Gets the option's lower barrier.</summary>
    public double BarrierLo => barrierLo;

    /// <summary>Gets the option's upper barrier.</summary>
    public double BarrierHi => barrierHi;

    /// <summary>Gets the option's rebate.</summary>
    public double Rebate => rebate;

    /// <inheritdoc/>
    public static DiscretizedDoubleBarrierOption Create(DoubleBarrierOption option, StochasticProcess process, TimeGrid? grid) =>
        new(option, process, grid);

    /// <summary>Sets the values to zero at the given size, initializes the vanilla sub-asset beside them, then applies
    /// the barriers.</summary>
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
    /// Applies the barriers at the asset's time to values on a grid: a knocked-out node takes the rebate, a knocked-in
    /// one the vanilla (exercised where the time is an exercise time), and the others the exercise, or a knock-in's
    /// rebate at the end.
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
                case DoubleBarrierType.KnockIn:
                    if (grid[j] <= barrierLo)
                    {
                        // knocked in dn
                        values[j] = stoppingTime ? Max(vanillaValues[j], payoff.Evaluate(grid[j])) : vanillaValues[j];
                    }
                    else if (grid[j] >= barrierHi)
                    {
                        // knocked in up
                        values[j] = stoppingTime ? Max(vanillaValues[j], payoff.Evaluate(grid[j])) : vanillaValues[j];
                    }
                    else if (endTime)
                    {
                        values[j] = rebate;
                    }

                    break;
                case DoubleBarrierType.KnockOut:
                    if (grid[j] <= barrierLo)
                    {
                        values[j] = rebate; // knocked out lo
                    }
                    else if (grid[j] >= barrierHi)
                    {
                        values[j] = rebate; // knocked out hi
                    }
                    else if (stoppingTime)
                    {
                        values[j] = Max(values[j], payoff.Evaluate(grid[j]));
                    }

                    break;
                case DoubleBarrierType.KIKO:
                    // low barrier is KI, high is KO
                    if (grid[j] <= barrierLo)
                    {
                        // knocked in dn
                        values[j] = stoppingTime ? Max(vanillaValues[j], payoff.Evaluate(grid[j])) : vanillaValues[j];
                    }
                    else if (grid[j] >= barrierHi)
                    {
                        values[j] = rebate; // knocked out hi
                    }
                    else if (endTime)
                    {
                        values[j] = rebate;
                    }

                    break;
                case DoubleBarrierType.KOKI:
                    // low barrier is KO, high is KI
                    if (grid[j] <= barrierLo)
                    {
                        values[j] = rebate; // knocked out lo
                    }
                    else if (grid[j] >= barrierHi)
                    {
                        // knocked in up
                        values[j] = stoppingTime ? Max(vanillaValues[j], payoff.Evaluate(grid[j])) : vanillaValues[j];
                    }
                    else if (endTime)
                    {
                        values[j] = rebate;
                    }

                    break;
                default:
                    throw new NanoQLException("invalid barrier type");
            }
        }
    }

    /// <summary>Rolls the vanilla sub-asset back to the asset's time unless the option is a knock-out, then applies
    /// the barriers on the lattice's grid there.</summary>
    protected override void PostAdjustValuesImpl()
    {
        if (barrierType != DoubleBarrierType.KnockOut)
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
