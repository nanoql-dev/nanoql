// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The fixing store's hook, <see cref="FinancialIndex.FixingDates"/>, QuantLib's virtual <c>Index::addFixing</c>:
/// <see cref="IndexFixings.AddFixing"/> writes a fixing on every date the index returns, the date alone by default and
/// every day of the period for an inflation index, an extension point an index of one's own overrides; the bulk writers
/// store the dates they are given, as QuantLib's non-virtual <c>addFixings</c> does, and a refused date in a period
/// stores nothing.
/// </summary>
public class IndexFixingsHookTests
{
    [Fact]
    public void AnIndexOfOnesOwnWritesItsFixingOnTheDatesItReturns()
    {
        var weekly = new WeekIndex();
        IndexFixings store = IndexFixings.Empty.AddFixing(weekly, new Date(15, Month.May, 2024), 1.5);

        var stored = new EvaluationContext { EvaluationDate = new Date(15, Month.May, 2024), Fixings = store };
        Assert.Equal([.. Enumerable.Range(0, 7).Select(i => new Date(13, Month.May, 2024) + i)], weekly.TimeSeries(stored).Dates());

        // the default hook: the date alone
        var plain = new PlainIndex();
        IndexFixings single = IndexFixings.Empty.AddFixing(plain, new Date(15, Month.May, 2024), 2.0);
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.May, 2024), Fixings = single };
        Assert.Equal([new Date(15, Month.May, 2024)], plain.TimeSeries(context).Dates());

        // a conflicting fixing on the returned dates refuses the whole write, naming the last date, as QuantLib's does
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => store.AddFixing(weekly, new Date(17, Month.May, 2024), 1.6));
        Assert.Equal("fixingDate", e.ParamName);
        Assert.StartsWith(
            "At least one duplicated fixing provided: May 19th, 2024, 1.6 while 1.5 value is already present",
            e.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnInflationIndexsBulkWriteStoresTheDatesItIsGiven()
    {
        var index = new UKRPI();
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.June, 2024) };
        IndexFixings bulk = context.Fixings.AddFixings(index, [new Date(1, Month.March, 2024)], [350.0]);
        IndexFixings single = context.Fixings.AddFixing(index, new Date(1, Month.March, 2024), 350.0);

        Assert.Single(index.TimeSeries(context with { Fixings = bulk }).Dates());
        Assert.Equal(31, index.TimeSeries(context with { Fixings = single }).Dates().Length);
        Assert.Equal(350.0, index.PastFixing(new Date(20, Month.March, 2024), context with { Fixings = bulk }));
    }

    // An index whose fixing covers the week from Monday to Sunday.
    private sealed class WeekIndex : FinancialIndex
    {
        public override string Name => "weekly test index";

        public override Calendar FixingCalendar { get; } = new NullCalendar();

        public override bool IsValidFixingDate(Date fixingDate) => true;

        public override double Fixing(Date fixingDate, EvaluationContext context, bool forecastTodaysFixing = false) =>
            throw new InvalidOperationException("no forecast");

        protected override IEnumerable<Date> FixingDates(Date fixingDate)
        {
            Date monday = fixingDate - ((int)fixingDate.Weekday + 5) % 7;
            return Enumerable.Range(0, 7).Select(i => monday + i);
        }
    }

    private sealed class PlainIndex : FinancialIndex
    {
        public override string Name => "plain test index";

        public override Calendar FixingCalendar { get; } = new NullCalendar();

        public override bool IsValidFixingDate(Date fixingDate) => true;

        public override double Fixing(Date fixingDate, EvaluationContext context, bool forecastTodaysFixing = false) =>
            throw new InvalidOperationException("no forecast");
    }
}
