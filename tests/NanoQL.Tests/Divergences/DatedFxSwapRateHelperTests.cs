// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An FX swap helper built from dates has no fixing days, convention or end-of-month flag, and its
/// <see cref="FxSwapRateHelper.FixingDays"/>, <see cref="FxSwapRateHelper.BusinessDayConvention"/> and
/// <see cref="FxSwapRateHelper.EndOfMonth"/> refuse. QuantLib's dated constructor leaves the three members
/// uninitialized (<c>ratehelpers.cpp:772-785</c>) and its inspectors return them (<c>ratehelpers.hpp:398-401</c>):
/// reading an indeterminate value, a result C++ leaves undefined.
/// </summary>
public class DatedFxSwapRateHelperTests
{
    [Fact]
    public void ADatedFxSwapHelperRefusesItsFixingDaysConventionAndEndOfMonth()
    {
        var curve = new FlatForward(new Date(15, Month.January, 2024), 0.05, new Actual360());
        var dated = new FxSwapRateHelper(
            0.0131, 1.0950, new Date(17, Month.January, 2024), new Date(17, Month.April, 2024), true, curve);

        Assert.Throws<NanoQLException>(() => dated.FixingDays());
        Assert.Throws<NanoQLException>(() => dated.BusinessDayConvention());
        Assert.Throws<NanoQLException>(() => dated.EndOfMonth());

        // a helper built from a tenor has them
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2024) };
        var tenor = new FxSwapRateHelper(
            0.0131,
            1.0950,
            new Period(3, TimeUnit.Months),
            2,
            new TARGET(),
            BusinessDayConvention.Following,
            false,
            true,
            curve,
            context);
        Assert.Equal(2, tenor.FixingDays());
        Assert.Equal(BusinessDayConvention.Following, tenor.BusinessDayConvention());
        Assert.False(tenor.EndOfMonth());
    }
}
