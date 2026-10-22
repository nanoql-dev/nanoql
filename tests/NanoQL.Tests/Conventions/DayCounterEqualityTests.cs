// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Day counters are equal when they are of the same type and name and, where a count reads data the name leaves out,
/// that data is equal: <see cref="Business252"/>'s calendar, as calendars compare (by type, name, and added and removed
/// holidays), <see cref="ActualActual"/>'s period dates under the ISMA and Bond conventions with a schedule, and
/// <see cref="Thirty360"/>'s termination date under the ISDA convention. QuantLib compares names alone
/// (<c>daycounter.hpp:135-138</c>), so its day counters equal by name count differently. Equal day counters hash alike,
/// and neither <c>Equals</c> nor <c>GetHashCode</c> throws.
/// </summary>
public class DayCounterEqualityTests
{
    [Fact]
    public void ABusiness252OnAnEditedCalendarIsUnequalToOneOnTheUnedited()
    {
        var plain = new Business252(new Brazil());
        var edited = new Business252(new Brazil().AddHoliday(new Date(13, Month.March, 2024)));
        Date d1 = new(1, Month.January, 2024), d2 = new(1, Month.January, 2025);

        // QuantLib: equal, their names "Business/252(Brazil)", and the cache counts the edited calendar's days as the
        // first count did
        Assert.Equal(plain.Name, edited.Name);
        Assert.NotEqual(plain.DayCount(d1, d2), edited.DayCount(d1, d2));
        Assert.Equal(edited.DayCount(d1, d2) + 1, plain.DayCount(d1, d2));
        Assert.False(plain == edited);
        Assert.True(plain != edited);
        Assert.False(plain.Equals((object)edited));
        Assert.False(edited.Equals(plain));
    }

    [Fact]
    public void EqualDayCountersHashAlike()
    {
        var a = new Business252(new Brazil().AddHoliday(new Date(13, Month.March, 2024)));
        var b = new Business252(new Brazil().AddHoliday(new Date(13, Month.March, 2024)));
        Assert.True(a == b);
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        // the default calendar is Brazil's settlement calendar
        Assert.True(new Business252() == new Business252(new Brazil()));
        Assert.Equal(new Business252().GetHashCode(), new Business252(new Brazil()).GetHashCode());
        Assert.True(new Business252(new Brazil(BrazilMarket.Exchange)) != new Business252());
    }

    [Fact]
    public void ADayCounterOfAnotherTypeWithTheSameNameIsUnequalBothWays()
    {
        DayCounter actual360 = new Actual360();
        DayCounter impostor = new NamedLike("Actual/360");
        DayCounter business252 = new Business252();
        DayCounter impostor252 = new NamedLike(business252.Name);

        Assert.Equal(actual360.Name, impostor.Name);
        Assert.False(actual360 == impostor);
        Assert.False(impostor == actual360);
        Assert.False(business252 == impostor252);
        Assert.False(impostor252 == business252);
        Assert.True(actual360 == new Actual360());
        Assert.False(actual360 == business252);
    }

    [Fact]
    public void ActualActualIsmaComparesItsPeriodDates()
    {
        Schedule semiannual = MakeSchedule(6), quarterly = MakeSchedule(3);
        Date d1 = new(15, Month.March, 2026), d2 = new(15, Month.September, 2026);

        foreach (ActualActualConvention c in (ActualActualConvention[])[ActualActualConvention.ISMA, ActualActualConvention.Bond])
        {
            var overSemiannual = new ActualActual(c, semiannual);
            var overQuarterly = new ActualActual(c, quarterly);

            // QuantLib: equal by name, their counts 0.5054948354552006 and 0.5045893719806763
            Assert.Equal(0.5054948354552006, overSemiannual.YearFraction(d1, d2));
            Assert.Equal(0.5045893719806763, overQuarterly.YearFraction(d1, d2));
            Assert.Equal(overSemiannual.Name, overQuarterly.Name);
            Assert.False(overSemiannual == overQuarterly);
            Assert.False(overQuarterly == overSemiannual);

            // two schedules with the same dates
            var again = new ActualActual(c, MakeSchedule(6));
            Assert.True(overSemiannual == again);
            Assert.Equal(overSemiannual.GetHashCode(), again.GetHashCode());

            // with no schedule (QuantLib: equal, its count 0.5), unequal to one with a schedule, equal to another with
            // none
            var none = new ActualActual(c);
            Assert.Equal(0.5, none.YearFraction(d1, d2));
            Assert.False(none == overSemiannual);
            Assert.False(overSemiannual == none);
            Assert.True(none == new ActualActual(c));
            Assert.Equal(none.GetHashCode(), new ActualActual(c).GetHashCode());
        }

        // the ISMA and Bond conventions share an implementation and a name
        Assert.True(new ActualActual(ActualActualConvention.ISMA, semiannual) == new ActualActual(ActualActualConvention.Bond, semiannual));
    }

