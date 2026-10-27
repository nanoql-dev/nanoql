// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/singleproductcomposite.hpp ql/models/marketmodels/products/singleproductcomposite.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// A composite of market-model products as one product: every part's products' cash flows, scaled by the part's
/// multiplier, are the one product's.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-mmp plan's MMPR1 (i)); a part's end is recorded and the composite's end is every part's or
/// its last step (<see cref="MarketModelComposite"/>; MMPR4 (vii), a documented divergence), and its count is 0 at a
/// step where no part steps, as <see cref="MultiProductComposite"/> zeroes a part it doesn't step, where QuantLib's
/// leaves the last step's count (<c>singleproductcomposite.cpp:63</c>).
/// </para>
/// <para>
/// <see cref="MaxNumberOfCashFlowsPerProductPerStep"/> counts each part's products (MMPR4 (viii), a documented
/// divergence): QuantLib's sums the parts' maxima per product, so a part of several products paying at one step writes
/// past its buffer (<c>Divergences/SingleProductCompositeBufferTests.cs</c>).
/// </para>
/// </remarks>
public sealed class SingleProductComposite : MarketModelComposite
{
    /// <summary>Initializes a new instance of the <see cref="SingleProductComposite"/> class, with no part.</summary>
    public SingleProductComposite()
    {
    }

    private SingleProductComposite(SingleProductComposite other)
        : base(other)
    {
    }

    /// <summary>Gets 1: the composite is one product.</summary>
    public override int NumberOfProducts => 1;

    /// <summary>Gets the most cash flows the parts generate at a step: each part's maximum times its
    /// products.</summary>
    public override int MaxNumberOfCashFlowsPerProductPerStep
    {
        get
        {
            int result = 0;
            for (int i = 0; i < Size; ++i)
            {
                MarketModelMultiProduct p = Item(i);
                result += p.MaxNumberOfCashFlowsPerProductPerStep * p.NumberOfProducts;
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
        int totalCashflows = 0;
        numberCashFlowsThisStep[0] = 0;
        MarketModelMultiProductCashFlow[] to = cashFlowsGenerated[0];

        // for each sub-product...
        for (int n = 0; n < Size; ++n)
        {
            if (StepPart(n, currentState))
            {
                // ...copy the results. Time indices need to be remapped so that they point into all cash-flow times.
                // Amounts need to be adjusted by the corresponding multiplier.
                ReadOnlySpan<int> counts = PartCounts(n);
                MarketModelMultiProductCashFlow[][] flows = PartCashFlows(n);
                double multiplier = Multiplier(n);
                int products = Item(n).NumberOfProducts;
                for (int j = 0; j < products; ++j)
                {
                    int offset = totalCashflows;
                    totalCashflows += counts[j];
                    for (int k = 0; k < counts[j]; ++k)
                    {
                        ref MarketModelMultiProductCashFlow from = ref flows[j][k];
                        to[k + offset].TimeIndex = CashFlowTimeIndex(n, from.TimeIndex);
                        to[k + offset].Amount = from.Amount * multiplier;
                    }

                    numberCashFlowsThisStep[0] = totalCashflows;
                }
            }
        }

        return EndStep();
    }

    /// <inheritdoc/>
    public override MarketModelMultiProduct Clone() => new SingleProductComposite(this);
}
