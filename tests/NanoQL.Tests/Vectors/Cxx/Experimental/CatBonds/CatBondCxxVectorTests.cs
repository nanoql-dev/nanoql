// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's floating cat bond vectors, <c>tests/vectors/cxx/experimental/catbonds/catbond.json</c>:
/// <see cref="FloatingCatBond"/>'s cash flows and <see cref="MonteCarloCatBondEngine"/>'s results on event sets and
/// long-return-period beta risks, on QuantLib's test's USD LIBOR 6M and Black coupon pricer.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CatBondCxxVectorTests
{
    [Fact]
    public void CatBondCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/catbonds/catbond");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            EvaluationContext context = c.Context();
            var riskFree = new FlatForward(context.EvaluationDate, VectorCase.Real(a[3]), new Actual360());
            FloatingCatBond bond = CouponPricerFunctions.SetCouponPricer(
                new FloatingCatBond(
                    a[0].GetInt32(),
                    VectorCase.Real(a[1]),
                    CreditInputs.ScheduleOf(a[2], context),
                    new USDLibor(new Period(6, TimeUnit.Months), riskFree),
                    CreditInputs.DayCounterOf(a[4]),
                    NotionalRiskCxxVectorTests.RiskOf(a[5]),
                    context,
                    BusinessDayConvention.ModifiedFollowing,
                    a[6].GetInt32(),
                    [],
                    [.. a[7].EnumerateArray().Select(VectorCase.Real)],
                    [],
                    [],
                    false,
                    VectorCase.Real(a[8]),
                    CatRiskCxxVectorTests.DateOf(a[9])),
                new BlackIborCouponPricer(context));
            CatRisk risk = a[10][0].GetString() == "EventSet"
                ? new EventSet(CatRiskCxxVectorTests.EventsOf(a[10][1]), CatRiskCxxVectorTests.DateOf(a[10][2]), CatRiskCxxVectorTests.DateOf(a[10][3]))
                : new BetaRisk(VectorCase.Real(a[10][1]), VectorCase.Real(a[10][2]), VectorCase.Real(a[10][3]), VectorCase.Real(a[10][4]));
            YieldTermStructure discount = CreditInputs.DiscountOf(a[11])!;

            check.Output(c, "cashflowDates", bond.Cashflows.Select(cf => cf.Date).ToArray());
            check.Output(c, "cashflowAmounts", bond.Cashflows.Select(cf => cf.Amount(context)).ToArray());
            CatBondResults Results() => MonteCarloCatBondEngine.Calculate(bond, context, risk, discount);
            check.Output(c, "NPV", () => Results().NPV);
            check.Output(c, "settlementValue", () => Results().SettlementValue);
            check.Output(c, "cleanPrice", () => bond.CleanPrice(Results(), context));
            check.Output(c, "lossProbability", () => Results().LossProbability);
            check.Output(c, "exhaustionProbability", () => Results().ExhaustionProbability);
            check.Output(c, "expectedLoss", () => Results().ExpectedLoss);
        }

        check.Complete();
    }
}
