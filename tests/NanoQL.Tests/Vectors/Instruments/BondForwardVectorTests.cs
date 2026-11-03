// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The bond forward vectors, <c>tests/vectors/instruments/bondforward.json</c>: forwards on six bonds, long and short,
/// delivering around their coupons, with their settlement dates, spot values and income, forward values and prices,
/// NPVs and implied yields, and an expired one; the bond priced by <see cref="DiscountingBondEngine"/> on the case's
/// curve, which also discounts the contract and its income.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BondForwardVectorTests
{
    [Fact]
    public void BondForwardVector()
    {
        var file = VectorFile.Load("instruments/bondforward");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Bond bond = BondInputs.BondOf(c.Input("bond"), context);
            YieldTermStructure curve = CashFlowInputs.CurveOf(c.Input("curve"));
            var forward = new BondForward(
                c.DateInput("valueDate"),
                c.DateInput("deliveryDate"),
                (PositionType)c.Int("position"),
                c.Double("strike"),
                c.Int("settlementDays"),
                new ActualActual(ActualActualConvention.ISDA),
                new TARGET(),
                BusinessDayConvention.Following,
                bond,
                curve,
                curve);
            BondResults BondEngine(Bond b, EvaluationContext ctx) => DiscountingBondEngine.Calculate(b, ctx, curve);

            check.Output(c, "settlementDate", forward.SettlementDate(context));
            check.Output(c, "isExpired", forward.IsExpired(context));
            ForwardResults results = forward.Calculate(context, BondEngine);
            if (forward.IsExpired(context))
            {
                check.Output(c, "NPV", results.NPV);
                continue;
            }

            check.Output(c, "spotValue", forward.SpotValue(context, BondEngine));

            // bondforward.py leaves the income's outputs out on a face other than 100 delivering after a coupon.
            if (!c.Expected.TryGetProperty("spotIncome", out _))
            {
                continue;
            }

            check.Output(c, "spotIncome", forward.SpotIncome(curve, context));
            check.Output(c, "forwardValue", results.ForwardValue);
            check.Output(c, "forwardPrice", BondForward.ForwardPrice(results));
            check.Output(c, "cleanForwardPrice", forward.CleanForwardPrice(results, context));
            check.Output(c, "NPV", results.NPV);
            Date settlement = forward.SettlementDate(context);
            double ImpliedYield(Compounding compounding) =>
                forward.ImpliedYield(
                    results.SpotValue!.Value,
                    results.ForwardValue!.Value,
                    settlement,
                    compounding,
                    new Actual365Fixed(),
                    context).Rate;

            // a zero forward has no implied yield, refused as QuantLib refuses it
            check.Output(c, "impliedYieldContinuous", () => ImpliedYield(Compounding.Continuous));
            check.Output(c, "impliedYieldCompounded", () => ImpliedYield(Compounding.Compounded));
        }

        check.Complete();
    }
}
