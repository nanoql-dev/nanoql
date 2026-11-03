// SPDX-FileCopyrightText: Copyright (C) 2018 Roy Zywina
// SPDX-FileCopyrightText: Copyright (C) 2018 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/sofrfutures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Not ported: testOvernightIndexFutureRateHelperNotification (observer notifications, which an immutable helper has no use for, R1)

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// Adapted throughout: the evaluation date and the fixings an index adds are the context; a future's price is its
// Calculate under it; QuantLib's convexity quote is a value, so its two settings are two futures. The last case is
// QuantLib's testForwardCurveStartingAfterEvaluationDate, which #2897 adds after 1.44.
[Trait("Oracle", "QuantLib")]
public class SofrFuturesTests
{
    [Fact]
    public void TestBootstrap()
    {
        // Testing bootstrap over SOFR futures...
        var today = new Date(26, October, 2018);

        SofrQuotes[] sofrQuotes =
        [
            new(Frequency.Monthly, October, 2018, 97.8175),
            new(Frequency.Monthly, November, 2018, 97.770),
            new(Frequency.Monthly, December, 2018, 97.685),
            new(Frequency.Monthly, January, 2019, 97.595),
            new(Frequency.Monthly, February, 2019, 97.590),
            new(Frequency.Monthly, March, 2019, 97.525),
            new(Frequency.Quarterly, March, 2019, 97.440),
            new(Frequency.Quarterly, June, 2019, 97.295),
            new(Frequency.Quarterly, September, 2019, 97.220),
            new(Frequency.Quarterly, December, 2019, 97.170),
            new(Frequency.Quarterly, March, 2020, 97.160),
            new(Frequency.Quarterly, June, 2020, 97.165),
            new(Frequency.Quarterly, September, 2020, 97.175),
        ];

        OvernightIndex index = new Sofr();
        var context = new EvaluationContext { EvaluationDate = today };
        (Date Date, double Fixing)[] fixings =
        [
            (new Date(1, October, 2018), 0.0222),
            (new Date(2, October, 2018), 0.022),
            (new Date(3, October, 2018), 0.022),
            (new Date(4, October, 2018), 0.0218),
            (new Date(5, October, 2018), 0.0216),
            (new Date(9, October, 2018), 0.0215),
            (new Date(10, October, 2018), 0.0215),
            (new Date(11, October, 2018), 0.0217),
            (new Date(12, October, 2018), 0.0218),
            (new Date(15, October, 2018), 0.0221),
            (new Date(16, October, 2018), 0.0218),
            (new Date(17, October, 2018), 0.0218),
            (new Date(18, October, 2018), 0.0219),
            (new Date(19, October, 2018), 0.0219),
            (new Date(22, October, 2018), 0.0218),
            (new Date(23, October, 2018), 0.0217),
            (new Date(24, October, 2018), 0.0218),
            (new Date(25, October, 2018), 0.0219),
        ];
        foreach ((Date date, double fixing) in fixings)
        {
            context = context with { Fixings = context.Fixings.AddFixing(index, date, fixing) };
        }

        var helpers = new List<BootstrapHelper<YieldTermStructure>>();
        foreach (SofrQuotes sofrQuote in sofrQuotes)
        {
            helpers.Add(new SofrFutureRateHelper(sofrQuote.Price, sofrQuote.Month, sofrQuote.Year, sofrQuote.Freq));
        }

        var curve = new PiecewiseYieldCurve<Discount, Linear, IterativeBootstrap>(today, helpers, new Actual365Fixed(), context);

        // test curve with one of the futures
        OvernightIndex sofr = new Sofr(curve);

        Real tolerance = 1.0e-9;
        foreach (double convAdj in new[] { 0.0, 0.1 })
        {
            var sf = new OvernightIndexFuture(sofr, new Date(20, March, 2019), new Date(19, June, 2019), convAdj);
            Real expectedPrice = 100.0 * (1 - (0.0256 + convAdj));
            Real npv = sf.Calculate(context).NPV!.Value;
            Real error = Math.Abs(npv - expectedPrice);
            Assert.False(
                error > tolerance,
                $"sample futures: estimated price {npv}, expected price {expectedPrice}, error {error}, tolerance {tolerance}");
        }
    }

