// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/inflationcapfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Replaced: testVolatilityQuoteObservability (Conventions/YoYInflationCapFloorQuoteTests.cs)

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

/// <summary>QuantLib's year-on-year inflation cap and floor tests, <c>test-suite/inflationcapfloor.cpp</c>.</summary>
[Trait("Oracle", "QuantLib")]
public class InflationCapFloorTests
{
    // Adapted: an engine is a function the instruments are priced by calling; the fixings go into the evaluation
    // context.
    [Fact]
    public void TestConsistency()
    {
        // Testing consistency between yoy inflation cap, floor and collar...

        var vars = new CommonVars();

        int[] lengths = [1, 2, 3, 5, 7, 10, 15, 20];
        double[] capRates = [0.01, 0.025, 0.029, 0.03, 0.031, 0.035, 0.07];
        double[] floorRates = [0.01, 0.025, 0.029, 0.03, 0.031, 0.035, 0.07];
        double[] vols = [0.001, 0.005, 0.010, 0.015, 0.020];

        for (int whichPricer = 0; whichPricer < 3; whichPricer++)
        {
            foreach (int length in lengths)
            {
                foreach (double capRate in capRates)
                {
                    foreach (double floorRate in floorRates)
                    {
                        foreach (double vol in vols)
                        {
                            ImmutableArray<CashFlow> leg = vars.MakeYoYLeg(vars.EvaluationDate, length);

                            (YoYInflationCapFloor cap, Engine capEngine) = vars.MakeYoYCapFloor(
                                YoYInflationCapFloorType.Cap, leg, capRate, vol, whichPricer);

                            (YoYInflationCapFloor floor, Engine floorEngine) = vars.MakeYoYCapFloor(
                                YoYInflationCapFloorType.Floor, leg, floorRate, vol, whichPricer);

                            var collar = new YoYInflationCollar(leg, [capRate], [floorRate]);
                            Engine collarEngine = vars.MakeEngine(vol, whichPricer);

                            double capNPV = Npv(cap, capEngine, vars.Context);
                            double floorNPV = Npv(floor, floorEngine, vars.Context);
                            double collarNPV = Npv(collar, collarEngine, vars.Context);
                            if (Math.Abs(capNPV - floorNPV - collarNPV) > 1e-6)
                            {
                                Assert.Fail(
                                    "inconsistency between cap, floor and collar:\n"
                                    + $"    length:       {length} years\n"
                                    + $"    volatility:   {Volatility(vol)}\n"
                                    + $"    cap value:    {capNPV} at strike: {Rate(capRate)}\n"
                                    + $"    floor value:  {floorNPV} at strike: {Rate(floorRate)}\n"
                                    + $"    collar value: {collarNPV}");

                                // test re-composition by optionlets, N.B. ONE per year
                                double capletsNPV = 0.0;
                                var caplets = new List<YoYInflationCapFloor>();
                                for (int m = 0; m < length * 1; m++)
                                {
                                    caplets.Add(cap.Optionlet(m));
                                    capletsNPV += Npv(caplets[m], vars.MakeEngine(vol, whichPricer), vars.Context);
                                }

                                if (Math.Abs(capNPV - capletsNPV) > 1e-6)
                                {
                                    Assert.Fail(
                                        "sum of caplet NPVs does not equal cap NPV:\n"
                                        + $"    length:       {length} years\n"
                                        + $"    volatility:   {Volatility(vol)}\n"
                                        + $"    cap value:    {capNPV} at strike: {Rate(capRate)}\n"
                                        + $"    sum of caplets value:  {capletsNPV} at strike (first): "
                                        + $"{Rate(caplets[0].CapRates[0])}\n");
                                }

                                double floorletsNPV = 0.0;
                                var floorlets = new List<YoYInflationCapFloor>();
                                for (int m = 0; m < length * 1; m++)
                                {
                                    floorlets.Add(floor.Optionlet(m));
                                    floorletsNPV += Npv(floorlets[m], vars.MakeEngine(vol, whichPricer), vars.Context);
                                }

                                if (Math.Abs(floorNPV - floorletsNPV) > 1e-6)
                                {
                                    Assert.Fail(
                                        "sum of floorlet NPVs does not equal floor NPV:\n"
                                        + $"    length:       {length} years\n"
                                        + $"    volatility:   {Volatility(vol)}\n"
                                        + $"    cap value:    {floorNPV} at strike: {Rate(floorRate)}\n"
                                        + $"    sum of floorlets value:  {floorletsNPV} at strike (first): "
                                        + $"{Rate(floorlets[0].FloorRates[0])}\n");
                                }

                                double collarletsNPV = 0.0;
                                var collarlets = new List<YoYInflationCapFloor>();
                                for (int m = 0; m < length * 1; m++)
                                {
                                    collarlets.Add(collar.Optionlet(m));
                                    collarletsNPV += Npv(collarlets[m], vars.MakeEngine(vol, whichPricer), vars.Context);
                                }

                                if (Math.Abs(collarNPV - collarletsNPV) > 1e-6)
                                {
                                    Assert.Fail(
                                        "sum of collarlet NPVs does not equal collar NPV:\n"
                                        + $"    length:       {length} years\n"
                                        + $"    volatility:   {Volatility(vol)}\n"
                                        + $"    cap value:    {collarNPV} at strike floor: {Rate(floorRate)} at strike "
                                        + $"cap: {Rate(capRate)}\n"
                                        + $"    sum of collarlets value:  {collarletsNPV} at strike floor (first): "
                                        + $"{Rate(collarlets[0].FloorRates[0])} at strike cap (first): "
                                        + $"{Rate(collarlets[0].CapRates[0])}\n");
                                }
                            }
                        }
                    }
                }
            }
        } // pricer loop
    }

