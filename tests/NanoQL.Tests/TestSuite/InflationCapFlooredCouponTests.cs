// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/inflationcapflooredcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

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

// Adapted throughout: an engine is a function the instruments are priced by calling, a swap's the static
// DiscountingSwapEngine.Calculate on the nominal curve; the fixings go into the evaluation context; and a leg's pricer
// is set by InflationCouponPricerFunctions.SetCouponPricer, which returns the leg.
[Trait("Oracle", "QuantLib")]
public class InflationCapFlooredCouponTests
{
    [Fact]
    public void TestDecomposition()
    {
        // Testing collared coupon against its decomposition...
        var vars = new CommonVars();

        const Real tolerance = 1e-10;
        Real npvVanilla, npvCappedLeg, npvFlooredLeg, npvCollaredLeg, npvCap, npvFloor, npvCollar;
        Real error;
        const Rate floorstrike = 0.05;
        const Rate capstrike = 0.10;
        Rate[] caps = [.. Enumerable.Repeat(capstrike, vars.Length)];
        Rate[] caps0 = [];
        Rate[] floors = [.. Enumerable.Repeat(floorstrike, vars.Length)];
        Rate[] floors0 = [];
        const Rate gearing_p = 0.5;
        const Spread spread_p = 0.002;
        const Rate gearing_n = -1.5;
        const Spread spread_n = 0.12;

        // fixed leg with zero rate
        ImmutableArray<CashFlow> fixedLeg = vars.MakeFixedLeg(vars.StartDate, vars.Length);

        // floating leg with gearing=1 and spread=0
        ImmutableArray<CashFlow> floatLeg = vars.MakeYoYLeg(vars.StartDate, vars.Length);

        // floating leg with positive gearing (gearing_p) and spread<>0
        ImmutableArray<CashFlow> floatLeg_p = vars.MakeYoYLeg(vars.StartDate, vars.Length, gearing_p, spread_p);

        // floating leg with negative gearing (gearing_n) and spread<>0
        ImmutableArray<CashFlow> floatLeg_n = vars.MakeYoYLeg(vars.StartDate, vars.Length, gearing_n, spread_n);

        // Swap with null fixed leg and floating leg with gearing=1 and spread=0
        var vanillaLeg = new Swap(fixedLeg, floatLeg);

        // Swap with null fixed leg and floating leg with positive gearing and spread<>0
        var vanillaLeg_p = new Swap(fixedLeg, floatLeg_p);

        // Swap with null fixed leg and floating leg with negative gearing and spread<>0
        var vanillaLeg_n = new Swap(fixedLeg, floatLeg_n);

        // CAPPED coupon - Decomposition of payoff
        // Payoff = Nom * Min(rate,strike) * accrualperiod =
        // = Nom * [rate + Min(0,strike-rate)] * accrualperiod =
        // = Nom * rate * accrualperiod - Nom * Max(rate-strike,0) * accrualperiod =
        // = VanillaFloatingLeg - Call
        const int whichPricer = 0;

        // Case gearing = 1 and spread = 0
        ImmutableArray<CashFlow> cappedLeg =
            vars.MakeYoYCapFlooredLeg(whichPricer, vars.StartDate, vars.Length, caps, floors0, vars.Volatility);
        var capLeg = new Swap(fixedLeg, cappedLeg);
        var cap = new YoYInflationCap(floatLeg, [capstrike]);
        npvVanilla = vars.Npv(vanillaLeg);
        npvCappedLeg = vars.Npv(capLeg);
        npvCap = vars.Npv(cap, vars.Volatility, whichPricer);
        error = Math.Abs(npvCappedLeg - (npvVanilla - npvCap));
        Assert.False(
            error > tolerance,
            $"\nYoY Capped Leg: gearing=1, spread=0%, strike={capstrike * 100}%\n  Capped Floating Leg NPV: {npvCappedLeg}\n"
            + $"  Floating Leg NPV - Cap NPV: {npvVanilla - npvCap}\n  Diff: {error}");

        // gearing = 1 and spread = 0
        // FLOORED coupon - Decomposition of payoff
        // Payoff = Nom * Max(rate,strike) * accrualperiod =
        // = Nom * [rate + Max(0,strike-rate)] * accrualperiod =
        // = Nom * rate * accrualperiod + Nom * Max(strike-rate,0) * accrualperiod =
        // = VanillaFloatingLeg + Put
        ImmutableArray<CashFlow> flooredLeg =
            vars.MakeYoYCapFlooredLeg(whichPricer, vars.StartDate, vars.Length, caps0, floors, vars.Volatility);
        var floorLeg = new Swap(fixedLeg, flooredLeg);
        var floor = new YoYInflationFloor(floatLeg, [floorstrike]);
        npvFlooredLeg = vars.Npv(floorLeg);
        npvFloor = vars.Npv(floor, vars.Volatility, whichPricer);
        error = Math.Abs(npvFlooredLeg - (npvVanilla + npvFloor));
        Assert.False(
            error > tolerance,
            $"YoY Floored Leg: gearing=1, spread=0%, strike={floorstrike * 100}%\n  Floored Floating Leg NPV: {npvFlooredLeg}\n"
            + $"  Floating Leg NPV + Floor NPV: {npvVanilla + npvFloor}\n  Diff: {error}");

        // gearing = 1 and spread = 0
        // COLLARED coupon - Decomposition of payoff
        // Payoff = Nom * Min(strikem,Max(rate,strikeM)) * accrualperiod =
        // = VanillaFloatingLeg - Collar
        ImmutableArray<CashFlow> collaredLeg =
            vars.MakeYoYCapFlooredLeg(whichPricer, vars.StartDate, vars.Length, caps, floors, vars.Volatility);
        var collarLeg = new Swap(fixedLeg, collaredLeg);
        var collar = new YoYInflationCollar(floatLeg, [capstrike], [floorstrike]);
        npvCollaredLeg = vars.Npv(collarLeg);
        npvCollar = vars.Npv(collar, vars.Volatility, whichPricer);
        error = Math.Abs(npvCollaredLeg - (npvVanilla - npvCollar));
        Assert.False(
            error > tolerance,
            $"\nYoY Collared Leg: gearing=1, spread=0%, strike={floorstrike * 100}% and {capstrike * 100}%\n"
            + $"  Collared Floating Leg NPV: {npvCollaredLeg}\n  Floating Leg NPV - Collar NPV: {npvVanilla - npvCollar}\n"
            + $"  Diff: {error}");

        // gearing = a and spread = b
        // CAPPED coupon - Decomposition of payoff
        // Payoff
        // = Nom * Min(a*rate+b,strike) * accrualperiod =
        // = Nom * [a*rate+b + Min(0,strike-a*rate-b)] * accrualperiod =
        // = Nom * a*rate+b * accrualperiod + Nom * Min(strike-b-a*rate,0) * accrualperiod
        // --> If a>0 (assuming positive effective strike):
        // Payoff = VanillaFloatingLeg - Call(a*rate+b,strike)
        // --> If a<0 (assuming positive effective strike):
        // Payoff = VanillaFloatingLeg + Nom * Min(strike-b+|a|*rate+,0) * accrualperiod =
        // = VanillaFloatingLeg + Put(|a|*rate+b,strike)

        // Positive gearing
        ImmutableArray<CashFlow> cappedLeg_p = vars.MakeYoYCapFlooredLeg(
            whichPricer, vars.StartDate, vars.Length, caps, floors0, vars.Volatility, gearing_p, spread_p);
        var capLeg_p = new Swap(fixedLeg, cappedLeg_p);
        var cap_p = new YoYInflationCap(floatLeg_p, [capstrike]);
        npvVanilla = vars.Npv(vanillaLeg_p);
        npvCappedLeg = vars.Npv(capLeg_p);
        npvCap = vars.Npv(cap_p, vars.Volatility, whichPricer);
        error = Math.Abs(npvCappedLeg - (npvVanilla - npvCap));
        Assert.False(
            error > tolerance,
            $"\nYoY Capped Leg: gearing={gearing_p}, spread= {spread_p * 100}%, strike={capstrike * 100}%, "
            + $"effective strike= {(capstrike - spread_p) / gearing_p * 100}%\n  Capped Floating Leg NPV: {npvCappedLeg}\n"
            + $"  Vanilla Leg NPV: {npvVanilla}\n  Cap NPV: {npvCap}\n  Floating Leg NPV - Cap NPV: {npvVanilla - npvCap}\n"
            + $"  Diff: {error}");

        // Negative gearing
        ImmutableArray<CashFlow> cappedLeg_n = vars.MakeYoYCapFlooredLeg(
            whichPricer, vars.StartDate, vars.Length, caps, floors0, vars.Volatility, gearing_n, spread_n);
        var capLeg_n = new Swap(fixedLeg, cappedLeg_n);
        var floor_n = new YoYInflationFloor(floatLeg, [(capstrike - spread_n) / gearing_n]);
        npvVanilla = vars.Npv(vanillaLeg_n);
        npvCappedLeg = vars.Npv(capLeg_n);
        npvFloor = vars.Npv(floor_n, vars.Volatility, whichPricer);
        error = Math.Abs(npvCappedLeg - (npvVanilla + (gearing_n * npvFloor)));
        Assert.False(
            error > tolerance,
            $"\nYoY Capped Leg: gearing={gearing_n}, spread= {spread_n * 100}%, strike={capstrike * 100}%, "
            + $"effective strike= {(capstrike - spread_n) / gearing_n * 100}%\n  Capped Floating Leg NPV: {npvCappedLeg}\n"
            + $"  npv Vanilla: {npvVanilla}\n  npvFloor: {npvFloor}\n"
            + $"  Floating Leg NPV - Cap NPV: {npvVanilla + (gearing_n * npvFloor)}\n  Diff: {error}");

        // gearing = a and spread = b
        // FLOORED coupon - Decomposition of payoff
        // Payoff
        // = Nom * Max(a*rate+b,strike) * accrualperiod =
        // = Nom * [a*rate+b + Max(0,strike-a*rate-b)] * accrualperiod =
        // = Nom * a*rate+b * accrualperiod + Nom * Max(strike-b-a*rate,0) * accrualperiod
        // --> If a>0 (assuming positive effective strike):
        // Payoff = VanillaFloatingLeg + Put(a*rate+b,strike)
        // --> If a<0 (assuming positive effective strike):
        // Payoff = VanillaFloatingLeg + Nom * Max(strike-b+|a|*rate+,0) * accrualperiod =
        // = VanillaFloatingLeg - Call(|a|*rate+b,strike)

        // Positive gearing
        ImmutableArray<CashFlow> flooredLeg_p1 = vars.MakeYoYCapFlooredLeg(
            whichPricer, vars.StartDate, vars.Length, caps0, floors, vars.Volatility, gearing_p, spread_p);
        var floorLeg_p1 = new Swap(fixedLeg, flooredLeg_p1);
        var floor_p1 = new YoYInflationFloor(floatLeg_p, [floorstrike]);
        npvVanilla = vars.Npv(vanillaLeg_p);
        npvFlooredLeg = vars.Npv(floorLeg_p1);
        npvFloor = vars.Npv(floor_p1, vars.Volatility, whichPricer);
        error = Math.Abs(npvFlooredLeg - (npvVanilla + npvFloor));
        Assert.False(
            error > tolerance,
            $"\nYoY Floored Leg: gearing={gearing_p}, spread= {spread_p * 100}%, strike={floorstrike * 100}%, "
            + $"effective strike= {(floorstrike - spread_p) / gearing_p * 100}%\n  Floored Floating Leg NPV: {npvFlooredLeg}\n"
            + $"  Floating Leg NPV + Floor NPV: {npvVanilla + npvFloor}\n  Diff: {error}");

        // Negative gearing
        ImmutableArray<CashFlow> flooredLeg_n = vars.MakeYoYCapFlooredLeg(
            whichPricer, vars.StartDate, vars.Length, caps0, floors, vars.Volatility, gearing_n, spread_n);
        var floorLeg_n = new Swap(fixedLeg, flooredLeg_n);
        var cap_n = new YoYInflationCap(floatLeg, [(floorstrike - spread_n) / gearing_n]);
        npvVanilla = vars.Npv(vanillaLeg_n);
        npvFlooredLeg = vars.Npv(floorLeg_n);
        npvCap = vars.Npv(cap_n, vars.Volatility, whichPricer);
        error = Math.Abs(npvFlooredLeg - (npvVanilla - (gearing_n * npvCap)));
        Assert.False(
            error > tolerance,
            $"\nYoY Capped Leg: gearing={gearing_n}, spread= {spread_n * 100}%, strike={floorstrike * 100}%, "
            + $"effective strike= {(floorstrike - spread_n) / gearing_n * 100}%\n  Capped Floating Leg NPV: {npvFlooredLeg}\n"
            + $"  Floating Leg NPV - Cap NPV: {npvVanilla - (gearing_n * npvCap)}\n  Diff: {error}");

        // gearing = a and spread = b
        // COLLARED coupon - Decomposition of payoff
        // Payoff = Nom * Min(caprate,Max(a*rate+b,floorrate)) * accrualperiod
        // --> If a>0 (assuming positive effective strike):
        // Payoff = VanillaFloatingLeg - Collar(a*rate+b, floorrate, caprate)
        // --> If a<0 (assuming positive effective strike):
        // Payoff = VanillaFloatingLeg + Collar(|a|*rate+b, caprate, floorrate)

        // Positive gearing
        ImmutableArray<CashFlow> collaredLeg_p = vars.MakeYoYCapFlooredLeg(
            whichPricer, vars.StartDate, vars.Length, caps, floors, vars.Volatility, gearing_p, spread_p);
        var collarLeg_p1 = new Swap(fixedLeg, collaredLeg_p);
        var collar_p = new YoYInflationCollar(floatLeg_p, [capstrike], [floorstrike]);
        npvVanilla = vars.Npv(vanillaLeg_p);
        npvCollaredLeg = vars.Npv(collarLeg_p1);
        npvCollar = vars.Npv(collar_p, vars.Volatility, whichPricer);
        error = Math.Abs(npvCollaredLeg - (npvVanilla - npvCollar));
        Assert.False(
            error > tolerance,
            $"\nYoY Collared Leg: gearing={gearing_p}, spread= {spread_p * 100}%, strike={floorstrike * 100}% and "
            + $"{capstrike * 100}%, effective strike={(floorstrike - spread_p) / gearing_p * 100}% and "
            + $"{(capstrike - spread_p) / gearing_p * 100}%\n  Collared Floating Leg NPV: {npvCollaredLeg}\n"
            + $"  Floating Leg NPV - Collar NPV: {npvVanilla - npvCollar}\n  Diff: {error}");

        // Negative gearing
        ImmutableArray<CashFlow> collaredLeg_n = vars.MakeYoYCapFlooredLeg(
            whichPricer, vars.StartDate, vars.Length, caps, floors, vars.Volatility, gearing_n, spread_n);
        var collarLeg_n1 = new Swap(fixedLeg, collaredLeg_n);
        var collar_n = new YoYInflationCollar(
            floatLeg, [(floorstrike - spread_n) / gearing_n], [(capstrike - spread_n) / gearing_n]);
        npvVanilla = vars.Npv(vanillaLeg_n);
        npvCollaredLeg = vars.Npv(collarLeg_n1);
        npvCollar = vars.Npv(collar_n, vars.Volatility, whichPricer);
        error = Math.Abs(npvCollaredLeg - (npvVanilla - (gearing_n * npvCollar)));
        Assert.False(
            error > tolerance,
            $"\nYoY Collared Leg: gearing={gearing_n}, spread= {spread_n * 100}%, strike={floorstrike * 100}% and "
            + $"{capstrike * 100}%, effective strike={(floorstrike - spread_n) / gearing_n * 100}% and "
            + $"{(capstrike - spread_n) / gearing_n * 100}%\n  Collared Floating Leg NPV: {npvCollaredLeg}\n"
            + $"  Floating Leg NPV - Collar NPV: {npvVanilla - (gearing_n * npvCollar)}\n  Diff: {error}");
    }

