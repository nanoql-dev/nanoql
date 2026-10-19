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
/// contexts and settlement settings; their BPS on an interest rate, at sampled rates and settlement settings; and the
/// functions ported with the bonds: the previous and next cash flows' dates and amounts and the accrual period and
/// days at sampled settlement dates, and at one settlement setting the NPV, yield, durations, convexity and
/// basis-point value at an interest rate (and through the forms taking its value and conventions), the NPV with a
/// Z-spread and the Z-spread that reproduces it, and the at-the-money rate; the accrual and accrued functions on legs
/// whose first coupon's payment date pays another flow first; and the solvers' edges, the Z-spread of an empty leg and
/// the yield and Z-spread from a zero guess or with one iteration.
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
            ImmutableArray<CashFlow> leg = LegOf(c.Input("leg"), context);
            if (c.Inputs.TryGetProperty("kind", out JsonElement kind))
            {
                if (kind.GetString() == "firstCoupon")
                {
                    CheckFirstCoupon(check, c, leg, context);
                }
                else
                {
                    CheckSolverEdges(check, c, leg, context);
                }

                continue;
            }

            if (c.Inputs.TryGetProperty("includes", out _))
            {
                CheckLaterFunctions(check, c, leg, context);
                continue;
            }

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

    // The functions ported with the bonds: at each of the case's settlement dates, and at its one settlement setting.
    private static void CheckLaterFunctions(
        VectorAssert check, VectorCase c, ImmutableArray<CashFlow> leg, EvaluationContext context)
    {
        Date?[] dates = c.Input("dates").EnumerateArray().Select(CashFlowInputs.NullableDate).ToArray();
        bool[] includes = c.Input("includes").EnumerateArray().Select(e => e.GetBoolean()).ToArray();
        Accrual[] at = dates.Zip(includes, (d, i) => new Accrual(i, d)).ToArray();
        check.Output(
            c, "previousCashFlowDate", Each(at, a => CashFlows.PreviousCashFlowDate(leg, context, a.IncludeFlows, a.SettlementDate)));
        check.Output(
            c, "nextCashFlowDate", Each(at, a => CashFlows.NextCashFlowDate(leg, context, a.IncludeFlows, a.SettlementDate)));
        check.Output(
            c,
            "previousCashFlowAmount",
            Each(at, a => CashFlows.PreviousCashFlowAmount(leg, context, a.IncludeFlows, a.SettlementDate)));
        check.Output(
            c,
            "nextCashFlowAmount",
            Each(at, a => CashFlows.NextCashFlowAmount(leg, context, a.IncludeFlows, a.SettlementDate)));
        check.Output(
            c, "accrualPeriod", Each(at, a => CashFlows.AccrualPeriod(leg, context, a.IncludeFlows, a.SettlementDate)));
        check.Output(
            c, "accrualDays", Each(at, a => CashFlows.AccrualDays(leg, context, a.IncludeFlows, a.SettlementDate)));

        JsonElement s = c.Input("settlement");
        bool include = s.GetProperty("includeSettlementDateFlows").GetBoolean();
        Date? settlement = CashFlowInputs.NullableDate(s.GetProperty("settlementDate"));
        Date? npvDate = CashFlowInputs.NullableDate(s.GetProperty("npvDate"));
        JsonElement r = c.Input("rate");
        double rate = IndexInputs.Real(r.GetProperty("rate"));
        DayCounter dc = CashFlowInputs.DayCounterOf(r.GetProperty("dayCounter"));
        var comp = (Compounding)r.GetProperty("compounding").GetInt32();
        var freq = (Frequency)r.GetProperty("frequency").GetInt32();
        var y = new InterestRate(rate, dc, comp, freq);
        JsonElement z = c.Input("zSpread");
        double spread = IndexInputs.Real(z.GetProperty("spread"));
        var zComp = (Compounding)z.GetProperty("compounding").GetInt32();
        var zFreq = (Frequency)z.GetProperty("frequency").GetInt32();
        FlatForward curve = CashFlowInputs.CurveOf(c.Input("curve"));

        check.Output(c, "npvYield", () => CashFlows.Npv(leg, y, context, include, settlement, npvDate));
        check.Output(
            c,
            "yield",
            () => CashFlows.Yield(
                leg, c.Double("yieldNpv"), dc, comp, freq, context, include, settlement, npvDate, 1.0e-10, 100, 0.05));
        check.Output(c, "durationSimple", () => CashFlows.Duration(leg, y, DurationType.Simple, context, include, settlement));
        check.Output(
            c, "durationModified", () => CashFlows.Duration(leg, y, DurationType.Modified, context, include, settlement));
        check.Output(
            c, "durationMacaulay", () => CashFlows.Duration(leg, y, DurationType.Macaulay, context, include, settlement));
        check.Output(c, "convexity", () => CashFlows.Convexity(leg, y, context, include, settlement, npvDate));
        check.Output(c, "basisPointValue", () => CashFlows.BasisPointValue(leg, y, context, include, settlement, npvDate));
        check.Output(c, "npvRate", () => CashFlows.Npv(leg, rate, dc, comp, freq, context, include, settlement, npvDate));
        check.Output(c, "bpsRate", () => CashFlows.Bps(leg, rate, dc, comp, freq, context, include, settlement, npvDate));
        check.Output(
            c,
            "durationRate",
            () => CashFlows.Duration(leg, rate, dc, comp, freq, DurationType.Modified, context, include, settlement, npvDate));
        check.Output(
            c, "convexityRate", () => CashFlows.Convexity(leg, rate, dc, comp, freq, context, include, settlement, npvDate));
        check.Output(
            c,
            "basisPointValueRate",
            () => CashFlows.BasisPointValue(leg, rate, dc, comp, freq, context, include, settlement, npvDate));
        check.Output(
            c, "zSpreadNpv", () => CashFlows.Npv(leg, curve, spread, zComp, zFreq, context, include, settlement, npvDate));
        check.Output(
            c,
            "zSpread",
            () => CashFlows.ZSpread(
                leg, c.Double("zSpreadNpv"), curve, zComp, zFreq, context, include, settlement, npvDate, 1.0e-10, 100, 0.0));
        check.Output(c, "atmRate", () => CashFlows.AtmRate(leg, curve, context, include, settlement, npvDate));
        check.Output(
            c,
            "atmRateNpv",
            () => CashFlows.AtmRate(leg, curve, context, include, settlement, npvDate, c.Double("atmNpv")));
    }

    // The leg a spec describes: its leading flows, where it has them, before CashFlowInputs.LegOf's coupons and flows.
    private static ImmutableArray<CashFlow> LegOf(JsonElement spec, EvaluationContext context)
    {
        ImmutableArray<CashFlow> rest = CashFlowInputs.LegOf(spec, context);
        if (!spec.TryGetProperty("leadingFlows", out JsonElement leading))
        {
            return rest;
        }

        ImmutableArray<CashFlow>.Builder flows = ImmutableArray.CreateBuilder<CashFlow>();
        foreach (JsonElement flow in leading.EnumerateArray())
        {
            double amount = flow.GetProperty("amount").GetDouble();
            var date = new Date(flow.GetProperty("date").GetInt32());
            flows.Add(flow.GetProperty("type").GetString() switch
            {
                "Redemption" => new Redemption(amount, date),
                "AmortizingPayment" => new AmortizingPayment(amount, date),
                var t => throw new InvalidOperationException($"unknown cash flow type '{t}'"),
            });
        }

        flows.AddRange(rest);
        return flows.ToImmutable();
    }

    // The accrual and accrued functions at the case's settlement settings, on a leg whose first coupon's payment date
    // pays another flow first.
    private static void CheckFirstCoupon(
        VectorAssert check, VectorCase c, ImmutableArray<CashFlow> leg, EvaluationContext context)
    {
        Accrual[] accruals = c.Input("accruals").EnumerateArray()
            .Select(s => new Accrual(
                s.GetProperty("includeSettlementDateFlows").GetBoolean(),
                CashFlowInputs.NullableDate(s.GetProperty("settlementDate"))))
            .ToArray();
        check.Output(
            c,
            "accrualPeriodFirstCoupon",
            Each(accruals, a => CashFlows.AccrualPeriod(leg, context, a.IncludeFlows, a.SettlementDate)));
        check.Output(
            c,
            "accrualDaysFirstCoupon",
            Each(accruals, a => CashFlows.AccrualDays(leg, context, a.IncludeFlows, a.SettlementDate)));
        check.Output(
            c,
            "accruedPeriodFirstCoupon",
            Each(accruals, a => CashFlows.AccruedPeriod(leg, context, a.IncludeFlows, a.SettlementDate)));
        check.Output(
            c,
            "accruedDaysFirstCoupon",
            Each(accruals, a => CashFlows.AccruedDays(leg, context, a.IncludeFlows, a.SettlementDate)));
        check.Output(
            c,
            "accruedAmountFirstCoupon",
            Each(accruals, a => CashFlows.AccruedAmount(leg, context, a.IncludeFlows, a.SettlementDate)));
    }

    // The Z-spread and the yield at the case's calls, each with its own target, guess and maximum iterations.
    private static void CheckSolverEdges(
        VectorAssert check, VectorCase c, ImmutableArray<CashFlow> leg, EvaluationContext context)
    {
        JsonElement s = c.Input("settlement");
        bool include = s.GetProperty("includeSettlementDateFlows").GetBoolean();
        Date? settlement = CashFlowInputs.NullableDate(s.GetProperty("settlementDate"));
        Date? npvDate = CashFlowInputs.NullableDate(s.GetProperty("npvDate"));
        FlatForward curve = CashFlowInputs.CurveOf(c.Input("curve"));
        JsonElement[] zCalls = [.. c.Input("zSpreadCalls").EnumerateArray()];
        check.Output(
            c,
            "zSpreadEdges",
            Each(zCalls, z => CashFlows.ZSpread(
                leg,
                z.GetProperty("target").GetDouble(),
                curve,
                Compounding.Continuous,
                Frequency.Annual,
                context,
                include,
                settlement,
                npvDate,
                1.0e-10,
                z.GetProperty("maxIterations").GetInt32(),
                z.GetProperty("guess").GetDouble())));
        JsonElement[] yCalls = [.. c.Input("yieldCalls").EnumerateArray()];
        check.Output(
            c,
            "yieldEdges",
            Each(yCalls, y =>
            {
                JsonElement r = y.GetProperty("rate");
                return CashFlows.Yield(
                    leg,
                    y.GetProperty("target").GetDouble(),
                    CashFlowInputs.DayCounterOf(r.GetProperty("dayCounter")),
                    (Compounding)r.GetProperty("compounding").GetInt32(),
                    (Frequency)r.GetProperty("frequency").GetInt32(),
                    context,
                    include,
                    settlement,
                    npvDate,
                    1.0e-10,
                    y.GetProperty("maxIterations").GetInt32(),
                    y.GetProperty("guess").GetDouble());
            }));
    }

    private static Func<object?>[] Each<TIn, TOut>(TIn[] settlements, Func<TIn, TOut> f) =>
        settlements.Select(s => (Func<object?>)(() => f(s))).ToArray();

    private sealed record Settlement(bool IncludeFlows, Date? SettlementDate, Date? NpvDate);

    private sealed record Accrual(bool IncludeFlows, Date? SettlementDate);
}
