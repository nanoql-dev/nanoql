// SPDX-FileCopyrightText: Copyright (C) 2020 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/indexes.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testCustomIborIndex (CustomIborIndex, BespokeCalendar); testCdiIndex (Cdi, Brazil, Business252); testShirIndex (Shir, Israel, ILSCurrency)
// Replaced: testFixingObservability (Vectors/Indexes/BMAIndexVectorTests.cs)

using NanoQL.Indexes;
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
}