    [Fact]
    public void TestBootstrapWithJuneteenth()
    {
        // Testing bootstrap over SOFR futures when third Wednesday falls on Juneteenth...
        var today = new Date(27, June, 2024);

        SofrQuotes[] sofrQuotes =
        [
            new(Frequency.Quarterly, June, 2024, 97.220),
            new(Frequency.Quarterly, September, 2024, 97.170),
            new(Frequency.Quarterly, December, 2024, 97.160),
            new(Frequency.Quarterly, March, 2025, 97.165),
            new(Frequency.Quarterly, June, 2025, 97.175),
        ];

        OvernightIndex index = new Sofr();
        var context = new EvaluationContext { EvaluationDate = today };
        foreach (Date date in new[]
        {
            new Date(18, June, 2024), new Date(20, June, 2024), new Date(21, June, 2024), new Date(24, June, 2024),
            new Date(25, June, 2024), new Date(26, June, 2024), new Date(27, June, 2024),
        })
        {
            context = context with { Fixings = context.Fixings.AddFixing(index, date, 0.02) };
        }

        var helpers = new List<BootstrapHelper<YieldTermStructure>>();
        foreach (SofrQuotes sofrQuote in sofrQuotes)
        {
            helpers.Add(new SofrFutureRateHelper(sofrQuote.Price, sofrQuote.Month, sofrQuote.Year, sofrQuote.Freq));
        }

        var curve = new PiecewiseYieldCurve<Discount, Linear, IterativeBootstrap>(today, helpers, new Actual365Fixed(), context);

        OvernightIndex sofr = new Sofr(curve);
        var sf = new OvernightIndexFuture(sofr, new Date(19, June, 2024), new Date(18, September, 2024));

        Real expectedPrice = 97.220;
        Real tolerance = 1.0e-9;

        Real npv = sf.Calculate(context).NPV!.Value;
        Real error = Math.Abs(npv - expectedPrice);
        Assert.False(
            error > tolerance,
            $"sample futures: estimated price {npv}, expected price {expectedPrice}, error {error}, tolerance {tolerance}");
    }

    [Fact]
    public void TestCompoundedRateWithHolidayMaturity()
    {
        // Testing compounded SOFR futures when maturity is a holiday...
        var today = new Date(18, June, 2024);

        YieldTermStructure curve = new FlatForward(today, 0.0, new Actual360());
        var sofr = new Sofr(curve);

        Rate previousFixing = 0.03;
        Rate todaysFixing = 0.09;
        var context = new EvaluationContext { EvaluationDate = today };
        context = context with { Fixings = context.Fixings.AddFixing(sofr, new Date(17, June, 2024), previousFixing) };
        context = context with { Fixings = context.Fixings.AddFixing(sofr, today, todaysFixing) };

        var valueDate = new Date(17, June, 2024);
        var maturityDate = new Date(19, June, 2024); // Juneteenth
        var future = new OvernightIndexFuture(sofr, valueDate, maturityDate);

        DayCounter dc = sofr.DayCounter;
        Real compoundFactor =
            (1.0 + (previousFixing * dc.YearFraction(valueDate, today)))
            * (1.0 + (todaysFixing * dc.YearFraction(today, maturityDate)));
        Rate expectedRate = (compoundFactor - 1.0) / dc.YearFraction(valueDate, maturityDate);
        Real expectedPrice = 100.0 * (1.0 - expectedRate);

        Assert.True(Math.Abs(future.Calculate(context).NPV!.Value - expectedPrice) <= 1.0e-12);
    }

