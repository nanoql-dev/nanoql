// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The mark-to-market cross-currency basis swap engine vectors,
/// <c>tests/vectors/experimental/fx/discountingmtmcrosscurrencybasisswapengine.json</c>: EUR against USD swaps priced
/// with either currency domestic, the settlement date's flows counted or not, later settlement and NPV dates, FX
/// settlement dates, and the engine's refusals; the results of
/// <see cref="DiscountingMtMCrossCurrencyBasisSwapEngine"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscountingMtMCrossCurrencyBasisSwapEngineVectorTests
{
    [Fact]
    public void DiscountingMtMCrossCurrencyBasisSwapEngineVector()
    {
        var file = VectorFile.Load("experimental/fx/discountingmtmcrosscurrencybasisswapengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            MtMCrossCurrencyBasisSwap swap = MtMCrossCurrencyBasisSwapVectorTests.SwapOf(c.Input("swap"), context);
            CrossCurrencyInputs.EngineArguments e = CrossCurrencyInputs.EngineOf(c.Input("engine"));
            MtMCrossCurrencyBasisSwapResults Price() => DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
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

            MtMCrossCurrencyBasisSwapVectorTests.CheckResults(check, c, swap, Price());
        }

        check.Complete();
    }
}
