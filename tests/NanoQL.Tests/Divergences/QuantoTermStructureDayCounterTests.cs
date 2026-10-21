// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="QuantoTermStructure"/> on structures with different day counters. QuantLib reads every structure at the
/// time the dividend curve's day counter measures, with its own comment that this assumes one day count and "It should
/// be QL_REQUIREd" (<c>quantotermstructure.hpp:124-131</c>): two risk-free curves equal at every date, one on
/// Actual/365 (Fixed) and one on Actual/360, give quanto zero rates of 5.0000% and 4.9589%. nanoql reads each structure
/// at the instants the curve's times denote, on its own clock, so both give QuantLib's value on one day counter;
/// QuantLib's case (<c>testFDMQuantoHelper</c>, Actual/360 throughout) is QuantLib's arithmetic.
/// </summary>
public class QuantoTermStructureDayCounterTests
{
    [Fact]
    public void StructuresOnDifferentDayCountersAreReadAtTheirInstants()
    {
        var today = new Date(15, Month.October, 2026);
        var a365 = new Actual365Fixed();
        var r360 = new FlatForward(today, 0.03 * 360.0 / 365.0, new Actual360());
        var q = new FlatForward(today, 0.02, a365);
        var rf = new FlatForward(today, 0.01, a365);
        var v = new BlackConstantVol(today, new NullCalendar(), 0.2, a365);
        var xv = new BlackConstantVol(today, new NullCalendar(), 0.1, a365);

        // on one day counter, QuantLib's value: q + r - rf + rho sigma_S sigma_X
        var quanto = new QuantoTermStructure(q, new FlatForward(today, 0.03, a365), rf, v, 100.0, xv, 1.0, 0.5);
        Assert.Equal(0.05, quanto.ZeroRate(today + 730, a365, Compounding.Continuous).Rate, 1e-14);

        // the domestic curve on Actual/360, equal at every date: QuantLib reads 4.9589%
        var mixed = new QuantoTermStructure(q, r360, rf, v, 100.0, xv, 1.0, 0.5);
        Assert.Equal(0.05, mixed.ZeroRate(today + 730, a365, Compounding.Continuous).Rate, 1e-14);
        Assert.Equal(quanto.Discount(today + 182), mixed.Discount(today + 182), 1e-15);

        // and the volatilities on Actual/360, with the same variances at every date
        var v360 = new BlackConstantVol(today, new NullCalendar(), 0.2 * Math.Sqrt(360.0 / 365.0), new Actual360());
        var xv360 = new BlackConstantVol(today, new NullCalendar(), 0.1 * Math.Sqrt(360.0 / 365.0), new Actual360());
        var volatilities = new QuantoTermStructure(q, r360, rf, v360, 100.0, xv360, 1.0, 0.5);
        Assert.Equal(0.05, volatilities.ZeroRate(today + 730, a365, Compounding.Continuous).Rate, 1e-14);
    }
}
