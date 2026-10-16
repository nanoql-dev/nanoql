// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// An enum value outside the type, which C# allows through a cast, meets QuantLib's <c>QL_FAIL</c> in a switch's
/// default; that maps to <see cref="NanoQLException"/>, as every <c>QL_FAIL</c> does, also where the value is a
/// constructor's argument.
/// </summary>
public class UnknownEnumValueTests
{
    [Fact]
    public void AnUnknownEnumValueThrowsNanoQLException()
    {
        Assert.Throws<NanoQLException>(() => new UnitedStates((UnitedStatesMarket)99));
        Assert.Throws<NanoQLException>(() => new UnitedKingdom((UnitedKingdomMarket)99));
        Assert.Throws<NanoQLException>(() => new JointCalendar(new TARGET(), new TARGET(), (JointCalendarRule)99));
        Assert.Throws<NanoQLException>(() => new Actual365Fixed((Actual365FixedConvention)99));
        Assert.Throws<NanoQLException>(() => new Thirty360((Thirty360Convention)99));
        Assert.Throws<NanoQLException>(() => new ActualActual((ActualActualConvention)99));
        Assert.Throws<NanoQLException>(() => new Period((Frequency)99));
    }
}
