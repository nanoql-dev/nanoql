// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The mark-to-market cross-currency basis swap vectors,
/// <c>tests/vectors/experimental/fx/mtmcrosscurrencybasisswap.json</c>: EUR against USD, paying or receiving the base
/// currency, either leg resettable, with FX reset lags and calendars, payment lags and conventions, overnight legs,
/// a stub index, a seasoned and an expired swap, and the refusals; each swap's inspectors, legs, and
/// <see cref="DiscountingMtMCrossCurrencyBasisSwapEngine"/>'s results with the fair spreads and the resets.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MtMCrossCurrencyBasisSwapVectorTests
{
    [Fact]
    public void MtMCrossCurrencyBasisSwapVector()
    {
        var file = VectorFile.Load("experimental/fx/mtmcrosscurrencybasisswap");
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

            MtMCrossCurrencyBasisSwap swap = SwapOf(spec, context);
            CheckInspectors(check, c, swap);
            for (int j = 0; j < 2; ++j)
            {
                CheckLeg(check, c, $"leg{j + 1}", swap.Legs[j], context);
            }

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

            CheckResults(check, c, swap, Price());
        }

        check.Complete();
    }

    /// <summary>Checks an MtM swap's results, as the MtM vectors write them.</summary>
    /// <param name="check">The checker.</param>
    /// <param name="c">The case.</param>
    /// <param name="swap">The swap.</param>
    /// <param name="results">The results.</param>
    internal static void CheckResults(
        VectorAssert check, VectorCase c, MtMCrossCurrencyBasisSwap swap, MtMCrossCurrencyBasisSwapResults results)
    {
        CrossCurrencyInputs.CheckResults(check, c, results, inCcyLegBps: false);

        // a leg compounding its spread has its fair spreads left out (the S-rx plan's C3 review, B1-2)
        foreach ((string output, double? fair) in new[]
                 {
                     ("fairFxBaseSpread", results.FairFxBaseSpread), ("fairFxQuoteSpread", results.FairFxQuoteSpread),
                     ("fairPaySpread", swap.FairPaySpread(results)), ("fairRecSpread", swap.FairRecSpread(results)),
                 })
        {
            if (c.Expected.TryGetProperty(output, out _))
            {
                check.Output(c, output, fair);
            }
        }

        check.Output(c, "fxResetRates", results.FxResetRates.ToArray());
        check.Output(c, "fxResetNotionals", results.FxResetNotionals.ToArray());
    }

    /// <summary>The MtM swap a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The swap.</returns>
    internal static MtMCrossCurrencyBasisSwap SwapOf(JsonElement spec, EvaluationContext context)
    {
        (double, Currency?, Schedule, IborIndex, double, double) Leg(string p) => (
            spec.GetProperty(p + "Nominal").GetDouble(),
            CrossCurrencyInputs.CurrencyOf(spec.GetProperty(p + "Currency")),
            SwapInputs.ScheduleOf(spec.GetProperty(p + "Schedule"), context),
            CrossCurrencyInputs.IndexOf(spec.GetProperty(p + "Index")),
            spec.GetProperty(p + "Spread").GetDouble(),
            spec.GetProperty(p + "Gearing").GetDouble());
        var (baseNominal, baseCurrency, baseSchedule, baseIndex, baseSpread, baseGearing) = Leg("fxBase");
        var (quoteNominal, quoteCurrency, quoteSchedule, quoteIndex, quoteSpread, quoteGearing) = Leg("fxQuote");
        JsonElement calendar = spec.GetProperty("fxResetFixingCalendar");
        return new MtMCrossCurrencyBasisSwap(
            (MtMCrossCurrencyBasisSwapType)spec.GetProperty("type").GetInt32(),
            baseNominal,
            baseCurrency,
            baseSchedule,
            baseIndex,
            baseSpread,
            baseGearing,
            quoteNominal,
            quoteCurrency,
            quoteSchedule,
            quoteIndex,
            quoteSpread,
            quoteGearing,
            spec.GetProperty("isFxBaseCurrencyLegResettable").GetBoolean(),
            context,
            spec.GetProperty("fxResetFixingDays").GetInt32(),
            calendar.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CalendarOf(calendar),
            spec.GetProperty("fxBasePaymentLag").GetInt32(),
            spec.GetProperty("fxQuotePaymentLag").GetInt32(),
            (BusinessDayConvention)spec.GetProperty("fxBasePaymentConvention").GetInt32(),
            (BusinessDayConvention)spec.GetProperty("fxQuotePaymentConvention").GetInt32(),
            spec.GetProperty("fxBaseCompoundSpread").GetBoolean(),
            CouponInputs.NullableInt(spec.GetProperty("fxBaseLookbackDays")),
            spec.GetProperty("fxBaseObservationShift").GetBoolean(),
            spec.GetProperty("fxBaseLockoutDays").GetInt32(),
            (RateAveragingType)spec.GetProperty("fxBaseAveragingMethod").GetInt32(),
            spec.GetProperty("fxQuoteCompoundSpread").GetBoolean(),
            CouponInputs.NullableInt(spec.GetProperty("fxQuoteLookbackDays")),
            spec.GetProperty("fxQuoteObservationShift").GetBoolean(),
            spec.GetProperty("fxQuoteLockoutDays").GetInt32(),
            (RateAveragingType)spec.GetProperty("fxQuoteAveragingMethod").GetInt32(),
            false,
            CouponInputs.NullableBool(spec.GetProperty("useIndexedCoupons")),
            CrossCurrencyInputs.StubOf(spec.GetProperty("fxBaseStubIndexSelection")),
            CrossCurrencyInputs.StubOf(spec.GetProperty("fxQuoteStubIndexSelection")));
    }

    private static void CheckInspectors(VectorAssert check, VectorCase c, MtMCrossCurrencyBasisSwap swap)
    {
        check.Output(c, "type", (int)swap.Type);
        check.Output(c, "paysFxBaseCurrency", swap.PaysFxBaseCurrency);
        check.Output(c, "fxBaseNominal", swap.FxBaseNominal);
        check.Output(c, "fxBaseCurrency", swap.FxBaseCurrency?.Code);
        check.Output(c, "fxBaseSpread", swap.FxBaseSpread);
        check.Output(c, "fxBaseGearing", swap.FxBaseGearing);
        check.Output(c, "fxBaseIndexName", swap.FxBaseIndex.Name);
        check.Output(c, "fxQuoteNominal", swap.FxQuoteNominal);
        check.Output(c, "fxQuoteCurrency", swap.FxQuoteCurrency?.Code);
        check.Output(c, "fxQuoteSpread", swap.FxQuoteSpread);
        check.Output(c, "fxQuoteGearing", swap.FxQuoteGearing);
        check.Output(c, "fxQuoteIndexName", swap.FxQuoteIndex.Name);
        check.Output(c, "payNominal", swap.PayNominal);
        check.Output(c, "payCurrency", swap.PayCurrency?.Code);
        check.Output(c, "paySpread", swap.PaySpread);
        check.Output(c, "payGearing", swap.PayGearing);
        check.Output(c, "payIndexName", swap.PayIndex.Name);
        check.Output(c, "paySchedule", swap.PaySchedule.Dates);
        check.Output(c, "recNominal", swap.RecNominal);
        check.Output(c, "recCurrency", swap.RecCurrency?.Code);
        check.Output(c, "recSpread", swap.RecSpread);
        check.Output(c, "recGearing", swap.RecGearing);
        check.Output(c, "recIndexName", swap.RecIndex.Name);
        check.Output(c, "recSchedule", swap.RecSchedule.Dates);
        check.Output(c, "isFxBaseCurrencyLegResettable", swap.IsFxBaseCurrencyLegResettable);
        check.Output(c, "resettingLegIndex", swap.ResettingLegIndex);
        check.Output(c, "constantLegIndex", swap.ConstantLegIndex);
        check.Output(c, "constantLegNotional", swap.ConstantLegNotional);
        check.Output(c, "fxResetFixingDays", swap.FxResetFixingDays);
        check.Output(c, "fxResetFixingCalendar", swap.FxResetFixingCalendar?.Name);
        check.Output(c, "fxBasePaymentConvention", (int)swap.FxBasePaymentConvention);
        check.Output(c, "fxQuotePaymentConvention", (int)swap.FxQuotePaymentConvention);
    }

    // A leg's payment dates and amounts, which of its flows are FX-reset coupons and exchanges, and the coupons'
    // reset dates.
    private static void CheckLeg(
        VectorAssert check, VectorCase c, string prefix, ImmutableArray<CashFlow> leg, EvaluationContext context)
    {
        check.Output(c, prefix + "Dates", leg.Select(cf => cf.Date).ToArray());
        check.Output(c, prefix + "Amounts", leg.Select(cf => (Func<object?>)(() => cf.Amount(context))).ToArray());
        check.Output(c, prefix + "FxResetCoupons", leg.Select(cf => cf is FxResetCoupon).ToArray());
        check.Output(c, prefix + "FxResetExchanges", leg.Select(cf => cf is FxResetNotionalExchange).ToArray());
        check.Output(c, prefix + "FxResetDates", leg.Select(cf => (cf as FxResetCoupon)?.FxResetDate).ToArray());
        check.Output(
            c, prefix + "FxResetValueDates", leg.Select(cf => (cf as FxResetCoupon)?.FxResetValueDate).ToArray());
    }
}