    [Fact]
    public void TestInstrumentEquality()
    {
        // Testing inflation capped/floored coupon against inflation capfloor instrument...
        var vars = new CommonVars();

        int[] lengths = [1, 2, 3, 5, 7, 10, 15, 20];

        // vol is low ...
        Rate[] strikes = [0.01, 0.025, 0.029, 0.03, 0.031, 0.035, 0.07];

        // yoy inflation vol is generally very low
        Volatility[] vols = [0.001, 0.005, 0.010, 0.015, 0.020];

        // this is model independent
        // capped coupon = fwd - cap, and fwd = swap(0)
        // floored coupon = fwd + floor
        for (int whichPricer = 0; whichPricer < 3; whichPricer++)
        {
            foreach (int length in lengths)
            {
                foreach (Real strike in strikes)
                {
                    foreach (Real vol in vols)
                    {
                        ImmutableArray<CashFlow> leg = vars.MakeYoYLeg(vars.EvaluationDate, length);

                        var cap = new YoYInflationCap(leg, [strike]);

                        var floor = new YoYInflationFloor(leg, [strike]);

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
                            0.0, // strikes[j],
                            vars.Dc,
                            yoySchedule,
                            vars.Iir,
                            vars.ObservationLag,
                            CPIInterpolationType.Flat,
                            0.0, // spread on index
                            vars.Dc,
                            new UnitedKingdom());
                        Real swapNPV = DiscountingSwapEngine.Calculate(swap, vars.Context, vars.NominalTS).NPV!.Value;

                        ImmutableArray<CashFlow> leg2 = vars.MakeYoYCapFlooredLeg(
                            whichPricer,
                            from,
                            length,
                            [.. Enumerable.Repeat(strike, length)], // cap
                            [], // floor
                            vol,
                            1.0, // gearing
                            0.0); // spread

                        ImmutableArray<CashFlow> leg3 = vars.MakeYoYCapFlooredLeg(
                            whichPricer,
                            from,
                            length,
                            [], // cap
                            [.. Enumerable.Repeat(strike, length)], // floor
                            vol,
                            1.0, // gearing
                            0.0); // spread

                        // N.B. nominals are 10e6
                        Real capped = CashFlows.Npv(leg2, vars.NominalTS, vars.Context, false);
                        Real capNPV = vars.Npv(cap, vol, whichPricer);
                        Assert.False(
                            Math.Abs(capped - (swapNPV - capNPV)) > 1.0e-6,
                            $"capped coupon != swap(0) - cap:\n    length:      {length} years\n    volatility:  {Percent(vol)}\n"
                            + $"    strike:      {Percent(strike)}\n    cap value:   {capNPV}\n    swap value:  {swapNPV}\n"
                            + $"   capped coupon {capped}");

                        // N.B. nominals are 10e6
                        Real floored = CashFlows.Npv(leg3, vars.NominalTS, vars.Context, false);
                        Real floorNPV = vars.Npv(floor, vol, whichPricer);
                        Assert.False(
                            Math.Abs(floored - (swapNPV + floorNPV)) > 1.0e-6,
                            $"floored coupon != swap(0) + floor :\n    length:      {length} years\n"
                            + $"    volatility:  {Percent(vol)}\n    strike:      {Percent(strike)}\n"
                            + $"    floor value: {floorNPV}\n    swap value:  {swapNPV}\n  floored coupon {floored}");
                    }
                }
            }
        }
    }

    // io::volatility and io::rate: a percentage with two decimals.
    private static string Percent(double x) => (x * 100.0).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " %";

    // QuantLib's makeHelpers (lines 65-87).
    private static List<BootstrapHelper<YoYInflationTermStructure>> MakeHelpers(
        Datum[] iiData,
        YoYInflationIndex ii,
        Period observationLag,
        CPIInterpolationType interpolation,
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

    // QuantLib's Datum (lines 60-63).
    private readonly record struct Datum(Date Date, double Rate);

    // QuantLib's CommonVars (lines 90-363), the fixings in its evaluation context.
    private sealed class CommonVars
    {
        // setup
        public CommonVars()
        {
            Nominals = [1000000];

            // option variables
            Frequency = Frequency.Annual;

            // usual setup
            Volatility = 0.01;
            Length = 7;
            Calendar = new UnitedKingdom();
            Convention = BusinessDayConvention.ModifiedFollowing;
            var today = new Date(13, August, 2007);
            EvaluationDate = Calendar.Adjust(today);
            Context = new EvaluationContext { EvaluationDate = EvaluationDate };
            SettlementDays = 0;
            Settlement = Calendar.Advance(today, SettlementDays, TimeUnit.Days);
            StartDate = Settlement;
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
                yyData, Iir, observationLag, CPIInterpolationType.Flat, Calendar, Convention, Dc, NominalTS, Context);

            Date baseDate = rpi.LastFixingDate(Context);
            double baseYYRate = yyData[0].Rate / 100.0;
            var pYYTS = new PiecewiseYoYInflationCurve<Linear, IterativeBootstrap, YoYInflationTraits>(
                EvaluationDate, baseDate, baseYYRate, Iir.Frequency, Dc, helpers, Context);

            // make sure that the index has the latest yoy term structure
            Iir = new YoYInflationIndex(rpi, pYYTS);
        }

        public int Length { get; }

        public Date StartDate { get; }

        public Real Volatility { get; }

        public Frequency Frequency { get; }

        public ImmutableArray<double> Nominals { get; }

        public Calendar Calendar { get; }

        public BusinessDayConvention Convention { get; }

        public Date EvaluationDate { get; }

        public int SettlementDays { get; }

        public Date Settlement { get; }

        // QuantLib's member, which its constructor never sets: Period().
        public Period ObservationLag { get; }

        public DayCounter Dc { get; }

        public YoYInflationIndex Iir { get; }

        public YieldTermStructure NominalTS { get; }

        public EvaluationContext Context { get; }

        // a swap's NPV on the discounting engine on the nominal curve
        public Real Npv(Swap swap) => DiscountingSwapEngine.Calculate(swap, Context, NominalTS).NPV!.Value;

        // a cap or floor's NPV on makeEngine's engine
        public Real Npv(YoYInflationCapFloor capFloor, Real volatility, int which) => MakeEngine(volatility, which)(capFloor, Context);

        // utilities
        public ImmutableArray<CashFlow> MakeYoYLeg(Date startDate, int length, Rate gearing = 1.0, Rate spread = 0.0)
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

            Rate[] gearingVector = [.. Enumerable.Repeat(gearing, length)];
            Spread[] spreadVector = [.. Enumerable.Repeat(spread, length)];

            return new YoYInflationLeg(schedule, Calendar, ii, ObservationLag, CPIInterpolationType.Flat)
                .WithNotionals(Nominals)
                .WithPaymentDayCounter(Dc)
                .WithGearings(gearingVector)
                .WithSpreads(spreadVector)
                .WithPaymentAdjustment(Convention)
                .ToLeg();
        }

        public ImmutableArray<CashFlow> MakeFixedLeg(Date startDate, int length)
        {
            Date endDate = Calendar.Advance(startDate, length, TimeUnit.Years, Convention);
            var schedule = new Schedule(
                startDate, endDate, new Period(Frequency), Calendar, Convention, Convention, DateGenerationRule.Forward, false, Context);
            Rate[] coupons = [.. Enumerable.Repeat(0.0, length)];
            return new FixedRateLeg(schedule)
                .WithNotionals(Nominals)
                .WithCouponRates(coupons, Dc)
                .ToLeg();
        }

        public ImmutableArray<CashFlow> MakeYoYCapFlooredLeg(
            int which,
            Date startDate,
            int length,
            Rate[] caps,
            Rate[] floors,
            Volatility volatility,
            Rate gearing = 1.0,
            Rate spread = 0.0)
        {
            YoYOptionletVolatilitySurface vol = ConstantYoYOptionletVolatility.Create(
                volatility, SettlementDays, Calendar, Convention, Dc, ObservationLag, Frequency, false, Context);

            YoYInflationCouponPricer pricer = which switch
            {
                0 => new BlackYoYInflationCouponPricer(vol, NominalTS),
                1 => new UnitDisplacedBlackYoYInflationCouponPricer(vol, NominalTS),
                2 => new BachelierYoYInflationCouponPricer(vol, NominalTS),
                _ => throw new InvalidOperationException(
                    $"unknown coupon pricer request: which = {which}should be 0=Black,1=DD,2=Bachelier"),
            };

            Rate[] gearingVector = [.. Enumerable.Repeat(gearing, length)];
            Spread[] spreadVector = [.. Enumerable.Repeat(spread, length)];

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

            ImmutableArray<CashFlow> yoyLeg = new YoYInflationLeg(schedule, Calendar, ii, ObservationLag, CPIInterpolationType.Flat)
                .WithNotionals(Nominals)
                .WithPaymentDayCounter(Dc)
                .WithPaymentAdjustment(Convention)
                .WithGearings(gearingVector)
                .WithSpreads(spreadVector)
                .WithCaps(caps.Select(c => (double?)c))
                .WithFloors(floors.Select(f => (double?)f))
                .ToLeg();

            return InflationCouponPricerFunctions.SetCouponPricer(yoyLeg, pricer);
        }

        public Func<YoYInflationCapFloor, EvaluationContext, Real> MakeEngine(Volatility volatility, int which)
        {
            YoYInflationIndex yyii = Iir;

            YoYOptionletVolatilitySurface vol = ConstantYoYOptionletVolatility.Create(
                volatility, SettlementDays, Calendar, Convention, Dc, ObservationLag, Frequency, false, Context);

            YieldTermStructure nominalTS = NominalTS;
            return which switch
            {
                0 => (c, ctx) => YoYInflationBlackCapFloorEngine.Calculate(c, ctx, yyii, vol, nominalTS).NPV!.Value,
                1 => (c, ctx) => YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(c, ctx, yyii, vol, nominalTS).NPV!.Value,
                2 => (c, ctx) => YoYInflationBachelierCapFloorEngine.Calculate(c, ctx, yyii, vol, nominalTS).NPV!.Value,
                _ => throw new InvalidOperationException(
                    $"unknown engine request: which = {which}should be 0=Black,1=DD,2=Bachelier"),
            };
        }
    }
}
