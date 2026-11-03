// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The constant-notional cross-currency basis swap vectors,
/// <c>tests/vectors/instruments/constnotionalcrosscurrencybasisswap.json</c>: Ibor and overnight legs in two
/// currencies with spreads, gearings, payment lags, indexed coupons, stub indexes, seasoned and expired swaps; each
/// swap's inspectors, legs, and <see cref="DiscountingConstNotionalCrossCurrencySwapEngine"/>'s results with the fair
/// spreads.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ConstNotionalCrossCurrencyBasisSwapVectorTests
{
    [Fact]
    public void ConstNotionalCrossCurrencyBasisSwapVector()
    {
        var file = VectorFile.Load("instruments/constnotionalcrosscurrencybasisswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("swap");
            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", () => SwapOf(spec, context));
                continue;
            }

            ConstNotionalCrossCurrencyBasisSwap swap = SwapOf(spec, context);
            check.Output(c, "payNominal", swap.PayNominal);
            check.Output(c, "payCurrency", swap.PayCurrency?.Code);
            check.Output(c, "paySpread", swap.PaySpread);
            check.Output(c, "payGearing", swap.PayGearing);
            check.Output(c, "payIndexName", swap.PayIndex.Name);
            check.Output(c, "recNominal", swap.RecNominal);
            check.Output(c, "recCurrency", swap.RecCurrency?.Code);
            check.Output(c, "recSpread", swap.RecSpread);
            check.Output(c, "recGearing", swap.RecGearing);
            check.Output(c, "recIndexName", swap.RecIndex.Name);
            check.Output(c, "paySchedule", swap.PaySchedule.Dates);
            check.Output(c, "recSchedule", swap.RecSchedule.Dates);
            CrossCurrencyInputs.CheckLegs(check, c, swap.Legs, context);

            CrossCurrencyInputs.EngineArguments e = CrossCurrencyInputs.EngineOf(c.Input("engine"));
            ConstNotionalCrossCurrencyBasisSwapResults Price() =>
                DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
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

            ConstNotionalCrossCurrencyBasisSwapResults results = Price();
            CrossCurrencyInputs.CheckResults(check, c, results);

            // a leg compounding its spread has its fair spread left out
            foreach ((string output, double? fair) in new[]
                     { ("fairPaySpread", results.FairPaySpread), ("fairRecSpread", results.FairRecSpread) })
            {
                if (c.Expected.TryGetProperty(output, out _))
                {
                    check.Output(c, output, fair);
                }
            }
        }

        check.Complete();
    }

    private static ConstNotionalCrossCurrencyBasisSwap SwapOf(JsonElement spec, EvaluationContext context)
    {
        (double, Currency?, Schedule, IborIndex, double, double) Leg(string p) => (
            spec.GetProperty(p + "Nominal").GetDouble(),
            CrossCurrencyInputs.CurrencyOf(spec.GetProperty(p + "Currency")),
            SwapInputs.ScheduleOf(spec.GetProperty(p + "Schedule"), context),
            CrossCurrencyInputs.IndexOf(spec.GetProperty(p + "Index")),
            spec.GetProperty(p + "Spread").GetDouble(),
            spec.GetProperty(p + "Gearing").GetDouble());
        var (payNominal, payCurrency, paySchedule, payIndex, paySpread, payGearing) = Leg("pay");
        var (recNominal, recCurrency, recSchedule, recIndex, recSpread, recGearing) = Leg("rec");
        return new ConstNotionalCrossCurrencyBasisSwap(
            payNominal,
            payCurrency,
            paySchedule,
            payIndex,
            paySpread,
            payGearing,
            recNominal,
            recCurrency,
            recSchedule,
            recIndex,
            recSpread,
            recGearing,
            context,
            spec.GetProperty("payPaymentLag").GetInt32(),
            spec.GetProperty("recPaymentLag").GetInt32(),
            spec.GetProperty("payCompoundSpread").GetBoolean(),
            CouponInputs.NullableInt(spec.GetProperty("payLookbackDays")),
            spec.GetProperty("payObservationShift").GetBoolean(),
            spec.GetProperty("payLockoutDays").GetInt32(),
            (RateAveragingType)spec.GetProperty("payAveragingMethod").GetInt32(),
            spec.GetProperty("recCompoundSpread").GetBoolean(),
            CouponInputs.NullableInt(spec.GetProperty("recLookbackDays")),
            spec.GetProperty("recObservationShift").GetBoolean(),
            spec.GetProperty("recLockoutDays").GetInt32(),
            (RateAveragingType)spec.GetProperty("recAveragingMethod").GetInt32(),
            false,
            CouponInputs.NullableBool(spec.GetProperty("useIndexedCoupons")),
            spec.GetProperty("paymentLagOnNotionalExchanges").GetBoolean(),
            CrossCurrencyInputs.StubOf(spec.GetProperty("payStubIndexSelection")),
            CrossCurrencyInputs.StubOf(spec.GetProperty("recStubIndexSelection")));
    }
}
