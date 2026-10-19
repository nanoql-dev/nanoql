// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The bond functions vectors, <c>tests/vectors/pricingengines/bond/bondfunctions.json</c>: the functions the wheel
/// binds on 11 bonds at 10 settlement dates each, the yields of nanoql's two solvers against the wheel's instances of
/// the same algorithms. A refused value is a throw.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BondFunctionsVectorTests
{
    [Fact]
    public void BondFunctionsVector()
    {
        var file = VectorFile.Load("pricingengines/bond/bondfunctions");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Bond bond = BondInputs.BondOf(c.Input("bond"), context);
            FlatForward curve = CashFlowInputs.CurveOf(c.Input("curve"));
            InterestRate r = BondInputs.RateOf(c.Input("rate"));
            BondPrice price = BondInputs.PriceOf(c.Input("price"));
            JsonElement z = c.Input("zSpread");
            double spread = z.GetProperty("spread").GetDouble();
            var zc = (Compounding)z.GetProperty("compounding").GetInt32();
            var zf = (Frequency)z.GetProperty("frequency").GetInt32();
            Date?[] dates = BondInputs.DatesOf(c, "dates");
            DayCounter rd = r.DayCounter;

            check.Output(c, "startDate", () => BondFunctions.StartDate(bond));
            check.Output(c, "maturityDate", () => BondFunctions.MaturityDate(bond));

            void Each<T>(string name, Func<Date?, T> f) => check.Output(c, name, BondInputs.Each(dates, f));

            Each("isTradable", d => BondFunctions.IsTradable(bond, context, d));
            Each("previousCashFlowDate", d => BondFunctions.PreviousCashFlowDate(bond, context, d));
            Each("nextCashFlowDate", d => BondFunctions.NextCashFlowDate(bond, context, d));
            Each("previousCashFlowAmount", d => BondFunctions.PreviousCashFlowAmount(bond, context, d));
            Each("nextCashFlowAmount", d => BondFunctions.NextCashFlowAmount(bond, context, d));
            Each("previousCouponRate", d => BondFunctions.PreviousCouponRate(bond, context, d));
            Each("nextCouponRate", d => BondFunctions.NextCouponRate(bond, context, d));
            Each("accrualStartDate", d => BondFunctions.AccrualStartDate(bond, context, d));
            Each("accrualEndDate", d => BondFunctions.AccrualEndDate(bond, context, d));
            Each("accrualPeriod", d => BondFunctions.AccrualPeriod(bond, context, d));
            Each("accrualDays", d => BondFunctions.AccrualDays(bond, context, d));
            Each("accruedPeriod", d => BondFunctions.AccruedPeriod(bond, context, d));
            Each("accruedDays", d => BondFunctions.AccruedDays(bond, context, d));
            Each("accruedAmount", d => BondFunctions.AccruedAmount(bond, context, d));
            Each("cleanPriceCurve", d => BondFunctions.CleanPrice(bond, curve, context, d));
            Each("dirtyPriceCurve", d => BondFunctions.DirtyPrice(bond, curve, context, d));
            Each("bpsCurve", d => BondFunctions.Bps(bond, curve, context, d));
            Each("atmRate", d => BondFunctions.AtmRate(bond, curve, context, d));
            Each("atmRatePrice", d => BondFunctions.AtmRate(bond, curve, context, d, price));
            Each("cleanPriceRate", d => BondFunctions.CleanPrice(bond, r, context, d));
            Each("cleanPriceRateForm", d => BondFunctions.CleanPrice(
                bond, r.Rate, rd, r.Compounding, r.Frequency, context, d));
            Each("bpsRate", d => BondFunctions.Bps(bond, r, context, d));
            Each("bpsRateForm", d => BondFunctions.Bps(bond, r.Rate, rd, r.Compounding, r.Frequency, context, d));
            Each("durationModified", d => BondFunctions.Duration(bond, r, context, DurationType.Modified, d));
            Each("durationSimple", d => BondFunctions.Duration(bond, r, context, DurationType.Simple, d));
            Each("durationMacaulay", d => BondFunctions.Duration(
                bond, r.Rate, rd, Compounding.Compounded, Frequency.Semiannual, context, DurationType.Macaulay, d));
            Each("convexity", d => BondFunctions.Convexity(bond, r, context, d));
            Each("convexityRateForm", d => BondFunctions.Convexity(
                bond, r.Rate, rd, r.Compounding, r.Frequency, context, d));
            Each("basisPointValue", d => BondFunctions.BasisPointValue(bond, r, context, d));
            Each("basisPointValueRateForm", d => BondFunctions.BasisPointValue(
                bond, r.Rate, rd, r.Compounding, r.Frequency, context, d));
            Each("yieldValueBasisPoint", d => BondFunctions.YieldValueBasisPoint(bond, r, context, d));
            Each("yieldValueBasisPointRateForm", d => BondFunctions.YieldValueBasisPoint(
                bond, r.Rate, rd, r.Compounding, r.Frequency, context, d));
            Each("bondYield", d => BondFunctions.Yield(
                bond, price, rd, r.Compounding, r.Frequency, context, d, 1.0e-10, 100, 0.05));
            Each("yieldNewtonSafe", d => BondFunctions.Yield(
                new NewtonSafe(), bond, price, rd, r.Compounding, r.Frequency, context, d, 1.0e-10, 0.05));
            Each("yieldBrent", d => BondFunctions.Yield(
                new Brent(), bond, price, rd, r.Compounding, r.Frequency, context, d, 1.0e-10, 0.05));
            Each("cleanPriceZSpread", d => BondFunctions.CleanPrice(bond, curve, spread, zc, zf, context, d));
            Each("dirtyPriceZSpread", d => BondFunctions.DirtyPrice(bond, curve, spread, zc, zf, context, d));
            Each("zSpread", d => BondFunctions.ZSpread(bond, price, curve, zc, zf, context, d, 1.0e-10, 100, 0.0));
        }

        check.Complete();
    }
}
