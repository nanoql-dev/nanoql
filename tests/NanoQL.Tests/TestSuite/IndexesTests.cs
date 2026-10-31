// SPDX-FileCopyrightText: Copyright (C) 2020 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/indexes.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Replaced: testFixingObservability (Vectors/Indexes/BMAIndexVectorTests.cs)

using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

/// <summary>QuantLib's index tests, <c>test-suite/indexes.cpp</c>, for the indexes nanoql ports.</summary>
[Trait("Oracle", "QuantLib")]
public class IndexesTests
{
    // Adapted: the fixing line 105 adds goes into a store held by the case's evaluation context, which the reads take;
    // line 114's clearHistories() becomes a second context, whose store is empty.
    [Fact]
    public void TestFixingHasHistoricalFixing()
    {
        // Testing if index has historical fixings...

        static void TestCase(string indexName, bool expected, bool testResult)
        {
            if (expected != testResult)
            {
                Assert.Fail($"Historical fixing {(testResult ? string.Empty : "not ")}found for {indexName}.");
            }
        }

        string name;
        bool fixingFound = true;
        bool fixingNotFound = false;

        var euribor3M = new Euribor3M();
        var euribor6M = new Euribor6M();
        var euribor6M_a = new Euribor6M();

        // QuantLib's test runner's global evaluation date.
        var context = new EvaluationContext { EvaluationDate = new Date(16, Month.September, 2015) };
        Date today = context.EvaluationDate;
        while (!euribor6M.IsValidFixingDate(today))
        {
            today--;
        }

        context = context with { Fixings = context.Fixings.AddFixing(euribor6M, today, 0.01) };

        name = euribor3M.Name;
        TestCase(name, fixingNotFound, euribor3M.HasHistoricalFixing(today, context));

        name = euribor6M.Name;
        TestCase(name, fixingFound, euribor6M.HasHistoricalFixing(today, context));
        TestCase(name, fixingFound, euribor6M_a.HasHistoricalFixing(today, context));

        context = context with { Fixings = IndexFixings.Empty };

        name = euribor3M.Name;
        TestCase(name, fixingNotFound, euribor3M.HasHistoricalFixing(today, context));

        name = euribor6M.Name;
        TestCase(name, fixingNotFound, euribor6M.HasHistoricalFixing(today, context));
        TestCase(name, fixingNotFound, euribor6M_a.HasHistoricalFixing(today, context));
    }

    [Fact]
    public void TestTenorNormalization()
    {
        // Testing that interest-rate index tenor is normalized correctly...

        // QuantLib's empty Currency() is a null currency.
        var i12m = new IborIndex(
            "foo", 12 * TimeUnit.Months, 2, null, new TARGET(), BusinessDayConvention.Following, false, new Actual360());
        var i1y = new IborIndex(
            "foo", 1 * TimeUnit.Years, 2, null, new TARGET(), BusinessDayConvention.Following, false, new Actual360());

        if (i12m.Name != i1y.Name)
        {
            Assert.Fail("12M index and 1Y index yield different names");
        }

        var i6d = new IborIndex(
            "foo", 6 * TimeUnit.Days, 2, null, new TARGET(), BusinessDayConvention.Following, false, new Actual360());
        var i7d = new IborIndex(
            "foo", 7 * TimeUnit.Days, 2, null, new TARGET(), BusinessDayConvention.Following, false, new Actual360());

        var testDate = new Date(28, Month.April, 2023);
        Date maturity6d = i6d.MaturityDate(testDate);
        Date maturity7d = i7d.MaturityDate(testDate);

        if (maturity6d >= maturity7d)
        {
            Assert.Fail(
                "inconsistent maturity dates and tenors"
                + $"\n  maturity date for 6-days index: {maturity6d}"
                + $"\n  maturity date for 7-days index: {maturity7d}");
        }
    }

