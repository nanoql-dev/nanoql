// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The constant-notional cross-currency swap vectors,
/// <c>tests/vectors/instruments/constnotionalcrosscurrencyswap.json</c>: swaps of EUR and USD fixed-rate and Ibor legs
/// with notional exchanges, from a paid and a received leg and from legs, payer flags and currencies, an expired swap
/// and the constructors' refusals; each swap's inspectors, legs, and
/// <see cref="DiscountingConstNotionalCrossCurrencySwapEngine"/>'s results.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ConstNotionalCrossCurrencySwapVectorTests
{
    [Fact]
    public void ConstNotionalCrossCurrencySwapVector()
    {
        var file = VectorFile.Load("instruments/constnotionalcrosscurrencyswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("swap");
            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", () => CrossCurrencyInputs.SwapOf(spec, context));
                continue;
            }

            ConstNotionalCrossCurrencySwap swap = CrossCurrencyInputs.SwapOf(spec, context);
            int n = swap.NumberOfLegs;
            check.Output(c, "numberOfLegs", n);
            check.Output(c, "payer", Enumerable.Range(0, n).Select(swap.Payer).ToArray());
            check.Output(c, "legCurrencies", Enumerable.Range(0, n).Select(j => swap.LegCurrency(j)?.Code).ToArray());
            check.Output(c, "startDate", () => swap.StartDate());
            check.Output(c, "maturityDate", () => swap.MaturityDate());
            check.Output(c, "isExpired", swap.IsExpired(context));
            CrossCurrencyInputs.CheckLegs(check, c, swap.Legs, context);

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
