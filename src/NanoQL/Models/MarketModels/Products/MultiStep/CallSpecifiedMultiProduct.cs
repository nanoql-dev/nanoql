// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multistep/callspecifiedmultiproduct.hpp ql/models/marketmodels/products/multistep/callspecifiedmultiproduct.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// A callable product: the underlying's cash flows until the strategy calls it at an exercise time, the rebate's from
/// then on; without a rebate, a cash rebate of 0 at the exercise times.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-mmp plan's MMPR1 (i)), unsealed: QuantLib's upper-bound engine derives its decorated hedge
/// from it. It steps its own clones of the underlying, the strategy and the rebate.
/// </para>
/// <para>
/// Its end (the S-mmp plan's MMPR4 (vii), option (1), ruled by the user, with Revision 4's (b), a documented
/// divergence): QuantLib's end, with its underlying, is kept, but on an underlying with no possible cash-flow times
/// (QuantLib's <c>MultiStepNothing</c>), which can't end it while an exercise time remains; a finished underlying isn't
/// stepped again; and once ended with its underlying it stays ended, with no call and no cash flow. QuantLib's
/// standalone Bermudan on <c>MultiStepNothing</c>, done at its first step, ends at once
/// (<c>Divergences/CallSpecifiedEndTests.cs</c>).
/// </para>
/// <para>
/// Every count is zeroed at each step's start (MMPR4 (xvi), a documented divergence): QuantLib writes none at a step
/// where neither the underlying nor the rebate steps, so the engine adds the previous step's flows again
/// (<c>callspecifiedmultiproduct.cpp:144-167</c>). A rebate of more products than the underlying, which writes past
/// the buffers QuantLib sizes by the underlying's, is refused; a rebate of fewer pays its own products, the others
/// nothing (the upper-bound engine's one-product rebate of a hedge of several, <c>upperboundengine.cpp:124-125</c>).
/// </para>
/// </remarks>
public class CallSpecifiedMultiProduct : MarketModelMultiProduct
{
    private readonly MarketModelMultiProduct underlying;
    private readonly ExerciseStrategy<CurveState> strategy;
    private readonly MarketModelMultiProduct rebate;
    private readonly EvolutionDescription evolution;
    private readonly ImmutableArray<ImmutableArray<bool>> isPresent;
    private readonly ImmutableArray<double> cashFlowTimes;
    private readonly int rebateOffset;
    private readonly bool underlyingHasCashFlows;
    private readonly int[] dummyCashFlowsThisStep;
    private readonly MarketModelMultiProductCashFlow[][] dummyCashFlowsGenerated;
    private bool wasCalled;
    private bool underlyingDone;
    private int currentIndex;
    private bool callable = true;

    /// <summary>Initializes a new instance of the <see cref="CallSpecifiedMultiProduct"/> class.</summary>
    /// <param name="underlying">The underlying, cloned.</param>
    /// <param name="strategy">The exercise strategy, cloned.</param>
    /// <param name="rebate">
    /// The rebate, cloned, on the underlying's rate times, of at most its products; <see langword="null"/> for a cash
    /// rebate of 0 at the exercise times.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The rebate's rate times aren't the underlying's, or it has more products.
    /// </exception>
    public CallSpecifiedMultiProduct(
        MarketModelMultiProduct underlying, ExerciseStrategy<CurveState> strategy, MarketModelMultiProduct? rebate = null)
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

            this.rebate = new MarketModelCashRebate(description, exerciseTimes.AsSpan(), amounts, products);
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
        dummyCashFlowsGenerated = new MarketModelMultiProductCashFlow[products][];
        for (int i = 0; i < products; ++i)
        {
            dummyCashFlowsGenerated[i] = new MarketModelMultiProductCashFlow[n];
        }
    }

    /// <summary>Initializes a new instance of the <see cref="CallSpecifiedMultiProduct"/> class: a deep copy.</summary>
    /// <param name="other">The product copied, its state included.</param>
    protected CallSpecifiedMultiProduct(CallSpecifiedMultiProduct other)
    {
        ArgumentNullException.ThrowIfNull(other);
        underlying = other.underlying.Clone();
        strategy = other.strategy.Clone();
        rebate = other.rebate.Clone();
        evolution = other.evolution;
        isPresent = other.isPresent;
        cashFlowTimes = other.cashFlowTimes;
        rebateOffset = other.rebateOffset;
        underlyingHasCashFlows = other.underlyingHasCashFlows;
        dummyCashFlowsThisStep = (int[])other.dummyCashFlowsThisStep.Clone();
        dummyCashFlowsGenerated = [.. other.dummyCashFlowsGenerated.Select(f => (MarketModelMultiProductCashFlow[])f.Clone())];
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

    /// <summary>Gets the underlying the product steps: its own clone.</summary>
    public MarketModelMultiProduct Underlying => underlying;

    /// <summary>Gets the strategy the product steps: its own clone, at the product's point on the path.</summary>
    public ExerciseStrategy<CurveState> Strategy => strategy;

    /// <summary>Gets the rebate the product steps: its own clone.</summary>
    public MarketModelMultiProduct Rebate => rebate;

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
        MarketModelMultiProductCashFlow[][] cashFlowsGenerated)
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
    public override MarketModelMultiProduct Clone() => new CallSpecifiedMultiProduct(this);

    /// <summary>Lets the strategy call the product, QuantLib's default.</summary>
    public void EnableCallability() => callable = true;

    /// <summary>Stops the strategy from calling the product: it pays its underlying's cash flows.</summary>
    public void DisableCallability() => callable = false;
}