    [Fact]
    public void TestPillarDates()
    {
        // Testing pillar date support in SOFR futures helpers...
        Real price = 99.0;
        var index = new Sofr();

        var valueDate = new Date(20, March, 2024);
        var maturityDate = new Date(20, June, 2024);

        // Default pillar (LastRelevantDate)
        var h1 = new OvernightIndexFutureRateHelper(price, valueDate, maturityDate, index);
        Assert.Equal(maturityDate, h1.PillarDate);

        // maturity pillar
        var h2 = new OvernightIndexFutureRateHelper(
            price, valueDate, maturityDate, index, null, RateAveragingType.Compound, PillarChoice.MaturityDate);
        Assert.Equal(maturityDate, h2.PillarDate);

        // Custom pillar
        var custom = new Date(20, April, 2024);
        var h3 = new OvernightIndexFutureRateHelper(
            price, valueDate, maturityDate, index, null, RateAveragingType.Compound, PillarChoice.CustomDate, custom);
        Assert.Equal(custom, h3.PillarDate);

        // Invalid custom pillar (after maturity)
        var badCustom = new Date(20, July, 2024);
        ArgumentException e = Assert.Throws<ArgumentException>(() => new OvernightIndexFutureRateHelper(
            price, valueDate, maturityDate, index, null, RateAveragingType.Compound, PillarChoice.CustomDate, badCustom));
        Assert.Contains("after end of reference period", e.Message, StringComparison.Ordinal);

        // SOFR helper custom pillar
        var sofrCustom = new Date(15, July, 2024);
        var sh = new SofrFutureRateHelper(price, June, 2024, Frequency.Quarterly, 0.0, PillarChoice.CustomDate, sofrCustom);
        Assert.Equal(sofrCustom, sh.PillarDate);
    }

    [Fact]
    public void TestHelperFuture()
    {
        // Testing the future underlying a SOFR futures helper...
        var helper = new SofrFutureRateHelper(97.8175, October, 2018, Frequency.Monthly);

        OvernightIndexFuture future = helper.Future;
        Assert.NotNull(future);
        Assert.Equal(new Date(1, October, 2018), future.ValueDate);
        Assert.Equal(new Date(1, November, 2018), future.MaturityDate);
        Assert.Equal(helper.EarliestDate, future.ValueDate);
        Assert.Equal(new Sofr().Name, future.OvernightIndex.Name);
    }

    [Fact]
    public void TestForwardCurveStartingAfterEvaluationDate()
    {
        // Testing SOFR futures with a forwarding curve starting after the evaluation date...
        var today = new Date(18, June, 2024);

        // the next business day, since June 19th is Juneteenth
        var curveStart = new Date(20, June, 2024);
        YieldTermStructure curve = new FlatForward(curveStart, 0.05, new Actual360());
        var sofr = new Sofr(curve);
        var context = new EvaluationContext { EvaluationDate = today };
        context = context with { Fixings = context.Fixings.AddFixing(sofr, new Date(17, June, 2024), 0.05) };

        var valueDate = new Date(17, June, 2024);
        var maturityDate = new Date(17, July, 2024);
        var averaged = new OvernightIndexFuture(sofr, valueDate, maturityDate, null, RateAveragingType.Simple);
        var compounded = new OvernightIndexFuture(sofr, valueDate, maturityDate, null, RateAveragingType.Compound);

        // today's rate is neither fixed nor covered by the curve
        NanoQLException e = Assert.Throws<NanoQLException>(() => averaged.Calculate(context));
        Assert.Contains("the forwarding term structure starts on", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<NanoQLException>(() => compounded.Calculate(context));
        Assert.Contains("the forwarding term structure starts on", e.Message, StringComparison.Ordinal);

        // once it is fixed, the curve covers the rest of the period
        context = context with { Fixings = context.Fixings.AddFixing(sofr, today, 0.05) };
        _ = averaged.Calculate(context);
        _ = compounded.Calculate(context);
    }

    private sealed record SofrQuotes(Frequency Freq, Month Month, int Year, Real Price);
}
