// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The constant-notional cross-currency swap engine vectors,
/// <c>tests/vectors/pricingengines/swap/discountingconstnotionalcrosscurrencyswapengine.json</c>: swaps priced with
/// either currency domestic, the settlement date's flows counted or not, later settlement and NPV dates, explicit FX
/// settlement dates, and the engine's refusals; the results of
/// <see cref="DiscountingConstNotionalCrossCurrencySwapEngine"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscountingConstNotionalCrossCurrencySwapEngineVectorTests
{
    [Fact]
    public void DiscountingConstNotionalCrossCurrencySwapEngineVector()
    {
        var file = VectorFile.Load("pricingengines/swap/discountingconstnotionalcrosscurrencyswapengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            ConstNotionalCrossCurrencySwap swap = CrossCurrencyInputs.SwapOf(c.Input("swap"), context);
            CrossCurrencyInputs.EngineArguments e = CrossCurrencyInputs.EngineOf(c.Input("engine"));
            CrossCurrencySwapResults Price() => DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
                swap,
                context,
                e.DomesticCcy,
                e.DomesticCurve,
                e.ForeignCcy,
                e.ForeignCurve,
                e.SpotFX,
                e.IncludeSettlementDateFlows,
                e.SettlementDate,
                e.NpvDate,
                e.SpotFXSettleDate);
            if (c.Expected.TryGetProperty("calculate", out _))
            {
                check.Output(c, "calculate", () => Price());
                continue;
            }

            CrossCurrencyInputs.CheckResults(check, c, Price());
        }

        check.Complete();
    }
}
