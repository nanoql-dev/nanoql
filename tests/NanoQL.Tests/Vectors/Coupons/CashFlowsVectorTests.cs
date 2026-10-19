// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The cash-flow analysis vectors, <c>tests/vectors/cashflows/cashflows.json</c>: the start and maturity dates of
/// fixed-rate legs, some followed by redemptions and amortizing payments, their NPV, BPS and the pair from
/// <see cref="CashFlows.NpvBps"/> on a flat curve, and their accrued period, days and amount, under sampled evaluation
/// contexts and settlement settings; and their BPS on an interest rate, at sampled rates and settlement settings.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CashFlowsVectorTests
{
    [Fact]
    public void CashFlowsVector()
    {
        var file = VectorFile.Load("cashflows/cashflows");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            ImmutableArray<CashFlow> leg = CashFlowInputs.LegOf(c.Input("leg"), context);
            Settlement[] settlements = c.Input("settlements").EnumerateArray()
                .Select(s => new Settlement(
                    s.GetProperty("includeSettlementDateFlows").GetBoolean(),
                    CashFlowInputs.NullableDate(s.GetProperty("settlementDate")),
                    CashFlowInputs.NullableDate(s.GetProperty("npvDate"))))
                .ToArray();
            if (c.Inputs.TryGetProperty("rates", out JsonElement rates))
            {
                // bps on an interest rate, [rate][settlement]
                InterestRate[] yields = rates.EnumerateArray()
                    .Select(r => new InterestRate(
                        IndexInputs.Real(r.GetProperty("rate")),
                        CashFlowInputs.DayCounterOf(r.GetProperty("dayCounter")),
                        (Compounding)r.GetProperty("compounding").GetInt32(),
                        (Frequency)r.GetProperty("frequency").GetInt32()))
                    .ToArray();
                check.Output(
                    c,
                    "bpsInterestRate",
                    yields.Select(y => Each(settlements, s =>
                        CashFlows.Bps(leg, y, context, s.IncludeFlows, s.SettlementDate, s.NpvDate))));
                continue;
            }

            FlatForward curve = CashFlowInputs.CurveOf(c.Input("curve"));
            Accrual[] accruals = c.Input("accruals").EnumerateArray()
                .Select(s => new Accrual(
                    s.GetProperty("includeSettlementDateFlows").GetBoolean(),
                    CashFlowInputs.NullableDate(s.GetProperty("settlementDate"))))
                .ToArray();

            check.Output(c, "startDate", () => CashFlows.StartDate(leg));
            check.Output(c, "maturityDate", () => CashFlows.MaturityDate(leg));
            check.Output(
                c,
                "npv",
                Each(settlements, s =>
                    CashFlows.Npv(leg, curve, context, s.IncludeFlows, s.SettlementDate, s.NpvDate)));
            check.Output(
                c,
                "bps",
                Each(settlements, s =>
                    CashFlows.Bps(leg, curve, context, s.IncludeFlows, s.SettlementDate, s.NpvDate)));
            check.Output(
                c,
                "npvbps",
                Each(settlements, s =>
                {
                    (double npv, double bps) = CashFlows.NpvBps(
                        leg, curve, context, s.IncludeFlows, s.SettlementDate, s.NpvDate);
                    return new object[] { npv, bps };
                }));
            check.Output(
                c,
                "accruedPeriod",
                Each(accruals, a => CashFlows.AccruedPeriod(leg, context, a.IncludeFlows, a.SettlementDate)));
            check.Output(
                c,
                "accruedDays",
                Each(accruals, a => CashFlows.AccruedDays(leg, context, a.IncludeFlows, a.SettlementDate)));
            check.Output(
                c,
                "accruedAmount",
                Each(accruals, a => CashFlows.AccruedAmount(leg, context, a.IncludeFlows, a.SettlementDate)));
        }

        check.Complete();
    }

    private static Func<object?>[] Each<TIn, TOut>(TIn[] settlements, Func<TIn, TOut> f) =>
        settlements.Select(s => (Func<object?>)(() => f(s))).ToArray();

    private sealed record Settlement(bool IncludeFlows, Date? SettlementDate, Date? NpvDate);

    private sealed record Accrual(bool IncludeFlows, Date? SettlementDate);
}