    // Adapted: nanoql's calendars are immutable, so each addHoliday (lines 155-165) builds the calendar it names from the
    // last; QuantLib's empty Currency() (line 168) is null.
    [Fact]
    public void TestCustomIborIndex()
    {
        // Testing CustomIborIndex...

        Calendar fixCal = new BespokeCalendar("Fixings").AddHoliday(new Date(8, Month.January, 2025));

        Calendar valCal = new BespokeCalendar("Value").AddHoliday(new Date(21, Month.January, 2025));

        Calendar matCal = new BespokeCalendar("Maturity")
            .AddHoliday(new Date(7, Month.January, 2025))
            .AddHoliday(new Date(15, Month.January, 2025))
            .AddHoliday(new Date(23, Month.April, 2025))
            .AddHoliday(new Date(30, Month.April, 2025));

        var ibor = new CustomIborIndex(
            "Custom Ibor", 3 * TimeUnit.Months, 2, null, fixCal, valCal, matCal,
            BusinessDayConvention.ModifiedFollowing, true, new Actual360());
        IborIndex iborClone = ibor.Clone(null);

        foreach (IborIndex index in (IborIndex[])[ibor, iborClone])
        {
            var asCustom = Assert.IsType<CustomIborIndex>(index);
            Assert.Equal(fixCal, index.FixingCalendar);
            Assert.Equal(valCal, asCustom.ValueCalendar);
            Assert.Equal(matCal, asCustom.MaturityCalendar);

            ArgumentException e = Assert.Throws<ArgumentException>(
                () => index.ValueDate(new Date(8, Month.January, 2025)));
            Assert.StartsWith("Fixing date January 8th, 2025 is not valid", e.Message, StringComparison.Ordinal);

            Assert.Equal(new Date(9, Month.January, 2025), index.ValueDate(new Date(7, Month.January, 2025)));
            Assert.Equal(new Date(16, Month.January, 2025), index.ValueDate(new Date(13, Month.January, 2025)));
            Assert.Equal(new Date(23, Month.January, 2025), index.ValueDate(new Date(20, Month.January, 2025)));

            Assert.Equal(new Date(20, Month.January, 2025), index.FixingDate(new Date(23, Month.January, 2025)));
            Assert.Equal(new Date(14, Month.January, 2025), index.FixingDate(new Date(16, Month.January, 2025)));
            Assert.Equal(new Date(7, Month.January, 2025), index.FixingDate(new Date(10, Month.January, 2025)));

            Assert.Equal(new Date(24, Month.April, 2025), index.MaturityDate(new Date(23, Month.January, 2025)));
            Assert.Equal(new Date(29, Month.April, 2025), index.MaturityDate(new Date(30, Month.January, 2025)));
            Assert.Equal(new Date(31, Month.May, 2025), index.MaturityDate(new Date(28, Month.February, 2025)));
        }
    }

    [Fact]
    public void TestCdiIndex()
    {
        // Testing Brazil CDI forecastFixing...
        Date today = new Date(16, Month.September, 2015);
        var context = new EvaluationContext { EvaluationDate = today };
        double flatRate = 0.05;
        YieldTermStructure ts = new FlatForward(today, flatRate, new Business252());

        var cdi = new Cdi(ts);
        Date testFixingDate = new Brazil(BrazilMarket.Settlement).Advance(today, new Period(1, TimeUnit.Months));
        double forecast = cdi.ForecastFixing(testFixingDate, context);

        double discountStart = ts.Discount(testFixingDate);
        double discountEnd = ts.Discount(
            new Brazil(BrazilMarket.Settlement).Advance(testFixingDate, new Period(1, TimeUnit.Days)));

        double approx = Math.Pow(discountStart / discountEnd, 252.0) - 1.0;
        Assert.True(Math.Abs(0.05127 - forecast) < 1e-5, "discrepancy in fixing forecast computation\n");
        Assert.True(
            Math.Abs(approx - forecast) < 1e-6, "discrepancy in fixing forecast computation with approximation\n");
    }

    [Fact]
    public void TestShirIndex()
    {
        // Testing Shir index...

        var shir = new Shir();

        Assert.Equal("Shir", shir.FamilyName);
        Assert.Equal(0, shir.FixingDays);
        Assert.True(shir.Currency == new ILSCurrency());
        Assert.True(shir.FixingCalendar == new Israel(IsraelMarket.SHIR));
        Assert.True(shir.DayCounter == new Actual365Fixed());
    }
}
