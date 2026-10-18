// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A rate helper given a null index is refused on entry, naming the parameter. QuantLib's helpers dereference their
/// index before any check (<c>ratehelpers.cpp:198, 206</c> for the deposit helper, <c>:533</c> for the swap helper),
/// a result C++ leaves undefined; the wheel stops on Boost's assertion.
/// </summary>
public class RateHelperNullIndexTests
{
    [Fact]
    public void AHelperGivenANullIndexIsRefused()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2024) };
        var thirty360 = new Thirty360(Thirty360Convention.BondBasis);

        Assert.Equal(
            "iborIndex",
            Assert.Throws<ArgumentNullException>(() => new DepositRateHelper(0.03, null!, context)).ParamName);
        Assert.Equal(
            "iborIndex",
            Assert.Throws<ArgumentNullException>(
                () => new DepositRateHelper(0.03, new Date(15, Month.January, 2024), null!)).ParamName);
        Assert.Equal(
            "iborIndex",
            Assert.Throws<ArgumentNullException>(() => new SwapRateHelper(
                0.03,
                new Period(5, TimeUnit.Years),
                new TARGET(),
                Frequency.Annual,
                BusinessDayConvention.Unadjusted,
                thirty360,
                null!,
                context)).ParamName);
        Assert.Equal(
            "iborIndex",
            Assert.Throws<ArgumentNullException>(() => new SwapRateHelper(
                0.03,
                new Date(17, Month.January, 2024),
                new Date(17, Month.January, 2029),
                new TARGET(),
                Frequency.Annual,
                BusinessDayConvention.Unadjusted,
                thirty360,
                null!,
                context)).ParamName);
    }
}
