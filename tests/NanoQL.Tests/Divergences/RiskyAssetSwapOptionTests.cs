// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The risky asset swap option at a zero standard deviation (the S-xcr plan's XR8 (xxii)): the formula's limit, the
/// intrinsic value, where QuantLib's reads 0·∞, NaN. On 31 August 2006, the payer swap of nominal 100 at spread 0.01
/// (an annual fixed and a quarterly float schedule on TARGET to 31 August 2011, Actual/360, recovery 0.4, the discount
/// curve 5% flat, the hazard rate 0.02 flat), the option expiring a year out at a market spread of 0.012 reads
/// 35.967035007653337 at a volatility of 0.2, 0.890356389102676 at 1e-9 and NaN at 0 in QuantLib (the S-xcr run's
/// <c>c4_facts.cpp</c>).
/// </summary>
public class RiskyAssetSwapOptionTests
{
    private static readonly Date Today = new(31, August, 2006);

    [Theory]
    [InlineData(0.2, 35.967035007653337)]
    [InlineData(1e-9, 0.890356389102676)]
    [InlineData(0.0, 0.890356389102676)]
    public void AZeroVolatilityIsTheIntrinsicValue(double volatility, double expected)
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        var option = new RiskyAssetSwapOption(Swap(), new Date(31, August, 2007), 0.012, volatility);
        Assert.Equal(expected, option.Calculate(context).NPV!.Value, 1e-12);
    }

    [Fact]
    public void AnOptionExpiringTodayIsExpired()
    {
        // QuantLib: 0
        var context = new EvaluationContext { EvaluationDate = Today };
        Assert.Equal(0.0, new RiskyAssetSwapOption(Swap(), Today, 0.012, 0.2).Calculate(context).NPV!.Value);
    }

    private static RiskyAssetSwap Swap()
    {
        Schedule fx = new MakeSchedule().From(Today).To(new Date(31, August, 2011)).WithTenor(new Period(1, TimeUnit.Years)).WithCalendar(new TARGET()).ToSchedule();
        Schedule fl = new MakeSchedule().From(Today).To(new Date(31, August, 2011)).WithTenor(new Period(3, TimeUnit.Months)).WithCalendar(new TARGET()).ToSchedule();
        return new RiskyAssetSwap(
            true, 100.0, fx, fl, new Actual360(), new Actual360(), 0.01, 0.4, new FlatForward(Today, 0.05, new Actual360()),
            new FlatHazardRate(Today, 0.02, new Actual365Fixed()));
    }
}
