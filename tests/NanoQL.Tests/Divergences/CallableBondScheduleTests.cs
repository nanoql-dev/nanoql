// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A callable bond on an empty coupon schedule, and a put/call schedule holding a null callability. QuantLib's
/// <c>CallableFixedRateBond</c> passes its base the schedule's last date unchecked (<c>schedule.dates().back()</c>,
/// <c>callablebond.cpp:499</c>), past the end of an empty vector, which is undefined: the wheel ends the process
/// (0xC0000005), where <c>FixedRateBond</c>, which reads the schedule's <c>endDate()</c>, refuses "empty Schedule: no end
/// date". nanoql reads <see cref="Schedule.EndDate"/> and refuses with that text. QuantLib's base constructor reads each
/// callability's date to find the last one (<c>callablebond.cpp:43-50</c>), dereferencing a null one; nanoql refuses it
/// with <see cref="ArgumentException"/>, naming the schedule.
/// </summary>
public class CallableBondScheduleTests
{
    [Fact]
    public void AnEmptyScheduleIsRefusedWithTheSchedulesOwnText()
    {
        var empty = new Schedule([], new TARGET());

        NanoQLException e = Assert.Throws<NanoQLException>(() => new CallableFixedRateBond(
            2, 100.0, empty, [0.05], new Thirty360(Thirty360Convention.BondBasis)));
        Assert.Equal("empty Schedule: no end date", e.Message);

        // the plain fixed-rate bond refuses it alike, as QuantLib's does
        Assert.Equal(
            e.Message,
            Assert.Throws<NanoQLException>(
                () => new FixedRateBond(2, 100.0, empty, [0.05], new Thirty360(Thirty360Convention.BondBasis))).Message);
    }

    [Fact]
    public void ANullCallabilityIsRefusedAtConstruction()
    {
        var call = new Callability(new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Call, new Date(15, Month.June, 2028));
        Callability[] calls = [call, null!];

        ArgumentException zero = Assert.Throws<ArgumentException>(() => new CallableZeroCouponBond(
            2, 100.0, new TARGET(), new Date(15, Month.June, 2030), new Actual365Fixed(), putCallSchedule: calls));
        Assert.Equal("putCallSchedule", zero.ParamName);
        Assert.StartsWith("null callability in schedule", zero.Message, StringComparison.Ordinal);

        // the schedule without it builds
        Assert.Single(new CallableZeroCouponBond(
            2, 100.0, new TARGET(), new Date(15, Month.June, 2030), new Actual365Fixed(), putCallSchedule: [call]).Callability);
    }
}
