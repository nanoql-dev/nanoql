// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Quotes;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="LastFixingQuote"/>'s reference date on a store holding later fixings: QuantLib's is the earlier of the
/// store's last date and the evaluation date (<c>lastfixingquote.cpp:40-43</c>), the evaluation date itself on a store
/// holding later fixings, as a backtest on one full history does, whose fixing is refused on a non-fixing day or
/// forecast where it is missing. nanoql's is the last stored date on or before the evaluation date: Euribor 6M fixed
/// through 30 April 2021, on Saturday 13 March 2021 and Good Friday 2 April 2021, where QuantLib refuses the evaluation
/// date's fixing, nanoql's quote is the last fixing available, on the 12th and the 1st, and is QuantLib's own quote on
/// the store cut at the evaluation date.
/// </summary>
public class LastFixingQuoteTests
{
    private static readonly Date First = new(4, Month.January, 2021);
    private static readonly Date Last = new(30, Month.April, 2021);

    // Every TARGET business day's fixing from First to the last date, 0.5% less a hundredth of a basis point a day.
    private static IndexFixings Store(FinancialIndex index, Date last)
    {
        Calendar target = new TARGET();
        var dates = new List<Date>();
        var values = new List<double>();
        for (Date d = First; d <= last; d += 1)
        {
            if (target.IsBusinessDay(d))
            {
                dates.Add(d);
                values.Add(-0.005 - (1e-6 * (d - First)));
            }
        }

        return IndexFixings.Empty.AddFixings(index, dates, values);
    }

    [Theory]
    [InlineData(13, 3, 12)]
    [InlineData(2, 4, 1)]
    public void TheReferenceDateIsTheLastStoredFixingOnOrBeforeTheEvaluationDate(int day, int month, int lastDay)
    {
        var index = new Euribor6M();
        var evaluationDate = new Date(day, (Month)month, 2021);
        var context = new EvaluationContext { EvaluationDate = evaluationDate, Fixings = Store(index, Last) };

        var quote = new LastFixingQuote(index, context);
        var expected = new Date(lastDay, (Month)month, 2021);
        Assert.Equal(expected, quote.ReferenceDate);
        Assert.Equal(index.PastFixing(expected, context), quote.Value);

        // QuantLib's reference date, min(last stored date, evaluation date), is the evaluation date, not a fixing date:
        // its fixing is refused ("Fixing date ... is not valid")
        Date quantLibReference = Last < evaluationDate ? Last : evaluationDate;
        Assert.Equal(evaluationDate, quantLibReference);
        Assert.ThrowsAny<ArgumentException>(() => index.Fixing(quantLibReference, context));

        // QuantLib's own quote on the store cut at the evaluation date reads that last fixing
        var cut = context with { Fixings = Store(index, evaluationDate) };
        var onCut = new LastFixingQuote(index, cut);
        Assert.Equal(expected, onCut.ReferenceDate);
        Assert.Equal(quote.Value, onCut.Value);
    }
}
