// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The ISDA engine's protection leg on curves that end before the maturity. QuantLib integrates over the union of the
/// two curves' nodes, adds the maturity only when the union is empty (<c>isdacdsengine.cpp:150-156</c>), and stops at
/// the maturity only on meeting a node past it (<c>:167-172</c>), so it drops the protection from the curves' last
/// node to the maturity (QuantLib's defect, R8). nanoql adds the maturity to the nodes when their last one is before
/// it. On 15 March 2024, a 10-year CDS buying protection on 10,000,000 at 1% from the next day (quarterly on
/// WeekendsOnly under the CDS rule, Actual/360 with the last period inclusive) on a hazard rate flat at 2% and a
/// discount curve continuously flat at 3%, Actual/365 (Fixed): on a log-linear discount curve with yearly nodes to
/// 5 years, nanoql's default leg is 945,721.5990409853, QuantLib's own on the flat curve and on nodes to 15 years,
/// where QuantLib's on nodes to 5 years is 531,134.1471852273; the coupon leg is -820,172.4119002547 on every curve.
/// </summary>
public class IsdaProtectionLegMaturityTests
{
    [Fact]
    public void TheProtectionRunsToTheMaturity()
    {
        var today = new Date(15, Month.March, 2024);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        var hazard = new FlatHazardRate(today, 0.02, dc);

        // QuantLib's: 531134.1471852273 on the nodes to 5 years
        CreditDefaultSwapResults nodes5Y = Price(context, NodesTo(today, 5), hazard);
        CreditDefaultSwapResults flat = Price(context, new FlatForward(today, 0.03, dc), hazard);

        Assert.True(Tolerance.Within(945721.5990409853, nodes5Y.DefaultLegNPV!.Value), $"{nodes5Y.DefaultLegNPV}");
        Assert.True(Tolerance.Within(945721.5990409853, flat.DefaultLegNPV!.Value), $"{flat.DefaultLegNPV}");
        Assert.True(Tolerance.Within(-820172.4119002547, nodes5Y.CouponLegNPV!.Value), $"{nodes5Y.CouponLegNPV}");
    }

    private static DiscountCurve NodesTo(Date today, int years)
    {
        var dates = new List<Date> { today };
        for (int i = 1; i <= years; i++)
        {
            dates.Add(today + new Period(i, TimeUnit.Years));
        }

        DayCounter dc = new Actual365Fixed();
        return new DiscountCurve(dates, dates.Select(d => Math.Exp(-0.03 * dc.YearFraction(today, d))), dc)
            .EnableExtrapolation();
    }

    private static CreditDefaultSwapResults Price(
        EvaluationContext context, YieldTermStructure discountCurve, DefaultProbabilityTermStructure hazard)
    {
        Date today = context.EvaluationDate;
        Schedule schedule = new MakeSchedule()
            .From(today)
            .To(today + new Period(10, TimeUnit.Years))
            .WithTenor(new Period(3, TimeUnit.Months))
            .WithCalendar(new WeekendsOnly())
            .WithConvention(BusinessDayConvention.Following)
            .WithTerminationDateConvention(BusinessDayConvention.Unadjusted)
            .WithRule(DateGenerationRule.CDS)
            .ToSchedule();
        var cds = new CreditDefaultSwap(
            ProtectionSide.Buyer,
            1e7,
            0.01,
            schedule,
            BusinessDayConvention.Following,
            new Actual360(),
            true,
            true,
            today + 1,
            new FaceValueClaim(),
            new Actual360(true),
            true,
            today,
            3);
        return IsdaCdsEngine.Calculate(cds, context, hazard, 0.4, discountCurve);
    }
}
