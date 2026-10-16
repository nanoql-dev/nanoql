// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The cash-flow analysis vectors, <c>tests/vectors/cashflows/cashflows.json</c>: the start and maturity dates of
/// fixed-rate legs, some followed by redemptions and amortizing payments, and their NPV, BPS and the pair from
/// <see cref="CashFlows.NpvBps"/> on a flat curve, under sampled evaluation contexts and settlement settings.
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
            FlatForward curve = CashFlowInputs.CurveOf(c.Input("curve"));
            Settlement[] settlements = c.Input("settlements").EnumerateArray()
                .Select(s => new Settlement(
                    s.GetProperty("includeSettlementDateFlows").GetBoolean(),
                    CashFlowInputs.NullableDate(s.GetProperty("settlementDate")),
                    CashFlowInputs.NullableDate(s.GetProperty("npvDate"))))
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
        }

        check.Complete();
    }

    private static Func<object?>[] Each<TOut>(Settlement[] settlements, Func<Settlement, TOut> f) =>
        settlements.Select(s => (Func<object?>)(() => f(s))).ToArray();

    private sealed record Settlement(bool IncludeFlows, Date? SettlementDate, Date? NpvDate);
}