    // Test inflation cap/floor parity, i.e. that cap-floor = swap, note that this
    // is different from nominal because in nominal world standard cap/floors do
    // not have the first optionlet.  This is because they set in advance so
    // there is no point.  However, yoy inflation generally sets in arrears,
    // (actually in arrears with a lag of a few months) thus the first optionlet
    // is relevant.  Hence we can do a parity test without a special definition
    // of the YoY cap/floor instrument.
    // Adapted: as the consistency case; the swap is priced by calling its engine.
    [Fact]
    public void TestParity()
    {
        // Testing yoy inflation cap/floor parity...

        var vars = new CommonVars();

        int[] lengths = [1, 2, 3, 5, 7, 10, 15, 20];

        // vol is low ...
        double[] strikes = [0.0, 0.025, 0.029, 0.03, 0.031, 0.035, 0.07];

        // yoy inflation vol is generally very low
        double[] vols = [0.001, 0.005, 0.010, 0.015, 0.020];

        // cap-floor-swap parity is model-independent
        for (int whichPricer = 0; whichPricer < 3; whichPricer++)
        {
            foreach (int length in lengths)
            {
                foreach (double strike in strikes)
                {
                    foreach (double vol in vols)
                    {
                        ImmutableArray<CashFlow> leg = vars.MakeYoYLeg(vars.EvaluationDate, length);

                        (YoYInflationCapFloor cap, Engine capEngine) = vars.MakeYoYCapFloor(
                            YoYInflationCapFloorType.Cap, leg, strike, vol, whichPricer);

                        (YoYInflationCapFloor floor, Engine floorEngine) = vars.MakeYoYCapFloor(
                            YoYInflationCapFloorType.Floor, leg, strike, vol, whichPricer);

                        Date from = vars.NominalTS.ReferenceDate;
                        Date to = from + new Period(length, TimeUnit.Years);
                        Schedule yoySchedule = new MakeSchedule()
                            .From(from)
                            .To(to)
                            .WithTenor(new Period(1, TimeUnit.Years))
                            .WithCalendar(new UnitedKingdom())
                            .WithConvention(BusinessDayConvention.Unadjusted)
                            .Backwards()
                            .ToSchedule();

                        var swap = new YearOnYearInflationSwap(
                            SwapType.Payer,
                            1000000.0,
                            yoySchedule, // fixed schedule, but same as yoy
                            strike,
                            vars.Dc,
                            yoySchedule,
                            vars.Iir,
                            vars.ObservationLag,
                            CPIInterpolationType.Flat,
                            0.0, // spread on index
                            vars.Dc,
                            new UnitedKingdom());

                        YearOnYearInflationSwapResults swapResults =
                            DiscountingSwapEngine.Calculate(swap, vars.Context, vars.NominalTS);

                        double capNPV = Npv(cap, capEngine, vars.Context);
                        double floorNPV = Npv(floor, floorEngine, vars.Context);
                        double swapNPV = swapResults.NPV!.Value;

                        // N.B. nominals are 10e6
                        if (Math.Abs(capNPV - floorNPV - swapNPV) > 1.0e-6)
                        {
                            Assert.Fail(
                                "put/call parity violated:\n"
                                + $"    length:      {length} years\n"
                                + $"    volatility:  {Volatility(vol)}\n"
                                + $"    strike:      {Rate(strike)}\n"
                                + $"    cap value:   {capNPV}\n"
                                + $"    floor value: {floorNPV}\n"
                                + $"    swap value:  {swapNPV}");
                        }
                    }
                }
            }
        }
    }

