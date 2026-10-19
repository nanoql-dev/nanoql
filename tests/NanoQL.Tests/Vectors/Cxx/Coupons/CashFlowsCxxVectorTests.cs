// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's cash-flow vectors, <c>tests/vectors/cxx/cashflows/cashflows.json</c>: the members of
/// <see cref="CashFlows"/> the wheel doesn't reach, on the wheel generator's legs and four legs of coupons given one by
/// one: whether the leg has expired, the positions of the previous and next cash flows, the coupon rates (QuantLib's
/// refusals to aggregate different coupons on one date, and of a date with no coupon, among them), the nominal and the
/// accrual and reference dates at three settlement settings; the yield value of a basis point in both forms and the
/// yield on a <see cref="Brent"/> solver at one; <see cref="CashFlowFunctions.EarlierThan"/> on pairs of flows; the
/// derivatives of <see cref="InterestRate"/>'s discount factor under each compounding rule; and the nominal, the
/// accrual and reference dates and the next coupon rate on legs whose first coupon's payment date pays another flow
/// first.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CashFlowsCxxVectorTests
{
    [Fact]
    public void CashFlowsCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/cashflows");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("times", out _))
            {
                InterestRate rate = RateOf(c.Input("rate"));
                double[] times = c.Doubles("times");
                check.Output(
                    c, "discountFactorFirstDerivative", Each(times, t => rate.DiscountFactorFirstDerivative(t)));
                check.Output(
                    c, "discountFactorSecondDerivative", Each(times, t => rate.DiscountFactorSecondDerivative(t)));
                continue;
            }

            EvaluationContext context = c.Context();
            ImmutableArray<CashFlow> leg = LegOf(c.Input("leg"), context);
            (bool? Include, Date? Date)[] at = c.Input("settlements").EnumerateArray()
                .Select(s => (
                    s.GetProperty("includeSettlementDateFlows").ValueKind == JsonValueKind.Null
                        ? (bool?)null
                        : s.GetProperty("includeSettlementDateFlows").GetBoolean(),
                    CashFlowInputs.NullableDate(s.GetProperty("settlementDate"))))
                .ToArray();
            if (c.Inputs.TryGetProperty("kind", out _))
            {
                // a leg whose first coupon's payment date pays another flow first
                check.Output(c, "nominal", Each(at, s => CashFlows.Nominal(leg, context, s.Include, s.Date)));
                check.Output(
                    c, "accrualStartDate", Each(at, s => CashFlows.AccrualStartDate(leg, context, s.Include, s.Date)));
                check.Output(
                    c, "accrualEndDate", Each(at, s => CashFlows.AccrualEndDate(leg, context, s.Include, s.Date)));
                check.Output(
                    c,
                    "referencePeriodStart",
                    Each(at, s => CashFlows.ReferencePeriodStart(leg, context, s.Include, s.Date)));
                check.Output(
                    c,
                    "referencePeriodEnd",
                    Each(at, s => CashFlows.ReferencePeriodEnd(leg, context, s.Include, s.Date)));
                check.Output(
                    c, "nextCouponRate", Each(at, s => CashFlows.NextCouponRate(leg, context, s.Include, s.Date)));
                continue;
            }

            check.Output(c, "isExpired", Each(at, s => CashFlows.IsExpired(leg, context, s.Include, s.Date)));
            check.Output(c, "previousCashFlow", Each(at, s => CashFlows.PreviousCashFlow(leg, context, s.Include, s.Date)));
            check.Output(c, "nextCashFlow", Each(at, s => CashFlows.NextCashFlow(leg, context, s.Include, s.Date)));
            check.Output(
                c, "previousCouponRate", Each(at, s => CashFlows.PreviousCouponRate(leg, context, s.Include, s.Date)));
            check.Output(c, "nextCouponRate", Each(at, s => CashFlows.NextCouponRate(leg, context, s.Include, s.Date)));
            check.Output(c, "nominal", Each(at, s => CashFlows.Nominal(leg, context, s.Include, s.Date)));
            check.Output(
                c, "accrualStartDate", Each(at, s => CashFlows.AccrualStartDate(leg, context, s.Include, s.Date)));
            check.Output(c, "accrualEndDate", Each(at, s => CashFlows.AccrualEndDate(leg, context, s.Include, s.Date)));
            check.Output(
                c, "referencePeriodStart", Each(at, s => CashFlows.ReferencePeriodStart(leg, context, s.Include, s.Date)));
            check.Output(
                c, "referencePeriodEnd", Each(at, s => CashFlows.ReferencePeriodEnd(leg, context, s.Include, s.Date)));

            JsonElement setting = c.Input("settlement");
            bool? include = setting.GetProperty("includeSettlementDateFlows").ValueKind == JsonValueKind.Null
                ? null
                : setting.GetProperty("includeSettlementDateFlows").GetBoolean();
            Date? settlement = CashFlowInputs.NullableDate(setting.GetProperty("settlementDate"));
            Date? npvDate = CashFlowInputs.NullableDate(setting.GetProperty("npvDate"));
            JsonElement r = c.Input("rate");
            InterestRate y = RateOf(r);
            check.Output(
                c,
                "yieldValueBasisPoint",
                () => CashFlows.YieldValueBasisPoint(leg, y, context, include, settlement, npvDate));
            check.Output(
                c,
                "yieldValueBasisPointRate",
                () => CashFlows.YieldValueBasisPoint(
                    leg, y.Rate, y.DayCounter, y.Compounding, y.Frequency, context, include, settlement, npvDate));
            check.Output(
                c,
                "yieldBrent",
                () => CashFlows.Yield(
                    new Brent().SetMaxEvaluations(100),
                    leg,
                    c.Double("yieldNpv"),
                    CashFlowInputs.DayCounterOf(r.GetProperty("dayCounter")),
                    (Compounding)r.GetProperty("compounding").GetInt32(),
                    (Frequency)r.GetProperty("frequency").GetInt32(),
                    context,
                    include,
                    settlement,
                    npvDate,
                    1.0e-10,
                    0.05));
            check.Output(
                c,
                "earlierThan",
                c.Input("pairs").EnumerateArray()
                    .Select(p => (object?)(CashFlowFunctions.EarlierThan(leg[p[0].GetInt32()], leg[p[1].GetInt32()]) < 0))
                    .ToArray());
        }

        check.Complete();
    }

    // The leg a spec describes, in the harness's order: its leading flows, the coupons of its fixed-rate leg, its
    // coupons given one by one, then its other flows.
    private static ImmutableArray<CashFlow> LegOf(JsonElement spec, EvaluationContext context)
    {
        ImmutableArray<CashFlow>.Builder flows = ImmutableArray.CreateBuilder<CashFlow>();
        if (spec.TryGetProperty("leadingFlows", out JsonElement leading))
        {
            foreach (JsonElement f in leading.EnumerateArray())
            {
                double amount = f.GetProperty("amount").GetDouble();
                var date = new Date(f.GetProperty("date").GetInt32());
                flows.Add(f.GetProperty("type").GetString() switch
                {
                    "Redemption" => new Redemption(amount, date),
                    "AmortizingPayment" => new AmortizingPayment(amount, date),
                    var t => throw new InvalidOperationException($"unknown cash flow type '{t}'"),
                });
            }
        }

        flows.AddRange(CashFlowInputs.LegOf(spec, context).Where(f => f is Coupon));
        IEnumerable<JsonElement> coupons =
            spec.TryGetProperty("coupons", out JsonElement given) ? given.EnumerateArray() : [];
        foreach (JsonElement c in coupons)
        {
            flows.Add(new FixedRateCoupon(
                new Date(c.GetProperty("paymentDate").GetInt32()),
                c.GetProperty("nominal").GetDouble(),
                c.GetProperty("rate").GetDouble(),
                CashFlowInputs.DayCounterOf(c.GetProperty("dayCounter")),
                new Date(c.GetProperty("accrualStartDate").GetInt32()),
                new Date(c.GetProperty("accrualEndDate").GetInt32()),
                CashFlowInputs.NullableDate(c.GetProperty("refPeriodStart")),
                CashFlowInputs.NullableDate(c.GetProperty("refPeriodEnd")),
                CashFlowInputs.NullableDate(c.GetProperty("exCouponDate"))));
        }

        flows.AddRange(CashFlowInputs.LegOf(spec, context).Where(f => f is not Coupon));
        return flows.ToImmutable();
    }

    private static InterestRate RateOf(JsonElement r) =>
        new(
            IndexInputs.Real(r.GetProperty("rate")),
            CashFlowInputs.DayCounterOf(r.GetProperty("dayCounter")),
            (Compounding)r.GetProperty("compounding").GetInt32(),
            (Frequency)r.GetProperty("frequency").GetInt32());

    private static Func<object?>[] Each<TIn, TOut>(TIn[] inputs, Func<TIn, TOut> f) =>
        inputs.Select(i => (Func<object?>)(() => f(i))).ToArray();
}
