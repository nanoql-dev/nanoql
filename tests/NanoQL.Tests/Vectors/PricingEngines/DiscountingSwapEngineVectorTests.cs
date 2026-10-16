// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The discounting swap engine vectors, <c>tests/vectors/pricingengines/swap/discountingswapengine.json</c>: the full
/// results of six swaps under sampled evaluation contexts, three flat curves and sampled engine settings. A result
/// QuantLib leaves null, where it throws "not available" or "not provided" on reading it, is null in the file and in
/// nanoql's <see cref="SwapResults"/>. A case whose pricing throws has that throw as its one output, "calculate".
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscountingSwapEngineVectorTests
{
    [Fact]
    public void DiscountingSwapEngineVector()
    {
        var file = VectorFile.Load("pricingengines/swap/discountingswapengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Swap swap = CashFlowInputs.SwapOf(c.Input("swap"), context);
            FlatForward curve = CashFlowInputs.CurveOf(c.Input("curve"));
            bool? includeSettlementDateFlows = c.NullableBool("includeSettlementDateFlows");
            Date? settlementDate = c.NullableDateInput("settlementDate");
            Date? npvDate = c.NullableDateInput("npvDate");
            SwapResults Calculate() => DiscountingSwapEngine.Calculate(
                swap, context, curve, includeSettlementDateFlows, settlementDate, npvDate);

            if (c.Expected.TryGetProperty("calculate", out _))
            {
                check.Output(c, "calculate", () => Calculate());
                continue;
            }

            // Priced once; a throw is reported on every output.
            SwapResults? results = null;
            Exception? error = null;
            try
            {
                results = Calculate();
            }
            catch (Exception e) when (e is ArgumentException or NanoQLException)
            {
                error = e;
            }

            SwapResults Results() => results ?? throw error!;
            check.Output(c, "NPV", () => Results().NPV);
            check.Output(c, "errorEstimate", () => Results().ErrorEstimate);
            check.Output(c, "valuationDate", () => Results().ValuationDate);
            check.Output(c, "legNPV", () => Results().LegNPV);
            check.Output(c, "legBPS", () => Results().LegBPS);
            check.Output(c, "startDiscounts", () => Results().StartDiscounts);
            check.Output(c, "endDiscounts", () => Results().EndDiscounts);
            check.Output(c, "npvDateDiscount", () => Results().NpvDateDiscount);
        }

        check.Complete();
    }
}
