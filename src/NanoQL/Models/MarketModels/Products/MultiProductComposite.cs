// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multiproductcomposite.hpp ql/models/marketmodels/products/multiproductcomposite.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// A composite of market-model products whose products stay apart: its products are each part's products in turn,
/// each part's cash flows scaled by its multiplier.
/// </summary>
/// <remarks>
/// A working type; a part's end is recorded and the composite's end is every part's or its last step (<see
/// cref="MarketModelComposite"/>; a documented divergence).
/// </remarks>
[WorkingType]
public sealed class MultiProductComposite : MarketModelComposite
{
    /// <summary>Initializes a new instance of the <see cref="MultiProductComposite"/> class, with no part.</summary>
    public MultiProductComposite()
    {
    }

    private MultiProductComposite(MultiProductComposite other)
        : base(other)
    {
    }

    /// <summary>Gets the parts' products in all.</summary>
    public override int NumberOfProducts
    {
        get
        {
            int result = 0;
            for (int i = 0; i < Size; ++i)
            {
                result += Item(i).NumberOfProducts;
            }

            return result;
        }
    }

    /// <summary>Gets the most cash flows a part's product generates at a step.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep
    {
        get
        {
            int result = 0;
            for (int i = 0; i < Size; ++i)
            {
                result = Math.Max(result, Item(i).MaxNumberOfCashFlowsPerProductPerStep);
            }

            return result;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The composite isn't finalized, or is stepped past its last step.</exception>
    public override bool NextTimeStep(
        CurveState currentState,
        Span<int> numberCashFlowsThisStep,
        MarketModelMultiProductCashFlow[][] cashFlowsGenerated)
    {
        ArgumentNullException.ThrowIfNull(cashFlowsGenerated);
        ThrowIfNotFinalized();
        int offset = 0;

        // for each sub-product...
        for (int n = 0; n < Size; ++n)
        {
            int products = Item(n).NumberOfProducts;
            if (StepPart(n, currentState))
            {
                // ...copy the results. Time indices need to be remapped so that they point into all cash-flow times.
                // Amounts need to be adjusted by the corresponding multiplier.
                ReadOnlySpan<int> counts = PartCounts(n);
                MarketModelMultiProductCashFlow[][] flows = PartCashFlows(n);
                double multiplier = Multiplier(n);
                for (int j = 0; j < products; ++j)
                {
                    numberCashFlowsThisStep[j + offset] = counts[j];
                    MarketModelMultiProductCashFlow[] to = cashFlowsGenerated[j + offset];
                    for (int k = 0; k < counts[j]; ++k)
                    {
                        ref MarketModelMultiProductCashFlow from = ref flows[j][k];
                        to[k].TimeIndex = CashFlowTimeIndex(n, from.TimeIndex);
                        to[k].Amount = from.Amount * multiplier;
                    }
                }
            }
            else
            {
                for (int j = 0; j < products; ++j)
                {
                    numberCashFlowsThisStep[j + offset] = 0;
                }
            }

            // the offset is updated whether or not the product was evolved
            offset += products;
        }

        return EndStep();
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new MultiProductComposite(this);
}
