// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A CMS caplet on the Gaussian1d cap/floor engine (the S-g1d plan's G1R6 (xix)): QuantLib's engine tests only for an
/// Ibor index (<c>gaussian1dcapfloorengine.cpp:47-48</c>) and otherwise prices the coupon period's simple forward
/// (<c>:81-85</c>, <c>:153-157</c>), so a CMS caplet reads no swap rate. Its same-model route is the float-float
/// swaption engine on a European exercise at the fixing into the CMS coupon against a fixed one, which estimates the
/// coupon by the model's swap rate at its fixing (<c>gaussian1dfloatfloatswaptionengine.cpp:460-464</c>). On the wheel,
/// on a GSR model (a = σ = 0.01) on a flat 3% curve, a one-year caplet on 1,000,000 of the 10-year EUR swap rate (ISDA
/// fix A), fixing in one, two or five years, is worth, against the float-float route: at 3% in a year 3845.936360680116
/// against 4153.063226583255; at 2%, 3% and 4.5% in two years 11120.554478976486, 5251.888776300536 and
/// 1001.0246098467576 against 11894.414352660851, 5692.585769904653 and 1092.524780338987; at 3% in five years
/// 7470.393687093831 against 8282.86559093049. nanoql prices a CMS coupon on the model's swap rate at its fixing, the
/// float-float engine's estimate, within 8.0e-9 of that route on the engines' default grid (64 points a side, 7
/// standard deviations), where each route moves by up to 1.1e-3 as the grid refines, as an Ibor caplet does (the
/// payoff's kink). QuantLib's check: that engine on the one-coupon swaption.
/// </summary>
public class CapFloorCmsTests
{
    [Fact]
    public void ACmsCapletReadsTheModelsSwapRate()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var index = new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), curve, curve);
        Gsr model = Gaussian1dInputs.Constant(curve, 0.01, 0.01);
        var calendar = new TARGET();
        (int Years, double Strike, double FloatFloat, double QuantLib)[] cases =
        [
            (1, 0.03, 4153.063226583255, 3845.936360680116),
            (2, 0.02, 11894.414352660851, 11120.554478976486),
            (2, 0.03, 5692.585769904653, 5251.888776300536),
            (2, 0.045, 1092.524780338987, 1001.0246098467576),
            (5, 0.03, 8282.86559093049, 7470.393687093831),
        ];
        foreach ((int years, double strike, double floatFloat, double quantLib) in cases)
        {
            Date start = calendar.Advance(calendar.Advance(today, years, TimeUnit.Years), 2, TimeUnit.Days);
            ImmutableArray<CashFlow> leg = new CmsLeg(new Schedule([start, calendar.Advance(start, 1, TimeUnit.Years)], calendar), index)
                .WithNotionals(1e6)
                .WithPaymentDayCounter(new Actual360())
                .WithPaymentAdjustment(BusinessDayConvention.ModifiedFollowing)
                .WithFixingDays(2)
                .ToLeg();

            // the engine reads no pricer's rate but the forwards' it reports; a coupon's own setter takes any pricer
            leg = [.. leg.Select(c => (CashFlow)((CmsCoupon)c).SetPricer(new IndexFixingPricer()))];
            double price = Gaussian1dCapFloorEngine.Calculate(new Cap(leg, [strike]), context, model).NPV!.Value;
            Assert.True(Math.Abs((price / floatFloat) - 1.0) <= 2e-8, $"{years}Y at {strike}: {price} against the float-float route's {floatFloat}");
            Assert.True(Math.Abs((price / quantLib) - 1.0) > 5e-2, $"{years}Y at {strike}: {price}");
        }
    }

    // A pricer whose rate is the index's fixing, which no CMS pricer gives before batch 3.
    private sealed class IndexFixingPricer : FloatingRateCouponPricer
    {
        public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context) => 0.0;

        public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context) => coupon.IndexFixing(context);

        public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) => 0.0;

        public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) => 0.0;

        public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) => 0.0;

        public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) => 0.0;
    }
}
