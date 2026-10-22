// SPDX-FileCopyrightText: Copyright (C) 2023 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/equityindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Not ported: testFixingObservability (observer notifications, which an immutable index has no use for, R1)

using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// QuantLib's fixture links relinkable handles that the index holds; here the index is built on the curves and spot,
// and a handle linked anew is a clone of the index on the new value. The fixture's evaluation date and today's fixing
// are its context.
[Trait("Oracle", "QuantLib")]
public class EquityIndexTests
{
    [Fact]
    public void TestTodaysFixing()
    {
        // Testing today's fixing...
        var vars = new CommonVars();
        const Real tolerance = 1.0e-8;

        const Real historicalIndex = 8690.0;
        Real todaysFixing = vars.EquityIndex.Fixing(vars.Today, vars.Context);

        Assert.False(
            Math.Abs(todaysFixing - historicalIndex) > tolerance,
            $"today's fixing should be equal to historical index\n    actual fixing:    {todaysFixing}\n"
            + $"    expected fixing:    {historicalIndex}\n");

        const Real spot = 8700.0;
        Real forecastedFixing = vars.EquityIndex.Fixing(vars.Today, vars.Context, true);

        Assert.False(
            Math.Abs(forecastedFixing - spot) > tolerance,
            $"today's fixing forecast should be equal to spot\n    actual forecast:    {forecastedFixing}\n"
            + $"    expected forecast:    {spot}\n");
    }

    [Fact]
    public void TestTodaysFixingWithSpotAsProxy()
    {
        // Testing today's fixing with spot as proxy...
        var vars = new CommonVars(false);
        const Real tolerance = 1.0e-8;

        const Real spot = 8700.0;
        Real fixing = vars.EquityIndex.Fixing(vars.Today, vars.Context);

        Assert.False(
            Math.Abs(fixing - spot) > tolerance,
            $"today's fixing should be equal to spot when historical index not added\n    actual fixing:    {fixing}\n"
            + $"    expected fixing:    {spot}\n");
    }

    [Fact]
    public void TestFixingForecast()
    {
        // Testing fixing forecast...
        var vars = new CommonVars();
        const Real tolerance = 1.0e-8;

        var forecastedDate = new Date(20, May, 2030);

        Real forecast = vars.EquityIndex.Fixing(forecastedDate, vars.Context);
        Real expectedForecast = vars.Spot * vars.DividendCurve.Discount(forecastedDate)
            / vars.InterestCurve.Discount(forecastedDate);

        Assert.False(
            Math.Abs(forecast - expectedForecast) > tolerance,
            $"could not replicate index forecast\n    actual forecast:    {forecast}\n"
            + $"    expected forecast:    {expectedForecast}\n");
    }

    [Fact]
    public void TestFixingForecastWithoutDividend()
    {
        // Testing fixing forecast without dividend...
        var vars = new CommonVars();
        const Real tolerance = 1.0e-8;

        var forecastedDate = new Date(20, May, 2030);

        EquityIndex equityIndexExDiv = vars.EquityIndex.Clone(vars.InterestCurve, null, vars.Spot);

        Real forecast = equityIndexExDiv.Fixing(forecastedDate, vars.Context);
        Real expectedForecast = vars.Spot / vars.InterestCurve.Discount(forecastedDate);

        Assert.False(
            Math.Abs(forecast - expectedForecast) > tolerance,
            $"could not replicate index forecast without dividend\n    actual forecast:    {forecast}\n"
            + $"    expected forecast:    {expectedForecast}\n");
    }

    [Fact]
    public void TestFixingForecastWithoutSpot()
    {
        // Testing fixing forecast without spot handle...
        var vars = new CommonVars();
        const Real tolerance = 1.0e-8;

        var forecastedDate = new Date(20, May, 2030);

        EquityIndex equityIndexExSpot = vars.EquityIndex.Clone(vars.InterestCurve, vars.DividendCurve, null);

        Real forecast = equityIndexExSpot.Fixing(forecastedDate, vars.Context);
        Real expectedForecast = equityIndexExSpot.PastFixing(vars.Today, vars.Context)!.Value
            * vars.DividendCurve.Discount(forecastedDate)
            / vars.InterestCurve.Discount(forecastedDate);

        Assert.False(
            Math.Abs(forecast - expectedForecast) > tolerance,
            $"could not replicate index forecast without spot handle\n    actual forecast:    {forecast}\n"
            + $"    expected forecast:    {expectedForecast}\n");
    }

    [Fact]
    public void TestFixingForecastWithoutSpotAndHistoricalFixing()
    {
        // Testing fixing forecast without spot handle and historical fixing...
        var vars = new CommonVars(false);

        var forecastedDate = new Date(20, May, 2030);

        EquityIndex equityIndexExSpot = vars.EquityIndex.Clone(vars.InterestCurve, vars.DividendCurve, null);

        CheckExpectedErrorMessage(
            () => equityIndexExSpot.Fixing(forecastedDate, vars.Context),
            "Cannot forecast equity index, missing both spot and historical index");
    }

