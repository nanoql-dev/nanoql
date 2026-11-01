// SPDX-FileCopyrightText: Copyright (C) 2009 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/pathwise/pathwiseproductcallspecified.hpp ql/models/marketmodels/products/pathwise/pathwiseproductcallspecified.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// A callable pathwise product: the underlying's cash flows and derivatives until the strategy calls it at an exercise
/// time, the rebate's from then on; without a rebate, a pathwise cash rebate of 0 at the exercise times.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-mmp plan's MMPR1 (i)): it steps its own clones of the underlying, the strategy and the rebate.
/// </para>
/// <para>
/// Its end and counts are <see cref="CallSpecifiedMultiProduct"/>'s (the S-mmp plan's MMPR4 (vii), option (1), with
/// Revision 4's (b), and (xvi), documented divergences): QuantLib's end with its underlying, but on an underlying with no
/// possible cash-flow times while an exercise time remains; a finished underlying not stepped again; ended for good
/// once ended with its underlying; and every count zeroed at each step's start
/// (<c>pathwiseproductcallspecified.cpp:136-153</c>). A rebate of more products than the underlying, which writes past
/// the buffers QuantLib sizes by the underlying's, is refused.
/// </para>
/// </remarks>
[WorkingType]
public sealed class CallSpecifiedPathwiseMultiProduct : MarketModelPathwiseMultiProduct
{
    private readonly MarketModelPathwiseMultiProduct underlying;
    private readonly ExerciseStrategy<CurveState> strategy;
    private readonly MarketModelPathwiseMultiProduct rebate;
    private readonly EvolutionDescription evolution;
    private readonly ImmutableArray<ImmutableArray<bool>> isPresent;
    private readonly ImmutableArray<double> cashFlowTimes;
    private readonly int rebateOffset;
    private readonly bool underlyingHasCashFlows;
    private readonly int[] dummyCashFlowsThisStep;
    private readonly MarketModelPathwiseMultiProductCashFlow[][] dummyCashFlowsGenerated;
    private bool wasCalled;
    private bool underlyingDone;
    private int currentIndex;
    private bool callable = true;

