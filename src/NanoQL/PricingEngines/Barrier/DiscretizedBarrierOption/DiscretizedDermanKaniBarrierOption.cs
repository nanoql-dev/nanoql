// SPDX-FileCopyrightText: Copyright (C) 2014 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/discretizedbarrieroption.hpp ql/pricingengines/barrier/discretizedbarrieroption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;

namespace NanoQL.PricingEngines;

/// <summary>
/// A barrier option on a lattice with Derman and Kani's correction: at the nodes straddling the barrier, the values
/// interpolate between the rolled-back value and the knocked-in vanilla or knocked-out rebate by the nodes' distances
/// from the barrier.
/// </summary>
/// <remarks>
/// A working type, as every discretized asset is: it rolls an unenhanced <see cref="DiscretizedBarrierOption"/> back
/// beside it, kept and initialized again at each reset, and reads the lattice's grid into a buffer it reserves at its
/// widest step, so a rollback allocates nothing.
/// </remarks>
public sealed class DiscretizedDermanKaniBarrierOption
    : DiscretizedAsset, IDiscretizedBarrierAsset<DiscretizedDermanKaniBarrierOption>
{
    private readonly DiscretizedBarrierOption unenhanced;
    private double[] grid = [];

    /// <summary>Initializes a new instance of the <see cref="DiscretizedDermanKaniBarrierOption"/> class.</summary>
    /// <param name="option">The barrier option.</param>
    /// <param name="process">The process, which converts the exercise dates to times.</param>
    /// <param name="grid">The time grid the exercise times are moved to the closest of, or <see langword="null"/>
    /// for none, QuantLib's empty grid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> or <paramref name="process"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The option has no payoff.</exception>
    public DiscretizedDermanKaniBarrierOption(BarrierOption option, StochasticProcess process, TimeGrid? grid = null)
    {
        unenhanced = new DiscretizedBarrierOption(option, process, grid);
    }

    /// <inheritdoc/>
    public static DiscretizedDermanKaniBarrierOption Create(BarrierOption option, StochasticProcess process, TimeGrid? grid) =>
        new(option, process, grid);

    /// <summary>Sets the values to zero at the given size, initializes the unenhanced asset beside them, then applies
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

        unenhanced.Initialize(Method ?? throw new NanoQLException("asset not initialized"), Time);
        ResetValues(size, 0.0);
        AdjustValues();
    }

    /// <summary>The option's mandatory times: the unenhanced asset's, its exercise times.</summary>
    /// <returns>The times.</returns>
    public override ImmutableArray<double> MandatoryTimes() => unenhanced.MandatoryTimes();

    /// <summary>Rolls the unenhanced asset back to the asset's time, corrects the nodes straddling the barrier, then
    /// applies the unenhanced asset's barrier check to the asset's values.</summary>
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
        AdjustBarrier(values, nodes);
        unenhanced.CheckBarrier(values, nodes); // compute payoffs
    }

    private void AdjustBarrier(Span<double> values, ReadOnlySpan<double> grid)
    {
        double barrier = unenhanced.Barrier;
        double rebate = unenhanced.Rebate;
        ReadOnlySpan<double> u = unenhanced.Values;
        ReadOnlySpan<double> vanilla = unenhanced.Vanilla;
        switch (unenhanced.BarrierType)
        {
            case BarrierType.DownIn:
                for (int j = 0; j < values.Length - 1; ++j)
                {
                    if (grid[j] <= barrier && grid[j + 1] > barrier)
                    {
                        // grid[j+1] above barrier, grid[j] under (in)
                        // interpolate optvalues[j+1]
                        double ltob = barrier - grid[j];
                        double htob = grid[j + 1] - barrier;
                        double htol = grid[j + 1] - grid[j];
                        double u1 = u[j + 1];
                        double t1 = vanilla[j + 1];
                        values[j + 1] = Max(0.0, ((ltob * t1) + (htob * u1)) / htol);
                    }
                }

                break;
            case BarrierType.DownOut:
                for (int j = 0; j < values.Length - 1; ++j)
                {
                    if (grid[j] <= barrier && grid[j + 1] > barrier)
                    {
                        // grid[j+1] above barrier, grid[j] under (out)
                        // interpolate optvalues[j+1]
                        double a = (barrier - grid[j]) * rebate;
                        double b = (grid[j + 1] - barrier) * u[j + 1];
                        double c = grid[j + 1] - grid[j];
                        values[j + 1] = Max(0.0, (a + b) / c);
                    }
                }

                break;
            case BarrierType.UpIn:
                for (int j = 0; j < values.Length - 1; ++j)
                {
                    if (grid[j] < barrier && grid[j + 1] >= barrier)
                    {
                        // grid[j+1] above barrier (in), grid[j] under
                        // interpolate optvalues[j]
                        double ltob = barrier - grid[j];
                        double htob = grid[j + 1] - barrier;
                        double htol = grid[j + 1] - grid[j];
                        double u0 = u[j];
                        double t0 = vanilla[j];
                        values[j] = Max(0.0, ((ltob * u0) + (htob * t0)) / htol); // derman std
                    }
                }

                break;
            case BarrierType.UpOut:
                for (int j = 0; j < values.Length - 1; ++j)
                {
                    if (grid[j] < barrier && grid[j + 1] >= barrier)
                    {
                        // grid[j+1] above barrier (out), grid[j] under
                        // interpolate optvalues[j]
                        double a = (barrier - grid[j]) * u[j];
                        double b = (grid[j + 1] - barrier) * rebate;
                        double c = grid[j + 1] - grid[j];
                        values[j] = Max(0.0, (a + b) / c);
                    }
                }

                break;
        }
    }

    // std::max
    private static double Max(double a, double b) => a < b ? b : a;
}
