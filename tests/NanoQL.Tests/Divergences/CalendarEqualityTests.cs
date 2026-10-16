// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Calendars are immutable values. QuantLib compares calendars by name only, and its <c>addHoliday</c>,
/// <c>removeHoliday</c> and <c>resetAddedAndRemovedHolidays</c> change the holidays of every instance of the calendar,
/// since all instances share one implementation. In nanoql an edit returns a modified copy, the added and removed
/// holidays are part of the calendar's value, and a joint calendar also compares its rule and its components.
/// </summary>
public class CalendarEqualityTests
{
    private static readonly Date May1st2004 = new(1, Month.May, 2004);       // a TARGET holiday
    private static readonly Date April26th2004 = new(26, Month.April, 2004); // a TARGET business day
    private static readonly Date April27th2004 = new(27, Month.April, 2004); // a TARGET business day

    [Fact]
    public void CalendarsAreEqualByNameAndByTheirAddedAndRemovedHolidaysComparedByContent()
    {
        Assert.Equal(new TARGET(), new TARGET());
        Assert.Equal(new TARGET().GetHashCode(), new TARGET().GetHashCode());

        // The same holidays added in another order: equal sets, equal calendars, equal hashes.
        Calendar a = new TARGET().AddHoliday(April26th2004).AddHoliday(April27th2004).RemoveHoliday(May1st2004);
        Calendar b = new TARGET().RemoveHoliday(May1st2004).AddHoliday(April27th2004).AddHoliday(April26th2004);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        // An added or a removed holiday makes a different calendar, where QuantLib's operator== sees one name.
        Calendar added = new TARGET().AddHoliday(April26th2004);
        Calendar removed = new TARGET().RemoveHoliday(May1st2004);
        Assert.Equal(new TARGET().Name, added.Name);
        Assert.NotEqual(new TARGET(), added);
        Assert.NotEqual(new TARGET(), removed);
        Assert.NotEqual(added, removed);

        // Undoing an edit gives back an equal calendar.
        Assert.Equal(new TARGET(), added.RemoveHoliday(April26th2004));
        Assert.Equal(new TARGET().GetHashCode(), added.RemoveHoliday(April26th2004).GetHashCode());
        Assert.Equal(new TARGET(), removed.AddHoliday(May1st2004));
        Assert.Equal(new TARGET(), a.ResetAddedAndRemovedHolidays());

        // Different names: different calendars.
        Assert.NotEqual<Calendar>(new UnitedStates(UnitedStatesMarket.Settlement), new UnitedStates(UnitedStatesMarket.NYSE));
        Assert.NotEqual<Calendar>(new UnitedKingdom(), new TARGET());
        Assert.False(new TARGET().Equals(null));
    }

    [Fact]
    public void AddHolidayRemoveHolidayAndResetLeaveTheReceiverUnchanged()
    {
        Calendar original = new TARGET();

        Calendar added = original.AddHoliday(April26th2004);
        Assert.IsType<TARGET>(added);
        Assert.Empty(original.AddedHolidays);
        Assert.True(original.IsBusinessDay(April26th2004));
        Assert.Equal(new[] { April26th2004 }, added.AddedHolidays);
        Assert.True(added.IsHoliday(April26th2004));

        Calendar removed = added.RemoveHoliday(May1st2004);
        Assert.Empty(added.RemovedHolidays);
        Assert.True(added.IsHoliday(May1st2004));
        Assert.Equal(new[] { May1st2004 }, removed.RemovedHolidays);
        Assert.True(removed.IsBusinessDay(May1st2004));

        Calendar reset = removed.ResetAddedAndRemovedHolidays();
        Assert.Equal(new[] { April26th2004 }, removed.AddedHolidays);
        Assert.Equal(new[] { May1st2004 }, removed.RemovedHolidays);
        Assert.Empty(reset.AddedHolidays);
        Assert.Empty(reset.RemovedHolidays);
        Assert.Equal(new TARGET(), original);
    }

