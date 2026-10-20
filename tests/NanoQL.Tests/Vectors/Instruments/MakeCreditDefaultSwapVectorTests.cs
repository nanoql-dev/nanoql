// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CDS builder vectors, <c>tests/vectors/instruments/makecds.json</c>: the swaps
/// <see cref="MakeCreditDefaultSwap"/> builds from a tenor or a term date with the setters the wheel binds, their
/// inspectors, and its refusal where the 2015 rule gives a zero tenor no maturity.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MakeCreditDefaultSwapVectorTests
{
    [Fact]
    public void MakeCreditDefaultSwapVector()
    {
        var file = VectorFile.Load("instruments/makecds");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            MakeCreditDefaultSwap builder = BuilderOf(c.Input("builder"), context);
            if (c.Expected.TryGetProperty("construction", out _))
            {
                check.Output(c, "construction", () => builder.ToCreditDefaultSwap(context) is null ? 1 : 0);
                continue;
            }

            CreditInputs.CheckInspectors(check, c, builder.ToCreditDefaultSwap(context), context);
        }

        check.Complete();
    }

    /// <summary>The builder a spec describes: its constructor, then each setter in order.</summary>
    /// <param name="spec">The spec: a tenor, a term date or a schedule; the running spread; the setters.</param>
    /// <param name="context">The case's evaluation context, which a schedule reads.</param>
    /// <returns>The builder.</returns>
    internal static MakeCreditDefaultSwap BuilderOf(JsonElement spec, EvaluationContext context)
    {
        double spread = spec.GetProperty("runningSpread").GetDouble();
        MakeCreditDefaultSwap builder = spec.TryGetProperty("tenor", out JsonElement tenor)
            ? new MakeCreditDefaultSwap(PeriodOf(tenor), spread)
            : spec.TryGetProperty("schedule", out JsonElement schedule)
                ? new MakeCreditDefaultSwap(CreditInputs.ScheduleOf(schedule, context), spread)
                : new MakeCreditDefaultSwap(new Date(spec.GetProperty("termDate").GetInt32()), spread);
        foreach (JsonElement setter in spec.GetProperty("setters").EnumerateArray())
        {
            JsonElement value = setter[1];
            builder = setter[0].GetString() switch
            {
                "upfrontRate" => builder.WithUpfrontRate(value.GetDouble()),
                "side" => builder.WithSide((ProtectionSide)value.GetInt32()),
                "nominal" => builder.WithNominal(value.GetDouble()),
                "couponTenor" => builder.WithCouponTenor(PeriodOf(value)),
                "dayCounter" => builder.WithDayCounter(CreditInputs.DayCounterOf(value)),
                "lastPeriodDayCounter" => builder.WithLastPeriodDayCounter(CreditInputs.DayCounterOf(value)),
                "dateGenerationRule" => builder.WithDateGenerationRule((DateGenerationRule)value.GetInt32()),
                "cashSettlementDays" => builder.WithCashSettlementDays(value.GetInt32()),
                "tradeDate" => builder.WithTradeDate(new Date(value.GetInt32())),
                "convention" => builder.WithConvention((BusinessDayConvention)value.GetInt32()),
                "settleAccrual" => builder.SettleAccrual(value.GetBoolean()),
                "payAtDefaultTime" => builder.PayAtDefaultTime(value.GetBoolean()),
                "protectionStart" => builder.WithProtectionStart(new Date(value.GetInt32())),
                "upfrontDate" => builder.WithUpfrontDate(new Date(value.GetInt32())),
                "rebateAccrual" => builder.RebateAccrual(value.GetBoolean()),
                "claim" => value.GetString() == "FaceValueClaim"
                    ? builder.WithClaim(new FaceValueClaim())
                    : throw new InvalidOperationException($"unknown claim '{value}'"),
                _ => throw new InvalidOperationException($"unknown setter '{setter[0]}'"),
            };
        }

        return builder;
    }

    private static Period PeriodOf(JsonElement e) => new(e[0].GetInt32(), (TimeUnit)e[1].GetInt32());
}
