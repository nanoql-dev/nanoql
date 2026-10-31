// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="PSACurve"/> follows SIFMA's PSA formula at its two ends (Chapter SF, SF-6), where QLNet's
/// <c>PSACurve.getCPR</c> (<c>PSACurve.cs:35-41</c>) has neither bound. Above 1,666⅔% PSA, QLNet's CPR passes 1 and
/// its SMM is NaN, so an MBS's flows are NaN; nanoql caps the CPR at 100%, its SMM 1. Before the curve's start, QLNet's
/// month falls to 0 and below, its CPR 0 and then negative; nanoql counts every prior month as the first, as SIFMA's
/// "MONTH equals 1 for all prior months". QLNet's values are QLNet 1.13.2's; elsewhere the two agree bit for bit (the
/// vectors).
/// </summary>
public class PsaCapAndFloorTests
{
    private static readonly Date Start = new(1, March, 2007);

    [Fact]
    public void ACprAboveOneIsCappedAtOne()
    {
        // QLNet at MONTH 30 (2009-08-01): 1,667% PSA CPR 1.0002, SMM NaN; 1,700% 1.02, NaN; 2,000% 1.2, NaN.
        Date month30 = Start + new Period(29, TimeUnit.Months);
        foreach (double multiplier in new[] { 16.67, 17.0, 20.0 })
        {
            var curve = new PSACurve(Start, multiplier);

            Assert.Equal(1.0, curve.GetCPR(month30));
            Assert.Equal(1.0, curve.GetSMM(month30));
        }
    }

    [Fact]
    public void BelowTheCapTheRatesAreQLNets()
    {
        // QLNet at MONTH 15 (2008-05-01): 1,700% PSA CPR 0.51, SMM 0.05771341846410616; 2,000% 0.6,
        // 0.07351512752093081.
        Date month15 = Start + new Period(14, TimeUnit.Months);

        Assert.Equal(0.51, new PSACurve(Start, 17.0).GetCPR(month15));
        Assert.Equal(0.05771341846410616, new PSACurve(Start, 17.0).GetSMM(month15));
        Assert.Equal(0.6, new PSACurve(Start, 20.0).GetCPR(month15));
        Assert.Equal(0.07351512752093081, new PSACurve(Start, 20.0).GetSMM(month15));
    }

    [Fact]
    public void AMonthBeforeTheStartCountsAsTheFirst()
    {
        // QLNet at 100% PSA: 1 and 15 days before the start CPR 0.002; 29 to 45 days 0; 59 to 61 days -0.002, SMM
        // -0.00016651408382073463; 90 days -0.004; 400 days -0.024.
        var curve = new PSACurve(Start);
        double first = curve.GetCPR(Start);
        foreach (int daysBefore in new[] { 1, 15, 29, 30, 31, 45, 59, 60, 61, 90, 400 })
        {
            Assert.Equal(first, curve.GetCPR(Start - daysBefore));
            Assert.Equal(curve.GetSMM(Start), curve.GetSMM(Start - daysBefore));
        }

        Assert.Equal(0.002, first);
        Assert.Equal(0.00016681963994558124, curve.GetSMM(Start));
    }
}