    /// <summary>Initializes a new instance of the <see cref="CallSpecifiedPathwiseMultiProduct"/> class.</summary>
    /// <param name="underlying">The underlying, cloned.</param>
    /// <param name="strategy">The exercise strategy, cloned.</param>
    /// <param name="rebate">
    /// The rebate, cloned, on the underlying's rate times and deflation, of at most its products;
    /// <see langword="null"/> for a pathwise cash rebate of 0 at the exercise times.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The rebate's rate times or deflation aren't the underlying's, or it has more products.
    /// </exception>
    public CallSpecifiedPathwiseMultiProduct(
        MarketModelPathwiseMultiProduct underlying,
        ExerciseStrategy<CurveState> strategy,
        MarketModelPathwiseMultiProduct? rebate = null)
    {
        ArgumentNullException.ThrowIfNull(underlying);
        ArgumentNullException.ThrowIfNull(strategy);
        this.underlying = underlying.Clone();
        this.strategy = strategy.Clone();
        int products = this.underlying.NumberOfProducts;
        EvolutionDescription d1 = underlying.Evolution;
        ImmutableArray<double> rateTimes1 = d1.RateTimes;
        ImmutableArray<double> evolutionTimes1 = d1.EvolutionTimes;
        ImmutableArray<double> exerciseTimes = strategy.ExerciseTimes();

        if (rebate is not null)
        {
            EvolutionDescription d2 = rebate.Evolution;
            if (!rateTimes1.AsSpan().SequenceEqual(d2.RateTimes.AsSpan()))
            {
                throw new ArgumentException("incompatible rate times", nameof(rebate));
            }

            if (underlying.AlreadyDeflated != rebate.AlreadyDeflated)
            {
                throw new ArgumentException("incompatible deflations", nameof(rebate));
            }

            // QuantLib sizes the rebate's buffers by the underlying's products: a rebate of more writes past them
            if (rebate.NumberOfProducts > products)
            {
                throw new ArgumentException(
                    $"the rebate's {rebate.NumberOfProducts} products are more than the underlying's {products}", nameof(rebate));
            }

            this.rebate = rebate.Clone();
        }
        else
        {
            var description = new EvolutionDescription(rateTimes1.AsSpan(), exerciseTimes.AsSpan());
            var amounts = new Matrix(products, exerciseTimes.Length, 0.0);

            this.rebate = new MarketModelPathwiseCashRebate(description, exerciseTimes.AsSpan(), amounts, products);
        }

        MergedTimes merged = UtilitiesFunctions.MergeTimes(
            [evolutionTimes1, exerciseTimes, this.rebate.Evolution.EvolutionTimes, strategy.RelevantTimes()]);
        isPresent = merged.IsPresent;

        // TODO: add relevant rates
        evolution = new EvolutionDescription(rateTimes1.AsSpan(), merged.Times.AsSpan());

        rebateOffset = this.underlying.PossibleCashFlowTimes().Length;
        cashFlowTimes = [.. this.underlying.PossibleCashFlowTimes(), .. this.rebate.PossibleCashFlowTimes()];
        underlyingHasCashFlows = !this.underlying.PossibleCashFlowTimes().IsEmpty;

        dummyCashFlowsThisStep = new int[products];
        int n = this.rebate.MaxNumberOfCashFlowsPerProductPerStep;
        dummyCashFlowsGenerated = new MarketModelPathwiseMultiProductCashFlow[products][];
        for (int i = 0; i < products; ++i)
        {
            dummyCashFlowsGenerated[i] = new MarketModelPathwiseMultiProductCashFlow[n];
            for (int j = 0; j < n; ++j)
            {
                dummyCashFlowsGenerated[i][j] = new MarketModelPathwiseMultiProductCashFlow(d1.NumberOfRates + 1);
            }
        }
    }

    private CallSpecifiedPathwiseMultiProduct(CallSpecifiedPathwiseMultiProduct other)
    {
        underlying = other.underlying.Clone();
        strategy = other.strategy.Clone();
        rebate = other.rebate.Clone();
        evolution = other.evolution;
        isPresent = other.isPresent;
        cashFlowTimes = other.cashFlowTimes;
        rebateOffset = other.rebateOffset;
        underlyingHasCashFlows = other.underlyingHasCashFlows;
        dummyCashFlowsThisStep = (int[])other.dummyCashFlowsThisStep.Clone();
        dummyCashFlowsGenerated = new MarketModelPathwiseMultiProductCashFlow[other.dummyCashFlowsGenerated.Length][];
        for (int i = 0; i < dummyCashFlowsGenerated.Length; ++i)
        {
            dummyCashFlowsGenerated[i] = new MarketModelPathwiseMultiProductCashFlow[other.dummyCashFlowsGenerated[i].Length];
            for (int j = 0; j < dummyCashFlowsGenerated[i].Length; ++j)
            {
                MarketModelPathwiseMultiProductCashFlow from = other.dummyCashFlowsGenerated[i][j];
                dummyCashFlowsGenerated[i][j] = new MarketModelPathwiseMultiProductCashFlow(from.Amount.Length);
                dummyCashFlowsGenerated[i][j].CopyFrom(from);
            }
        }

        wasCalled = other.wasCalled;
        underlyingDone = other.underlyingDone;
        currentIndex = other.currentIndex;
        callable = other.callable;
    }

    /// <inheritdoc/>
    public override EvolutionDescription Evolution => evolution;

    /// <summary>Gets the underlying's number of products.</summary>
    public override int NumberOfProducts => underlying.NumberOfProducts;

    /// <summary>Gets the larger of the underlying's and the rebate's most cash flows at a step.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep =>
        Math.Max(underlying.MaxNumberOfCashFlowsPerProductPerStep, rebate.MaxNumberOfCashFlowsPerProductPerStep);

