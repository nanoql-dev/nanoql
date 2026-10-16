// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's null date, <c>Date()</c>, is a <c>Date?</c> null in nanoql, and <c>default(Date)</c> (serial 0) is never a
/// valid date: no constructor produces it, and public members given it throw <see cref="ArgumentException"/>, whether
/// they read it or not. QuantLib's <c>nullDate</c> case, which checks that the null date's serial number and hash can
/// be read, becomes the last test.
/// </summary>
public class NullDateTests
{
    [Fact]
    public void NoConstructorProducesSerialZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Date(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Date(366));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Date(0, Month.January, 1901));
        Assert.Throws<ArgumentOutOfRangeException>(() => Date.FromDateOnly(new DateOnly(1899, 12, 30)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Date.MinDate - 1);
    }

    [Fact]
    public void MembersGivenTheDefaultDateThrow()
    {
        Date d = default;

        Assert.Throws<ArgumentException>(() => d.Year);
        Assert.Throws<ArgumentException>(() => d.Month);
        Assert.Throws<ArgumentException>(() => d.DayOfMonth);
        Assert.Throws<ArgumentException>(() => d + 1);
        Assert.Throws<ArgumentException>(() => d - Date.MinDate);
        Assert.Throws<ArgumentException>(() => d.ToDateOnly());
        Assert.Throws<ArgumentException>(() => new NullCalendar().Adjust(d));
        Assert.Throws<ArgumentException>(() => new NullCalendar().Advance(d, 1, TimeUnit.Days));
        Assert.Throws<ArgumentException>(() => new EvaluationContext { EvaluationDate = d });
    }

    [Fact]
    public void MembersThatDontReadTheDefaultDateRefuseItToo()
    {
        // Without a check on entry, these would answer for serial 0 instead of throwing: NullCalendar never reads the
        // date, the schedule's navigation only compares it, and some day counters ignore the reference dates.
        Date d = default;
        Date valid = new(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = valid };
        Calendar[] calendars = [new NullCalendar(), new JointCalendar(new NullCalendar(), new NullCalendar()), new TARGET()];
        foreach (Calendar calendar in calendars)
        {
            AssertParam("d", () => calendar.IsBusinessDay(d));
            AssertParam("d", () => calendar.IsHoliday(d));
            AssertParam("d", () => calendar.AddHoliday(d));
            AssertParam("d", () => calendar.RemoveHoliday(d));
            AssertParam("from", () => calendar.BusinessDaysBetween(d, d));
            AssertParam("to", () => calendar.BusinessDaysBetween(valid, d));
            AssertParam("from", () => calendar.HolidayList(d, valid));
            AssertParam("to", () => calendar.HolidayList(valid, d));
            AssertParam("from", () => calendar.BusinessDayList(d, valid));
            AssertParam("to", () => calendar.BusinessDayList(valid, d));
        }

        AssertParam("dates", () => new Schedule([d, d, valid]));
        var schedule = new Schedule([valid, valid + 180, valid + 365]);
        AssertParam("refDate", () => schedule.LowerBound(context, d));
        AssertParam("refDate", () => schedule.NextDate(d, context));
        AssertParam("refDate", () => schedule.PreviousDate(d, context));
        AssertParam("truncationDate", () => schedule.After(d));
        AssertParam("truncationDate", () => schedule.Until(d));
        AssertParam("firstDate", () => new MakeSchedule().From(valid).To(valid + 730)
            .WithFrequency(Frequency.Annual).WithFirstDate(d).ToSchedule());
        AssertParam("nextToLastDate", () => new MakeSchedule().From(valid).To(valid + 730)
            .WithFrequency(Frequency.Annual).WithNextToLastDate(d).ToSchedule());

        DayCounter[] dayCounters =
        [
            new Actual360(),
            new Actual365Fixed(),
            new Actual365Fixed(Actual365FixedConvention.Canadian),
            new Thirty360(Thirty360Convention.USA),
            new ActualActual(ActualActualConvention.ISDA),
            new ActualActual(ActualActualConvention.ISMA),
            new ActualActual(ActualActualConvention.ISMA, schedule),
        ];
        foreach (DayCounter dayCounter in dayCounters)
        {
            AssertParam("d1", () => dayCounter.YearFraction(d, d));
            AssertParam("d2", () => dayCounter.YearFraction(valid, d));
            AssertParam("refPeriodStart", () => dayCounter.YearFraction(valid, valid + 30, d, valid + 180));
            AssertParam("refPeriodEnd", () => dayCounter.YearFraction(valid, valid + 30, valid, d));
        }

        AssertParam("terminationDate", () => new Thirty360(Thirty360Convention.ISDA, d));
    }

    [Fact]
    public void AMissingDateIsNull()
    {
        Date? missing = null;

        Assert.Equal("null date", DateFunctions.LongDate(missing));
        Assert.Equal("null date", DateFunctions.IsoDate(missing));
        Assert.Equal("null date", DateFunctions.ShortDate(missing));
    }

    [Fact]
    public void SerialNumberEqualityHashAndTextOfTheDefaultDateDontThrow()
    {
        // QuantLib's nullDate case: the null date's serial number and hash can be read.
        Date d = default;

        Assert.Equal(0, d.SerialNumber);
        _ = d.GetHashCode();
        Assert.True(d == default(Date));
        Assert.True(d < Date.MinDate);
        Assert.Equal("null date", d.ToString());
    }

    private static void AssertParam(string paramName, Action action) =>
        Assert.Equal(paramName, Assert.Throws<ArgumentException>(action).ParamName);
}
