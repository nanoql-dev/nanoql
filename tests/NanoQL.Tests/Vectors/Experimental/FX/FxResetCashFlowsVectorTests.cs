// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FX-reset cash flow vectors, <c>tests/vectors/experimental/fx/fxresetcashflows.json</c>: resets and their
/// conventions, <see cref="DiscountingFxResetPricer"/>'s rates from past, today's and future fixings,
/// <see cref="FxResetCoupon"/>s and <see cref="FxResetNotionalExchange"/>s priced through it, and a leg given the
/// pricer by <see cref="FxResetCashFlowsFunctions.SetFxResetPricer(ImmutableArray{CashFlow}, FxResetPricer?)"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FxResetCashFlowsVectorTests
{
    [Fact]
    public void FxResetCashFlowsVector()
    {
        var file = VectorFile.Load("experimental/fx/fxresetcashflows");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            string kind = c.Input("kind").GetString()!;
            JsonElement spec = c.Input(kind);
            switch (kind)
            {
                case "reset":
                    CheckReset(check, c, spec);
                    break;
                case "convention":
                    CheckConvention(check, c, spec);
                    break;
                case "rates":
                    {
                        DiscountingFxResetPricer pricer = PricerOf(spec.GetProperty("pricer"));
                        check.Output(
                            c,
                            "fxRates",
                            spec.GetProperty("resets").EnumerateArray()
                                .Select(r => (Func<object?>)(() => pricer.FxRate(ResetOf(r), context)))
                                .ToArray());
                        break;
                    }

                case "coupons":
                    CheckCoupons(check, c, spec, context);
                    break;
                case "exchanges":
                    CheckExchanges(check, c, spec, context);
                    break;
                case "leg":
                    {
                        ImmutableArray<CashFlow> leg =
                        [
                            .. spec.GetProperty("coupons").EnumerateArray().Select(x => CouponOf(x, context)),
                            .. spec.GetProperty("exchanges").EnumerateArray().Select(ExchangeOf),
                            .. spec.GetProperty("plainFlows").EnumerateArray().Select(f => new SimpleCashFlow(
                                f.GetProperty("amount").GetDouble(), new Date(f.GetProperty("date").GetInt32()))),
                        ];
                        leg = FxResetCashFlowsFunctions.SetFxResetPricer(leg, PricerOf(spec.GetProperty("pricer")));
                        check.Output(c, "dates", leg.Select(cf => cf.Date).ToArray());
                        check.Output(
                            c, "amounts", leg.Select(cf => (Func<object?>)(() => cf.Amount(context))).ToArray());
                        break;
                    }

                default:
                    throw new InvalidOperationException($"unknown kind '{kind}'");
            }
        }

        check.Complete();
    }

    private static void CheckReset(VectorAssert check, VectorCase c, JsonElement spec)
    {
        if (c.Expected.GetProperty("build").ValueKind == JsonValueKind.Object)
        {
            check.Output(c, "build", () => ResetOf(spec));
            return;
        }

        FxReset reset = ResetOf(spec);
        check.Output(c, "build", true);
        check.Output(c, "fixingDate", reset.FixingDate);
        check.Output(c, "valueDate", reset.ValueDate);
    }

    private static void CheckConvention(VectorAssert check, VectorCase c, JsonElement spec)
    {
        JsonElement calendar = spec.GetProperty("calendar");
        var convention = new FxResetConvention(
            spec.GetProperty("fixingDays").GetInt32(),
            calendar.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CalendarOf(calendar));
        var valueDate = new Date(spec.GetProperty("valueDate").GetInt32());
        var fixingDate = new Date(spec.GetProperty("fixingDate").GetInt32());
        check.Output(c, "reset", () =>
        {
            FxReset reset = convention.Reset(valueDate);
            return new[] { reset.FixingDate, reset.ValueDate };
        });
        check.Output(c, "valueDate", () => convention.ValueDate(fixingDate));
    }

    // Each coupon's outputs as one list per output, an entry null for a coupon that didn't build.
    private static void CheckCoupons(VectorAssert check, VectorCase c, JsonElement spec, EvaluationContext context)
    {
        DiscountingFxResetPricer pricer = PricerOf(spec.GetProperty("pricer"));
        JsonElement[] specs = [.. spec.GetProperty("coupons").EnumerateArray()];
        FxResetCoupon?[] built = [.. specs.Select(x => Try(() => CouponOf(x, context)))];
        FxResetCoupon?[] priced = [.. built.Select(x => x?.SetFxResetPricer(pricer))];
        Date?[] accrualDates = [.. priced.Select(x => x is null ? (Date?)null : x.AccrualStartDate + 20)];
        Func<object?>[] Each(Func<FxResetCoupon, object?> f) =>
            [.. priced.Select(x => (Func<object?>)(() => x is null ? null : f(x)))];

        check.Output(
            c,
            "builds",
            specs.Select(x => (Func<object?>)(() =>
            {
                CouponOf(x, context);
                return true;
            })).ToArray());
        check.Output(
            c,
            "nominalWithoutPricers",
            built.Select(x => (Func<object?>)(() => x is null ? null : x.Nominal(context))).ToArray());
        check.Output(c, "nominals", Each(x => x.Nominal(context)));
        check.Output(c, "rates", Each(x => x.Rate(context)));
        check.Output(c, "amounts", Each(x => x.Amount(context)));
        check.Output(
            c,
            "accruedAmounts",
            priced
                .Select((x, i) => (Func<object?>)(() => x?.AccruedAmount(accrualDates[i]!.Value, context)))
                .ToArray());
        check.Output(c, "accrualDates", accrualDates);
        check.Output(c, "constantLegNotionals", Each(x => x.ConstantLegNotional));
        check.Output(c, "fxResetDates", Each(x => x.FxResetDate));
        check.Output(c, "fxResetValueDates", Each(x => x.FxResetValueDate));
    }

    private static void CheckExchanges(VectorAssert check, VectorCase c, JsonElement spec, EvaluationContext context)
    {
        DiscountingFxResetPricer pricer = PricerOf(spec.GetProperty("pricer"));
        JsonElement[] specs = [.. spec.GetProperty("exchanges").EnumerateArray()];
        FxResetNotionalExchange?[] built = [.. specs.Select(x => Try(() => ExchangeOf(x)))];
        check.Output(
            c,
            "builds",
            specs.Select(x => (Func<object?>)(() =>
            {
                ExchangeOf(x);
                return true;
            })).ToArray());
        check.Output(
            c,
            "amountWithoutPricers",
            built.Select(x => (Func<object?>)(() => x is null ? null : x.Amount(context))).ToArray());
        check.Output(
            c,
            "amounts",
            built.Select(x => (Func<object?>)(() => x?.SetFxResetPricer(pricer).Amount(context))).ToArray());
    }

    private static T? Try<T>(Func<T> build)
        where T : class
    {
        try
        {
            return build();
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static FxReset ResetOf(JsonElement spec) =>
        new(DateOrDefault(spec.GetProperty("fixingDate")), DateOrDefault(spec.GetProperty("valueDate")));

    private static FxReset? NullableResetOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : ResetOf(spec);

    // QuantLib's null Date() is nanoql's default(Date).
    private static Date DateOrDefault(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null ? default : new Date(e.GetInt32());

    private static DiscountingFxResetPricer PricerOf(JsonElement spec) => new(
        CrossCurrencyInputs.CurrencyOf(spec.GetProperty("constantLegCurrency")),
        CrossCurrencyInputs.CurrencyOf(spec.GetProperty("resettableLegCurrency")),
        CashFlowInputs.CurveOf(spec.GetProperty("constantLegCurve")),
        CashFlowInputs.CurveOf(spec.GetProperty("resettableLegCurve")),
        spec.GetProperty("spotFx").GetDouble(),
        spec.GetProperty("spotIsResettablePerConstant").GetBoolean(),
        CashFlowInputs.NullableDate(spec.GetProperty("spotFxSettleDate")));

    private static FxResetCoupon CouponOf(JsonElement spec, EvaluationContext context)
    {
        ImmutableArray<CashFlow> leg = CrossCurrencyInputs.LegOf(spec.GetProperty("leg"), context);
        return new FxResetCoupon(
            (FloatingRateCoupon)leg[spec.GetProperty("index").GetInt32()],
            spec.GetProperty("constantLegNotional").GetDouble(),
            ResetOf(spec.GetProperty("fxReset")));
    }

    private static FxResetNotionalExchange ExchangeOf(JsonElement spec) => new(
        DateOrDefault(spec.GetProperty("paymentDate")),
        spec.GetProperty("constantLegNotional").GetDouble(),
        NullableResetOf(spec.GetProperty("previousReset")),
        NullableResetOf(spec.GetProperty("currentReset")));
}
