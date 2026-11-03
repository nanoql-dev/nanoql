// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/pathwise/pathwiseproductcaplet.hpp ql/models/marketmodels/products/pathwise/pathwiseproductcaplet.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// Caps for the pathwise engines, already deflated: cap k is the deflated caplets on one strike from its start rate to
/// before its end rate, each caplet's cash flow and derivatives its cap's.
/// </summary>
/// <remarks>
/// A working type: it steps its own deflated caplets (<see cref="MarketModelPathwiseMultiDeflatedCaplet"/>) and copies
/// each caplet's cash flow into each cap holding it, QuantLib's assignment (<see
/// cref="MarketModelPathwiseMultiProductCashFlow.CopyFrom"/>).
/// </remarks>
[WorkingType]
public sealed class MarketModelPathwiseMultiDeflatedCap : MarketModelPathwiseMultiProduct
{
    private readonly MarketModelPathwiseMultiDeflatedCaplet underlyingCaplets;
    private readonly int numberRates;
    private readonly ImmutableArray<(int Start, int End)> startsAndEnds;
    private readonly int[] innerCashFlowSizes;
    private readonly MarketModelPathwiseMultiProductCashFlow[][] innerCashFlowsGenerated;

    /// <summary>Initializes a new instance of the <see cref="MarketModelPathwiseMultiDeflatedCap"/> class.</summary>
    /// <param name="rateTimes">The rate times, increasing, one more than the accruals.</param>
    /// <param name="accruals">The accruals, one a rate.</param>
    /// <param name="paymentTimes">The payment times, increasing, one a rate.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="startsAndEnds">Each cap's first rate and the rate after its last, at most the number of rates.</param>
    /// <exception cref="ArgumentException">
    /// The times aren't increasing, the inputs aren't one a rate, or a cap doesn't start before it ends within the rates.
    /// </exception>
    public MarketModelPathwiseMultiDeflatedCap(
        ReadOnlySpan<double> rateTimes,
        ReadOnlySpan<double> accruals,
        ReadOnlySpan<double> paymentTimes,
        double strike,
        ReadOnlySpan<(int Start, int End)> startsAndEnds)
    {
        underlyingCaplets = new MarketModelPathwiseMultiDeflatedCaplet(rateTimes, accruals, paymentTimes, strike);
        numberRates = accruals.Length;
        this.startsAndEnds = [.. startsAndEnds];
        for (int j = 0; j < startsAndEnds.Length; ++j)
        {
            (int start, int end) = startsAndEnds[j];
            ArgumentOutOfRangeException.ThrowIfNegative(start, nameof(startsAndEnds));
            if (!(start < end))
            {
                throw new ArgumentException($"a cap must start before it ends: {j}{start}{end}", nameof(startsAndEnds));
            }

            if (!(end <= accruals.Length))
            {
                throw new ArgumentException(
                    $"a cap must end when the underlying caplets: {j}{start}{end}", nameof(startsAndEnds));
            }
        }

        innerCashFlowSizes = new int[accruals.Length];
        innerCashFlowsGenerated = new MarketModelPathwiseMultiProductCashFlow[accruals.Length][];
        int max = underlyingCaplets.MaxNumberOfCashFlowsPerProductPerStep;
        for (int i = 0; i < innerCashFlowsGenerated.Length; ++i)
        {
            innerCashFlowsGenerated[i] = new MarketModelPathwiseMultiProductCashFlow[max];
            for (int j = 0; j < max; ++j)
            {
                innerCashFlowsGenerated[i][j] = new MarketModelPathwiseMultiProductCashFlow(accruals.Length + 1);
            }
        }
    }

    private MarketModelPathwiseMultiDeflatedCap(MarketModelPathwiseMultiDeflatedCap other)
    {
        underlyingCaplets = (MarketModelPathwiseMultiDeflatedCaplet)other.underlyingCaplets.Clone();
        numberRates = other.numberRates;
        startsAndEnds = other.startsAndEnds;
        innerCashFlowSizes = (int[])other.innerCashFlowSizes.Clone();
        innerCashFlowsGenerated = new MarketModelPathwiseMultiProductCashFlow[other.innerCashFlowsGenerated.Length][];
        for (int i = 0; i < innerCashFlowsGenerated.Length; ++i)
        {
            innerCashFlowsGenerated[i] = new MarketModelPathwiseMultiProductCashFlow[other.innerCashFlowsGenerated[i].Length];
            for (int j = 0; j < innerCashFlowsGenerated[i].Length; ++j)
            {
                MarketModelPathwiseMultiProductCashFlow from = other.innerCashFlowsGenerated[i][j];
                innerCashFlowsGenerated[i][j] = new MarketModelPathwiseMultiProductCashFlow(from.Amount.Length);
                innerCashFlowsGenerated[i][j].CopyFrom(from);
            }
        }
    }

    /// <inheritdoc/>
    public override EvolutionDescription Evolution => underlyingCaplets.Evolution;

    /// <summary>Gets the number of caps.</summary>
    public override int NumberOfProducts => startsAndEnds.Length;

    /// <summary>Gets the caplets' most cash flows at a step, 1.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep => underlyingCaplets.MaxNumberOfCashFlowsPerProductPerStep;

    /// <summary>Gets <see langword="true"/>: the caplets' amounts are deflated.</summary>
    public override bool AlreadyDeflated => underlyingCaplets.AlreadyDeflated;

    /// <inheritdoc/>
    public override ImmutableArray<int> SuggestedNumeraires() => underlyingCaplets.SuggestedNumeraires();

    /// <inheritdoc/>
    public override ImmutableArray<double> PossibleCashFlowTimes() => underlyingCaplets.PossibleCashFlowTimes();

    /// <inheritdoc/>
    public override void Reset() => underlyingCaplets.Reset();

    /// <inheritdoc/>
    public override bool NextTimeStep(
        CurveState currentState,
        Span<int> numberCashFlowsThisStep,
        MarketModelPathwiseMultiProductCashFlow[][] cashFlowsGenerated)
    {
        ArgumentNullException.ThrowIfNull(cashFlowsGenerated);
        bool done = underlyingCaplets.NextTimeStep(currentState, innerCashFlowSizes, innerCashFlowsGenerated);

        for (int k = 0; k < startsAndEnds.Length; ++k)
        {
            numberCashFlowsThisStep[k] = 0;
        }

        for (int j = 0; j < numberRates; ++j)
        {
            if (innerCashFlowSizes[j] > 0)
            {
                for (int k = 0; k < startsAndEnds.Length; ++k)
                {
                    if (startsAndEnds[k].Start <= j && j < startsAndEnds[k].End)
                    {
                        for (int l = 0; l < innerCashFlowSizes[j]; ++l)
                        {
                            cashFlowsGenerated[k][numberCashFlowsThisStep[k]++].CopyFrom(innerCashFlowsGenerated[j][l]);
                        }
                    }
                }
            }
        }

        return done;
    }

    /// <inheritdoc/>
    public override MarketModelPathwiseMultiProduct Clone() => new MarketModelPathwiseMultiDeflatedCap(this);
}
