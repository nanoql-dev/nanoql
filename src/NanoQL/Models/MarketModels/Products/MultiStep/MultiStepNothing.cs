// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multistep/multistepnothing.hpp ql/models/marketmodels/products/multistep/multistepnothing.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// Products that pay nothing, its path finished once it has stepped past a given index: the underlying of a Bermudan
/// option on <see cref="MarketModelMultiProduct"/>'s callable framework, and a placeholder.
/// </summary>
/// <remarks>
/// A working type. Its evolution is <see cref="MultiProductMultiStep"/>'s on the given evolution's rate times, its
/// evolution times not read, as QuantLib's.
/// </remarks>
[WorkingType]
public sealed class MultiStepNothing : MultiProductMultiStep
{
    private readonly int numberOfProducts;
    private readonly int doneIndex;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MultiStepNothing"/> class.</summary>
    /// <param name="evolution">The evolution whose rate times the products take.</param>
    /// <param name="numberOfProducts">The number of products.</param>
    /// <param name="doneIndex">The number of steps after which the path is finished; 0 or 1 for the first step.</param>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
    /// <exception cref="ArgumentException">The evolution has fewer than two rate times.</exception>
    public MultiStepNothing(EvolutionDescription evolution, int numberOfProducts = 1, int doneIndex = 0)
        : base(RatesOf(evolution))
    {
        ArgumentOutOfRangeException.ThrowIfNegative(numberOfProducts);
        ArgumentOutOfRangeException.ThrowIfNegative(doneIndex);
        this.numberOfProducts = numberOfProducts;
        this.doneIndex = doneIndex;
    }

    private MultiStepNothing(MultiStepNothing other)
        : base(other)
    {
        numberOfProducts = other.numberOfProducts;
        doneIndex = other.doneIndex;
        currentIndex = other.currentIndex;
    }

    /// <inheritdoc/>
    public override int NumberOfProducts => numberOfProducts;

    /// <summary>Gets 0.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep => 0;

    /// <summary>No time.</summary>
    /// <returns>An empty array.</returns>
    public override ImmutableArray<double> PossibleCashFlowTimes() => [];

    /// <inheritdoc/>
    public override void Reset() => currentIndex = 0;

    /// <inheritdoc/>
    public override bool NextTimeStep(
        CurveState currentState,
        Span<int> numberCashFlowsThisStep,
        MarketModelMultiProductCashFlow[][] cashFlowsGenerated)
    {
        numberCashFlowsThisStep.Clear();
        ++currentIndex;
        return currentIndex >= doneIndex;
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new MultiStepNothing(this);

    private static ReadOnlySpan<double> RatesOf(EvolutionDescription evolution)
    {
        ArgumentNullException.ThrowIfNull(evolution);
        return evolution.RateTimes.AsSpan();
    }
}