    // Adapted: the spot handle linked to a new quote and back (lines 194 and 201) is a clone of the index on each
    // spot, whose spot is checked.
    [Fact]
    public void TestSpotChange()
    {
        // Testing spot change...
        var vars = new CommonVars();
        const Real tolerance = 1.0e-8;

        const Real newSpot = 9000.0;
        EquityIndex equityIndex = vars.EquityIndex.Clone(vars.InterestCurve, vars.DividendCurve, newSpot);

        Assert.False(
            Math.Abs(newSpot - equityIndex.Spot!.Value) > tolerance,
            $"could not re-link spot quote to new value\n    actual spot:    {equityIndex.Spot}\n"
            + $"    expected spot:    {newSpot}\n");

        equityIndex = equityIndex.Clone(vars.InterestCurve, vars.DividendCurve, vars.Spot);

        Assert.False(
            Math.Abs(vars.Spot - equityIndex.Spot!.Value) > tolerance,
            $"could not re-link spot quote back to old value\n    actual spot:    {equityIndex.Spot}\n"
            + $"    expected spot:    {vars.Spot}\n");
    }

    [Fact]
    public void TestErrorWhenInvalidFixingDate()
    {
        // Testing error when invalid fixing date is used...
        var vars = new CommonVars();

        CheckExpectedErrorMessage(
            () => vars.EquityIndex.Fixing(new Date(1, January, 2023), vars.Context),
            "Fixing date January 1st, 2023 is not valid");
    }

    [Fact]
    public void TestErrorWhenFixingMissing()
    {
        // Testing error when required fixing is missing...
        var vars = new CommonVars();

        CheckExpectedErrorMessage(
            () => vars.EquityIndex.Fixing(new Date(2, January, 2023), vars.Context),
            "Missing eqIndex fixing for January 2nd, 2023");
    }

    [Fact]
    public void TestErrorWhenInterestHandleMissing()
    {
        // Testing error when interest handle is missing...
        var vars = new CommonVars();

        var forecastedDate = new Date(20, May, 2030);

        EquityIndex equityIndexExDiv = vars.EquityIndex.Clone(null, null, null);

        CheckExpectedErrorMessage(
            () => equityIndexExDiv.Fixing(forecastedDate, vars.Context),
            "null interest rate term structure set to this instance of eqIndex");
    }

    // Adapted: the evaluation date set to a Saturday (line 273) is a new context, and the fixture's curves, which move
    // with the evaluation date (flatRate(rate, dc)), are built anew in it.
    [Fact]
    public void TestNoErrorIfTodayIsNotBusinessDay()
    {
        // Testing that no error is thrown if today is not a business day...
        var vars = new CommonVars();

        var today = new Date(28, January, 2023);
        var forecastedDate = new Date(20, May, 2030);

        EvaluationContext context = vars.Context with { EvaluationDate = today };
        EquityIndex equityIndex = vars.EquityIndex.Clone(
            FlatRate(0.03, vars.DayCount, context), FlatRate(0.01, vars.DayCount, context), vars.Spot);

        Exception? e = Record.Exception(() => equityIndex.Fixing(forecastedDate, context));
        Assert.Null(e);
    }

    // The test suite's flatRate(forward, dc): a flat forward from the evaluation date.
    private static YieldTermStructure FlatRate(Rate forward, DayCounter dc, EvaluationContext context) =>
        FlatForward.Create(0, new NullCalendar(), forward, dc, context);

    // BOOST_CHECK_EXCEPTION(..., Error, ExpectedErrorMessage(expected)): QuantLib's Error is an ArgumentException or a
    // NanoQLException in nanoql, its message containing the expected text.
    private static void CheckExpectedErrorMessage(Action action, string expected)
    {
        Exception? e = Record.Exception(action);
        if (e is not (ArgumentException or NanoQLException))
        {
            Assert.Fail(
                $"expected an ArgumentException or a NanoQLException, got {e?.GetType().Name ?? "no exception"}");
        }

        Assert.True(
            e.Message.Contains(expected, StringComparison.Ordinal),
            $"Error expected to contain: '{expected}'. Actual error is: '{e.Message}'.");
    }

    private sealed class CommonVars
    {
        public CommonVars(bool addTodaysFixing = true)
        {
            Calendar = new TARGET();
            DayCount = new Actual365Fixed();

            Today = Calendar.Adjust(new Date(27, January, 2023));
            var context = new EvaluationContext { EvaluationDate = Today };

            InterestCurve = FlatRate(0.03, DayCount, context);
            DividendCurve = FlatRate(0.01, DayCount, context);

            Spot = 8700.0;

            EquityIndex = new EquityIndex(
                "eqIndex", Calendar, new EURCurrency(), InterestCurve, DividendCurve, Spot);

            if (addTodaysFixing)
            {
                context = context with { Fixings = context.Fixings.AddFixing(EquityIndex, Today, 8690.0) };
            }

            Context = context;
        }

        public Date Today { get; }

        public Calendar Calendar { get; }

        public DayCounter DayCount { get; }

        public EquityIndex EquityIndex { get; }

        public YieldTermStructure InterestCurve { get; }

        public YieldTermStructure DividendCurve { get; }

        public Real Spot { get; }

        public EvaluationContext Context { get; }
    }
}
