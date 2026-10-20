// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The inflation-coupon pricer vectors, <c>tests/vectors/cashflows/inflationcouponpricer.json</c>: the Black,
/// unit-displaced Black and Bachelier year-on-year pricers' swaplet, caplet and floorlet rates, composed as QuantLib's
/// capped and floored year-on-year coupon composes them: the swaplet rate plus the floorlet rate less the caplet rate,
/// each at its effective strike.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InflationCouponPricerVectorTests
{
    [Fact]
    public void InflationCouponPricerVector()
    {
        var file = VectorFile.Load("cashflows/inflationcouponpricer");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
            YoYOptionletVolatilitySurface vol = InflationInputs.YoYVolatilityOf(c.Input("volatility"), context);
            YieldTermStructure nominal = CashFlowInputs.CurveOf(c.Input("nominalCurve"));
            YoYInflationCouponPricer pricer = c.String("pricer") switch
            {
                "Black" => new BlackYoYInflationCouponPricer(vol, nominal),
                "UnitDisplacedBlack" => new UnitDisplacedBlackYoYInflationCouponPricer(vol, nominal),
                "Bachelier" => new BachelierYoYInflationCouponPricer(vol, nominal),
                string kind => throw new InvalidOperationException($"unknown pricer '{kind}'"),
            };
            double nominalAmount = c.Double("nominal");
            double gearing = c.Double("gearing"), spread = c.Double("spread");
            YoYInflationCoupon coupon = new YoYInflationCoupon(
                c.DateInput("paymentDate"),
                nominalAmount,
                c.DateInput("startDate"),
                c.DateInput("endDate"),
                c.Int("fixingDays"),
                (YoYInflationIndex)index,
                InflationCashFlowInputs.PeriodOf(c.Input("observationLag")),
                (CPIInterpolationType)c.Int("interpolation"),
                CashFlowInputs.DayCounterOf(c.Input("dayCounter")),
                gearing,
                spread).SetPricer(pricer);

            // CappedFlooredYoYInflationCoupon's effective strikes and rate (capflooredinflationcoupon.cpp)
            double effectiveCap = (c.Double("cap") - spread) / gearing;
            double effectiveFloor = (c.Double("floor") - spread) / gearing;
            double Swaplet() => pricer.SwapletRate(coupon, context);
            double Caplet() => pricer.CapletRate(coupon, effectiveCap, context);
            double Floorlet() => pricer.FloorletRate(coupon, effectiveFloor, context);
            double Collared() => Swaplet() + Floorlet() - Caplet();
            check.Output(c, "underlyingRate", () => Swaplet());
            check.Output(c, "effectiveCap", effectiveCap);
            check.Output(c, "effectiveFloor", effectiveFloor);
            check.Output(c, "cappedRate", () => Swaplet() + 0.0 - Caplet());
            check.Output(c, "flooredRate", () => Swaplet() + Floorlet() - 0.0);
            check.Output(c, "collaredRate", () => Collared());
            check.Output(c, "collaredAmount", () => Collared() * coupon.AccrualPeriod() * nominalAmount);
        }

        check.Complete();
    }
}
