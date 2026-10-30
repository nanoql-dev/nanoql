// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="JointCalendar.Calendars"/> and <see cref="JointCalendar.Rule"/>, nanoql's additions: the joined
/// calendars in construction order and the rule, each constructor's, the parts that make the calendar's value.
/// </summary>
public class JointCalendarAccessorTests
{
    private static readonly Calendar Target = new TARGET();
    private static readonly Calendar London = new UnitedKingdom(UnitedKingdomMarket.Exchange);
    private static readonly Calendar NewYork = new UnitedStates(UnitedStatesMarket.NYSE);
    private static readonly Calendar Tokyo = new Japan();

    [Fact]
    public void EachConstructorKeepsItsCalendarsInOrderAndItsRule()
    {
        var two = new JointCalendar(London, Target);
        var three = new JointCalendar(NewYork, London, Target, JointCalendarRule.JoinBusinessDays);
        var four = new JointCalendar(Tokyo, NewYork, London, Target);
        var many = new JointCalendar([Target, Tokyo, London, NewYork, Target], JointCalendarRule.JoinBusinessDays);

        Assert.Equal([London, Target], two.Calendars);
        Assert.Equal(JointCalendarRule.JoinHolidays, two.Rule);
        Assert.Equal([NewYork, London, Target], three.Calendars);
        Assert.Equal(JointCalendarRule.JoinBusinessDays, three.Rule);
        Assert.Equal([Tokyo, NewYork, London, Target], four.Calendars);
        Assert.Equal(JointCalendarRule.JoinHolidays, four.Rule);
        Assert.Equal([Target, Tokyo, London, NewYork, Target], many.Calendars);
        Assert.Equal(JointCalendarRule.JoinBusinessDays, many.Rule);
    }

    [Fact]
    public void TheCalendarsAreTheOnesGivenWithTheirEdits()
    {
        Calendar edited = Target.AddHoliday(new Date(24, Month.December, 2026));
        var joint = new JointCalendar(London, edited);

        Assert.Same(edited, joint.Calendars[1]);
        Assert.Equal(joint, new JointCalendar(joint.Calendars, joint.Rule));
        Assert.NotEqual(joint, new JointCalendar(London, Target));
    }
}