    [Fact]
    public void Thirty360IsdaComparesItsTerminationDate()
    {
        var ending2027 = new Thirty360(Thirty360Convention.ISDA, new Date(28, Month.February, 2027));
        var ending2030 = new Thirty360(Thirty360Convention.ISDA, new Date(28, Month.February, 2030));
        Date d1 = new(30, Month.November, 2026), d2 = new(28, Month.February, 2027);

        // QuantLib: equal by name, their counts 88 and 90
        Assert.Equal(88, ending2027.DayCount(d1, d2));
        Assert.Equal(90, ending2030.DayCount(d1, d2));
        Assert.Equal(ending2027.Name, ending2030.Name);
        Assert.False(ending2027 == ending2030);
        Assert.False(ending2030 == ending2027);

        var again = new Thirty360(Thirty360Convention.German, new Date(28, Month.February, 2027));
        Assert.True(ending2027 == again);
        Assert.Equal(ending2027.GetHashCode(), again.GetHashCode());
        Assert.True(new Thirty360(Thirty360Convention.ISDA) == new Thirty360(Thirty360Convention.ISDA));
        Assert.False(new Thirty360(Thirty360Convention.ISDA) == ending2027);
    }

    [Fact]
    public void ASchedulesDatesAloneCompareWhereItsPeriodsCantBeDerived()
    {
        Date[] dates = [new(15, Month.January, 2026), new(15, Month.July, 2026), new(15, Month.January, 2027)];
        var a1 = new ActualActual(ActualActualConvention.ISMA, new Schedule(dates));
        var a2 = new ActualActual(ActualActualConvention.ISMA, new Schedule(dates));
        var other = new ActualActual(
            ActualActualConvention.ISMA,
            new Schedule([new(15, Month.January, 2026), new(15, Month.July, 2026), new(15, Month.January, 2028)]));

        // QuantLib: the two over one schedule equal; neither Equals nor GetHashCode throws
        Exception? e = Record.Exception(() =>
        {
            Assert.True(a1 == a2);
            Assert.Equal(a1.GetHashCode(), a2.GetHashCode());
            Assert.False(a1 == other);
            Assert.False(other == a1);
        });
        Assert.Null(e);

        // the count refuses, with the schedule's text, as QuantLib's does
        NanoQLException refused = Assert.Throws<NanoQLException>(
            () => a1.YearFraction(new Date(15, Month.March, 2026), new Date(15, Month.September, 2026)));
        Assert.Equal("full interface (end of month) not available", refused.Message);
    }

    [Fact]
    public void ACounterWhoseDatesDeriveIsUnequalToOneOnTheSameDatesAlone()
    {
        Date[] dates =
        [
            new(15, Month.January, 2026), new(15, Month.March, 2026), new(15, Month.September, 2026),
            new(15, Month.March, 2027),
        ];
        bool[] regular = [false, true, true];
        var derived6M = new ActualActual(
            ActualActualConvention.ISMA,
            new Schedule(
                dates, new NullCalendar(), BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted,
                new Period(6, TimeUnit.Months), DateGenerationRule.Backward, false, regular));
        var derived3M = new ActualActual(
            ActualActualConvention.ISMA,
            new Schedule(
                dates, new NullCalendar(), BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted,
                new Period(3, TimeUnit.Months), DateGenerationRule.Backward, false, regular));
        var datesAlone = new ActualActual(ActualActualConvention.ISMA, new Schedule(dates));
        Date d1 = new(20, Month.January, 2026), d2 = new(10, Month.March, 2026);

        // QuantLib: 0.13535911602209943 over the 6M schedule; the dates-only schedule refused at the count
        Assert.Equal(0.13535911602209943, derived6M.YearFraction(d1, d2));
        Assert.NotEqual(derived6M.YearFraction(d1, d2), derived3M.YearFraction(d1, d2));
        Assert.Throws<NanoQLException>(() => datesAlone.YearFraction(d1, d2));

        // a derived counter never equals a refused one, either way, so neither 6M nor 3M equals the dates-only one
        Exception? e = Record.Exception(() =>
        {
            Assert.False(derived6M == datesAlone);
            Assert.False(datesAlone == derived6M);
            Assert.False(derived3M == datesAlone);
            Assert.False(derived6M == derived3M);
            _ = datesAlone.GetHashCode();
        });
        Assert.Null(e);
    }

    private static Schedule MakeSchedule(int months) => new(
        new Date(15, Month.January, 2026),
        new Date(15, Month.January, 2028),
        new Period(months, TimeUnit.Months),
        new NullCalendar(),
        BusinessDayConvention.Unadjusted,
        BusinessDayConvention.Unadjusted,
        DateGenerationRule.Backward,
        false,
        new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) });

    // A day counter of the test's own, named like another.
    private sealed class NamedLike(string name) : DayCounter
    {
        public override string Name => name;

        public override double YearFraction(Date d1, Date d2, Date? refPeriodStart = null, Date? refPeriodEnd = null) =>
            (d2 - d1) / 360.0;
    }
}
