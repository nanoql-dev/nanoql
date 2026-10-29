// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/barrieroption/discretizeddoublebarrieroption.hpp ql/experimental/barrieroption/discretizeddoublebarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;

namespace NanoQL.Experimental;

/// <summary>
/// A double barrier option on a lattice with Derman, Kani, Ergener and Bardhan's correction ("Enhanced Numerical
/// Methods for Options with Barriers", 1995): at the nodes straddling a barrier, the values interpolate between the
/// rolled-back value and the knocked-in vanilla or knocked-out rebate by the nodes' distances from the barrier.
/// </summary>
/// <remarks>
/// <para>
/// A working type, as every discretized asset is: it rolls an unenhanced <see cref="DiscretizedDoubleBarrierOption"/>
/// back beside it, kept and initialized again at each reset, and reads the lattice's grid into a buffer it reserves at
/// its widest step, so a rollback allocates nothing.
/// </para>
/// <para>
/// QuantLib's note: the algorithm suits payoffs that can be approximated linearly, not cash-or-nothing ones. Knock-ins
/// and knock-outs only, as QuantLib's ("unsupported barrier type" for KIKO and KOKI).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class DiscretizedDermanKaniDoubleBarrierOption
    : DiscretizedAsset, IDiscretizedDoubleBarrierAsset<DiscretizedDermanKaniDoubleBarrierOption>
{
    private readonly DiscretizedDoubleBarrierOption unenhanced;
    private double[] grid = [];

    /// <summary>Initializes a new instance of the <see cref="DiscretizedDermanKaniDoubleBarrierOption"/> class.</summary>
    /// <param name="option">The double barrier option.</param>
    /// <param name="process">The process, which converts the exercise dates to times.</param>
    /// <param name="grid">The time grid the exercise times are moved to the closest of, or <see langword="null"/>
    /// for none, QuantLib's empty grid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> or <paramref name="process"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The option has no payoff.</exception>
    public DiscretizedDermanKaniDoubleBarrierOption(DoubleBarrierOption option, StochasticProcess process, TimeGrid? grid = null)
    {
        unenhanced = new DiscretizedDoubleBarrierOption(option, process, grid);
    }

    /// <inheritdoc/>
    public static DiscretizedDermanKaniDoubleBarrierOption Create(
        DoubleBarrierOption option, StochasticProcess process, TimeGrid? grid) => new(option, process, grid);

    /// <summary>Sets the values to zero at the given size, initializes the unenhanced asset beside them, then applies
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

        unenhanced.Initialize(Method ?? throw new NanoQLException("asset not initialized"), Time);
        ResetValues(size, 0.0);
        AdjustValues();
    }

    /// <summary>The option's mandatory times: the unenhanced asset's, its exercise times.</summary>
    /// <returns>The times.</returns>
    public override ImmutableArray<double> MandatoryTimes() => unenhanced.MandatoryTimes();

    /// <summary>Rolls the unenhanced asset back to the asset's time, applies its barrier check to the asset's values,
    /// then corrects the nodes straddling the barriers.</summary>
    /// <exception cref="NanoQLException">The option is a KIKO or a KOKI ("unsupported barrier type").</exception>
    protected override void PostAdjustValuesImpl()
    {
        unenhanced.Rollback(Time);

        Span<double> values = Values;
        if (grid.Length < values.Length)
        {
            grid = new double[values.Length];
        }

        Method!.Grid(Time, grid);
        ReadOnlySpan<double> nodes = grid.AsSpan(0, values.Length);
        unenhanced.CheckBarrier(values, nodes); // compute payoffs
        AdjustBarrier(values, nodes);
    }

    private void AdjustBarrier(Span<double> values, ReadOnlySpan<double> grid)
    {
        double barrierLo = unenhanced.BarrierLo;
        double barrierHi = unenhanced.BarrierHi;
        double rebate = unenhanced.Rebate;
        ReadOnlySpan<double> u = unenhanced.Values;
        ReadOnlySpan<double> vanilla = unenhanced.Vanilla;
        switch (unenhanced.BarrierType)
        {
            case DoubleBarrierType.KnockIn:
                for (int j = 0; j < values.Length - 1; ++j)
                {
                    if (grid[j] <= barrierLo && grid[j + 1] > barrierLo)
                    {
                        // grid[j+1] above barrier_lo, grid[j] under (in),
                        // interpolate optvalues[j+1]
                        double ltob = barrierLo - grid[j];
                        double htob = grid[j + 1] - barrierLo;
                        double htol = grid[j + 1] - grid[j];
                        double u1 = u[j + 1];
                        double t1 = vanilla[j + 1];
                        values[j + 1] = Max(0.0, ((ltob * t1) + (htob * u1)) / htol); // derman std
                    }
                    else if (grid[j] < barrierHi && grid[j + 1] >= barrierHi)
                    {
                        // grid[j+1] above barrier_hi (in), grid[j] under,
                        // interpolate optvalues[j]
                        double ltob = barrierHi - grid[j];
                        double htob = grid[j + 1] - barrierHi;
                        double htol = grid[j + 1] - grid[j];
                        double u0 = u[j];
                        double t0 = vanilla[j];
                        values[j] = Max(0.0, ((ltob * u0) + (htob * t0)) / htol); // derman std
                    }
                }

                break;
            case DoubleBarrierType.KnockOut:
                for (int j = 0; j < values.Length - 1; ++j)
                {
                    if (grid[j] <= barrierLo && grid[j + 1] > barrierLo)
                    {
                        // grid[j+1] above barrier_lo, grid[j] under (out),
                        // interpolate optvalues[j+1]
                        double a = (barrierLo - grid[j]) * rebate;
                        double b = (grid[j + 1] - barrierLo) * u[j + 1];
                        double c = grid[j + 1] - grid[j];
                        values[j + 1] = Max(0.0, (a + b) / c);
                    }
                    else if (grid[j] < barrierHi && grid[j + 1] >= barrierHi)
                    {
                        // grid[j+1] above barrier_hi (out), grid[j] under,
                        // interpolate optvalues[j]
                        double a = (barrierHi - grid[j]) * u[j];
                        double b = (grid[j + 1] - barrierHi) * rebate;
                        double c = grid[j + 1] - grid[j];
                        values[j] = Max(0.0, (a + b) / c);
                    }
                }

                break;
            default:
                throw new NanoQLException("unsupported barrier type");
        }
    }

    // std::max
    private static double Max(double a, double b) => a < b ? b : a;
}
