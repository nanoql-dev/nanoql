// SPDX-FileCopyrightText: Copyright (C) 2011, 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmvppstartlimitstepcondition.hpp ql/experimental/finitedifferences/fdmvppstartlimitstepcondition.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;

namespace NanoQL.Experimental;

/// <summary>
/// A virtual power plant's step condition with its minimum up and down times and, optionally, a limit on its starts: a
/// block of states per start left.
/// </summary>
[Experimental("NQL0001")]
public sealed class FdmVPPStartLimitStepCondition : FdmVPPStepCondition
{
    private readonly int? nStarts;

    /// <summary>Initializes a new instance of the <see cref="FdmVPPStartLimitStepCondition"/> class.</summary>
    /// <param name="parameters">The plant's parameters.</param>
    /// <param name="nStarts">The most starts, or <see langword="null"/> for no limit.</param>
    /// <param name="mesh">The mesher and the states' direction.</param>
    /// <param name="gasPrice">The gas price.</param>
    /// <param name="sparkSpreadPrice">The spark spread.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The mesher doesn't fit the states, or a minimum time isn't positive ("minimum
    /// up time must be greater than one", "minimum down time must be greater than one").</exception>
    public FdmVPPStartLimitStepCondition(
        FdmVPPStepConditionParams parameters,
        int? nStarts,
        FdmVPPStepConditionMesher mesh,
        FdmInnerValueCalculator gasPrice,
        FdmInnerValueCalculator sparkSpreadPrice)
        : base(parameters, NStatesOf(parameters.TMinUp, parameters.TMinDown, nStarts), mesh, gasPrice, sparkSpreadPrice)
    {
        this.nStarts = nStarts;
        if (!(TMinUp > 0))
        {
            throw new NanoQLException("minimum up time must be greater than one");
        }

        if (!(TMinDown > 0))
        {
            throw new NanoQLException("minimum down time must be greater than one");
        }
    }

    /// <summary>The number of states of a plant: a block of 2·tMinUp + tMinDown, per start left plus one.</summary>
    /// <param name="tMinUp">The minimum up time, in steps.</param>
    /// <param name="tMinDown">The minimum down time, in steps.</param>
    /// <param name="nStarts">The most starts, or <see langword="null"/> for no limit (one block).</param>
    /// <returns>The number of states.</returns>
    public static int NStatesOf(int tMinUp, int tMinDown, int? nStarts) =>
        ((2 * tMinUp) + tMinDown) * (nStarts is int n ? n + 1 : 1);

    /// <inheritdoc/>
    public override double MaxValue(ReadOnlySpan<double> states)
    {
        // std::max_element: the first largest
        double best = states[0];
        for (int i = 1; i < states.Length; ++i)
        {
            if (best < states[i])
            {
                best = states[i];
            }
        }

        return best;
    }

    /// <inheritdoc/>
    protected override void ChangeState(double gasPrice, ReadOnlySpan<double> state, double t, Span<double> result)
    {
        double startUpCost = StartUpFixCost + ((gasPrice + FuelCostAddon) * StartUpFuel);
        int sss = (2 * TMinUp) + TMinDown;
        for (int i = 0; i < NStates; ++i)
        {
            int j = i % sss;
            if (j < TMinUp - 1)
            {
                result[i] = Max(state[i + 1], state[TMinUp + i + 1]);
            }
            else if (j == TMinUp - 1)
            {
                // std::max({a, b, c}): the first largest
                result[i] = Max(Max(state[i + TMinUp + 1], state[i]), state[i + TMinUp]);
            }
            else if (j < 2 * TMinUp)
            {
                result[i] = result[i - TMinUp];
            }
            else if (j < (2 * TMinUp) + TMinDown - 1)
            {
                result[i] = state[i + 1];
            }
            else if (nStarts is null)
            {
                result[i] = Max(state[i], Max(state[0], state[TMinUp]) - startUpCost);
            }
            else if (i >= sss)
            {
                result[i] = Max(state[i], Max(state[i + 1 - (2 * sss)], state[i + 1 - (2 * sss) + TMinUp]) - startUpCost);
            }
            else
            {
                result[i] = state[i];
            }
        }
    }

    // std::max(a, b): a unless a < b
    private static double Max(double a, double b) => a < b ? b : a;
}
