// SPDX-FileCopyrightText: Copyright (C) 2020 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/ultimateforwardtermstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Replaced: testObservability (TestSuite/UltimateForwardTermStructureTests.cs)

using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class UltimateForwardTermStructureTests
{
    [Fact]
    public void TestDutchCentralBankRates()
    {
        // Testing DNB replication of UFR zero annually compounded rates...

        Datum[] expectedRates =
        [
            new(10, TimeUnit.Years, 0.00477), new(20, TimeUnit.Years, 0.01004), new(30, TimeUnit.Years, 0.01223),
            new(40, TimeUnit.Years, 0.01433), new(50, TimeUnit.Years, 0.01589), new(60, TimeUnit.Years, 0.01702),
            new(70, TimeUnit.Years, 0.01785), new(80, TimeUnit.Years, 0.01849), new(90, TimeUnit.Years, 0.01899),
            new(100, TimeUnit.Years, 0.01939),
        ];

        CheckDutchBankRates(expectedRates);
    }

    [Fact]
    public void TestDutchCentralBankRatesWithRounding()
    {
        // Testing DNB replication of UFR zero annually compounded rates with rounding...

        Datum[] expectedRates =
        [
            new(10, TimeUnit.Years, 0.005), new(20, TimeUnit.Years, 0.01), new(30, TimeUnit.Years, 0.012),
            new(40, TimeUnit.Years, 0.014), new(50, TimeUnit.Years, 0.016), new(60, TimeUnit.Years, 0.017),
            new(70, TimeUnit.Years, 0.018), new(80, TimeUnit.Years, 0.018), new(90, TimeUnit.Years, 0.019),
            new(100, TimeUnit.Years, 0.019),
        ];

        CheckDutchBankRates(expectedRates, 3, Compounding.Compounded, Frequency.Annual, 1.0e-12);
    }

    [Fact]
    public void TestDutchCentralBankRatesWithRoundingAndContinuousCompounding()
    {
        // Testing DNB replication of UFR zero continuously compounded rates with rounding...

        Datum[] expectedRates =
        [
            new(10, TimeUnit.Years, 0.00477), new(20, TimeUnit.Years, 0.01002), new(30, TimeUnit.Years, 0.01211),
            new(40, TimeUnit.Years, 0.01417), new(50, TimeUnit.Years, 0.01571), new(60, TimeUnit.Years, 0.01683),
            new(70, TimeUnit.Years, 0.01766), new(80, TimeUnit.Years, 0.01829), new(90, TimeUnit.Years, 0.01878),
            new(100, TimeUnit.Years, 0.01917),
        ];

        CheckDutchBankRates(expectedRates, 5, Compounding.Continuous, Frequency.NoFrequency, 1.0e-12);
    }

    [Fact]
    public void TestExtrapolatedForward()
    {
        // Testing continuous forward rates in extrapolation region...

        var vars = new CommonVars();

        double llfr = 0.0125;
        YieldTermStructure ufrTs = new UltimateForwardTermStructure(vars.FtkCurve, llfr, vars.UfrRate, vars.Fsp, vars.Alpha);

        double cutOff = ufrTs.TimeFromReference(ufrTs.ReferenceDate + vars.Fsp);
        Period[] tenors =
        [
            new(20, TimeUnit.Years), new(30, TimeUnit.Years), new(40, TimeUnit.Years), new(50, TimeUnit.Years),
            new(60, TimeUnit.Years), new(70, TimeUnit.Years), new(80, TimeUnit.Years), new(90, TimeUnit.Years),
            new(100, TimeUnit.Years),
        ];

        foreach (Period tenor in tenors)
        {
            Date maturity = vars.Settlement + tenor;
            double t = ufrTs.TimeFromReference(maturity);
            Rate actual = ufrTs.ForwardRate(cutOff, t, Compounding.Continuous, Frequency.NoFrequency, true).Rate;
            Rate expected = CalculateExtrapolatedForward(t, cutOff, llfr, vars.UfrRate, vars.Alpha);

            Real tolerance = 1.0e-10;
            if (Math.Abs(actual - expected) > tolerance)
            {
                Assert.Fail(
                    "unable to replicate the forward rate from the UFR curve\n"
                    + $"    calculated: {actual:G5}\n"
                    + $"    expected:   {expected:G5}\n"
                    + $"    tenor:       {tenor}\n");
            }
        }
    }

    [Fact]
    public void TestZeroRateAtFirstSmoothingPoint()
    {
        // Testing zero rate on the first smoothing point...

        var vars = new CommonVars();

        double llfr = 0.0125;
        YieldTermStructure ufrTs = new UltimateForwardTermStructure(vars.FtkCurve, llfr, vars.UfrRate, vars.Fsp, vars.Alpha);

        double cutOff = ufrTs.TimeFromReference(ufrTs.ReferenceDate + vars.Fsp);
        Rate actual = ufrTs.ZeroRate(cutOff, Compounding.Continuous, Frequency.NoFrequency, true).Rate;
        Rate expected = vars.FtkCurve.ZeroRate(cutOff, Compounding.Continuous, Frequency.NoFrequency, true).Rate;

        Real tolerance = 1.0e-10;
        if (Math.Abs(actual - expected) > tolerance)
        {
            Assert.Fail(
                "unable to replicate the zero rate on the First Smoothing Point\n"
                + $"    calculated: {actual:G5}\n"
                + $"    expected:   {expected:G5}\n"
                + $"    FSP:       {vars.Fsp}\n");
        }
    }

    [Fact]
    public void TestThatInspectorsEqualToBaseCurve()
    {
        // Testing UFR curve inspectors...

        var vars = new CommonVars();

        double llfr = 0.0125;
        YieldTermStructure ufrTs = new UltimateForwardTermStructure(vars.FtkCurve, llfr, vars.UfrRate, vars.Fsp, vars.Alpha);

        if (ufrTs.DayCounter != vars.FtkCurve.DayCounter)
        {
            Assert.Fail(
                "different day counter on the UFR curve than on the base curve\n"
                + $"    UFR curve: {ufrTs.DayCounter.Name}\n"
                + $"    base curve:   {vars.FtkCurve.DayCounter.Name}\n");
        }

        if (ufrTs.ReferenceDate != vars.FtkCurve.ReferenceDate)
        {
            Assert.Fail(
                "different reference date on the UFR curve than on the base curve\n"
                + $"    UFR curve: {ufrTs.ReferenceDate}\n"
                + $"    base curve:   {vars.FtkCurve.ReferenceDate}\n");
        }

        if (ufrTs.MaxDate == vars.FtkCurve.MaxDate)
        {
            Assert.Fail(
                "same max date on the UFR curve as on the base curve\n"
                + $"    UFR curve: {ufrTs.MaxDate}\n"
                + $"    base curve:   {vars.FtkCurve.MaxDate}\n");
        }

        if (ufrTs.MaxTime == vars.FtkCurve.MaxTime)
        {
            Assert.Fail(
                "same max time on the UFR curve as on the base curve\n"
                + $"    UFR curve: {ufrTs.MaxTime}\n"
                + $"    base curve:   {vars.FtkCurve.MaxTime}\n");
        }
    }

    [Fact]
    public void TestExceptionWhenFspLessOrEqualZero()
    {
        // Testing exception when the first smoothing point is less than or equal to zero...

        var vars = new CommonVars();

        double llfr = 0.0125;
        Assert.Throws<ArgumentException>(() => new UltimateForwardTermStructure(
            vars.FtkCurve, llfr, vars.UfrRate, new Period(0, TimeUnit.Years), vars.Alpha));
        Assert.Throws<ArgumentException>(() => new UltimateForwardTermStructure(
            vars.FtkCurve, llfr, vars.UfrRate, new Period(-1, TimeUnit.Years), vars.Alpha));
    }

    // Replaces testObservability, which changes the LLFR and UFR quotes under the curve and checks that it notifies its
    // observers: a nanoql curve doesn't change, so new quotes make a new curve, which extrapolates the new forwards,
    // and the curve built before keeps the old ones.
    [Fact]
    public void ARebuiltCurveFollowsItsNewQuotes()
    {
        var vars = new CommonVars();

        var ufrTs = new UltimateForwardTermStructure(vars.FtkCurve, 0.0125, 0.02, vars.Fsp, vars.Alpha);
        var rebuilt = new UltimateForwardTermStructure(vars.FtkCurve, 0.012, 0.019, vars.Fsp, vars.Alpha);

        double cutOff = ufrTs.TimeFromReference(ufrTs.ReferenceDate + vars.Fsp);
        Real tolerance = 1.0e-10;
        foreach (int years in new[] { 30, 60, 100 })
        {
            double t = ufrTs.TimeFromReference(vars.Settlement + new Period(years, TimeUnit.Years));
            Rate before = ufrTs.ForwardRate(cutOff, t, Compounding.Continuous, Frequency.NoFrequency, true).Rate;
            Rate after = rebuilt.ForwardRate(cutOff, t, Compounding.Continuous, Frequency.NoFrequency, true).Rate;
            Assert.True(
                Math.Abs(before - CalculateExtrapolatedForward(t, cutOff, 0.0125, 0.02, vars.Alpha)) <= tolerance,
                $"the curve built before changed at {years} years: {before}");
            Assert.True(
                Math.Abs(after - CalculateExtrapolatedForward(t, cutOff, 0.012, 0.019, vars.Alpha)) <= tolerance,
                $"the rebuilt curve doesn't follow its new quotes at {years} years: {after}");
        }
    }

    private static double CalculateLLFR(YieldTermStructure ts, Period fsp)
    {
        Real omega = 8.0 / 15.0;
        double cutOff = ts.TimeFromReference(ts.ReferenceDate + fsp);

        LLFRWeight[] llfrWeights = [new(25.0, 1.0), new(30.0, 0.5), new(40.0, 0.25), new(50.0, 0.125)];
        Rate llfr = 0.0;
        foreach (LLFRWeight w in llfrWeights)
        {
            llfr += w.Weight * ts.ForwardRate(cutOff, w.Ttm, Compounding.Continuous, Frequency.NoFrequency, true).Rate;
        }

        return omega * llfr;
    }

    private static Rate CalculateExtrapolatedForward(double t, double fsp, Rate llfr, Rate ufr, Real alpha)
    {
        double deltaT = t - fsp;
        Real beta = (1.0 - Math.Exp(-alpha * deltaT)) / (alpha * deltaT);
        return ufr + ((llfr - ufr) * beta);
    }

    private static void CheckDutchBankRates(
        Datum[] expectedRates,
        int? rounding = null,
        Compounding compounding = Compounding.Compounded,
        Frequency frequency = Frequency.Annual,
        Real tolerance = 1.0e-4)
    {
        var vars = new CommonVars();

        double llfr = CalculateLLFR(vars.FtkCurve, vars.Fsp);

        YieldTermStructure ufrTs = new UltimateForwardTermStructure(
            vars.FtkCurve, llfr, vars.UfrRate, vars.Fsp, vars.Alpha, rounding, compounding, frequency);

        foreach (Datum d in expectedRates)
        {
            var p = new Period(d.N, d.Units);
            Date maturity = vars.Settlement + p;

            Rate actual = ufrTs.ZeroRate(maturity, vars.DayCount, compounding, frequency).Rate;
            Rate expected = d.Rate;

            if (Math.Abs(actual - expected) > tolerance)
            {
                Assert.Fail(
                    "unable to reproduce zero yield rate from the UFR curve\n"
                    + $"    calculated: {actual:G5}\n"
                    + $"    expected:   {expected:G5}\n"
                    + $"    tenor:       {p}\n");
            }
        }
    }

    private readonly record struct Datum(Integer N, TimeUnit Units, Rate Rate);

    private readonly record struct LLFRWeight(double Ttm, Real Weight);

    private sealed class CommonVars
    {
        // Data source: https://fred.stlouisfed.org/
        // Note that these rates are used as a proxy.
        // In order to fully replicate the rates published by the Dutch Central Bank
        // (with the required accuracy) one needs to use Bloomberg CMPL BID Euribor 6m swap
        // rates as stated in the documentation: https://www.toezicht.dnb.nl
        private static readonly Datum[] SwapData =
        [
            new(1, TimeUnit.Years, -0.00315), new(2, TimeUnit.Years, -0.00205), new(3, TimeUnit.Years, -0.00144),
            new(4, TimeUnit.Years, -0.00068), new(5, TimeUnit.Years, 0.00014), new(6, TimeUnit.Years, 0.00103),
            new(7, TimeUnit.Years, 0.00194), new(8, TimeUnit.Years, 0.00288), new(9, TimeUnit.Years, 0.00381),
            new(10, TimeUnit.Years, 0.00471), new(12, TimeUnit.Years, 0.0063), new(15, TimeUnit.Years, 0.00808),
            new(20, TimeUnit.Years, 0.00973), new(25, TimeUnit.Years, 0.01035), new(30, TimeUnit.Years, 0.01055),
            new(40, TimeUnit.Years, 0.0103), new(50, TimeUnit.Years, 0.0103),
        ];

        public CommonVars()
        {
            int settlementDays = 2;
            BusinessDayConvention businessConvention = BusinessDayConvention.Unadjusted;
            DayCount = new SimpleDayCounter();
            Calendar calendar = new NullCalendar();
            Currency ccy = new EURCurrency();
            Frequency fixedFrequency = Frequency.Annual;
            var floatingTenor = new Period(6, TimeUnit.Months);

            // the index forwards on the curve its helpers bootstrap, QuantLib's empty handle linked later
            var index = new IborIndex(
                "FTK_IDX", floatingTenor, settlementDays, ccy, calendar, businessConvention, false, DayCount);

            var ufr = new InterestRate(0.023, DayCount, Compounding.Compounded, Frequency.Annual);
            UfrRate = ufr.EquivalentRate(Compounding.Continuous, Frequency.Annual, 1.0).Rate;
            Fsp = new Period(20, TimeUnit.Years);
            Alpha = 0.1;

            Date today = calendar.Adjust(new Date(29, Month.March, 2019));
            var context = new EvaluationContext { EvaluationDate = today };
            Settlement = calendar.Advance(today, settlementDays, TimeUnit.Days);

            var instruments = new RateHelper[SwapData.Length];
            for (int i = 0; i < SwapData.Length; i++)
            {
                instruments[i] = new SwapRateHelper(
                    SwapData[i].Rate,
                    new Period(SwapData[i].N, SwapData[i].Units),
                    calendar,
                    fixedFrequency,
                    businessConvention,
                    DayCount,
                    index,
                    context);
            }

            FtkCurve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
                Settlement, instruments, DayCount, context).EnableExtrapolation();
        }

        public Date Settlement { get; }

        public DayCounter DayCount { get; }

        public YieldTermStructure FtkCurve { get; }

        public Rate UfrRate { get; }

        public Period Fsp { get; }

        public Real Alpha { get; }
    }
}