    /// <summary>Gets a value indicating whether the underlying's amounts are deflated.</summary>
    public override bool AlreadyDeflated => underlying.AlreadyDeflated;

    /// <summary>Gets the underlying the product steps: its own clone.</summary>
    public MarketModelPathwiseMultiProduct Underlying => underlying;

    /// <summary>Gets the strategy the product steps: its own clone, at the product's point on the path.</summary>
    public ExerciseStrategy<CurveState> Strategy => strategy;

    /// <summary>Gets the rebate the product steps: its own clone.</summary>
    public MarketModelPathwiseMultiProduct Rebate => rebate;

    /// <summary>The underlying's suggested numeraires.</summary>
    /// <returns>The numeraires.</returns>
    public override ImmutableArray<int> SuggestedNumeraires() => underlying.SuggestedNumeraires();

    /// <summary>The underlying's cash-flow times, then the rebate's.</summary>
    /// <returns>The times.</returns>
    public override ImmutableArray<double> PossibleCashFlowTimes() => cashFlowTimes;

    /// <inheritdoc/>
    public override void Reset()
    {
        underlying.Reset();
        rebate.Reset();
        strategy.Reset();
        currentIndex = 0;
        wasCalled = false;
        underlyingDone = false;
    }

    /// <inheritdoc/>
    public override bool NextTimeStep(
        CurveState currentState,
        Span<int> numberCashFlowsThisStep,
        MarketModelPathwiseMultiProductCashFlow[][] cashFlowsGenerated)
    {
        ArgumentNullException.ThrowIfNull(cashFlowsGenerated);
        numberCashFlowsThisStep.Clear();
        if (!wasCalled && underlyingDone && underlyingHasCashFlows)
        {
            ++currentIndex;
            return true;
        }

        bool isUnderlyingTime = isPresent[0][currentIndex];
        bool isExerciseTime = isPresent[1][currentIndex];
        bool isRebateTime = isPresent[2][currentIndex];
        bool isStrategyRelevantTime = isPresent[3][currentIndex];

        bool done = false;

        if (!wasCalled && isStrategyRelevantTime)
        {
            strategy.NextStep(currentState);
        }

        if (!wasCalled && isExerciseTime && callable)
        {
            wasCalled = strategy.Exercise(currentState);
        }

        if (wasCalled)
        {
            if (isRebateTime)
            {
                done = rebate.NextTimeStep(currentState, numberCashFlowsThisStep, cashFlowsGenerated);
                for (int i = 0; i < numberCashFlowsThisStep.Length; ++i)
                {
                    for (int j = 0; j < numberCashFlowsThisStep[i]; ++j)
                    {
                        cashFlowsGenerated[i][j].TimeIndex += rebateOffset;
                    }
                }
            }
        }
        else
        {
            if (isRebateTime)
            {
                rebate.NextTimeStep(currentState, dummyCashFlowsThisStep, dummyCashFlowsGenerated);
            }

            if (isUnderlyingTime && !underlyingDone)
            {
                underlyingDone = underlying.NextTimeStep(currentState, numberCashFlowsThisStep, cashFlowsGenerated);
            }

            done = underlyingDone;
        }

        if (!wasCalled && done && !underlyingHasCashFlows)
        {
            for (int k = currentIndex + 1; k < isPresent[1].Length; ++k)
            {
                if (isPresent[1][k])
                {
                    done = false;
                    break;
                }
            }
        }

        ++currentIndex;
        return done || currentIndex == evolution.EvolutionTimes.Length;
    }

    /// <inheritdoc/>
    public override MarketModelPathwiseMultiProduct Clone() => new CallSpecifiedPathwiseMultiProduct(this);

    /// <summary>Lets the strategy call the product, QuantLib's default.</summary>
    public void EnableCallability() => callable = true;

    /// <summary>Stops the strategy from calling the product: it pays its underlying's cash flows.</summary>
    public void DisableCallability() => callable = false;
}
