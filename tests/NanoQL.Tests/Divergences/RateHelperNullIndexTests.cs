// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A rate helper given a null index is refused on entry, naming the parameter. QuantLib's helpers dereference their
/// index before any check (<c>ratehelpers.cpp:198, 206</c> for the deposit helper, <c>:533</c> for the swap helper,
/// <c>:148</c> for the futures helper, <c>:312, 332, 350</c> for the FRA helper, <c>oisratehelper.cpp:114</c> for the
/// OIS helper), a result C++ leaves undefined; the wheel stops on Boost's assertion.
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

    [Fact]
    public void AFuturesFraOrOisHelperGivenANullIndexIsRefused()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2024) };
        var start = new Date(20, Month.March, 2024);

        Assert.Equal(
            "iborIndex",
            Assert.Throws<ArgumentNullException>(() => new FuturesRateHelper(96.0, start, (IborIndex)null!)).ParamName);
        Assert.Equal(
            "iborIndex",
            Assert.Throws<ArgumentNullException>(() => new FraRateHelper(0.03, 3, (IborIndex)null!, context)).ParamName);
        Assert.Equal(
            "iborIndex",
            Assert.Throws<ArgumentNullException>(
                () => new FraRateHelper(0.03, new Period(3, TimeUnit.Months), (IborIndex)null!, context)).ParamName);
        Assert.Equal(
            "iborIndex",
            Assert.Throws<ArgumentNullException>(() => new FraRateHelper(0.03, 1, 2, null!, context)).ParamName);
        Assert.Equal(
            "iborIndex",
            Assert.Throws<ArgumentNullException>(
                () => new FraRateHelper(0.03, start, new Date(20, Month.June, 2024), null!)).ParamName);
        Assert.Equal(
            "overnightIndex",
            Assert.Throws<ArgumentNullException>(
                () => new OISRateHelper(2, new Period(1, TimeUnit.Years), 0.03, null!, context)).ParamName);
        Assert.Equal(
            "overnightIndex",
            Assert.Throws<ArgumentNullException>(
                () => new OISRateHelper(start, new Date(20, Month.March, 2025), 0.03, null!, context)).ParamName);
    }
}
