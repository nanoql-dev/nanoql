// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="QuantoTermStructure"/> on structures with different day counters. QuantLib reads every structure at the
/// time the dividend curve's day counter measures, with its own comment that this assumes one day count and "It should
/// be QL_REQUIREd" (<c>quantotermstructure.hpp:124-131</c>): two risk-free curves equal at every date, one on
/// Actual/365 (Fixed) and one on Actual/360, give quanto zero rates of 5.0000% and 4.9589%. nanoql makes the check
/// QuantLib names and refuses with <see cref="ArgumentException"/>, "all term structures must share one day counter",
/// naming the first structure that differs; QuantLib's case (<c>testFDMQuantoHelper</c>, Actual/360 throughout) is
/// unchanged.
/// </summary>
public class QuantoTermStructureDayCounterTests
{
    [Fact]
    public void StructuresOnDifferentDayCountersAreRefused()
    {
        var today = new Date(15, Month.October, 2026);
        var a365 = new Actual365Fixed();
        var r360 = new FlatForward(today, 0.03 * 360.0 / 365.0, new Actual360());
        var q = new FlatForward(today, 0.02, a365);
        var rf = new FlatForward(today, 0.01, a365);
        var v = new BlackConstantVol(today, new NullCalendar(), 0.2, a365);
        var xv = new BlackConstantVol(today, new NullCalendar(), 0.1, a365);

        ArgumentException e = Assert.Throws<ArgumentException>(() => new QuantoTermStructure(q, r360, rf, v, 100.0, xv, 1.0, 0.5));
        Assert.Equal("riskFreeTS", e.ParamName);
        Assert.StartsWith("all term structures must share one day counter", e.Message, StringComparison.Ordinal);

        // on one day counter, QuantLib's value: q + r - rf + rho sigma_S sigma_X
        var quanto = new QuantoTermStructure(q, new FlatForward(today, 0.03, a365), rf, v, 100.0, xv, 1.0, 0.5);
        Assert.Equal(0.05, quanto.ZeroRate(today + 730, a365, Compounding.Continuous).Rate, 1e-14);
    }
}
