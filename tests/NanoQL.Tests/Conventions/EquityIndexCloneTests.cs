// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="EquityIndex.Clone"/> is QuantLib's <c>clone</c>: a new index with the same name, fixing calendar and
/// currency on the curves and spot it is given (<c>equityindex.cpp:92-97</c>), <see langword="null"/> for QuantLib's
/// empty handle. Relinking a handle in QuantLib changes the index that holds it; here the original index is unchanged,
/// and it shares its stored fixings with the clone, which are keyed by its name.
/// </summary>
public class EquityIndexCloneTests
{
    [Fact]
    public void ACloneKeepsTheIndexAndTakesTheCurvesAndSpotGiven()
    {
        Date today = new(27, Month.January, 2023);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure r = new FlatForward(today, 0.03, new Actual365Fixed());
        YieldTermStructure q = new FlatForward(today, 0.01, new Actual365Fixed());
        YieldTermStructure r2 = new FlatForward(today, 0.04, new Actual365Fixed());
        var index = new EquityIndex("eqIndex", new TARGET(), new EURCurrency(), r, q, 8700.0);
        context = context with { Fixings = context.Fixings.AddFixing(index, today, 8690.0) };

        EquityIndex clone = index.Clone(r2, null, null);

        Assert.Equal(index.Name, clone.Name);
        Assert.Equal(index.FixingCalendar, clone.FixingCalendar);
        Assert.Equal(index.Currency, clone.Currency);
        Assert.Same(r2, clone.EquityInterestRateCurve);
        Assert.Null(clone.EquityDividendCurve);
        Assert.Null(clone.Spot);

        // the original is unchanged
        Assert.Same(r, index.EquityInterestRateCurve);
        Assert.Same(q, index.EquityDividendCurve);
        Assert.Equal(8700.0, index.Spot);

        // the clone reads the index's stored fixings, and forecasts from today's on its own curve
        Date forecastDate = new(20, Month.May, 2030);
        Assert.Equal(8690.0, clone.Fixing(today, context));
        Assert.Equal(8690.0 / r2.Discount(forecastDate), clone.Fixing(forecastDate, context));
        Assert.Equal(8700.0 * q.Discount(forecastDate) / r.Discount(forecastDate), index.Fixing(forecastDate, context));
    }
}