    // In place of testModifiedCalendars' shared-instance checks (calendars.cpp, lines 147-152 and 160-167), where a new
    // TARGET instance sees an edit made through another one, and restoring the holidays through it restores them for
    // the first.
    [Fact]
    public void ANewTargetAndTheOriginalDontSeeAnEditMadeThroughACopy()
    {
        Date d1 = May1st2004, d2 = April26th2004;
        Calendar c1 = new TARGET();
        Calendar edited = c1.RemoveHoliday(d1).AddHoliday(d2);

        Assert.True(edited.IsBusinessDay(d1));
        Assert.True(edited.IsHoliday(d2));

        // The original and a new instance keep TARGET's own holidays.
        Calendar c3 = new TARGET();
        foreach (Calendar c in new[] { c1, c3 })
        {
            Assert.True(c.IsHoliday(d1));
            Assert.True(c.IsBusinessDay(d2));
            Assert.Empty(c.AddedHolidays);
            Assert.Empty(c.RemovedHolidays);
        }

        // Editing the new instance back doesn't change the edited copy.
        Calendar restored = c3.AddHoliday(d1).RemoveHoliday(d2);
        Assert.Equal(new TARGET(), restored);
        Assert.True(edited.IsBusinessDay(d1));
        Assert.True(edited.IsHoliday(d2));
    }

    [Fact]
    public void JointCalendarsWhoseComponentsDifferByAnAddedHolidayAreUnequal()
    {
        Calendar target = new TARGET();
        Calendar editedTarget = target.AddHoliday(April26th2004);
        Calendar j1 = new JointCalendar(target, new UnitedKingdom());
        Calendar j2 = new JointCalendar(editedTarget, new UnitedKingdom());

        // QuantLib would call them equal: the joint calendar's name is built from its components' names only.
        Assert.Equal(j1.Name, j2.Name);
        Assert.Empty(j1.AddedHolidays);
        Assert.Empty(j2.AddedHolidays);
        Assert.NotEqual(j1.IsBusinessDay(April26th2004), j2.IsBusinessDay(April26th2004));

        Assert.NotEqual(j1, j2);
        Assert.False(j1 == j2);
        Assert.False(j1.Equals((object)j2));

        // The same holds for a component's component.
        Calendar us = new UnitedStates(UnitedStatesMarket.Settlement);
        Assert.NotEqual<Calendar>(new JointCalendar(j1, us), new JointCalendar(j2, us));
    }

    [Fact]
    public void JointCalendarsBuiltFromEqualComponentsAreEqualWithEqualHashes()
    {
        Calendar Build() => new JointCalendar(
            new TARGET().AddHoliday(April26th2004),
            new UnitedKingdom(UnitedKingdomMarket.Exchange),
            new UnitedStates(UnitedStatesMarket.NYSE),
            JointCalendarRule.JoinBusinessDays);

        Calendar j1 = Build(), j2 = Build();
        Assert.NotSame(j1, j2);
        Assert.Equal(j1, j2);
        Assert.True(j1 == j2);
        Assert.Equal(j1.GetHashCode(), j2.GetHashCode());

        // The constructor taking a sequence builds the same calendar as the one taking three.
        Calendar fromSequence = new JointCalendar(
            [new TARGET().AddHoliday(April26th2004), new UnitedKingdom(UnitedKingdomMarket.Exchange), new UnitedStates(UnitedStatesMarket.NYSE)],
            JointCalendarRule.JoinBusinessDays);
        Assert.Equal(j1, fromSequence);
        Assert.Equal(j1.GetHashCode(), fromSequence.GetHashCode());

        // The joint calendar's own added holidays count too, as for any calendar.
        Calendar edited = j1.AddHoliday(April27th2004);
        Assert.IsType<JointCalendar>(edited);
        Assert.NotEqual(j1, edited);
        Assert.Equal(j1, edited.RemoveHoliday(April27th2004));
        Assert.Equal(j1.GetHashCode(), edited.RemoveHoliday(April27th2004).GetHashCode());

        // A joint calendar equals only a joint calendar.
        Assert.NotEqual<Calendar>(new TARGET(), new JointCalendar([new TARGET()]));
    }

    [Fact]
    public void CalendarsOfDifferentKindsAreUnequalFromEitherSide()
    {
        Calendar target = new TARGET();
        Calendar lookalike = new Lookalike(target.Name);

        Assert.False(target.Equals(lookalike));
        Assert.False(lookalike.Equals(target));
        Assert.False(target == lookalike);

        // A joint calendar's name with another kind of calendar behind it.
        Calendar joint = new JointCalendar(new TARGET(), new NullCalendar());
        Calendar jointLookalike = new Lookalike(joint.Name);
        Assert.False(joint.Equals(jointLookalike));
        Assert.False(jointLookalike.Equals(joint));
    }

    // A calendar subclass that copies another calendar's name.
    private sealed class Lookalike(string name) : Calendar
    {
        public override string Name => name;

        public override bool IsWeekend(Weekday w) => false;

        protected override bool IsBusinessDayCore(Date d) => true;
    }
}