    // Adapted: as the consistency case.
    [Fact]
    public void TestCachedValue()
    {
        // Testing Black yoy inflation cap/floor price against cached values...

        var vars = new CommonVars();

        int whichPricer = 0; // black

        double k = 0.0295; // one centi-point is fair rate error i.e. < 1 cp
        int j = 2;
        ImmutableArray<CashFlow> leg = vars.MakeYoYLeg(vars.EvaluationDate, j);
        (YoYInflationCapFloor cap, Engine capEngine) =
            vars.MakeYoYCapFloor(YoYInflationCapFloorType.Cap, leg, k, 0.01, whichPricer);

        (YoYInflationCapFloor floor, Engine floorEngine) =
            vars.MakeYoYCapFloor(YoYInflationCapFloorType.Floor, leg, k, 0.01, whichPricer);

        // close to atm prices
        double cachedCapNPVblack = 219.452;
        double cachedFloorNPVblack = 314.641;

        // N.B. notionals are 10e6.
        double capNPV = Npv(cap, capEngine, vars.Context);
        double floorNPV = Npv(floor, floorEngine, vars.Context);
        Assert.True(
            Math.Abs(capNPV - cachedCapNPVblack) < 0.02,
            $"yoy cap cached NPV wrong {capNPV} should be {cachedCapNPVblack} Black pricer diff was "
            + $"{Math.Abs(capNPV - cachedCapNPVblack)}");
        Assert.True(
            Math.Abs(floorNPV - cachedFloorNPVblack) < 0.02,
            $"yoy floor cached NPV wrong {floorNPV} should be {cachedFloorNPVblack} Black pricer diff was "
            + $"{Math.Abs(floorNPV - cachedFloorNPVblack)}");

        whichPricer = 1; // dd

        (cap, capEngine) = vars.MakeYoYCapFloor(YoYInflationCapFloorType.Cap, leg, k, 0.01, whichPricer);

        (floor, floorEngine) = vars.MakeYoYCapFloor(YoYInflationCapFloorType.Floor, leg, k, 0.01, whichPricer);

        // close to atm prices
        double cachedCapNPVdd = 9114.61;
        double cachedFloorNPVdd = 9209.8;

        // N.B. notionals are 10e6.
        capNPV = Npv(cap, capEngine, vars.Context);
        floorNPV = Npv(floor, floorEngine, vars.Context);
        Assert.True(
            Math.Abs(capNPV - cachedCapNPVdd) < 0.22,
            $"yoy cap cached NPV wrong {capNPV} should be {cachedCapNPVdd} dd Black pricer diff was "
            + $"{Math.Abs(capNPV - cachedCapNPVdd)}");
        Assert.True(
            Math.Abs(floorNPV - cachedFloorNPVdd) < 0.22,
            $"yoy floor cached NPV wrong {floorNPV} should be {cachedFloorNPVdd} dd Black pricer diff was "
            + $"{Math.Abs(floorNPV - cachedFloorNPVdd)}");

        whichPricer = 2; // bachelier

        (cap, capEngine) = vars.MakeYoYCapFloor(YoYInflationCapFloorType.Cap, leg, k, 0.01, whichPricer);

        (floor, floorEngine) = vars.MakeYoYCapFloor(YoYInflationCapFloorType.Floor, leg, k, 0.01, whichPricer);

        // close to atm prices
        double cachedCapNPVbac = 8852.4;
        double cachedFloorNPVbac = 8947.59;

        // N.B. notionals are 10e6.
        capNPV = Npv(cap, capEngine, vars.Context);
        floorNPV = Npv(floor, floorEngine, vars.Context);
        Assert.True(
            Math.Abs(capNPV - cachedCapNPVbac) < 0.22,
            $"yoy cap cached NPV wrong {capNPV} should be {cachedCapNPVbac} bac Black pricer diff was "
            + $"{Math.Abs(capNPV - cachedCapNPVbac)}");
        Assert.True(
            Math.Abs(floorNPV - cachedFloorNPVbac) < 0.22,
            $"yoy floor cached NPV wrong {floorNPV} should be {cachedFloorNPVbac} bac Black pricer diff was "
            + $"{Math.Abs(floorNPV - cachedFloorNPVbac)}");
    }

