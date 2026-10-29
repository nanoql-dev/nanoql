// SPDX-FileCopyrightText: Copyright (C) 2011, 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmvppstepcondition.hpp ql/experimental/finitedifferences/fdmvppstepcondition.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;

namespace NanoQL.Experimental;

/// <summary>
/// The step condition of a virtual power plant valued by dynamic programming: each step adds the running states'
/// spark-spread earnings, at the minimum or the maximum output, then lets each point's states change as the plant's
/// rules allow, at the gas price there.
/// </summary>
/// <remarks>
/// <para>
/// The plant's states cycle in blocks of 2·tMinUp + tMinDown: tMinUp states running at the minimum output, tMinUp at the
/// maximum, and tMinDown off. A derived condition gives the state changes and the value of a point's states.
/// </para>
/// <para>
/// A step condition is a solver's working state: it holds the states it changes at a point.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class FdmVPPStepCondition : StepCondition
{
    private readonly FdmMesher mesher;
    private readonly int stateDirection;
    private readonly FdmInnerValueCalculator gasPrice;
    private readonly FdmInnerValueCalculator sparkSpreadPrice;

    // each state's earnings: 0 off, 1 at the minimum output, 2 at the maximum (QuantLib's stateEvolveFcts_)
    private readonly byte[] stateEvolve;

    // a point's states before and after their change, held
    private readonly double[] x;
    private readonly double[] changed;

    /// <summary>Initializes a new instance of the <see cref="FdmVPPStepCondition"/> class.</summary>
    /// <param name="parameters">The plant's parameters.</param>
    /// <param name="nStates">The number of states.</param>
    /// <param name="mesh">The mesher and the states' direction.</param>
    /// <param name="gasPrice">The gas price.</param>
    /// <param name="sparkSpreadPrice">The spark spread.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The mesher's states direction doesn't hold the number of states ("mesher does
    /// not fit to vpp arguments").</exception>
    protected FdmVPPStepCondition(
        FdmVPPStepConditionParams parameters,
        int nStates,
        FdmVPPStepConditionMesher mesh,
        FdmInnerValueCalculator gasPrice,
        FdmInnerValueCalculator sparkSpreadPrice)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(gasPrice);
        ArgumentNullException.ThrowIfNull(sparkSpreadPrice);
        HeatRate = parameters.HeatRate;
        PMin = parameters.PMin;
        PMax = parameters.PMax;
        TMinUp = parameters.TMinUp;
        TMinDown = parameters.TMinDown;
        StartUpFuel = parameters.StartUpFuel;
        StartUpFixCost = parameters.StartUpFixCost;
        FuelCostAddon = parameters.FuelCostAddon;
        stateDirection = mesh.StateDirection;
        NStates = nStates;
        mesher = mesh.Mesher;
        this.gasPrice = gasPrice;
        this.sparkSpreadPrice = sparkSpreadPrice;
        if (!(NStates == mesher.Layout.Dim[stateDirection]))
        {
            throw new NanoQLException("mesher does not fit to vpp arguments");
        }

        stateEvolve = new byte[NStates];
        for (int i = 0; i < NStates; ++i)
        {
            int j = i % ((2 * TMinUp) + TMinDown);
            if (j < TMinUp)
            {
                stateEvolve[i] = 1;
            }
            else if (j < 2 * TMinUp)
            {
                stateEvolve[i] = 2;
            }
        }

        x = new double[NStates];
        changed = new double[NStates];
    }

    /// <summary>Gets the number of states.</summary>
    public int NStates { get; }

    /// <summary>Gets the heat rate.</summary>
    protected double HeatRate { get; }

    /// <summary>Gets the minimum output while running.</summary>
    protected double PMin { get; }

    /// <summary>Gets the maximum output.</summary>
    protected double PMax { get; }

    /// <summary>Gets the minimum up time, in steps.</summary>
    protected int TMinUp { get; }

    /// <summary>Gets the minimum down time, in steps.</summary>
    protected int TMinDown { get; }

    /// <summary>Gets the fuel a start burns.</summary>
    protected double StartUpFuel { get; }

    /// <summary>Gets a start's fixed cost.</summary>
    protected double StartUpFixCost { get; }

    /// <summary>Gets the cost added to each unit of fuel.</summary>
    protected double FuelCostAddon { get; }

    /// <inheritdoc/>
    public override void ApplyTo(Span<double> a, double t)
    {
        FdmLinearOpLayout layout = mesher.Layout;
        int nStates = layout.Dim[stateDirection];
        FdmLinearOpIterator iter = layout.Begin();
        for (int k = 0; k < layout.Size; ++k, iter.Increment())
        {
            a[iter.Index] += Evolve(iter, t);
        }

        iter = layout.Begin();
        for (int k = 0; k < layout.Size; ++k, iter.Increment())
        {
            if (iter.Coordinates[stateDirection] == 0)
            {
                for (int i = 0; i < nStates; ++i)
                {
                    x[i] = a[layout.Neighbourhood(iter, stateDirection, i)];
                }

                double gas = gasPrice.InnerValue(iter, t);
                ChangeState(gas, x, t, changed);
                for (int i = 0; i < nStates; ++i)
                {
                    a[layout.Neighbourhood(iter, stateDirection, i)] = changed[i];
                }
            }
        }
    }

    /// <summary>The value of a point's states: the best of them the plant can be in.</summary>
    /// <param name="states">The values of a point's states.</param>
    /// <returns>The value.</returns>
    public abstract double MaxValue(ReadOnlySpan<double> states);

    /// <summary>The earnings of a step at the minimum output.</summary>
    /// <param name="sparkSpread">The spark spread.</param>
    /// <returns>The earnings.</returns>
    protected double EvolveAtPMin(double sparkSpread) => PMin * (sparkSpread - (HeatRate * FuelCostAddon));

    /// <summary>The earnings of a step at the maximum output.</summary>
    /// <param name="sparkSpread">The spark spread.</param>
    /// <returns>The earnings.</returns>
    protected double EvolveAtPMax(double sparkSpread) => PMax * (sparkSpread - (HeatRate * FuelCostAddon));

    /// <summary>The earnings of a step in the state at a point: 0 off.</summary>
    /// <param name="iter">The point.</param>
    /// <param name="t">The time.</param>
    /// <returns>The earnings.</returns>
    protected double Evolve(FdmLinearOpIterator iter, double t)
    {
        ArgumentNullException.ThrowIfNull(iter);
        int state = iter.Coordinates[stateDirection];
        return stateEvolve[state] switch
        {
            0 => 0.0,
            1 => EvolveAtPMin(sparkSpreadPrice.InnerValue(iter, t)),
            _ => EvolveAtPMax(sparkSpreadPrice.InnerValue(iter, t)),
        };
    }

    /// <summary>A point's states after the plant's allowed changes, at the gas price there.</summary>
    /// <param name="gasPrice">The gas price.</param>
    /// <param name="state">The states' values before the change.</param>
    /// <param name="t">The time.</param>
    /// <param name="result">The states' values after it.</param>
    protected abstract void ChangeState(double gasPrice, ReadOnlySpan<double> state, double t, Span<double> result);
}
