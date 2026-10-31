// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/gsr.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;
using RsgType = NanoQL.Numerics.InverseCumulativeRsg<
    NanoQL.Numerics.RandomSequenceGenerator<NanoQL.Numerics.MersenneTwisterUniformRng>,
    NanoQL.Numerics.InverseCumulativeNormal>;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class GsrTests
{
    // QuantLib's test runner's evaluation date (quantlibglobalfixture.cpp:62-63)
    private static readonly Date RunnerDate = new(16, September, 2015);

    [Fact]
    public void TestGsrProcess()
    {
        // Testing GSR process...

        Date refDate = RunnerDate;
        var context = new EvaluationContext { EvaluationDate = refDate };

        // constant reversion, constant volatility, test conditional expectation and variance against existing
        // HullWhiteForwardProcess; technically we test two representations of the same constant reversion and
        // volatility structure, namely with and without step dates

        Real tol = 1E-8;

        Real reversion = 0.01;
        Real modelvol = 0.01;

        YieldTermStructure yts0 = FlatForward.Create(0, new TARGET(), 0.00, new Actual365Fixed(), context);

        var stepDates0 = new List<Date>();
        var vols0 = new List<Real> { modelvol };
        var reversions0 = new List<Real> { reversion };

        var stepDates1 = new List<Date>();
        for (Size i = 1; i < 60; i++)
        {
            stepDates1.Add(refDate + new Period(i * 6, TimeUnit.Months));
        }

        var vols1 = Enumerable.Repeat(modelvol, stepDates1.Count + 1).ToList();
        var reversions1 = Enumerable.Repeat(reversion, stepDates1.Count + 1).ToList();

        Real T = 10.0;
        do
        {
            var model = new Gsr(yts0, stepDates0, vols0, reversions0, T);
            StochasticProcess1D gsrProcess = model.StateProcess();
            var model2 = new Gsr(yts0, stepDates1, vols1, reversions1, T);
            StochasticProcess1D gsrProcess2 = model2.StateProcess();

            HullWhiteForwardProcess hwProcess = new HullWhiteForwardProcess(yts0, reversion, modelvol).WithForwardMeasureTime(T);

            Real w, t, xw, hwVal, gsrVal, gsr2Val;

            t = 0.5;
            do
            {
                w = 0.0;
                do
                {
                    xw = -0.1;
                    do
                    {
                        hwVal = hwProcess.Expectation(w, xw, t - w);
                        gsrVal = gsrProcess.Expectation(w, xw, t - w);
                        gsr2Val = gsrProcess2.Expectation(w, xw, t - w);
                        if (Math.Abs(hwVal - gsrVal) > tol)
                        {
                            Assert.Fail($"Expectation E^{{T={T}}}(x({t}) | x({w}) = {xw} is different in HullWhiteProcess({hwVal}) and GsrProcess ({gsrVal})");
                        }

                        if (Math.Abs(hwVal - gsr2Val) > tol)
                        {
                            Assert.Fail($"Expectation E^{{T={T}}}(x({t}) | x({w}) = {xw} is different in HullWhiteProcess({hwVal}) and GsrProcess2 ({gsr2Val})");
                        }

                        hwVal = hwProcess.Variance(w, xw, t - w);
                        gsrVal = gsrProcess.Variance(w, xw, t - w);
                        gsr2Val = gsrProcess2.Variance(w, xw, t - w);
                        if (Math.Abs(hwVal - gsrVal) > tol)
                        {
                            Assert.Fail($"Variance V((x({t}) | x({w}) = {xw} is different in HullWhiteProcess({hwVal}) and GsrProcess ({gsrVal})");
                        }

                        if (Math.Abs(hwVal - gsr2Val) > tol)
                        {
                            Assert.Fail($"Variance V((x({t}) | x({w}) = {xw} is different in HullWhiteProcess({hwVal}) and GsrProcess2 ({gsr2Val})");
                        }

                        xw += 0.01;
                    }
                    while (xw <= 0.1);
                    w += t / 5.0;
                }
                while (w <= t - 0.1);
                t += T / 20.0;
            }
            while (t <= T - 0.1);
            T += 10.0;
        }
        while (T <= 30.0);

        // time dependent reversion and volatility (test cases to be added)

        double[] times = [1.0, 2.0];
        double[] vols = [0.2, 0.3, 0.4];
        double[] reversions = [0.50, 0.80, 1.30];

        var p = new GsrProcess(times, vols, reversions);
        _ = p.WithForwardMeasureTime(10.0);

        // add more test cases here ...
    }

    [Fact]
    public void TestGsrModel()
    {
        // Testing GSR model...

        Date refDate = RunnerDate;
        var context = new EvaluationContext { EvaluationDate = refDate };

        Real modelvol = 0.01;
        Real reversion = 0.01;

        var stepDates = new List<Date>(); // no step dates
        var vols = new List<Real> { modelvol };
        var reversions = new List<Real> { reversion };

        // artificial step dates (should yield the same result)
        var stepDates1 = new List<Date>();
        for (int i = 1; i < 60; i++)
        {
            stepDates1.Add(refDate + (i * 6 * TimeUnit.Months));
        }

        var vols1 = Enumerable.Repeat(modelvol, stepDates1.Count + 1).ToList();
        var reversions1 = Enumerable.Repeat(reversion, stepDates1.Count + 1).ToList();

        YieldTermStructure yts = FlatForward.Create(0, new TARGET(), 0.03, new Actual365Fixed(), context);
        var model = new Gsr(yts, stepDates, vols, reversions, 50.0);
        var model2 = new Gsr(yts, stepDates1, vols1, reversions1, 50.0);
        var hw = new HullWhite(yts, reversion, modelvol);

        // test zerobond prices against existing HullWhite model
        // technically we test two representations of the same constant reversion
        // and volatility structure,
        // namely with and without step dates

        Real tol0 = 1E-8;

        Real w, t, xw;

        w = 0.1;
        do
        {
            t = w + 0.1;
            do
            {
                xw = -0.10;
                do
                {
                    Real yw = (xw - model.StateProcess().Expectation(0.0, 0.0, w)) / model.StateProcess().StdDeviation(0.0, 0.0, w);
                    Real rw = xw + 0.03; // instantaneous forward is 0.03
                    Real gsrVal = model.Zerobond(t, w, yw);
                    Real gsr2Val = model2.Zerobond(t, w, yw);
                    Real hwVal = hw.DiscountBond(w, t, rw);
                    Assert.True(
                        Math.Abs(gsrVal - hwVal) <= tol0,
                        $"Zerobond P({w},{t} | x={xw} / y={yw}) is different in HullWhite ({hwVal}) and Gsr ({gsrVal})");
                    Assert.True(
                        Math.Abs(gsr2Val - hwVal) <= tol0,
                        $"Zerobond P({w},{t} | x={xw} / y={yw}) is different in HullWhite ({hwVal}) and Gsr2 ({gsr2Val})");
                    xw += 0.01;
                }
                while (xw <= 0.10);

                t += 2.5;
            }
            while (t <= 50.0);

            w += 5.0;
        }
        while (w <= 50.0);

        // test standard, nonstandard and jamshidian engine against existing Hull
        // White Jamshidian engine

        Date expiry = new TARGET().Advance(refDate, 5 * TimeUnit.Years);
        Period tenor = 10 * TimeUnit.Years;
        var swpIdx = new EuriborSwapIsdaFixA(tenor, yts);
        Real forward = swpIdx.Fixing(expiry, context);

        _ = swpIdx.UnderlyingSwap(expiry, context); // QuantLib's underlying, unused
        VanillaSwap underlyingFixed = new MakeVanillaSwap(10 * TimeUnit.Years, swpIdx.IborIndex)
            .WithFixedRate(forward)
            .WithEffectiveDate(swpIdx.ValueDate(expiry))
            .WithFixedLegCalendar(swpIdx.FixingCalendar)
            .WithFixedLegDayCount(swpIdx.DayCounter)
            .WithFixedLegTenor(swpIdx.FixedLegTenor)
            .WithFixedLegConvention(swpIdx.FixedLegConvention)
            .WithFixedLegTerminationDateConvention(swpIdx.FixedLegConvention)
            .ToVanillaSwap(context);
        var exercise = new EuropeanExercise(expiry);
        var stdswaption = new Swaption(underlyingFixed, exercise);
        var nonstdswaption = new NonstandardSwaption(stdswaption, context);

        Real hwJamNpv = JamshidianSwaptionEngine.Calculate(stdswaption, context, hw, yts).NPV!.Value;

        Real gsrNonStdNpv = Gaussian1dNonstandardSwaptionEngine.Calculate(nonstdswaption, context, model, 64, 7.0, true, false).NPV!.Value;
        Real gsrStdNpv = Gaussian1dSwaptionEngine.Calculate(stdswaption, context, model, 64, 7.0, true, false).NPV!.Value;
        Real gsrJamNpv = Gaussian1dJamshidianSwaptionEngine.Calculate(stdswaption, context, model).NPV!.Value;

        Assert.True(
            Math.Abs(hwJamNpv - gsrNonStdNpv) <= 0.00005,
            $"Jamshidian HW NPV ({hwJamNpv}) deviates from Gaussian1dNonstandardSwaptionEngine NPV ({gsrNonStdNpv})");
        Assert.True(
            Math.Abs(hwJamNpv - gsrStdNpv) <= 0.00005,
            $"Jamshidian HW NPV ({hwJamNpv}) deviates from Gaussian1dSwaptionEngine NPV ({gsrStdNpv})");
        Assert.True(
            Math.Abs(hwJamNpv - gsrJamNpv) <= 0.00005,
            $"Jamshidian HW NPV ({hwJamNpv}) deviates from Gaussian1dJamshidianEngine NPV ({gsrJamNpv})");
    }

    [Fact]
    public void TestGsrProcessWithPathGenerator()
    {
        // Testing GSR process path generation...

        // This test verifies that GsrProcess works correctly with PathGenerator. Previously, GsrProcessCore stored
        // references to the input arrays instead of copies, which caused crashes when temporary arrays were passed
        // (common in language bindings like Python/SWIG).

        Size timeSteps = 4;
        double length = 2.0;

        // Create GsrProcess with temporary Array objects
        var process = new GsrProcess([1.0], [0.005, 0.005], [0.03]);

        // Create path generator
        RsgType rsg = PseudoRandom.MakeSequenceGenerator(timeSteps, 42);
        var generator = new PathGenerator<RsgType>(process, length, timeSteps, rsg, false);

        // Generate paths - this would crash before the fix due to dangling references
        Sample<MonteCarloPath> sample = generator.Next();
        MonteCarloPath path = sample.Value;

        // Verify the path has the expected structure
        Assert.Equal(timeSteps + 1, path.Length);
        Assert.Equal(0.0, path.Front()); // x0 is always 0 for GsrProcess

        // Verify path values are finite (not NaN or Inf)
        for (Size i = 0; i < path.Length; ++i)
        {
            Assert.True(double.IsFinite(path[i]), $"Path value at index {i} is not finite: {path[i]}");
        }

        // Generate a few more paths to ensure stability
        for (Size n = 0; n < 10; ++n)
        {
            Sample<MonteCarloPath> s = generator.Next();
            for (Size i = 0; i < s.Value.Length; ++i)
            {
                Assert.True(double.IsFinite(s.Value[i]), $"Path {n} value at index {i} is not finite: {s.Value[i]}");
            }
        }
    }

    // Adapted: the quote's change (line 751) is a model, an index and a swaption engine built afresh on a 4% curve,
    // the swap's fixed rate the forward on the 3% curve as before, since a quote is a value and a model doesn't observe
    // its curve (master plan §2's quote and observer rows); the unused Hull-White model (line 726) isn't built.
    [Fact]
    public void TestGsrModelQuoteUpdate()
    {
        // Testing GSR model when updating quotes...

        Date refDate = RunnerDate;
        var context = new EvaluationContext { EvaluationDate = refDate };

        Real modelvol = 0.01;
        Real reversion = 0.01;

        var stepDates = new List<Date>();
        var vols = new List<Real> { modelvol };
        var reversions = new List<Real> { reversion };

        Date expiry = new TARGET().Advance(refDate, 5 * TimeUnit.Years);
        Period tenor = 10 * TimeUnit.Years;
        Real forward = new EuriborSwapIsdaFixA(tenor, FlatForward.Create(0, new TARGET(), 0.03, new Actual365Fixed(), context))
            .Fixing(expiry, context);

        Real Price(Rate rate)
        {
            YieldTermStructure yts = FlatForward.Create(0, new TARGET(), rate, new Actual365Fixed(), context);
            var model = new Gsr(yts, stepDates, vols, reversions, 50.0);
            var swpIdx = new EuriborSwapIsdaFixA(tenor, yts);
            VanillaSwap underlyingFixed = new MakeVanillaSwap(10 * TimeUnit.Years, swpIdx.IborIndex)
                .WithFixedRate(forward)
                .WithEffectiveDate(swpIdx.ValueDate(expiry))
                .WithFixedLegCalendar(swpIdx.FixingCalendar)
                .WithFixedLegDayCount(swpIdx.DayCounter)
                .WithFixedLegTenor(swpIdx.FixedLegTenor)
                .WithFixedLegConvention(swpIdx.FixedLegConvention)
                .WithFixedLegTerminationDateConvention(swpIdx.FixedLegConvention)
                .ToVanillaSwap(context);
            var exercise = new EuropeanExercise(expiry);
            var stdswaption = new Swaption(underlyingFixed, exercise);
            return Gaussian1dSwaptionEngine.Calculate(stdswaption, context, model, 64, 7.0, true, false).NPV!.Value;
        }

        Real before = 0.0;
        Exception? thrown = Record.Exception(() => before = Price(0.03));
        Assert.Null(thrown);

        Real after = Price(0.04);
        Assert.True(Math.Abs(before - after) > 0.01);
    }

    [Fact]
    public void TestGsrIborSwaptionAgainstMonteCarlo()
    {
        // Testing GSR Ibor swaption against Monte Carlo...
        CheckGsrIborSwaptionAgainstMonteCarlo();
    }

    [Fact]
    public void TestGsrSofrSwaptionAgainstMonteCarlo()
    {
        // Testing GSR SOFR swaption against Monte Carlo...
        CheckGsrSofrSwaptionAgainstMonteCarlo(0, null, false);
    }

    [Fact]
    public void TestGsrSofrPaymentLagSwaptionAgainstMonteCarlo()
    {
        // Testing GSR payment-lagged SOFR swaption against Monte Carlo...
        CheckGsrSofrSwaptionAgainstMonteCarlo(2, null, false);
    }

    [Fact]
    public void TestGsrSofrObservationLagSwaptionAgainstMonteCarlo()
    {
        // Testing GSR observation-lagged SOFR swaption against Monte Carlo...
        CheckGsrSofrSwaptionAgainstMonteCarlo(0, 5, true);
    }

    [Fact]
    public void TestGsrSofrPaymentAndObservationLagSwaptionAgainstMonteCarlo()
    {
        // Testing GSR payment- and observation-lagged SOFR swaption against Monte Carlo...
        CheckGsrSofrSwaptionAgainstMonteCarlo(2, 5, true);
    }

    [Fact]
    public void TestSofrSwaptionObservationConventions()
    {
        // Testing Gaussian1d telescoping with SOFR observation conventions...

        Date today = new(30, June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var dc = new Actual365Fixed();

        var curveDates = new List<Date> { today };
        var zeroRates = new List<Rate> { 0.02 };
        for (Size i = 1; i <= 12; ++i)
        {
            curveDates.Add(today + new Period(i, TimeUnit.Years));
            zeroRates.Add(0.02 + (0.015 * (1.0 - Math.Exp(-((Real)i) / 3.0))));
        }

        YieldTermStructure curve = new InterpolatedZeroCurve<Linear>(curveDates, zeroRates, dc);

        var sofr = new Sofr(curve);
        Calendar calendar = sofr.FixingCalendar;
        var gsr = new Gsr(curve, [], [0.008], 0.03, 12.0);
        InstrumentResults Engine(Swaption s) => Gaussian1dSwaptionEngine.Calculate(s, context, gsr, 128, 8.0);

        // The exercise date sits two business days before the accrual start, as it does for a real SOFR swaption, so a
        // lookback puts the coupon's first value date on or before the exercise date.
        foreach (Natural gapDays in (Natural[])[2, 5])
        {
            Date expiry = calendar.Adjust(today + (2 * TimeUnit.Years));
            Date start = calendar.Advance(expiry, gapDays * TimeUnit.Days);
            Date end = calendar.Advance(start, 5 * TimeUnit.Years);
            var schedule = new Schedule(
                start, end, 1 * TimeUnit.Years, calendar, BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
            var exercise = new EuropeanExercise(expiry);

            // Plain compounding and lookback-with-observation-shift both telescope exactly, so both must price.
            (Swaption plain, _) = MakeObservationConventionSwaption(schedule, dc, sofr, calendar, exercise, null, 0, false);
            (Swaption shifted, OvernightIndexedSwap shiftedSwap) = MakeObservationConventionSwaption(schedule, dc, sofr, calendar, exercise, 5, 0, true);

            var shiftedCoupon = shiftedSwap.OvernightLeg[0] as OvernightIndexedCoupon;
            Assert.NotNull(shiftedCoupon);
            Assert.True(shiftedCoupon.ValueDates()[0] < shiftedCoupon.AccrualStartDate);

            // the lookback is what this test is about: it must reach back past the exercise date, which is the case
            // the projection has to handle
            Assert.True(shiftedCoupon.ValueDates()[0] <= expiry);

            Real plainValue = Engine(plain).NPV!.Value;
            Real shiftedValue = Engine(shifted).NPV!.Value;
            Assert.True(plainValue > 0.0);
            Assert.True(shiftedValue > 0.0);

            // the lookback moves the underlying forward, so the prices must differ
            Assert.True(Math.Abs(shiftedValue - plainValue) > 1.0e-8);

            // Lookback without observation shift and lockout do not telescope exactly; they are approximated by the
            // same endpoint ratio rather than raising, so the engine must still return a finite, positive value.
            foreach (Real v in (Real[])[
                Engine(MakeObservationConventionSwaption(schedule, dc, sofr, calendar, exercise, 5, 0, false).Swaption).NPV!.Value,
                Engine(MakeObservationConventionSwaption(schedule, dc, sofr, calendar, exercise, 5, 2, true).Swaption).NPV!.Value])
            {
                Assert.True(double.IsFinite(v));
                Assert.True(v > 0.0);
            }
        }
    }

    [Fact]
    public void TestSofrSwaptionObservationShiftNormalization()
    {
        // Testing Gaussian1d observation-shift accrual normalization...

        // Under an observation shift the coupon divides its compounded factor by the year fraction over the shifted
        // value dates, while its amount multiplies by the contractual accrual period. The engine must keep that
        // accrualPeriod/tau adjustment, otherwise the swaption underlying drifts from the forward swap on periods whose
        // shifted interval has a different calendar length. Put-call parity ties the engine underlying to the
        // DiscountingSwapEngine value.

        Date today = new(30, June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var dc = new Actual360();

        var curveDates = new List<Date> { today };
        var zeroRates = new List<Rate> { 0.02 };
        for (Size i = 1; i <= 12; ++i)
        {
            curveDates.Add(today + new Period(i, TimeUnit.Years));
            zeroRates.Add(0.02 + (0.015 * (1.0 - Math.Exp(-((Real)i) / 3.0))));
        }

        YieldTermStructure curve = new InterpolatedZeroCurve<Linear>(curveDates, zeroRates, dc);

        var sofr = new Sofr(curve);
        Calendar calendar = sofr.FixingCalendar;
        var gsr = new Gsr(curve, [], [0.008], 0.03, 12.0);

        const Rate strike = 0.03;
        const Natural lookback = 5;

        // Use a single 3M period whose observation-shift window is holiday-asymmetric (tau != accrualPeriod), so the
        // normalization error does not cancel across coupons. Place the exercise a few weeks before the accrual start
        // so the first value date stays after expiry (no pre-expiry stub in the check).
        Date expiry = default, start = default, end = default;
        OvernightIndexedCoupon? coupon = null;
        Real bugMagnitude = 0.0;
        for (Natural wk = 26; wk < 130 && coupon is null; ++wk)
        {
            Date s = calendar.Advance(today, wk, TimeUnit.Weeks);
            Date e = calendar.Advance(s, 3 * TimeUnit.Months);
            Date ex = calendar.Advance(s, -3, TimeUnit.Weeks);
            var c = MakeObservationShiftedSwap(s, e, SwapType.Payer, calendar, dc, sofr, strike, lookback, context).OvernightLeg[0] as OvernightIndexedCoupon;
            Real ap = c!.AccrualPeriod();
            Real tau = dc.YearFraction(c.ValueDates()[0], c.ValueDates()[^1]);
            if (Math.Abs(ap - tau) > 1.0e-3 && c.ValueDates()[0] > ex)
            {
                Real cfm1 = (curve.Discount(c.ValueDates()[0]) / curve.Discount(c.ValueDates()[^1])) - 1.0;
                bugMagnitude = Math.Abs(cfm1 * (1.0 - (ap / tau)) * curve.Discount(c.Date));
                expiry = ex;
                start = s;
                end = e;
                coupon = c;
            }
        }

        Assert.NotNull(coupon);

        // the chosen period must actually exercise the bug, else the test is vacuous
        Assert.True(bugMagnitude > 5.0e-5);

        var exercise = new EuropeanExercise(expiry);
        OvernightIndexedSwap forwardSwap = MakeObservationShiftedSwap(start, end, SwapType.Payer, calendar, dc, sofr, strike, lookback, context);
        Real reference = DiscountingSwapEngine.Calculate(forwardSwap, context, curve).NPV!.Value;

        var payer = new Swaption(MakeObservationShiftedSwap(start, end, SwapType.Payer, calendar, dc, sofr, strike, lookback, context), exercise);
        var receiver = new Swaption(MakeObservationShiftedSwap(start, end, SwapType.Receiver, calendar, dc, sofr, strike, lookback, context), exercise);

        // put-call parity: payer - receiver = value of the forward payer swap; the old normalization missed this by
        // ~bugMagnitude, so require a tight match
        Real parity = Gaussian1dSwaptionEngine.Calculate(payer, context, gsr, 128, 8.0).NPV!.Value
            - Gaussian1dSwaptionEngine.Calculate(receiver, context, gsr, 128, 8.0).NPV!.Value;
        Assert.True(
            Math.Abs(parity - reference) < 1.0e-5,
            $"Gaussian1d observation-shifted underlying ({parity}) does not match the DiscountingSwapEngine forward swap ({reference}), diff {parity - reference} (bug magnitude {bugMagnitude})");
    }

    [Fact]
    public void TestOvernightIndexUnderlyingWithIborCoupons()
    {
        // Testing Gaussian1dSwaptionEngine with a VanillaSwap built on an overnight index...

        // A VanillaSwap always builds an IborLeg, so an overnight index yields IborCoupons rather than
        // OvernightIndexedCoupons. The engine must project each coupon from its single fixing instead of compounding
        // over the whole schedule period.

        Date today = new(30, June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var dc = new Actual365Fixed();
        YieldTermStructure curve = new FlatForward(today, 0.03, dc);

        var sofr = new Sofr(curve);
        Calendar calendar = sofr.FixingCalendar;
        Date expiry = calendar.Adjust(today + (2 * TimeUnit.Years));
        Date start = calendar.Advance(expiry, 2 * TimeUnit.Days);
        Date end = calendar.Advance(start, 5 * TimeUnit.Years);
        var schedule = new Schedule(
            start, end, 1 * TimeUnit.Years, calendar, BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);

        var gsr = new Gsr(curve, [], [0.008], 0.03, 12.0);
        var payerSwap = new VanillaSwap(SwapType.Payer, 1.0, schedule, 0.03, dc, schedule, sofr, 0.0, dc, context);
        Assert.Null(payerSwap.FloatingLeg[0] as OvernightIndexedCoupon);

        // Build an otherwise identical one-day Ibor index. Both swaps contain the same IborCoupons and must therefore
        // take the same generic projection path; dispatching on the SOFR index type instead would compound over each
        // annual schedule period and make the prices diverge.
        var oneDayIndex = new IborIndex(
            "SOFR-like", 1 * TimeUnit.Days, sofr.FixingDays, sofr.Currency, calendar, BusinessDayConvention.Following, false, sofr.DayCounter, curve);
        var referenceSwap = new VanillaSwap(SwapType.Payer, 1.0, schedule, 0.03, dc, schedule, oneDayIndex, 0.0, dc, context);

        var exercise = new EuropeanExercise(expiry);
        var payer = new Swaption(payerSwap, exercise);
        var referenceSwaption = new Swaption(referenceSwap, exercise);

        Real value = Gaussian1dSwaptionEngine.Calculate(payer, context, gsr, 128, 8.0).NPV!.Value;
        Real reference = Gaussian1dSwaptionEngine.Calculate(referenceSwaption, context, gsr, 128, 8.0).NPV!.Value;
        Assert.True(
            Math.Abs(value - reference) < 1.0e-10,
            $"Gaussian1d price for IborCoupons on SOFR ({value}) does not match the equivalent one-day Ibor index price ({reference}), diff {value - reference}");
    }

    [Fact]
    public void TestSofrSwaptionPaymentLag()
    {
        // Testing Gaussian1d engines with SOFR payment lag...

        Date today = new(30, June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var dc = new Actual365Fixed();

        var curveDates = new List<Date> { today };
        var zeroRates = new List<Rate> { 0.025 };
        for (int i = 1; i <= 12; ++i)
        {
            curveDates.Add(today + (i * TimeUnit.Years));
            zeroRates.Add(0.025 + (0.01 * (1.0 - Math.Exp(-(Real)i / 3.0))));
        }

        YieldTermStructure curve = new InterpolatedZeroCurve<Linear>(curveDates, zeroRates, dc);

        var sofr = new Sofr(curve);
        Calendar calendar = sofr.FixingCalendar;
        Date expiry = calendar.Adjust(today + (3 * TimeUnit.Years));
        Date start = calendar.Advance(expiry, 2 * TimeUnit.Days);
        Date end = calendar.Advance(start, 5 * TimeUnit.Years);
        var schedule = new Schedule(
            start, end, 1 * TimeUnit.Years, calendar, BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);

        var probe = new OvernightIndexedSwap(SwapType.Payer, 1.0, schedule, 0.0, dc, sofr, 0.0, 2, BusinessDayConvention.Following, calendar);
        Rate fairRate = DiscountingSwapEngine.Calculate(probe, context, curve).FairRate!.Value;
        var swap = new OvernightIndexedSwap(SwapType.Payer, 1.0, schedule, fairRate, dc, sofr, 0.0, 2, BusinessDayConvention.Following, calendar);
        Real swapNpv = DiscountingSwapEngine.Calculate(swap, context, curve).NPV!.Value;
        CheckSmall(swapNpv, 1.0e-12);

        foreach (CashFlow cashflow in swap.OvernightLeg)
        {
            var coupon = cashflow as OvernightIndexedCoupon;
            Assert.NotNull(coupon);
            Date expectedPayment = calendar.Advance(coupon.AccrualEndDate, 2 * TimeUnit.Days, BusinessDayConvention.Following);
            Assert.Equal(expectedPayment, coupon.Date);
            Assert.True(coupon.Date > coupon.AccrualEndDate);
        }

        Real reversion = 0.03, sigma = 0.008;
        var gsr = new Gsr(curve, [], [sigma], reversion, 12.0);
        var exercise = new EuropeanExercise(expiry);
        var swaption = new Swaption(swap, exercise);

        Real gaussian = Gaussian1dSwaptionEngine.Calculate(swaption, context, gsr, 128, 8.0).NPV!.Value;
        Real gaussianJamshidian = Gaussian1dJamshidianSwaptionEngine.Calculate(swaption, context, gsr).NPV!.Value;

        CheckClose(gaussianJamshidian, gaussian, 0.2);

        // Conversion to a NonstandardSwap must preserve the payment lag, and the
        // nonstandard Gaussian engine must reproduce the standard engine.
        var nonstandardSwap = new NonstandardSwap(swap, context);
        Assert.Equal(2, nonstandardSwap.PaymentLag);
        CheckSmall(DiscountingSwapEngine.Calculate(nonstandardSwap, context, curve).NPV!.Value - swapNpv, 1.0e-12);
        var nonstandardSwaption = new NonstandardSwaption(nonstandardSwap, exercise);
        CheckClose(
            Gaussian1dNonstandardSwaptionEngine.Calculate(nonstandardSwaption, context, gsr, 128, 8.0, true, false, null, curve).NPV!.Value,
            gaussian,
            0.2);

        // Exercise the same path with variable notionals and margins.
        int coupons = schedule.Size - 1;
        var fixedNominals = new Real[coupons];
        var floatingNominals = new Real[coupons];
        Real[] fixedRates = [.. Enumerable.Repeat(fairRate, coupons)];
        Real[] gearings = [.. Enumerable.Repeat(1.0, coupons)];
        var spreads = new Spread[coupons];
        for (int i = 0; i < coupons; ++i)
        {
            fixedNominals[i] = 1.0 - (0.05 * i);
            floatingNominals[i] = 1.0 - (0.04 * i);
            spreads[i] = 0.0001 * i;
        }

        var variableSwap = new NonstandardSwap(
            SwapType.Payer, fixedNominals, floatingNominals, schedule, fixedRates, dc, schedule, sofr, gearings, spreads, dc, context, false, false,
            BusinessDayConvention.Following, 2, calendar);
        Assert.True(double.IsFinite(DiscountingSwapEngine.Calculate(variableSwap, context, curve).NPV!.Value));
        foreach (CashFlow cashflow in variableSwap.FloatingLeg)
        {
            var coupon = cashflow as OvernightIndexedCoupon;
            Assert.NotNull(coupon);
            Assert.Equal(calendar.Advance(coupon.AccrualEndDate, 2 * TimeUnit.Days, BusinessDayConvention.Following), coupon.Date);
        }

        var variableSwaption = new NonstandardSwaption(variableSwap, exercise);
        Real payerValue = Gaussian1dNonstandardSwaptionEngine.Calculate(variableSwaption, context, gsr, 128, 8.0, true, false, null, curve).NPV!.Value;
        Assert.True(double.IsFinite(payerValue));
        Assert.True(payerValue >= 0.0);

        Real[] scaledFixedNominals = [.. fixedNominals.Select(n => n * 2.0)];
        Real[] scaledFloatingNominals = [.. floatingNominals.Select(n => n * 2.0)];
        var scaledSwap = new NonstandardSwap(
            SwapType.Payer, scaledFixedNominals, scaledFloatingNominals, schedule, fixedRates, dc, schedule, sofr, gearings, spreads, dc, context, false,
            false, BusinessDayConvention.Following, 2, calendar);
        var scaledSwaption = new NonstandardSwaption(scaledSwap, exercise);

        // Exact notional homogeneity provides a reference value while retaining
        // the variable-notional, variable-margin and payment-lag path. The two
        // prices come from separate integrations, so the check is relative but not
        // at roundoff level (BOOST_CHECK_CLOSE takes a percentage).
        CheckClose(
            Gaussian1dNonstandardSwaptionEngine.Calculate(scaledSwaption, context, gsr, 128, 8.0, true, false, null, curve).NPV!.Value,
            2.0 * payerValue,
            1.0e-6);
    }

    // QL_CHECK_CLOSE, Boost's BOOST_CHECK_CLOSE: |a - b| within the tolerance, in percent, of both values.
    private static void CheckClose(Real a, Real b, Real tolerancePercent)
    {
        Real diff = Math.Abs(a - b);
        Assert.True(
            diff <= tolerancePercent / 100.0 * Math.Abs(a) && diff <= tolerancePercent / 100.0 * Math.Abs(b),
            $"difference between {a} and {b} exceeds {tolerancePercent}%");
    }

    // QL_CHECK_SMALL, Boost's BOOST_CHECK_SMALL: |value| within the tolerance.
    private static void CheckSmall(Real value, Real tolerance) =>
        Assert.True(Math.Abs(value) <= tolerance, $"{value} is not small, tolerance {tolerance}");

    private static (Swaption Swaption, OvernightIndexedSwap Swap) MakeObservationConventionSwaption(
        Schedule schedule,
        DayCounter fixedDayCounter,
        OvernightIndex overnightIndex,
        Calendar paymentCalendar,
        Exercise exercise,
        Natural? lookbackDays,
        Natural lockoutDays,
        bool applyObservationShift,
        RateAveragingType averaging = RateAveragingType.Compound)
    {
        var swap = new OvernightIndexedSwap(
            SwapType.Payer, 1.0, schedule, 0.03, fixedDayCounter, overnightIndex, 0.0, 0, BusinessDayConvention.Following, paymentCalendar, false, averaging,
            lookbackDays, lockoutDays, applyObservationShift);
        var swaption = new Swaption(swap, exercise);
        return (swaption, swap);
    }

    private static OvernightIndexedSwap MakeObservationShiftedSwap(
        Date start,
        Date end,
        SwapType type,
        Calendar calendar,
        DayCounter fixedDayCounter,
        OvernightIndex overnightIndex,
        Rate strike,
        Natural lookbackDays,
        EvaluationContext context)
    {
        var schedule = new Schedule(
            start, end, 3 * TimeUnit.Months, calendar, BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
        return new OvernightIndexedSwap(
            type, 1.0, schedule, strike, fixedDayCounter, overnightIndex, 0.0, 0, BusinessDayConvention.Following, calendar, false, RateAveragingType.Compound,
            lookbackDays, 0, true);
    }

    // Independent constant-parameter Hull-White reference. The Monte Carlo side does not use Gsr bonds, numeraires or
    // its state process. Here x is the zero-mean Ornstein-Uhlenbeck state under the risk-neutral measure.
    private static DiscountFactor HullWhiteDiscountBond(YieldTermStructure curve, double t, double maturity, Real x, Real reversion, Real volatility)
    {
        Real b = (1.0 - Math.Exp(-reversion * (maturity - t))) / reversion;
        Real shift = volatility * volatility / (2.0 * reversion * reversion) * Math.Pow(1.0 - Math.Exp(-reversion * t), 2);
        Real variance = volatility * volatility / (4.0 * reversion) * (1.0 - Math.Exp(-2.0 * reversion * t));
        return curve.Discount(maturity) / curve.Discount(t) * Math.Exp((-b * x) - (b * shift) - (b * b * variance));
    }

    private static DiscountFactor HullWhiteDiscountBond(YieldTermStructure curve, double t, Date maturity, Real x, Real reversion, Real volatility) =>
        HullWhiteDiscountBond(curve, t, curve.TimeFromReference(maturity), x, reversion, volatility);

    private static Rate ConditionalFloatingRate(
        YieldTermStructure curve, FloatingRateCoupon coupon, double expiry, Real x, Real reversion, Real volatility)
    {
        if (coupon is OvernightIndexedCoupon overnightCoupon)
        {
            var valueDates = overnightCoupon.ValueDates();
            var interestDates = overnightCoupon.InterestDates();
            DayCounter dayCounter = overnightCoupon.Index.DayCounter;
            double rateAccrualTime = overnightCoupon.ApplyObservationShift && overnightCoupon.FixingDays > 0
                ? dayCounter.YearFraction(valueDates[0], valueDates[^1])
                : dayCounter.YearFraction(interestDates[0], interestDates[^1]);
            return ((HullWhiteDiscountBond(curve, expiry, valueDates[0], x, reversion, volatility)
                / HullWhiteDiscountBond(curve, expiry, valueDates[^1], x, reversion, volatility)) - 1.0) / rateAccrualTime;
        }

        var iborCoupon = coupon as IborCoupon ?? throw new NanoQLException("unsupported floating coupon");
        return ((HullWhiteDiscountBond(curve, expiry, iborCoupon.FixingValueDate(), x, reversion, volatility)
            / HullWhiteDiscountBond(curve, expiry, iborCoupon.FixingMaturityDate(), x, reversion, volatility)) - 1.0) / iborCoupon.SpanningTimeIndexMaturity();
    }

    private static Real DiscountedEuropeanSwaptionPayoff(
        YieldTermStructure discountCurve,
        FixedVsFloatingSwap swap,
        Date expiry,
        double expiryTime,
        Real x,
        Real pathDiscount,
        Real reversion,
        Real volatility,
        EvaluationContext context)
    {
        Real fixedLegNpv = 0.0;
        foreach (CashFlow cashflow in swap.FixedLeg)
        {
            var coupon = cashflow as FixedRateCoupon ?? throw new NanoQLException("unsupported fixed cashflow");
            if (!(coupon.AccrualStartDate >= expiry))
            {
                throw new NanoQLException("fixed coupon starts before exercise");
            }

            fixedLegNpv += coupon.Amount(context) * HullWhiteDiscountBond(discountCurve, expiryTime, coupon.Date, x, reversion, volatility);
        }

        YieldTermStructure forwardingCurve = swap.IborIndex.ForwardingTermStructure!;
        Real floatingLegNpv = 0.0;
        foreach (CashFlow cashflow in swap.FloatingLeg)
        {
            var coupon = cashflow as FloatingRateCoupon ?? throw new NanoQLException("unsupported floating cashflow");
            if (!(coupon.AccrualStartDate >= expiry))
            {
                throw new NanoQLException("floating coupon starts before exercise");
            }

            Rate rate = ConditionalFloatingRate(forwardingCurve, coupon, expiryTime, x, reversion, volatility);

            // A delayed coupon is projected at exercise and discounted from its actual payment date, consistently with
            // the GSR engine. This deliberately excludes post-exercise payment-delay convexity.
            Real amount = coupon.Nominal(context) * coupon.AccrualPeriod() * ((coupon.Gearing * rate) + coupon.Spread);
            floatingLegNpv += amount * HullWhiteDiscountBond(discountCurve, expiryTime, coupon.Date, x, reversion, volatility);
        }

        Real payerNpv = floatingLegNpv - fixedLegNpv;
        Real exerciseValue = swap.Type == SwapType.Payer ? payerNpv : -payerNpv;
        return pathDiscount * (exerciseValue < 0.0 ? 0.0 : exerciseValue);
    }

    private static Real HullWhiteMonteCarloEuropeanSwaption(
        YieldTermStructure curve, Real reversion, Real volatility, FixedVsFloatingSwap swap, Date expiry, EvaluationContext context, Size samplePairs = 65536)
    {
        double expiryTime = curve.TimeFromReference(expiry);

        // x_T and the integral of x over [0,T] are sampled jointly from their exact Gaussian distribution, avoiding a
        // time-discretisation error in both the state and the path discount factor.
        Real varianceX = volatility * volatility / (2.0 * reversion) * (1.0 - Math.Exp(-2.0 * reversion * expiryTime));
        Real varianceIntegral = volatility * volatility / (reversion * reversion)
            * (expiryTime - (2.0 * (1.0 - Math.Exp(-reversion * expiryTime)) / reversion) + ((1.0 - Math.Exp(-2.0 * reversion * expiryTime)) / (2.0 * reversion)));
        Real covariance = volatility * volatility / (2.0 * reversion * reversion) * Math.Pow(1.0 - Math.Exp(-reversion * expiryTime), 2);
        Real stdDevX = Math.Sqrt(varianceX);
        Real integralLoading = covariance / stdDevX;
        Real residualStdDev = Math.Sqrt(varianceIntegral - (integralLoading * integralLoading));

        var sobol = new SobolRsg(2, 42, SobolRsgDirectionIntegers.JoeKuoD7);
        var inverseNormal = new InverseCumulativeNormal();
        Real sum = 0.0;
        for (Size i = 0; i < samplePairs; ++i)
        {
            ReadOnlySpan<double> sequence = sobol.NextSequence().Value.Span;
            Real z1 = inverseNormal.Evaluate(sequence[0]);
            Real z2 = inverseNormal.Evaluate(sequence[1]);
            Real x = stdDevX * z1;
            Real integral = (integralLoading * z1) + (residualStdDev * z2);
            Real pathDiscount = curve.Discount(expiryTime) * Math.Exp(-integral - (0.5 * varianceIntegral));
            sum += DiscountedEuropeanSwaptionPayoff(curve, swap, expiry, expiryTime, x, pathDiscount, reversion, volatility, context);
            sum += DiscountedEuropeanSwaptionPayoff(
                curve, swap, expiry, expiryTime, -x, curve.Discount(expiryTime) * Math.Exp(integral - (0.5 * varianceIntegral)), reversion, volatility, context);
        }

        return sum / (2.0 * samplePairs);
    }

    private static YieldTermStructure GsrMonteCarloCurve(Date today)
    {
        var dc = new Actual365Fixed();
        var dates = new List<Date> { today };
        var rates = new List<Rate> { 0.02 };
        for (Size i = 1; i <= 15; ++i)
        {
            dates.Add(today + new Period(i, TimeUnit.Years));
            rates.Add(0.02 + (0.015 * (1.0 - Math.Exp(-((Real)i) / 4.0))));
        }

        return new InterpolatedZeroCurve<Linear>(dates, rates, dc);
    }

    private static Gsr GsrMonteCarloModel(YieldTermStructure curve, Real reversion, Real volatility) =>
        new(curve, [], [volatility], reversion, 12.0);

    private static void CheckGsrIborSwaptionAgainstMonteCarlo()
    {
        Date today = new(30, June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        Real reversion = 0.03;
        Real volatility = 0.009;
        Calendar calendar = new TARGET();
        YieldTermStructure curve = GsrMonteCarloCurve(today);
        var index = new Euribor6M(curve);
        Date expiry = calendar.Adjust(today + (2 * TimeUnit.Years));
        Date start = calendar.Advance(expiry, 2 * TimeUnit.Days);
        Date end = calendar.Advance(start, 5 * TimeUnit.Years);
        var fixedSchedule = new Schedule(
            start, end, 1 * TimeUnit.Years, calendar, BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
        var floatingSchedule = new Schedule(
            start, end, 6 * TimeUnit.Months, calendar, BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
        var probe = new VanillaSwap(
            SwapType.Payer, 1.0, fixedSchedule, 0.0, new Thirty360(Thirty360Convention.BondBasis), floatingSchedule, index, 0.0, new Actual360(), context);
        Rate fairRate = DiscountingSwapEngine.Calculate(probe, context, curve).FairRate!.Value;
        var swap = new VanillaSwap(
            SwapType.Payer, 1.0, fixedSchedule, fairRate, new Thirty360(Thirty360Convention.BondBasis), floatingSchedule, index, 0.0, new Actual360(), context);
        Gsr model = GsrMonteCarloModel(curve, reversion, volatility);
        var swaption = new Swaption(swap, new EuropeanExercise(expiry));

        Real calculated = Gaussian1dSwaptionEngine.Calculate(swaption, context, model, 128, 8.0).NPV!.Value;
        Real expected = HullWhiteMonteCarloEuropeanSwaption(curve, reversion, volatility, swap, expiry, context);
        Assert.True(Math.Abs(calculated - expected) < 2.0e-6, $"GSR Ibor swaption price {calculated} differs from Monte Carlo {expected}");
    }

    private static void CheckGsrSofrSwaptionAgainstMonteCarlo(Integer paymentLag, Natural? lookbackDays, bool applyObservationShift)
    {
        Date today = new(30, June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        Real reversion = 0.03;
        Real volatility = 0.009;
        YieldTermStructure curve = GsrMonteCarloCurve(today);
        var sofr = new Sofr(curve);
        Calendar calendar = sofr.FixingCalendar;
        Date expiry = calendar.Adjust(today + (2 * TimeUnit.Years));
        Date start = calendar.Advance(expiry, 10 * TimeUnit.Days);
        Date end = calendar.Advance(start, 5 * TimeUnit.Years);
        var schedule = new Schedule(
            start, end, 1 * TimeUnit.Years, calendar, BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
        var probe = new OvernightIndexedSwap(
            SwapType.Payer, 1.0, schedule, 0.0, new Actual360(), sofr, 0.0, paymentLag, BusinessDayConvention.Following, calendar, false, RateAveragingType.Compound,
            lookbackDays, 0, applyObservationShift);
        Rate fairRate = DiscountingSwapEngine.Calculate(probe, context, curve).FairRate!.Value;
        var swap = new OvernightIndexedSwap(
            SwapType.Payer, 1.0, schedule, fairRate, new Actual360(), sofr, 0.0, paymentLag, BusinessDayConvention.Following, calendar, false, RateAveragingType.Compound,
            lookbackDays, 0, applyObservationShift);
        var firstCoupon = swap.OvernightLeg[0] as OvernightIndexedCoupon ?? throw new NanoQLException("expected overnight coupon");
        if (!(firstCoupon.ValueDates()[0] > expiry))
        {
            throw new NanoQLException("observation period must start after exercise");
        }

        Date expectedRateStart = lookbackDays is null
            ? firstCoupon.AccrualStartDate
            : calendar.Advance(firstCoupon.AccrualStartDate, -lookbackDays.Value, TimeUnit.Days);
        Date expectedPaymentDate = calendar.Advance(firstCoupon.AccrualEndDate, paymentLag, TimeUnit.Days, BusinessDayConvention.Following);
        Assert.Equal(expectedRateStart, firstCoupon.ValueDates()[0]);
        Assert.Equal(expectedPaymentDate, firstCoupon.Date);
        Gsr model = GsrMonteCarloModel(curve, reversion, volatility);
        var swaption = new Swaption(swap, new EuropeanExercise(expiry));

        Real calculated = Gaussian1dSwaptionEngine.Calculate(swaption, context, model, 128, 8.0).NPV!.Value;
        Real expected = HullWhiteMonteCarloEuropeanSwaption(curve, reversion, volatility, swap, expiry, context);
        Assert.True(
            Math.Abs(calculated - expected) < 2.0e-6,
            $"GSR SOFR swaption price {calculated} differs from Monte Carlo {expected} (payment lag {paymentLag}, lookback {lookbackDays}, observation shift {applyObservationShift})");
    }
}