    // An instrument's NPV by its engine: QuantLib's setPricingEngine then NPV().
    private static double Npv(YoYInflationCapFloor capFloor, Engine engine, EvaluationContext context) =>
        engine(capFloor, context).NPV!.Value;

    // io::volatility and io::rate: a percentage with two decimals.
    private static string Volatility(double v) => Percent(v);

    private static string Rate(double r) => Percent(r);

    private static string Percent(double x) => (x * 100.0).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " %";

    // QuantLib's makeHelpers (lines 64-86).
    private static List<BootstrapHelper<YoYInflationTermStructure>> MakeHelpers(
        Datum[] iiData,
        YoYInflationIndex ii,
        CPIInterpolationType interpolation,
        Period observationLag,
        Calendar calendar,
        BusinessDayConvention bdc,
        DayCounter dc,
        YieldTermStructure discountCurve,
        EvaluationContext context)
    {
        var instruments = new List<BootstrapHelper<YoYInflationTermStructure>>();
        foreach (Datum datum in iiData)
        {
            Date maturity = datum.Date;
            double quote = datum.Rate / 100.0;
            var anInstrument = new YearOnYearInflationSwapHelper(
                quote, observationLag, maturity, calendar, bdc, dc, ii, interpolation, discountCurve, context);
            instruments.Add(anInstrument);
        }

        return instruments;
    }

    // An engine, QuantLib's engine object, as the function a call prices with.
    internal delegate InstrumentResults Engine(YoYInflationCapFloor capFloor, EvaluationContext context);

    // QuantLib's Datum (lines 59-62).
    private readonly record struct Datum(Date Date, double Rate);

    // QuantLib's CommonVars (lines 89-265), the fixings in its evaluation context; Conventions/
    // YoYInflationCapFloorQuoteTests.cs shares it.
    internal sealed class CommonVars
    {
        public CommonVars()
        {
            Nominals = [1000000];

            // option variables
            Frequency = Frequency.Annual;

            // usual setup
            Calendar = new UnitedKingdom();
            Convention = BusinessDayConvention.ModifiedFollowing;
            var today = new Date(13, August, 2007);
            EvaluationDate = Calendar.Adjust(today);
            Context = new EvaluationContext { EvaluationDate = EvaluationDate };
            SettlementDays = 0;
            Dc = new Thirty360(Thirty360Convention.BondBasis);

            // yoy index
            //      fixing data
            var from = new Date(1, January, 2005);
            var to = new Date(13, August, 2007);
            Schedule rpiSchedule = new MakeSchedule()
                .From(from)
                .To(to)
                .WithTenor(new Period(1, TimeUnit.Months))
                .WithCalendar(new UnitedKingdom())
                .WithConvention(BusinessDayConvention.ModifiedFollowing)
                .ToSchedule();
            double[] fixData =
            [
                189.9, 189.9, 189.6, 190.5, 191.6, 192.0,
                192.2, 192.2, 192.6, 193.1, 193.3, 193.6,
                194.1, 193.4, 194.2, 195.0, 196.5, 197.7,
                198.5, 198.5, 199.2, 200.1, 200.4, 201.1,
                202.7, 201.6, 203.1, 204.4, 205.4, 206.2,
                207.3, -999.0, -999,
            ];
            var rpi = new UKRPI();
            for (int i = 0; i < rpiSchedule.Size; i++)
            {
                Context = Context with { Fixings = Context.Fixings.AddFixing(rpi, rpiSchedule[i], fixData[i]) };
            }

            Iir = new YoYInflationIndex(rpi);

            NominalTS = new FlatForward(EvaluationDate, 0.05, new ActualActual(ActualActualConvention.ISDA));

            // now build the YoY inflation curve; QuantLib's local observationLag shadows the member, which stays
            // Period(), as here
            var observationLag = new Period(2, TimeUnit.Months);

            Datum[] yyData =
            [
                new(new Date(13, August, 2008), 2.95),
                new(new Date(13, August, 2009), 2.95),
                new(new Date(13, August, 2010), 2.93),
                new(new Date(15, August, 2011), 2.955),
                new(new Date(13, August, 2012), 2.945),
                new(new Date(13, August, 2013), 2.985),
                new(new Date(13, August, 2014), 3.01),
                new(new Date(13, August, 2015), 3.035),
                new(new Date(13, August, 2016), 3.055), // note that
                new(new Date(13, August, 2017), 3.075), // some dates will be on
                new(new Date(13, August, 2019), 3.105), // holidays but the payment
                new(new Date(15, August, 2022), 3.135), // calendar will roll them
                new(new Date(13, August, 2027), 3.155),
                new(new Date(13, August, 2032), 3.145),
                new(new Date(13, August, 2037), 3.145),
            ];

            // now build the helpers ...
            List<BootstrapHelper<YoYInflationTermStructure>> helpers = MakeHelpers(
                yyData, Iir, CPIInterpolationType.Flat, observationLag, Calendar, Convention, Dc, NominalTS, Context);

            Date baseDate = rpi.LastFixingDate(Context);
            double baseYYRate = yyData[0].Rate / 100.0;
            var pYYTS = new PiecewiseYoYInflationCurve<Linear, IterativeBootstrap, YoYInflationTraits>(
                EvaluationDate, baseDate, baseYYRate, Iir.Frequency, Dc, helpers, Context);

            // make sure that the index has the latest yoy term structure
            Iir = new YoYInflationIndex(rpi, pYYTS);
        }

