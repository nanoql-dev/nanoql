// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FxForward vectors, <c>tests/vectors/instruments/fxforward.json</c>: forwards built from two nominals and from a
/// rate, with their inspectors, and their settlement dates and expiry under several evaluation dates; and the
/// constructors' refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FxForwardVectorTests
{
    [Fact]
    public void FxForwardVector()
    {
        var file = VectorFile.Load("instruments/fxforward");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("forward");
            FxForward Forward() => ForwardOf(spec);
            check.Output(c, "sourceNominal", () => Forward().SourceNominal);
            check.Output(c, "targetNominal", () => Forward().TargetNominal);
            check.Output(c, "sourceCurrency", () => Forward().SourceCurrency.Code);
            check.Output(c, "targetCurrency", () => Forward().TargetCurrency.Code);
            check.Output(c, "maturityDate", () => Forward().MaturityDate);
            check.Output(c, "paySourceCurrency", () => Forward().PaySourceCurrency);
            check.Output(c, "forwardRate", () => Forward().ForwardRate);
            check.Output(c, "settlementDays", () => Forward().SettlementDays);
            check.Output(c, "calendar", () => Forward().SettlementCalendar.Name);
            EvaluationContext context = c.Context();
            EvaluationContext[] contexts = [.. c.DateInputs("evaluationDates").Select(d => context with { EvaluationDate = d })];
            check.Output(c, "settlementDates", contexts.Select(x => (Func<object?>)(() => Forward().SettlementDate(x))));
            check.Output(c, "isExpired", contexts.Select(x => (Func<object?>)(() => Forward().IsExpired(x))));
        }

        check.Complete();
    }

    /// <summary>
    /// A forward as the vectors write it: <c>{kind, sourceNominal, sourceCurrency, targetNominal or forwardRate,
    /// targetCurrency, maturityDate, paySourceCurrency, settlementDays, calendar}</c>, a null currency QuantLib's empty
    /// one and a null calendar none.
    /// </summary>
    /// <param name="spec">The JSON value.</param>
    /// <returns>The forward.</returns>
    internal static FxForward ForwardOf(JsonElement spec)
    {
        Currency source = CurrencyOf(spec.GetProperty("sourceCurrency"))!;
        Currency target = CurrencyOf(spec.GetProperty("targetCurrency"))!;
        var maturity = new Date(spec.GetProperty("maturityDate").GetInt32());
        bool pay = spec.GetProperty("paySourceCurrency").GetBoolean();
        int days = spec.GetProperty("settlementDays").GetInt32();
        Calendar? calendar = spec.GetProperty("calendar").GetString() switch
        {
            null => null,
            "TARGET" => new TARGET(),
            "UnitedStates" => new UnitedStates(UnitedStatesMarket.Settlement),
            string other => throw new ArgumentException($"unknown calendar {other}", nameof(spec)),
        };
        double sourceNominal = spec.GetProperty("sourceNominal").GetDouble();
        return spec.GetProperty("kind").GetString() == "nominals"
            ? new FxForward(sourceNominal, source, spec.GetProperty("targetNominal").GetDouble(), target, maturity, pay, days, calendar)
            : new FxForward(sourceNominal, source, target, spec.GetProperty("forwardRate").GetDouble(), maturity, pay, days, calendar);
    }

    private static Currency? CurrencyOf(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null ? null : VectorCurrencies.ByCode(e.GetString()!);
}
