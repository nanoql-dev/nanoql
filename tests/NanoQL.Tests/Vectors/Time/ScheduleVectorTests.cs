// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>The schedule vector, <c>tests/vectors/time/schedule.json</c>.</summary>
[Trait("Oracle", "QuantLib")]
public class ScheduleVectorTests
{
    [Fact]
    public void ScheduleVector()
    {
        var file = VectorFile.Load("time/schedule");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            switch (c.String("kind"))
            {
                case "rule":
                    Compare(check, c, "dates", "isRegular", () => new Schedule(
                        c.NullableDateInput("effectiveDate"),
                        c.DateInput("terminationDate"),
                        Tenor(c),
                        CalendarNamed(c.String("calendar")),
                        (BusinessDayConvention)c.Int("convention"),
                        (BusinessDayConvention)c.Int("terminationDateConvention"),
                        (DateGenerationRule)c.Int("rule"),
                        c.Bool("endOfMonth"),
                        context,
                        c.NullableDateInput("firstDate"),
                        c.NullableDateInput("nextToLastDate")));
                    break;
                case "make":
                    MakeSchedule builder = new MakeSchedule()
                        .From(c.DateInput("effectiveDate"))
                        .To(c.DateInput("terminationDate"))
                        .WithTenor(Tenor(c))
                        .WithCalendar(CalendarNamed(c.String("calendar")))
                        .WithConvention((BusinessDayConvention)c.Int("convention"))
                        .EndOfMonth(c.Bool("endOfMonth"));
                    if (c.Inputs.TryGetProperty("rule", out JsonElement rule))
                    {
                        builder = builder.WithRule((DateGenerationRule)rule.GetInt32());
                    }
                    else
                    {
                        builder = c.Bool("forwards") ? builder.Forwards() : builder.Backwards();
                    }

                    Compare(check, c, "dates", "isRegular", builder.ToSchedule);
                    break;
                case "navigationDefault":
                    Schedule navigated = Base(context);
                    check.Output(c, "previousDate", navigated.PreviousDate(null, context));
                    check.Output(c, "nextDate", navigated.NextDate(null, context));
                    break;
                default:
                    Schedule schedule = Base(context);
                    Date[] refs = c.DateInputs("refDates");
                    check.Output(c, "previousDate", refs.Select(r => schedule.PreviousDate(r, context)).ToArray());
                    check.Output(c, "nextDate", refs.Select(r => schedule.NextDate(r, context)).ToArray());
                    check.Output(c, "afterDates", refs.Select(r => (Func<object?>)(() => schedule.After(r).Dates)).ToArray());
                    check.Output(c, "afterIsRegular", refs.Select(r => (Func<object?>)(() => Regular(schedule.After(r)))).ToArray());
                    check.Output(c, "untilDates", refs.Select(r => (Func<object?>)(() => schedule.Until(r).Dates)).ToArray());
                    check.Output(c, "untilIsRegular", refs.Select(r => (Func<object?>)(() => Regular(schedule.Until(r)))).ToArray());
                    break;
            }
        }

        check.Complete();
    }

    private static void Compare(VectorAssert check, VectorCase c, string dates, string isRegular, Func<Schedule> make)
    {
        Schedule? schedule = null;
        Exception? error = null;
        try
        {
            schedule = make();
        }
        catch (Exception e)
        {
            error = e;
        }

        check.Output(c, dates, () => schedule is not null ? schedule.Dates : throw error!);
        check.Output(c, isRegular, () => schedule is not null ? Regular(schedule) : throw error!);
        if (schedule is { Empty: false })
        {
            // The accessors over the dates the vector checks, and what a rule-based schedule knows.
            Assert.Equal(schedule.Dates[0], schedule.Front());
            Assert.Equal(schedule.Dates[0], schedule.StartDate());
            Assert.Equal(schedule.Dates[^1], schedule.Back());
            Assert.Equal(schedule.Dates[^1], schedule.EndDate());
            var enumerated = new List<Date>();
            foreach (Date d in schedule)
            {
                enumerated.Add(d);
            }

            Assert.Equal(schedule.Dates, enumerated);
            Assert.True(schedule.HasTenor);
            Assert.True(schedule.HasRule);
            Assert.True(schedule.HasEndOfMonth);
            Assert.True(schedule.HasTerminationDateBusinessDayConvention);
            Assert.True(schedule.HasIsRegular);
        }
    }

    private static Schedule Base(EvaluationContext context) => new(
        new Date(31, Month.January, 2026),
        new Date(31, Month.January, 2031),
        new Period(6, TimeUnit.Months),
        new TARGET(),
        BusinessDayConvention.ModifiedFollowing,
        BusinessDayConvention.ModifiedFollowing,
        DateGenerationRule.Backward,
        true,
        context);

    private static object? Regular(Schedule s) => s.HasIsRegular ? s.IsRegular() : null;

    private static Period Tenor(VectorCase c) =>
        new(c.Input("tenor")[0].GetInt32(), (TimeUnit)c.Input("tenor")[1].GetInt32());

    private static Calendar CalendarNamed(string name) => name switch
    {
        "TARGET" => new TARGET(),
        "UnitedStates.Settlement" => new UnitedStates(UnitedStatesMarket.Settlement),
        "NullCalendar" => new NullCalendar(),
        _ => throw new ArgumentException($"unknown calendar {name}", nameof(name)),
    };
}