        public Frequency Frequency { get; }

        public ImmutableArray<double> Nominals { get; }

        public Calendar Calendar { get; }

        public BusinessDayConvention Convention { get; }

        public Date EvaluationDate { get; }

        public EvaluationContext Context { get; }

        public int SettlementDays { get; }

        // QuantLib's member, which its constructor never sets: Period().
        public Period ObservationLag { get; }

        public DayCounter Dc { get; }

        public YoYInflationIndex Iir { get; }

        public YieldTermStructure NominalTS { get; }

        // utilities
        public ImmutableArray<CashFlow> MakeYoYLeg(Date startDate, int length)
        {
            YoYInflationIndex ii = Iir;
            Date endDate = Calendar.Advance(startDate, length, TimeUnit.Years, BusinessDayConvention.Unadjusted);
            var schedule = new Schedule(
                startDate,
                endDate,
                new Period(Frequency),
                Calendar,
                BusinessDayConvention.Unadjusted,
                BusinessDayConvention.Unadjusted, // ref periods & acc periods
                DateGenerationRule.Forward,
                false,
                Context);
            return new YoYInflationLeg(schedule, Calendar, ii, ObservationLag, CPIInterpolationType.Flat)
                .WithNotionals(Nominals)
                .WithPaymentDayCounter(Dc)
                .WithPaymentAdjustment(Convention)
                .ToLeg();
        }

        public Engine MakeEngine(double volatility, int which)
        {
            YoYInflationIndex yyii = Iir;

            YoYOptionletVolatilitySurface vol = ConstantYoYOptionletVolatility.Create(
                volatility, SettlementDays, Calendar, Convention, Dc, ObservationLag, Frequency, false, Context);

            YieldTermStructure nominalTS = NominalTS;
            return which switch
            {
                0 => (c, ctx) => YoYInflationBlackCapFloorEngine.Calculate(c, ctx, yyii, vol, nominalTS),
                1 => (c, ctx) => YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(c, ctx, yyii, vol, nominalTS),
                2 => (c, ctx) => YoYInflationBachelierCapFloorEngine.Calculate(c, ctx, yyii, vol, nominalTS),
                _ => throw new InvalidOperationException(
                    $"unknown engine request: which = {which}should be 0=Black,1=DD,2=Bachelier"),
            };
        }

        public (YoYInflationCapFloor CapFloor, Engine Engine) MakeYoYCapFloor(
            YoYInflationCapFloorType type, ImmutableArray<CashFlow> leg, double strike, double volatility, int which)
        {
            YoYInflationCapFloor result = type switch
            {
                YoYInflationCapFloorType.Cap => new YoYInflationCap(leg, [strike]),
                YoYInflationCapFloorType.Floor => new YoYInflationFloor(leg, [strike]),
                _ => throw new NanoQLException("unknown YoYInflation cap/floor type"),
            };
            return (result, MakeEngine(volatility, which));
        }
    }
}
