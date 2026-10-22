// SPDX-FileCopyrightText: Copyright (C) 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Sergio Araujo
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/overnightindexedswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testNonstandardSwapConversionPreservesObservationConventions, which waits for S-g1d's NonstandardSwap
// Replaced: testNotifications (Vectors/Instruments/OvernightIndexedSwapVectorTests.cs)

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class OvernightIndexedSwapTests
{
    [Fact]
    public void TestFairRate()
    {
        // Testing Estr-swap calculation of fair fixed rate...

        var vars = new CommonVars();

        Period[] lengths =
        [
            new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(5, TimeUnit.Years), new(10, TimeUnit.Years),
            new(20, TimeUnit.Years),
        ];
        Spread[] spreads = [-0.001, -0.01, 0.0, 0.01, 0.001];

        foreach (Period length in lengths)
        {
            foreach (Spread spread in spreads)
            {
                OvernightIndexedSwap swap = vars.MakeSwap(length, 0.0, spread, false);
                OvernightIndexedSwap swap2 = vars.MakeSwap(length, 0.0, spread, true);
                Rate fairRate = vars.Price(swap).FairRate!.Value;
                Rate fairRate2 = vars.Price(swap2).FairRate!.Value;
                Assert.True(
                    Math.Abs(fairRate - fairRate2) <= 1.0e-10,
                    $"fair rates are different: length {length}, floating spread {spread}, fair rate (non telescopic "
                    + $"value dates) {fairRate}, fair rate (telescopic value dates) {fairRate2}");

                swap = vars.MakeSwap(length, fairRate, spread, false);
                Real npv = vars.Price(swap).NPV!.Value;
                Assert.True(
                    Math.Abs(npv) <= 1.0e-10,
                    $"recalculating with implied rate (non telescopic value dates): length {length}, floating spread "
                    + $"{spread}, swap value {npv}");

                swap = vars.MakeSwap(length, vars.Price(swap).FairRate!.Value, spread, true);
                npv = vars.Price(swap).NPV!.Value;
                Assert.True(
                    Math.Abs(npv) <= 1.0e-10,
                    $"recalculating with implied rate (telescopic value dates): length {length}, floating spread "
                    + $"{spread}, swap value {npv}");
            }
        }
    }

    [Fact]
    public void TestFairSpread()
    {
        // Testing Estr-swap calculation of fair floating spread...

        var vars = new CommonVars();

        Period[] lengths =
        [
            new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(5, TimeUnit.Years), new(10, TimeUnit.Years),
            new(20, TimeUnit.Years),
        ];
        Rate[] rates = [0.04, 0.05, 0.06, 0.07];

        foreach (Period length in lengths)
        {
            foreach (Rate j in rates)
            {
                OvernightIndexedSwap swap = vars.MakeSwap(length, j, 0.0, false);
                OvernightIndexedSwap swap2 = vars.MakeSwap(length, j, 0.0, true);
                Spread fairSpread = vars.Price(swap).FairSpread!.Value;
                Spread fairSpread2 = vars.Price(swap2).FairSpread!.Value;
                Assert.True(
                    Math.Abs(fairSpread - fairSpread2) <= 1.0e-10,
                    $"fair spreads are different: length {length}, fixed rate {j}, fair spread (non telescopic value "
                    + $"dates) {fairSpread}, fair spread (telescopic value dates) {fairSpread2}");

                swap = vars.MakeSwap(length, j, fairSpread, false);
                Real npv = vars.Price(swap).NPV!.Value;
                Assert.True(
                    Math.Abs(npv) <= 1.0e-10,
                    $"recalculating with implied spread (non telescopic value dates): length {length}, fixed rate "
                    + $"{j}, fair spread {fairSpread}, swap value {npv}");

                swap = vars.MakeSwap(length, j, fairSpread, true);
                npv = vars.Price(swap).NPV!.Value;
                Assert.True(
                    Math.Abs(npv) <= 1.0e-10,
                    $"recalculating with implied spread (telescopic value dates): length {length}, fixed rate {j}, "
                    + $"fair spread {fairSpread}, swap value {npv}");
            }
        }
    }

    // Adapted: the curve relinked to the settlement date (line 387) becomes the index and the engine built on the new
    // curve.
    [Fact]
    public void TestCachedValue()
    {
        // Testing Estr-swap calculation against cached value...

        var vars = new CommonVars();

        Real flat = 0.05;
        vars = vars.Relinked(new FlatForward(vars.Settlement, flat, new Actual360()));
        Rate fixedRate = Math.Exp(flat) - 1;
        OvernightIndexedSwap swap = vars.MakeSwap(new Period(1, TimeUnit.Years), fixedRate, 0.0, false);
        OvernightIndexedSwap swap2 = vars.MakeSwap(new Period(1, TimeUnit.Years), fixedRate, 0.0, true);
        Real cachedNPV = 0.001730450147;
        Real tolerance = 1.0e-11;
        Real npv = vars.Price(swap).NPV!.Value;
        Real npv2 = vars.Price(swap2).NPV!.Value;
        Assert.True(
            Math.Abs(npv - cachedNPV) <= tolerance,
            $"failed to reproduce cached swap value (non telescopic value dates): calculated {npv}, expected "
            + $"{cachedNPV}");
        Assert.True(
            Math.Abs(npv2 - cachedNPV) <= tolerance,
            $"failed to reproduce cached swap value (telescopic value dates): calculated {npv2}, expected {cachedNPV}");
    }

    [Fact]
    public void TestBaseBootstrap()
    {
        // Testing Estr-swap curve building with daily compounded ON rates...
        TestBootstrap(false, RateAveragingType.Compound);
    }

    [Fact]
    public void TestBootstrapWithArithmeticAverage()
    {
        // Testing Estr-swap curve building with arithmetic average ON rates...
        TestBootstrap(false, RateAveragingType.Simple);
    }

    [Fact]
    public void TestBootstrapWithTelescopicDates()
    {
        // Testing Estr-swap curve building with telescopic value dates and DCON rates...
        TestBootstrap(true, RateAveragingType.Compound);
    }

    [Fact]
    public void TestBootstrapWithTelescopicDatesAndArithmeticAverage()
    {
        // Testing Estr-swap curve building with telescopic value dates and AAON rates...

        // Given that we are using an approximation that omits
        // the required convexity correction, a lower tolerance
        // is needed.
        TestBootstrap(true, RateAveragingType.Simple, 1.0e-5);
    }

    // Adapted: the ESTR term structure relinked to the bootstrapped curve (line 481) becomes a copy of the fixture on
    // it; setCouponPricer on the swap's overnight leg (line 490) returns a copy of the swap.
    [Fact]
    public void TestBootstrapWithCustomPricer()
    {
        // Testing Estr-swap curve building with custom pricer...

        var vars = new CommonVars();

        Natural paymentLag = 2;
        bool telescopicValueDates = false;
        RateAveragingType averagingMethod = RateAveragingType.Simple;
        var pricer = new ArithmeticAveragedOvernightIndexedCouponPricer(0.02, 0.15, true);

        var estrHelpers = new List<RateHelper>();
        Spread spread = 0.0;

        var estr = new Estr();

        foreach (Datum i in EstrSwapData)
        {
            Real rate = 0.01 * i.Rate;
            var term = new Period(i.N, i.Unit);
            var helper = new OISRateHelper(
                i.SettlementDays,
                term,
                rate,
                estr,
                vars.Context,
                null,
                telescopicValueDates,
                paymentLag,
                BusinessDayConvention.Following,
                Frequency.Annual,
                null,
                new Period(0, TimeUnit.Days),
                spread,
                PillarChoice.LastRelevantDate,
                null,
                averagingMethod,
                null,
                null,
                null,
                null,
                0,
                false,
                pricer);
            estrHelpers.Add(helper);
        }

        var estrTS = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            vars.Today, estrHelpers, new Actual365Fixed(), vars.Context);

        vars = vars.Relinked(estrTS);

        // test curve consistency
        foreach (Datum i in EstrSwapData)
        {
            Rate expected = i.Rate / 100;
            var term = new Period(i.N, i.Unit);

            OvernightIndexedSwap swap = vars.MakeSwap(term, 0.0, 0.0, false, null, paymentLag, averagingMethod);
            swap = CouponPricerFunctions.SetCouponPricer(swap, 1, pricer);

            Rate calculated = vars.Price(swap).FairRate!.Value;
            Rate error = Math.Abs(expected - calculated);
            Real tolerance = 1.0e-8;

            Assert.True(
                error <= tolerance,
                $"curve inconsistency:\n swap length:     {term}\n quoted rate:     {expected:R}\n calculated rate: "
                + $"{calculated:R}\n error:           {error:R}\n tolerance:       {tolerance:R}");
        }
    }

    [Fact]
    public void TestBootstrapWithLookbackDays()
    {
        // Testing Estr-swap curve building with lookback days...

        int lookbackDays = 2;
        int lockoutDays = 0;
        bool applyObservationShift = false;
        int paymentLag = 2;

        TestBootstrapWithLookback(lookbackDays, lockoutDays, applyObservationShift, false, paymentLag);

        ArgumentException e = Assert.Throws<ArgumentException>(
            () => TestBootstrapWithLookback(lookbackDays, lockoutDays, applyObservationShift, true, paymentLag));
        Assert.Contains("Telescopic formula cannot be applied", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TestBootstrapWithLookbackDaysAndShift()
    {
        // Testing Estr-swap curve building with lookback days and observation shift...

        int lookbackDays = 2;
        int lockoutDays = 0;
        bool applyObservationShift = true;
        int paymentLag = 2;

        TestBootstrapWithLookback(lookbackDays, lockoutDays, applyObservationShift, false, paymentLag);

        TestBootstrapWithLookback(lookbackDays, lockoutDays, applyObservationShift, true, paymentLag);
    }

    [Fact]
    public void TestBootstrapWithLockoutDays()
    {
        // Testing Estr-swap curve building with lookback and lockout days...

        int lookbackDays = 2;
        int lockoutDays = 2;
        bool applyObservationShift = false;
        int paymentLag = 0;

        TestBootstrapWithLookback(lookbackDays, lockoutDays, applyObservationShift, false, paymentLag);

        ArgumentException e = Assert.Throws<ArgumentException>(
            () => TestBootstrapWithLookback(lookbackDays, lockoutDays, applyObservationShift, true, paymentLag));
        Assert.Contains("Telescopic formula cannot be applied", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TestBootstrapWithLockoutDaysAndShift()
    {
        // Testing Estr-swap curve building with lookback and lockout days and observation shift...

        int lookbackDays = 2;
        int lockoutDays = 2;
        bool applyObservationShift = true;
        int paymentLag = 0;

        TestBootstrapWithLookback(lookbackDays, lockoutDays, applyObservationShift, false, paymentLag);

        TestBootstrapWithLookback(lookbackDays, lockoutDays, applyObservationShift, true, paymentLag);
    }

    // Adapted: the fixings added to the index (lines 644-647) become the context's store.
    [Fact]
    public void TestSeasonedSwaps()
    {
        // Testing seasoned Estr-swap calculation...

        var vars = new CommonVars();

        Period[] lengths =
        [
            new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(5, TimeUnit.Years), new(10, TimeUnit.Years),
            new(20, TimeUnit.Years),
        ];
        Spread[] spreads = [-0.001, -0.01, 0.0, 0.01, 0.001];

        Date effectiveDate = new(2, February, 2009);

        vars = vars.WithFixings(vars.Context.Fixings
            .AddFixing(vars.EstrIndex, new Date(2, February, 2009), 0.0010) // fake fixing values
            .AddFixing(vars.EstrIndex, new Date(3, February, 2009), 0.0011)
            .AddFixing(vars.EstrIndex, new Date(4, February, 2009), 0.0012)
            .AddFixing(vars.EstrIndex, new Date(5, February, 2009), 0.0013));

        foreach (Period length in lengths)
        {
            foreach (Spread spread in spreads)
            {
                OvernightIndexedSwap swap = vars.MakeSwap(length, 0.0, spread, false, effectiveDate);
                OvernightIndexedSwap swap2 = vars.MakeSwap(length, 0.0, spread, true, effectiveDate);
                Real npv = vars.Price(swap).NPV!.Value;
                Real npv2 = vars.Price(swap2).NPV!.Value;
                Assert.True(
                    Math.Abs(npv - npv2) <= 1.0e-10,
                    $"swap npv is different: length {length}, floating spread {spread}, swap value (non telescopic "
                    + $"value dates) {npv}, swap value (telescopic value dates) {npv2}");
            }
        }
    }

    // Adapted: the check that discount(1.0) doesn't throw (line 728) is the curve's construction, which bootstraps,
    // and the discount; the moving curve (line 725) is built by Create in the case's context, the MonotonicLogCubic
    // given converting to the LogCubic it is.
    [Fact]
    public void TestBootstrapRegression()
    {
        // Testing 1.16 regression with OIS bootstrap...

        Datum[] data =
        [
            new(0, 1, TimeUnit.Days, 0.0066),
            new(2, 1, TimeUnit.Weeks, 0.006445),
            new(2, 2, TimeUnit.Weeks, 0.006455),
            new(2, 3, TimeUnit.Weeks, 0.00645),
            new(2, 1, TimeUnit.Months, 0.00675),
            new(2, 2, TimeUnit.Months, 0.007),
            new(2, 3, TimeUnit.Months, 0.00724),
            new(2, 4, TimeUnit.Months, 0.007533),
            new(2, 5, TimeUnit.Months, 0.00785),
            new(2, 6, TimeUnit.Months, 0.00814),
            new(2, 9, TimeUnit.Months, 0.00889),
            new(2, 1, TimeUnit.Years, 0.00967),
            new(2, 2, TimeUnit.Years, 0.01221),
            new(2, 3, TimeUnit.Years, 0.01413),
            new(2, 4, TimeUnit.Years, 0.01555),
            new(2, 5, TimeUnit.Years, 0.01672),
            new(2, 10, TimeUnit.Years, 0.02005),
            new(2, 12, TimeUnit.Years, 0.0208),
            new(2, 15, TimeUnit.Years, 0.02152),
            new(2, 20, TimeUnit.Years, 0.02215),
            new(2, 25, TimeUnit.Years, 0.02233),
            new(2, 30, TimeUnit.Years, 0.02234),
            new(2, 40, TimeUnit.Years, 0.02233),
        ];

        var context = new EvaluationContext { EvaluationDate = new Date(21, February, 2017) };

        var helpers = new List<RateHelper>();
        var index = new FedFunds();
        Spread spread = 0.0;

        helpers.Add(
            new DepositRateHelper(
                data[0].Rate,
                new Period(data[0].N, data[0].Unit),
                index.FixingDays,
                index.FixingCalendar,
                index.BusinessDayConvention,
                index.EndOfMonth,
                index.DayCounter,
                context));

        for (int i = 1; i < data.Length; ++i)
        {
            helpers.Add(
                new OISRateHelper(
                    data[i].SettlementDays,
                    new Period(data[i].N, data[i].Unit),
                    data[i].Rate,
                    index,
                    context,
                    null,
                    false,
                    2,
                    BusinessDayConvention.Following,
                    Frequency.Annual,
                    null,
                    new Period(0, TimeUnit.Days),
                    spread,
                    // this bootstrap fails with the default LastRelevantDate choice
                    PillarChoice.MaturityDate));
        }

        var curve = PiecewiseYieldCurve<Discount, LogCubic, IterativeBootstrap>.Create(
            0, new UnitedStates(UnitedStatesMarket.GovernmentBond), helpers, new Actual365Fixed(), context,
            i: new MonotonicLogCubic());

        _ = curve.Discount(1.0);
    }

    // Adapted: the check that nodes() doesn't throw (line 744) becomes a check that construction, which bootstraps,
    // doesn't throw; the moving curve (line 743) is built by Create in the case's context.
    [Fact]
    public void Test131BootstrapRegression()
    {
        // Testing 1.31 regression with OIS bootstrap...

        var today = new Date(11, December, 2012);
        var context = new EvaluationContext { EvaluationDate = today };

        var estr = new Estr();

        var helpers = new List<RateHelper>
        {
            new OISRateHelper(2, new Period(1, TimeUnit.Weeks), 0.070 / 100, estr, context),
            new OISRateHelper(new Date(16, January, 2013), new Date(13, February, 2013), 0.046 / 100, estr, context),
        };

        var curve = PiecewiseYieldCurve<ForwardRate, BackwardFlat, IterativeBootstrap>.Create(
            0, new TARGET(), helpers, new Actual365Fixed(), context);
        _ = curve.Nodes();
    }

    // Adapted: the check that nodes() doesn't throw (line 787) becomes a check that construction, which bootstraps,
    // doesn't throw.
    [Fact]
    public void TestBootstrapWithDifferentCalendars()
    {
        // Testing OIS bootstrap when the swap maturity is not a fixing day for the index...

        var today = new Date(10, April, 2025);
        var context = new EvaluationContext { EvaluationDate = today };

        Datum[] data =
        [
            new(2, 1, TimeUnit.Years, 0.037755),
            new(2, 2, TimeUnit.Years, 0.034115),
            new(2, 3, TimeUnit.Years, 0.033417),
        ];

        var helpers = new List<RateHelper>();
        var index = new Sofr();

        var calendar = new UnitedStates(UnitedStatesMarket.FederalReserve);

        foreach (Datum i in data)
        {
            helpers.Add(new OISRateHelper(
                i.SettlementDays,
                new Period(i.N, i.Unit),
                i.Rate,
                index,
                context,
                null,
                false,
                0,
                BusinessDayConvention.Following,
                Frequency.Annual,
                calendar,
                new Period(0, TimeUnit.Days),
                0.0,
                PillarChoice.LastRelevantDate,
                null,
                RateAveragingType.Compound,
                null,
                null,
                calendar,
                null,
                0,
                false,
                null,
                DateGenerationRule.Backward,
                calendar));
        }

        Assert.Equal(new Date(14, April, 2028), helpers[^1].MaturityDate); // Good Friday; holiday for SOFR
                                                                             // but not for Federal Reserve
        Assert.Equal(new Date(17, April, 2028), helpers[^1].LatestRelevantDate); // end of last fixing

        var curve = new PiecewiseYieldCurve<ForwardRate, BackwardFlat, IterativeBootstrap>(
            today, helpers, new Actual365Fixed(), context);
        _ = curve.Nodes();
    }

    [Fact]
    public void TestConstructorsAndNominals()
    {
        // Testing different constructors for OIS...

        var vars = new CommonVars();

        Date spot = vars.Calendar.Advance(vars.Today, 2, TimeUnit.Days);
        Real nominal = 100000.0;

        // constant notional, same schedule

        Schedule schedule = new MakeSchedule()
            .From(spot)
            .To(vars.Calendar.Advance(spot, 2, TimeUnit.Years))
            .WithCalendar(vars.Calendar)
            .WithFrequency(Frequency.Annual)
            .ToSchedule();

        var ois1 = new OvernightIndexedSwap(SwapType.Payer, nominal, schedule, 0.03, new Actual360(), vars.EstrIndex);

        Assert.Equal(new Period(1, TimeUnit.Years), ois1.FixedSchedule.Tenor());
        Assert.Equal(new Period(1, TimeUnit.Years), ois1.OvernightSchedule.Tenor());
        Assert.Equal(Frequency.Annual, ois1.PaymentFrequency());

        Assert.Equal(nominal, ois1.Nominal());

        Assert.Single(ois1.Nominals());
        Assert.Equal(nominal, ois1.Nominals()[0]);

        Assert.Single(ois1.FixedNominals);
        Assert.Equal(nominal, ois1.FixedNominals[0]);

        Assert.Single(ois1.OvernightNominals);
        Assert.Equal(nominal, ois1.OvernightNominals[0]);

        // amortizing notionals, same schedule

        var ois2 = new OvernightIndexedSwap(
            SwapType.Payer, [nominal, nominal / 2], schedule, 0.03, new Actual360(), vars.EstrIndex);

        Assert.Equal(new Period(1, TimeUnit.Years), ois2.FixedSchedule.Tenor());
        Assert.Equal(new Period(1, TimeUnit.Years), ois2.OvernightSchedule.Tenor());
        Assert.Equal(Frequency.Annual, ois2.PaymentFrequency());

        NanoQLException e = Assert.Throws<NanoQLException>(() => ois2.Nominal());
        Assert.Contains("nominal is not constant", e.Message, StringComparison.Ordinal);

        Assert.Equal(2, ois2.Nominals().Length);
        Assert.Equal(nominal, ois2.Nominals()[0]);
        Assert.Equal(nominal / 2, ois2.Nominals()[1]);

        Assert.Equal(2, ois2.FixedNominals.Length);
        Assert.Equal(nominal, ois2.FixedNominals[0]);
        Assert.Equal(nominal / 2, ois2.FixedNominals[1]);

        Assert.Equal(2, ois2.OvernightNominals.Length);
        Assert.Equal(nominal, ois2.OvernightNominals[0]);
        Assert.Equal(nominal / 2, ois2.OvernightNominals[1]);

        // constant notional, different schedules

        Schedule fixedSchedule = schedule;
        Schedule overnightSchedule = new MakeSchedule()
            .From(spot)
            .To(vars.Calendar.Advance(spot, 2, TimeUnit.Years))
            .WithCalendar(vars.Calendar)
            .WithFrequency(Frequency.Semiannual)
            .ToSchedule();

        var ois3 = new OvernightIndexedSwap(
            SwapType.Payer, nominal, fixedSchedule, 0.03, new Actual360(), overnightSchedule, vars.EstrIndex);

        Assert.Equal(new Period(1, TimeUnit.Years), ois3.FixedSchedule.Tenor());
        Assert.Equal(new Period(6, TimeUnit.Months), ois3.OvernightSchedule.Tenor());
        Assert.Equal(Frequency.Semiannual, ois3.PaymentFrequency());

        Assert.Equal(nominal, ois3.Nominal());

        Assert.Single(ois3.Nominals());
        Assert.Equal(nominal, ois3.Nominals()[0]);

        Assert.Single(ois3.FixedNominals);
        Assert.Equal(nominal, ois3.FixedNominals[0]);

        Assert.Single(ois3.OvernightNominals);
        Assert.Equal(nominal, ois3.OvernightNominals[0]);

        // amortizing notionals, different schedules

        var ois4 = new OvernightIndexedSwap(
            SwapType.Payer,
            [nominal, nominal / 2],
            fixedSchedule,
            0.03,
            new Actual360(),
            [nominal, nominal, nominal / 2, nominal / 2],
            overnightSchedule,
            vars.EstrIndex);

        Assert.Equal(new Period(1, TimeUnit.Years), ois4.FixedSchedule.Tenor());
        Assert.Equal(new Period(6, TimeUnit.Months), ois4.OvernightSchedule.Tenor());
        Assert.Equal(Frequency.Semiannual, ois4.PaymentFrequency());

        e = Assert.Throws<NanoQLException>(() => ois4.Nominal());
        Assert.Contains("nominal is not constant", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<NanoQLException>(() => ois4.Nominals());
        Assert.Contains("different nominals", e.Message, StringComparison.Ordinal);

        Assert.Equal(2, ois4.FixedNominals.Length);
        Assert.Equal(nominal, ois4.FixedNominals[0]);
        Assert.Equal(nominal / 2, ois4.FixedNominals[1]);

        Assert.Equal(4, ois4.OvernightNominals.Length);
        Assert.Equal(nominal, ois4.OvernightNominals[0]);
        Assert.Equal(nominal, ois4.OvernightNominals[1]);
        Assert.Equal(nominal / 2, ois4.OvernightNominals[2]);
        Assert.Equal(nominal / 2, ois4.OvernightNominals[3]);
    }

    // Adapted: the evaluation dates (lines 965 and 1014) are two contexts.
    [Fact]
    public void TestMakeOISDefaultSettlementDays()
    {
        // Testing default settlement days in MakeOIS...

        Date today = new(12, May, 2025);
        var context = new EvaluationContext { EvaluationDate = today };

        // Create all overnight indices
        (string Name, OvernightIndex Index)[] indices =
        [
            // 0-day settlement index
            ("SONIA", new Sonia()),

            // 1-day settlement index
            ("CORRA", new Corra()),

            // 2-day settlement indices
            ("EONIA", new Eonia()),
            ("ESTR", new Estr()),
            ("FedFunds", new FedFunds()),
            ("SOFR", new Sofr()),
            ("AONIA", new Aonia()),
            ("TONAR", new Tonar()),
            ("SARON", new Saron()),
            ("NZOCR", new Nzocr()),
            ("DESTR", new Destr()),
            ("SWESTR", new Swestr()),
            ("KOFR", new Kofr()),
        ];

        // Test default settlement days
        foreach ((string name, OvernightIndex index) in indices)
        {
            OvernightIndexedSwap swap = new MakeOIS(new Period(6, TimeUnit.Months), index)
                .WithFixedRate(0.01)
                .ToOvernightIndexedSwap(context);
            Date expected = name switch
            {
                "SONIA" => today, // T+0 settlement for SONIA
                "CORRA" => today + new Period(1, TimeUnit.Days), // T+1 settlement for CORRA
                _ => today + new Period(2, TimeUnit.Days), // T+2 settlement for all others
            };
            Assert.Equal(expected, swap.StartDate());
        }

        // Test manual override
        foreach ((string name, OvernightIndex index) in indices)
        {
            // Override settlement days: 2 for CORRA, 1 for all others
            Natural settlementDaysOverride = name == "CORRA" ? 2 : 1;
            OvernightIndexedSwap swap = new MakeOIS(new Period(6, TimeUnit.Months), index)
                .WithFixedRate(0.01)
                .WithSettlementDays(settlementDaysOverride)
                .ToOvernightIndexedSwap(context);
            Date expected = today + new Period(settlementDaysOverride, TimeUnit.Days);
            Assert.Equal(expected, swap.StartDate());
        }

        // Test weekend handling
        Date weekend = new(10, May, 2025); // Saturday
        EvaluationContext onWeekend = context with { EvaluationDate = weekend };

        // Test 0-day settlement index on weekend
        {
            OvernightIndexedSwap swap = new MakeOIS(new Period(6, TimeUnit.Months), indices[0].Index)
                .WithFixedRate(0.01)
                .ToOvernightIndexedSwap(onWeekend); // SONIA
            Date expected = new(12, May, 2025); // Monday
            Assert.Equal(expected, swap.StartDate());
        }

        // Test 1-day settlement index on weekend
        {
            OvernightIndexedSwap swap = new MakeOIS(new Period(6, TimeUnit.Months), indices[1].Index)
                .WithFixedRate(0.01)
                .ToOvernightIndexedSwap(onWeekend); // CORRA
            Date expected = new(12, May, 2025); // Monday: T+1 from the actual trade date
            Assert.Equal(expected, swap.StartDate());
        }

        // Test 2-day settlement index on weekend
        {
            OvernightIndexedSwap swap = new MakeOIS(new Period(6, TimeUnit.Months), indices[2].Index)
                .WithFixedRate(0.01)
                .ToOvernightIndexedSwap(onWeekend); // EONIA
            Date expected = new(13, May, 2025); // Tuesday: T+2 from the actual trade date
            Assert.Equal(expected, swap.StartDate());
        }
    }

    // Adapted: the evaluation date (line 1046) is the context's, and the index's curve (line 1049) is QuantLib's
    // flatRate on it.
    [Fact]
    public void TestMakeOisEndOfMonthRegression2453()
    {
        /* See https://github.com/lballabio/QuantLib/issues/2453. Before the fix, the swap will roll
         * backwards from 18 December 2028 (instead of 17 December 2028) and create a front stub. */

        // Testing end-of-month regression in MakeOIS...

        Date today = new(16, December, 2025);
        var context = new EvaluationContext { EvaluationDate = today };

        var aonia = new Aonia(FlatForward.Create(0, new NullCalendar(), 0.03, new Actual365Fixed(), context));
        OvernightIndexedSwap swap = new MakeOIS(3 * TimeUnit.Years, aonia)
            .WithSettlementDays(1)
            .WithEndOfMonth(true)
            .ToOvernightIndexedSwap(context);

        Assert.Equal(new Date(17, December, 2025), swap.OvernightSchedule[0]);
        Assert.Equal(new Date(17, December, 2026), swap.OvernightSchedule[1]);
    }

    // Adapted: the curve linked to a handle (line 1127) and the index built on the handle after it (line 1129) become
    // the index built on the curve, and the evaluation date (line 1124) is the context's.
    [Fact]
    public void TestSettlementDaysEffectiveDateConflict()
    {
        // Testing that MakeOIS rejects settlementDays and effectiveDate together...

        Date today = new(5, February, 2009);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure yts = new FlatForward(today, 0.05, new Actual365Fixed());

        var index = new Estr(yts);
        Date effectiveDate = new(9, February, 2009);

        // settlementDays first, then effectiveDate
        NanoQLException e = Assert.Throws<NanoQLException>(() =>
            new MakeOIS(new Period(5, TimeUnit.Years), index)
                .WithFixedRate(0.03)
                .WithSettlementDays(2)
                .WithEffectiveDate(effectiveDate)
                .ToOvernightIndexedSwap(context));
        Assert.Contains("cannot set both", e.Message, StringComparison.Ordinal);

        // effectiveDate first, then settlementDays
        e = Assert.Throws<NanoQLException>(() =>
            new MakeOIS(new Period(5, TimeUnit.Years), index)
                .WithFixedRate(0.03)
                .WithEffectiveDate(effectiveDate)
                .WithSettlementDays(2)
                .ToOvernightIndexedSwap(context));
        Assert.Contains("cannot set both", e.Message, StringComparison.Ordinal);

        // withSettlementDays alone works
        OvernightIndexedSwap swap1 = new MakeOIS(new Period(5, TimeUnit.Years), index)
            .WithFixedRate(0.03)
            .WithSettlementDays(2)
            .ToOvernightIndexedSwap(context);
        Assert.True(swap1.StartDate() != default);

        // withEffectiveDate alone works
        OvernightIndexedSwap swap2 = new MakeOIS(new Period(5, TimeUnit.Years), index)
            .WithFixedRate(0.03)
            .WithEffectiveDate(effectiveDate)
            .ToOvernightIndexedSwap(context);
        Assert.Equal(effectiveDate, swap2.StartDate());

        // neither set (constructor defaults) works
        OvernightIndexedSwap swap3 = new MakeOIS(new Period(5, TimeUnit.Years), index)
            .WithFixedRate(0.03)
            .ToOvernightIndexedSwap(context);
        Assert.True(swap3.StartDate() != default);
    }

    [Fact]
    public void TestRoundingPrecision()
    {
        // Testing rounding of the compounded rate in overnight-indexed swaps...

        var vars = new CommonVars();
        EvaluationContext context = vars.Context;

        Integer precision = 6;
        Rate fixedRate = 0.03;
        var length = new Period(2, TimeUnit.Years);

        OvernightIndexedSwap plainSwap = vars.MakeSwap(length, fixedRate, 0.0, false);
        OvernightIndexedSwap roundedSwap =
            vars.MakeSwap(length, fixedRate, 0.0, false, null, 0, RateAveragingType.Compound, precision);

        Assert.True(roundedSwap.RoundingPrecision == precision);
        Assert.True(plainSwap.RoundingPrecision is null);

        var round = new ClosestRounding(precision);
        bool anyRoundingApplied = false;
        for (int i = 0; i < roundedSwap.OvernightLeg.Length; ++i)
        {
            var rounded = (Coupon)roundedSwap.OvernightLeg[i];
            var plain = (Coupon)plainSwap.OvernightLeg[i];

            // the projected rate is not affected by the rounding...
            Assert.True(
                Math.Abs(rounded.Rate(context) - plain.Rate(context)) <= 1.0e-15,
                $"rounding changed the projected rate: without rounding {plain.Rate(context)}, with rounding "
                + $"{rounded.Rate(context)}");

            // ...but the amount is computed off the rounded rate
            Real expected = rounded.Nominal(context) * round.Evaluate(rounded.Rate(context)) * rounded.AccrualPeriod();
            Assert.True(
                Math.Abs(rounded.Amount(context) - expected) <= 1.0e-10,
                $"amount not consistent with the rounded rate: expected {expected}, calculated "
                + $"{rounded.Amount(context)}");

            if (rounded.Amount(context) != plain.Amount(context))
            {
                anyRoundingApplied = true;
            }
        }

        Assert.True(
            anyRoundingApplied,
            "all coupon amounts unchanged; rounding precision was not propagated to the overnight leg");

        // the engine prices off the rounded amounts, so the leg NPV
        // remains consistent with the discounted sum of the cashflows
        Real calculated = vars.Price(roundedSwap).OvernightLegNPV!.Value;
        Real expectedNPV = 0.0;
        foreach (CashFlow cf in roundedSwap.OvernightLeg)
        {
            expectedNPV += cf.Amount(context) * vars.EstrTermStructure.Discount(cf.Date);
        }

        Assert.True(
            Math.Abs(calculated - expectedNPV) <= 1.0e-10,
            $"overnight-leg NPV inconsistent with discounted rounded cashflows: NPV {calculated}, discounted sum "
            + $"{expectedNPV}");

        // MakeOIS solves the ATM rate on the same rounded coupons the
        // returned swap is built with, so the swap is at market
        OvernightIndexedSwap atmSwap =
            vars.MakeSwap(length, null, 0.0, false, null, 0, RateAveragingType.Compound, precision);
        Real atmNpv = vars.Price(atmSwap).NPV!.Value;
        Assert.True(Math.Abs(atmNpv) <= 1.0e-10, $"ATM swap with rounding does not price to zero: NPV {atmNpv}");

        // the constructor propagates the rounding precision like MakeOIS
        var ctorSwap = new OvernightIndexedSwap(
            SwapType.Payer,
            vars.Nominal,
            roundedSwap.FixedSchedule,
            fixedRate,
            vars.FixedEstrDayCount,
            roundedSwap.OvernightSchedule,
            vars.EstrIndex,
            0.0,
            0,
            BusinessDayConvention.Following,
            null,
            false,
            RateAveragingType.Compound,
            null,
            0,
            false,
            precision);
        for (int i = 0; i < ctorSwap.OvernightLeg.Length; ++i)
        {
            Real viaCtor = ctorSwap.OvernightLeg[i].Amount(context);
            Real viaMakeOIS = roundedSwap.OvernightLeg[i].Amount(context);
            Assert.True(
                viaCtor == viaMakeOIS,
                $"constructor and MakeOIS produce different rounded amounts: constructor {viaCtor}, MakeOIS "
                + $"{viaMakeOIS}");
        }
    }

    // Adapted: the evaluation date (line 1269) is the context's.
    [Fact]
    public void TestSpotDateFromNonBusinessEvaluationDate()
    {
        // Testing that the OIS spot date is calculated from the actual evaluation date when the latter is not a
        // business day...

        // Saturday
        Date today = new(20, June, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        var index = new Estr();
        Calendar calendar = index.FixingCalendar;

        // settlement days are counted from the actual trade date,
        // not from the next business day
        Date expectedStart = calendar.Advance(today, 2, TimeUnit.Days);

        OvernightIndexedSwap swap = new MakeOIS(new Period(1, TimeUnit.Years), index)
            .WithFixedRate(0.03)
            .WithSettlementDays(2)
            .ToOvernightIndexedSwap(context);

        Assert.True(
            swap.StartDate() == expectedStart,
            $"OIS start date not calculated from the actual evaluation date: expected {expectedStart}, obtained "
            + $"{swap.StartDate()}");
    }

    // Adapted: the evaluation date (line 1297) is the context's.
    [Fact]
    public void TestSettlementCalendar()
    {
        // Testing that the OIS spot date can be calculated on an explicit settlement calendar...

        // 3 July 2026 is a TARGET business day, but a US holiday
        // (Independence Day observed), so the settlement calendar and the
        // index fixing calendar diverge between here and the spot date.
        Date today = new(2, July, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        var index = new Estr();
        Calendar settlementCalendar = new JointCalendar(new TARGET(), new UnitedStates(UnitedStatesMarket.Settlement));

        Date expectedStart = settlementCalendar.Advance(today, 2, TimeUnit.Days);

        OvernightIndexedSwap swap = new MakeOIS(new Period(1, TimeUnit.Years), index)
            .WithFixedRate(0.03)
            .WithSettlementDays(2)
            .WithSettlementCalendar(settlementCalendar)
            .ToOvernightIndexedSwap(context);

        Assert.True(
            swap.StartDate() == expectedStart,
            $"OIS start date not calculated on the settlement calendar: expected {expectedStart}, obtained "
            + $"{swap.StartDate()}");

        // sanity check: the two calendars must actually diverge here
        Assert.True(expectedStart != index.FixingCalendar.Advance(today, 2, TimeUnit.Days));
    }

    private static readonly Datum[] DepositData =
    [
        new(0, 1, TimeUnit.Days, 1.10),
        new(1, 1, TimeUnit.Days, 1.10),
        new(2, 1, TimeUnit.Weeks, 1.40),
        new(2, 2, TimeUnit.Weeks, 1.50),
        new(2, 1, TimeUnit.Months, 1.70),
        new(2, 2, TimeUnit.Months, 1.90),
        new(2, 3, TimeUnit.Months, 2.05),
        new(2, 4, TimeUnit.Months, 2.08),
        new(2, 5, TimeUnit.Months, 2.11),
        new(2, 6, TimeUnit.Months, 2.13),
    ];

    private static readonly Datum[] EstrSwapData =
    [
        new(2, 1, TimeUnit.Weeks, 1.245),
        new(2, 2, TimeUnit.Weeks, 1.269),
        new(2, 3, TimeUnit.Weeks, 1.277),
        new(2, 1, TimeUnit.Months, 1.281),
        new(2, 2, TimeUnit.Months, 1.18),
        new(2, 3, TimeUnit.Months, 1.143),
        new(2, 4, TimeUnit.Months, 1.125),
        new(2, 5, TimeUnit.Months, 1.116),
        new(2, 6, TimeUnit.Months, 1.111),
        new(2, 7, TimeUnit.Months, 1.109),
        new(2, 8, TimeUnit.Months, 1.111),
        new(2, 9, TimeUnit.Months, 1.117),
        new(2, 10, TimeUnit.Months, 1.129),
        new(2, 11, TimeUnit.Months, 1.141),
        new(2, 12, TimeUnit.Months, 1.153),
        new(2, 15, TimeUnit.Months, 1.218),
        new(2, 18, TimeUnit.Months, 1.308),
        new(2, 21, TimeUnit.Months, 1.407),
        new(2, 2, TimeUnit.Years, 1.510),
        new(2, 3, TimeUnit.Years, 1.916),
        new(2, 4, TimeUnit.Years, 2.254),
        new(2, 5, TimeUnit.Years, 2.523),
        new(2, 6, TimeUnit.Years, 2.746),
        new(2, 7, TimeUnit.Years, 2.934),
        new(2, 8, TimeUnit.Years, 3.092),
        new(2, 9, TimeUnit.Years, 3.231),
        new(2, 10, TimeUnit.Years, 3.380),
        new(2, 11, TimeUnit.Years, 3.457),
        new(2, 12, TimeUnit.Years, 3.544),
        new(2, 15, TimeUnit.Years, 3.702),
        new(2, 20, TimeUnit.Years, 3.703),
        new(2, 25, TimeUnit.Years, 3.541),
        new(2, 30, TimeUnit.Years, 3.369),
    ];

    // Adapted: the ESTR term structure relinked to the bootstrapped curve (line 271) becomes a copy of the fixture on
    // it.
    private static void TestBootstrap(
        bool telescopicValueDates, RateAveragingType averagingMethod, Real tolerance = 1.0e-8)
    {
        var vars = new CommonVars();

        Natural paymentLag = 2;

        var estrHelpers = new List<RateHelper>();
        Spread spread = 0.0;

        var euribor3m = new Euribor3M();
        var estr = new Estr();

        foreach (Datum i in DepositData)
        {
            Real rate = 0.01 * i.Rate;
            var term = new Period(i.N, i.Unit);
            var helper = new DepositRateHelper(
                rate,
                term,
                i.SettlementDays,
                euribor3m.FixingCalendar,
                euribor3m.BusinessDayConvention,
                euribor3m.EndOfMonth,
                euribor3m.DayCounter,
                vars.Context);

            if (term <= new Period(2, TimeUnit.Days))
            {
                estrHelpers.Add(helper);
            }
        }

        foreach (Datum i in EstrSwapData)
        {
            Real rate = 0.01 * i.Rate;
            var term = new Period(i.N, i.Unit);
            var helper = new OISRateHelper(
                i.SettlementDays,
                term,
                rate,
                estr,
                vars.Context,
                null,
                telescopicValueDates,
                paymentLag,
                BusinessDayConvention.Following,
                Frequency.Annual,
                null,
                new Period(0, TimeUnit.Days),
                spread,
                PillarChoice.LastRelevantDate,
                null,
                averagingMethod);
            estrHelpers.Add(helper);
        }

        var estrTS = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            vars.Today, estrHelpers, new Actual365Fixed(), vars.Context);

        vars = vars.Relinked(estrTS);

        // test curve consistency
        foreach (Datum i in EstrSwapData)
        {
            Rate expected = i.Rate / 100;
            var term = new Period(i.N, i.Unit);

            // test telescopic value dates (in bootstrap) against non telescopic value dates (swap here)
            OvernightIndexedSwap swap = vars.MakeSwap(term, 0.0, 0.0, false, null, paymentLag, averagingMethod);
            Rate calculated = vars.Price(swap).FairRate!.Value;
            Rate error = Math.Abs(expected - calculated);

            Assert.True(
                error <= tolerance,
                $"curve inconsistency:\n swap length:     {term}\n quoted rate:     {expected:R}\n calculated rate: "
                + $"{calculated:R}\n error:           {error:R}\n tolerance:       {tolerance:R}");
        }
    }

    // Adapted: the ESTR term structure relinked to the bootstrapped curve (line 551) becomes a copy of the fixture on
    // it.
    private static void TestBootstrapWithLookback(
        Natural lookbackDays,
        Natural lockoutDays,
        bool applyObservationShift,
        bool telescopicValueDates,
        Natural paymentLag)
    {
        var vars = new CommonVars();

        var estrHelpers = new List<RateHelper>();

        var estr = new Estr();
        Spread spread = 0.0;

        foreach (Datum i in EstrSwapData)
        {
            Real rate = 0.01 * i.Rate;
            var term = new Period(i.N, i.Unit);
            var helper = new OISRateHelper(
                i.SettlementDays,
                term,
                rate,
                estr,
                vars.Context,
                null,
                telescopicValueDates,
                paymentLag,
                BusinessDayConvention.Following,
                Frequency.Annual,
                null,
                new Period(0, TimeUnit.Days),
                spread,
                PillarChoice.LastRelevantDate,
                null,
                RateAveragingType.Compound,
                null,
                null,
                null,
                lookbackDays,
                lockoutDays,
                applyObservationShift);
            estrHelpers.Add(helper);
        }

        var estrTS = new PiecewiseYieldCurve<ForwardRate, BackwardFlat, IterativeBootstrap>(
            vars.Today, estrHelpers, new Actual365Fixed(), vars.Context);

        vars = vars.Relinked(estrTS);

        // test curve consistency
        foreach (Datum i in EstrSwapData)
        {
            Rate expected = i.Rate / 100;
            var term = new Period(i.N, i.Unit);
            OvernightIndexedSwap swap = vars.MakeSwapWithLookback(
                term, 0.0, paymentLag, lookbackDays, lockoutDays, applyObservationShift, telescopicValueDates);
            Rate calculated = vars.Price(swap).FairRate!.Value;
            Rate error = Math.Abs(expected - calculated);
            Real tolerance = 1e-8;

            Assert.True(
                error <= tolerance,
                $"curve inconsistency:\n swap length:     {term}\n quoted rate:     {expected:R}\n calculated rate: "
                + $"{calculated:R}\n error:           {error:R}\n tolerance:       {tolerance:R}");
        }
    }

    private readonly record struct Datum(Integer SettlementDays, Integer N, TimeUnit Unit, Rate Rate);

    // Adapted: the evaluation date set by the fixture (lines 212-213) is its context; the ESTR term structure, a handle
    // linked after the index is built on it (lines 206 and 215), becomes the curve the index is built on, and a relink
    // becomes a copy of the fixture on the new curve; the engine each swap holds (line 171) becomes the pricing of each
    // swap on that curve, without the settlement date's flows, as withDiscountingTermStructure sets it; the swap index
    // and its term structure (lines 152-153, 210) are left out, since no ported case reads them.
    private sealed class CommonVars
    {
        public CommonVars()
            : this(
                new EvaluationContext { EvaluationDate = new Date(5, February, 2009) },
                new FlatForward(new Date(5, February, 2009), 0.05, new Actual365Fixed()))
        {
        }

        private CommonVars(EvaluationContext context, YieldTermStructure estrTermStructure)
        {
            Context = context;
            Type = SwapType.Payer;
            SettlementDays = 2;
            Nominal = 100.0;
            FixedEstrConvention = BusinessDayConvention.ModifiedFollowing;
            FloatingEstrConvention = BusinessDayConvention.ModifiedFollowing;
            FixedEstrPeriod = new Period(1, TimeUnit.Years);
            FloatingEstrPeriod = new Period(1, TimeUnit.Years);
            FixedEstrDayCount = new Actual360();
            EstrTermStructure = estrTermStructure;
            EstrIndex = new Estr(estrTermStructure);
            Calendar = EstrIndex.FixingCalendar;
            Today = context.EvaluationDate;
            Settlement = Calendar.Advance(Today, SettlementDays, TimeUnit.Days, BusinessDayConvention.Following);
        }

        public EvaluationContext Context { get; }

        public Date Today { get; }

        public Date Settlement { get; }

        public SwapType Type { get; }

        public Real Nominal { get; }

        public Calendar Calendar { get; }

        public Natural SettlementDays { get; }

        public Period FixedEstrPeriod { get; }

        public Period FloatingEstrPeriod { get; }

        public DayCounter FixedEstrDayCount { get; }

        public BusinessDayConvention FixedEstrConvention { get; }

        public BusinessDayConvention FloatingEstrConvention { get; }

        public Estr EstrIndex { get; }

        public YieldTermStructure EstrTermStructure { get; }

        // The fixture with its curve relinked: the index built on the new curve.
        public CommonVars Relinked(YieldTermStructure estrTermStructure) => new(Context, estrTermStructure);

        // The fixture with fixings stored.
        public CommonVars WithFixings(IndexFixings fixings) =>
            new(Context with { Fixings = fixings }, EstrTermStructure);

        public OvernightIndexedSwap MakeSwap(
            Period length,
            Rate? fixedRate,
            Spread spread,
            bool telescopicValueDates,
            Date? effectiveDate = null,
            Integer paymentLag = 0,
            RateAveragingType averagingMethod = RateAveragingType.Compound,
            int? roundingPrecision = null) =>
            new MakeOIS(length, EstrIndex)
                .WithFixedRate(fixedRate)
                .WithEffectiveDate(effectiveDate ?? Settlement)
                .WithOvernightLegSpread(spread)
                .WithNominal(Nominal)
                .WithPaymentLag(paymentLag)
                .WithDiscountingTermStructure(EstrTermStructure)
                .WithTelescopicValueDates(telescopicValueDates)
                .WithAveragingMethod(averagingMethod)
                .WithRoundingPrecision(roundingPrecision)
                .ToOvernightIndexedSwap(Context);

        public OvernightIndexedSwap MakeSwapWithLookback(
            Period length,
            Rate fixedRate,
            Integer paymentLag,
            Natural lookbackDays,
            Natural lockoutDays,
            bool applyObservationShift,
            bool telescopicValueDates) =>
            new MakeOIS(length, EstrIndex)
                .WithFixedRate(fixedRate)
                .WithEffectiveDate(Settlement)
                .WithNominal(Nominal)
                .WithPaymentLag(paymentLag)
                .WithDiscountingTermStructure(EstrTermStructure)
                .WithLookbackDays(lookbackDays)
                .WithLockoutDays(lockoutDays)
                .WithObservationShift(applyObservationShift)
                .WithTelescopicValueDates(telescopicValueDates)
                .ToOvernightIndexedSwap(Context);

        public OvernightIndexedSwapResults Price(OvernightIndexedSwap swap) =>
            DiscountingSwapEngine.Calculate(swap, Context, EstrTermStructure, includeSettlementDateFlows: false);
    }
}
