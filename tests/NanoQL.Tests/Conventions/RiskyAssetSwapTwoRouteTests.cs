// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The risky asset swap against a CDS on its issuer, two routes to the protection: at a discount rate of 0 the par
/// coupon is 0, so the asset swap's fair spread times its float annuity is the principal's expected loss, (1 − R)·PD(T),
/// which is also the CDS's fair spread times its risky annuity (no accrual on default). On equal quarterly schedules
/// over five years (Actual/360, no calendar), the two agree to rounding at recovery 0 and within the recovery integral's
/// daily sum at 0.4, at most 4.6e-5 relative at a hazard rate of 0.05, proportional to the rate. At a positive rate
/// they differ by about 0.6% to 1.1% at every hazard rate: the package pays the principal's loss at maturity and loses
/// the coupons, where the CDS pays at default.
/// </summary>
public class RiskyAssetSwapTwoRouteTests
{
    [Theory]
    [InlineData(0.005, 0.0)]
    [InlineData(0.02, 0.0)]
    [InlineData(0.05, 0.0)]
    [InlineData(0.005, 0.4)]
    [InlineData(0.02, 0.4)]
    [InlineData(0.05, 0.4)]
    public void TheAssetSwapAndTheCdsPriceTheSameProtection(double hazardRate, double recovery)
    {
        var today = new Date(31, Month.August, 2006);
        var end = new Date(31, Month.August, 2011);
        var context = new EvaluationContext { EvaluationDate = today };
        var discount = new FlatForward(today, 0.0, new Actual360());
        var hazard = new FlatHazardRate(today, hazardRate, new Actual360());
        Schedule schedule = new MakeSchedule().From(today).To(end).WithTenor(new Period(3, TimeUnit.Months))
            .WithCalendar(new NullCalendar()).WithConvention(BusinessDayConvention.Unadjusted).ToSchedule();

        var asw = new RiskyAssetSwap(true, 100.0, schedule, schedule, new Actual360(), new Actual360(), 0.0, recovery, discount, hazard);
        double aswProtection = asw.Calculate(context).FairSpread!.Value * asw.FloatAnnuity(context);

        var cds = new CreditDefaultSwap(ProtectionSide.Buyer, 100.0, 0.01, schedule, BusinessDayConvention.Unadjusted, new Actual360(), false, true, today);
        CreditDefaultSwapResults results = MidPointCdsEngine.Calculate(cds, context, hazard, recovery, discount);
        double riskyAnnuity = Math.Abs(results.CouponLegNPV!.Value / 0.01) / 100.0;
        double cdsProtection = results.FairSpread!.Value * riskyAnnuity;

        double protection = (1 - recovery) * hazard.DefaultProbability(end);
        Assert.Equal(protection, cdsProtection, 1e-14);
        Assert.Equal(protection, aswProtection, (recovery == 0.0 ? 1e-14 : hazardRate / 500.0) * protection);
    }
}
