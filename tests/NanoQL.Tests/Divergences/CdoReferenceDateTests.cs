// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CdoBuilders;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The CDO and the random models on default curves dated after the evaluation date (the S-xcr plan's XR8 (x)): a
/// default probability before a curve's reference date is 0 and a random model counts its days from the evaluation
/// date, so T+2 curves read as their restatement from the evaluation date. QuantLib's mid-point CDO on T+2 curves throws
/// ("negative time (-0.00821918) given") and on the restated curves reads NPV −0.94756528135798312, fair premium
/// 0.10656135275585535; its random model counts from each curve's date, reading P(≥ 1, a year) 0.1215 on the T+2 curves
/// and throwing on the restated ones ("t &lt; 0", its day solver bracketing below 0 where the hazard is 0) (the S-xcr
/// run's <c>c2_facts.cpp</c>).
/// </summary>
public class CdoReferenceDateTests
{
    [Fact]
    public void TheCdoOnT2CurvesIsTheRestatedOnes()
    {
        SyntheticCDOResults onT2 = Mid(Cdo(EightNames(0.03, 0.07, T2()).SetLossModel(Pool(8, 0.3)), ProtectionSide.Seller, 0.0, 0.02));
        SyntheticCDOResults onRestated = Mid(Cdo(EightNames(0.03, 0.07, Restated()).SetLossModel(Pool(8, 0.3)), ProtectionSide.Seller, 0.0, 0.02));

        Assert.Equal(-0.94756528135798312, onRestated.NPV!.Value, 1e-13);
        Assert.Equal(0.10656135275585535, onRestated.FairPremium!.Value, 1e-14);
        Assert.Equal(onRestated.NPV!.Value, onT2.NPV!.Value, 1e-13);
        Assert.Equal(onRestated.FairPremium!.Value, onT2.FairPremium!.Value, 1e-14);
    }

    [Fact]
    public void TheRandomModelCountsFromTheEvaluationDate()
    {
        double onT2 = EightNames(0.0, 1.0, T2()).SetLossModel(Random(8, 0.3, 2000)).ProbAtLeastNEvents(1, Today + 365);
        double onRestated = EightNames(0.0, 1.0, Restated()).SetLossModel(Random(8, 0.3, 2000)).ProbAtLeastNEvents(1, Today + 365);

        Assert.Equal(onRestated, onT2);
        Assert.NotEqual(0.1215, onT2);
    }

    [Fact]
    public void ASeasonedCdosUpfrontIsDiscountedFromTheEvaluationDate()
    {
        // its first coupon from 1 August 2006 unpaid, its upfront due at the accrual start: QuantLib refuses the reads
        // before the curves' date, and on the curves restated from 1 August (hazard and rate 0 to the evaluation date)
        // reads mid-point premium 0.22158255409925076 and upfront 0.032 (NPV -0.90356602571233158 less the premium,
        // plus the protection 1.1571485798115824), integral NPV -0.8963331485521745 and fair upfront
        // 0.2901041089225545 (the S-xcr run's c2_o12.cpp); its mid-point protection dates the first period's defaults
        // from the restated discount curve's 1 August, where both engines date them from the evaluation date
        var basket = new Basket(
            new Date(1, Month.January, 2006),
            Eight,
            Enumerable.Repeat(10.0, 8),
            PoolOf(Eight, [.. EightHazards.Select(h => (DefaultProbabilityTermStructure)Flat(Today, h))]),
            Context,
            0.03,
            0.07);
        Schedule seasoned = new MakeSchedule().From(new Date(1, Month.August, 2006)).To(new Date(1, Month.August, 2011))
            .WithTenor(new Period(3, TimeUnit.Months)).WithCalendar(new TARGET()).ToSchedule();
        SyntheticCDO cdo = Cdo(basket.SetLossModel(Pool(8, 0.3)), ProtectionSide.Seller, 0.01, 0.02, schedule: seasoned);
        SyntheticCDOResults mid = Mid(cdo);
        SyntheticCDOResults integral = Integral(cdo);

        Assert.Equal(0.22158255409925076, mid.PremiumValue!.Value, 1e-13);
        Assert.Equal(0.032, mid.NPV!.Value - mid.PremiumValue!.Value + mid.ProtectionValue!.Value, 1e-13);
        Assert.Equal(-0.8963331485521745, integral.NPV!.Value, 1e-13);
        Assert.Equal(0.2901041089225545, integral.FairUpfrontPremium!.Value, 1e-13);
    }

    private static Date T2Date => new TARGET().Advance(Today, 2, TimeUnit.Days);

    private static DefaultProbabilityTermStructure[] T2() =>
        [.. EightHazards.Select(h => (DefaultProbabilityTermStructure)Flat(T2Date, h))];

    private static DefaultProbabilityTermStructure[] Restated() =>
        [.. EightHazards.Select(h => (DefaultProbabilityTermStructure)new HazardRateCurve(
            [Today, T2Date, Today + new Period(20, TimeUnit.Years)], [0.0, 0.0, h], new Actual365Fixed()))];
}
