// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The piecewise inflation curves' named classes, nodes and last time, on <see cref="InflationFixture"/>'s market with
/// four swaps (1, 2, 3 and 5 years, observed three months back, flat): <see cref="PiecewiseZeroInflation"/> with its
/// base date a function of the context, the index's last fixing date, and <see cref="PiecewiseYoYInflation"/> based in
/// April 2024 at 3%, each with QuantLib's nodes and <c>maxTime()</c> on the wheel. A year-on-year swap helper given
/// its start and end dates prices the swap the maturity form builds from the evaluation date, so its curve is the
/// same; and the year-on-year curve's global bootstrap, from the traits' global guess, fits the quotes as the
/// iterative one does.
/// </summary>
public class PiecewiseInflationCurveTests
{
    private static readonly Period Lag = new(3, TimeUnit.Months);

    [Fact]
    public void TheZeroCurveWithALazyBaseDateIsQuantLibs()
    {
        EvaluationContext context = InflationFixture.Context();
        var index = new UKRPI();
        (int Years, double Quote)[] quotes = [(1, 0.030), (2, 0.029), (3, 0.028), (5, 0.027)];
        ZeroCouponInflationSwapHelper[] helpers =
        [
            .. quotes.Select(q => new ZeroCouponInflationSwapHelper(
                q.Quote,
                Lag,
                Maturity(q.Years),
                new UnitedKingdom(),
                BusinessDayConvention.ModifiedFollowing,
                new Actual365Fixed(),
                index,
                CPIInterpolationType.Flat,
                context)),
        ];

        var curve = new PiecewiseZeroInflation(
            InflationFixture.Today, ctx => index.LastFixingDate(ctx), Frequency.Monthly, new Actual365Fixed(), helpers, context);

        // QuantLib's, on the wheel
        Assert.Equal(new Date(1, Month.April, 2024), curve.BaseDate);
        AssertNodes(
            [
                (new Date(1, Month.April, 2024), 0.028089017001558653),
                (new Date(1, Month.March, 2025), 0.028089017001558653),
                (new Date(1, Month.March, 2026), 0.027999568399784704),
                (new Date(1, Month.March, 2027), 0.027314188582799475),
                (new Date(1, Month.March, 2029), 0.026576565470077973),
            ],
            curve.Nodes());
        Assert.Equal(new Date(1, Month.March, 2029), curve.MaxDate);
        Assert.True(Tolerance.Within(4.712328767123288, curve.MaxTime), $"{curve.MaxTime}");
    }

    [Fact]
    public void TheYearOnYearCurveIsQuantLibsWhicheverHelperFormAndBootstrap()
    {
        EvaluationContext context = InflationFixture.Context();
        var index = new YoYInflationIndex(new UKRPI());
        (int Years, double Quote)[] quotes = [(1, 0.031), (2, 0.030), (3, 0.029), (5, 0.028)];
        YearOnYearInflationSwapHelper[] byMaturity =
        [
            .. quotes.Select(q => new YearOnYearInflationSwapHelper(
                q.Quote,
                Lag,
                Maturity(q.Years),
                new UnitedKingdom(),
                BusinessDayConvention.ModifiedFollowing,
                new Actual365Fixed(),
                index,
                CPIInterpolationType.Flat,
                InflationFixture.Nominal(),
                context)),
        ];
        YearOnYearInflationSwapHelper[] byDates =
        [
            .. quotes.Select(q => new YearOnYearInflationSwapHelper(
                q.Quote,
                Lag,
                InflationFixture.Today,
                Maturity(q.Years),
                new UnitedKingdom(),
                BusinessDayConvention.ModifiedFollowing,
                new Actual365Fixed(),
                index,
                CPIInterpolationType.Flat,
                InflationFixture.Nominal())),
        ];
        var baseDate = new Date(1, Month.April, 2024);

        var curve = new PiecewiseYoYInflation(
            InflationFixture.Today, baseDate, 0.03, Frequency.Monthly, new Actual365Fixed(), byMaturity, context);
        var fromDates = new PiecewiseYoYInflation(
            InflationFixture.Today, baseDate, 0.03, Frequency.Monthly, new Actual365Fixed(), byDates, context);
        var global = new PiecewiseYoYInflationCurve<Linear, GlobalBootstrap, YoYInflationTraits>(
            InflationFixture.Today, baseDate, 0.03, Frequency.Monthly, new Actual365Fixed(), byMaturity, context);

        // QuantLib's, on the wheel
        (Date, double)[] expected =
        [
            (baseDate, 0.03),
            (new Date(1, Month.March, 2025), 0.03099728236692534),
            (new Date(1, Month.March, 2026), 0.028962131512836817),
            (new Date(1, Month.March, 2027), 0.026876020868018873),
            (new Date(1, Month.March, 2029), 0.026161754110119916),
        ];
        AssertNodes(expected, curve.Nodes());
        Assert.True(Tolerance.Within(4.712328767123288, curve.MaxTime), $"{curve.MaxTime}");
        Assert.Equal(curve.Nodes(), fromDates.Nodes());

        Assert.True(YoYInflationTraits.HasGlobalGuess);
        Assert.Equal(curve.Dates(), global.Dates());
        foreach (YearOnYearInflationSwapHelper helper in byMaturity)
        {
            double implied = helper.SetTermStructure(global).ImpliedQuote(context);
            Assert.True(Math.Abs(implied - helper.Quote) <= 1e-8, $"{helper.LatestDate}: {implied}");
        }
    }

    private static Date Maturity(int years) =>
        new UnitedKingdom().Advance(InflationFixture.Today, new Period(years, TimeUnit.Years));

    private static void AssertNodes((Date Date, double Value)[] expected, IEnumerable<KeyValuePair<Date, double>> nodes)
    {
        KeyValuePair<Date, double>[] actual = [.. nodes];
        Assert.Equal(expected.Select(e => e.Date), actual.Select(n => n.Key));
        foreach (((Date _, double value), KeyValuePair<Date, double> node) in expected.Zip(actual))
        {
            Assert.True(Tolerance.Within(value, node.Value), $"{node.Key}: {node.Value}");
        }
    }
}
