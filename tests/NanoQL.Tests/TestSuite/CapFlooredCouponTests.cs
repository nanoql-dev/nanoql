// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/capflooredcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class CapFlooredCouponTests
{
    // Adapted: CommonVars' relinkable handle, linked to the curve (lines 79-80) after the index is built on it (line
    // 70), is the curve itself, built first, with the index built on it; the evaluation date it sets (line 74) is the
    // context the legs, the caplet volatility (lines 129-132, a reference date from the evaluation date, so a factory
    // taking the context) and the engine read; the pricer, built with QuantLib's defaults, reads the context's at-par
    // switch; and the engine is the static DiscountingSwapEngine.Calculate.
    [Fact]
    public void TestLargeRates()
    {
        // Testing degenerate collared coupon...

        var vars = new CommonVars();

        /* A vanilla floating leg and a capped floating leg with strike
           equal to 100 and floor equal to 0 must have (about) the same NPV
           (depending on variance: option expiry and volatility)
        */

        double?[] caps = [.. Enumerable.Repeat<double?>(100.0, vars.Length)];
        double?[] floors = [.. Enumerable.Repeat<double?>(0.0, vars.Length)];
        Real tolerance = 1e-10;

        // fixed leg with zero rate
        Leg fixedLeg =
            vars.MakeFixedLeg(vars.StartDate, vars.Length);
        Leg floatLeg =
            vars.MakeFloatingLeg(vars.StartDate, vars.Length);
        Leg collaredLeg =
            vars.MakeCapFlooredLeg(vars.StartDate, vars.Length, caps, floors, vars.Volatility);

        var vanillaLeg = new Swap(fixedLeg, floatLeg);
        var collarLeg = new Swap(fixedLeg, collaredLeg);
        Real vanillaNpv = DiscountingSwapEngine.Calculate(vanillaLeg, vars.Context, vars.TermStructure).NPV!.Value;
        Real collarNpv = DiscountingSwapEngine.Calculate(collarLeg, vars.Context, vars.TermStructure).NPV!.Value;

        if (Math.Abs(vanillaNpv - collarNpv) > tolerance)
        {
            Assert.Fail($"Length: {vars.Length} y\n"
                        + $"Volatility: {vars.Volatility * 100}%\n"
                        + $"Notional: {vars.Nominal}\n"
                        + $"Vanilla floating leg NPV: {vanillaNpv}\n"
                        + $"Collared floating leg NPV (strikes 0 and 100): {collarNpv}\n"
                        + $"Diff: {Math.Abs(vanillaNpv - collarNpv)}");
        }
    }

    // Adapted: as TestLargeRates; each instrument's engine set with setPricingEngine (makeEngine's, lines 152-156) is
    // the static BlackCapFloorEngine.Calculate MakeEngine returns, called on the instrument and the context; and the
    // empty cap and floor vectors (lines 243 and 245) are empty arrays of optional rates.
    [Fact]
    public void TestDecomposition()
    {
        // Testing collared coupon against its decomposition...

        var vars = new CommonVars();

        Real tolerance = 1e-12;
        Real npvVanilla, npvCappedLeg, npvFlooredLeg, npvCollaredLeg, npvCap, npvFloor, npvCollar;
        Real error;
        Rate floorstrike = 0.05;
        Rate capstrike = 0.10;
        double?[] caps = [.. Enumerable.Repeat<double?>(capstrike, vars.Length)];
        double?[] caps0 = [];
        double?[] floors = [.. Enumerable.Repeat<double?>(floorstrike, vars.Length)];
        double?[] floors0 = [];
        Rate gearing_p = 0.5;
        Spread spread_p = 0.002;
        Rate gearing_n = -1.5;
        Spread spread_n = 0.12;

        // fixed leg with zero rate
        Leg fixedLeg =
            vars.MakeFixedLeg(vars.StartDate, vars.Length);

        // floating leg with gearing=1 and spread=0
        Leg floatLeg =
            vars.MakeFloatingLeg(vars.StartDate, vars.Length);

        // floating leg with positive gearing (gearing_p) and spread<>0
        Leg floatLeg_p =
            vars.MakeFloatingLeg(vars.StartDate, vars.Length, gearing_p, spread_p);

        // floating leg with negative gearing (gearing_n) and spread<>0
        Leg floatLeg_n =
            vars.MakeFloatingLeg(vars.StartDate, vars.Length, gearing_n, spread_n);

        // Swap with null fixed leg and floating leg with gearing=1 and spread=0
        var vanillaLeg = new Swap(fixedLeg, floatLeg);

        // Swap with null fixed leg and floating leg with positive gearing and spread<>0
        var vanillaLeg_p = new Swap(fixedLeg, floatLeg_p);

        // Swap with null fixed leg and floating leg with negative gearing and spread<>0
        var vanillaLeg_n = new Swap(fixedLeg, floatLeg_n);

        Real Npv(Swap swap) => DiscountingSwapEngine.Calculate(swap, vars.Context, vars.TermStructure).NPV!.Value;
        Real CapNpv(CapFloor capFloor) => vars.MakeEngine(vars.Volatility)(capFloor, vars.Context).NPV!.Value;
        var failures = new List<string>();

        /* CAPPED coupon - Decomposition of payoff
           Payoff = Nom * Min(rate,strike) * accrualperiod =
                  = Nom * [rate + Min(0,strike-rate)] * accrualperiod =
                  = Nom * rate * accrualperiod - Nom * Max(rate-strike,0) * accrualperiod =
                  = VanillaFloatingLeg - Call
        */

        // Case gearing = 1 and spread = 0
        Leg cappedLeg =
            vars.MakeCapFlooredLeg(vars.StartDate, vars.Length, caps, floors0, vars.Volatility);
        var capLeg = new Swap(fixedLeg, cappedLeg);
        var cap = new Cap(floatLeg, [capstrike]);
        npvVanilla = Npv(vanillaLeg);
        npvCappedLeg = Npv(capLeg);
        npvCap = CapNpv(cap);
        error = Math.Abs(npvCappedLeg - (npvVanilla - npvCap));
        if (error > tolerance)
        {
            failures.Add($"\nCapped Leg: gearing=1, spread=0%, strike={capstrike * 100}%\n"
                         + $"  Capped Floating Leg NPV: {npvCappedLeg}\n"
                         + $"  Floating Leg NPV - Cap NPV: {npvVanilla - npvCap}\n"
                         + $"  Diff: {error}");
        }

        /* gearing = 1 and spread = 0
           FLOORED coupon - Decomposition of payoff
           Payoff = Nom * Max(rate,strike) * accrualperiod =
                  = Nom * [rate + Max(0,strike-rate)] * accrualperiod =
                  = Nom * rate * accrualperiod + Nom * Max(strike-rate,0) * accrualperiod =
                  = VanillaFloatingLeg + Put
        */

        Leg flooredLeg =
            vars.MakeCapFlooredLeg(vars.StartDate, vars.Length, caps0, floors, vars.Volatility);
        var floorLeg = new Swap(fixedLeg, flooredLeg);
        var floor = new Floor(floatLeg, [floorstrike]);
        npvFlooredLeg = Npv(floorLeg);
        npvFloor = CapNpv(floor);
        error = Math.Abs(npvFlooredLeg - (npvVanilla + npvFloor));
        if (error > tolerance)
        {
            failures.Add($"Floored Leg: gearing=1, spread=0%, strike={floorstrike * 100}%\n"
                         + $"  Floored Floating Leg NPV: {npvFlooredLeg}\n"
                         + $"  Floating Leg NPV + Floor NPV: {npvVanilla + npvFloor}\n"
                         + $"  Diff: {error}");
        }

        /* gearing = 1 and spread = 0
           COLLARED coupon - Decomposition of payoff
           Payoff = Nom * Min(strikem,Max(rate,strikeM)) * accrualperiod =
                  = VanillaFloatingLeg - Collar
        */

        Leg collaredLeg =
            vars.MakeCapFlooredLeg(vars.StartDate, vars.Length, caps, floors, vars.Volatility);
        var collarLeg = new Swap(fixedLeg, collaredLeg);
        var collar = new Collar(floatLeg, [capstrike], [floorstrike]);
        npvCollaredLeg = Npv(collarLeg);
        npvCollar = CapNpv(collar);
        error = Math.Abs(npvCollaredLeg - (npvVanilla - npvCollar));
        if (error > tolerance)
        {
            failures.Add($"\nCollared Leg: gearing=1, spread=0%, strike={floorstrike * 100}% and {capstrike * 100}%\n"
                         + $"  Collared Floating Leg NPV: {npvCollaredLeg}\n"
                         + $"  Floating Leg NPV - Collar NPV: {npvVanilla - npvCollar}\n"
                         + $"  Diff: {error}");
        }

        /* gearing = a and spread = b
           CAPPED coupon - Decomposition of payoff
           Payoff
           = Nom * Min(a*rate+b,strike) * accrualperiod =
           = Nom * [a*rate+b + Min(0,strike-a*rate-b)] * accrualperiod =
           = Nom * a*rate+b * accrualperiod + Nom * Min(strike-b-a*rate,0) * accrualperiod
           --> If a>0 (assuming positive effective strike):
               Payoff = VanillaFloatingLeg - Call(a*rate+b,strike)
           --> If a<0 (assuming positive effective strike):
               Payoff = VanillaFloatingLeg + Nom * Min(strike-b+|a|*rate+,0) * accrualperiod =
                      = VanillaFloatingLeg + Put(|a|*rate+b,strike)
        */

        // Positive gearing
        Leg cappedLeg_p =
            vars.MakeCapFlooredLeg(vars.StartDate, vars.Length, caps, floors0, vars.Volatility, gearing_p, spread_p);
        var capLeg_p = new Swap(fixedLeg, cappedLeg_p);
        var cap_p = new Cap(floatLeg_p, [capstrike]);
        npvVanilla = Npv(vanillaLeg_p);
        npvCappedLeg = Npv(capLeg_p);
        npvCap = CapNpv(cap_p);
        error = Math.Abs(npvCappedLeg - (npvVanilla - npvCap));
        if (error > tolerance)
        {
            failures.Add($"\nCapped Leg: gearing={gearing_p}, spread= {spread_p * 100}%, strike={capstrike * 100}%, "
                         + $"effective strike= {(capstrike - spread_p) / gearing_p * 100}%\n"
                         + $"  Capped Floating Leg NPV: {npvCappedLeg}\n"
                         + $"  Vanilla Leg NPV: {npvVanilla}\n"
                         + $"  Cap NPV: {npvCap}\n"
                         + $"  Floating Leg NPV - Cap NPV: {npvVanilla - npvCap}\n"
                         + $"  Diff: {error}");
        }

        // Negative gearing
        Leg cappedLeg_n =
            vars.MakeCapFlooredLeg(vars.StartDate, vars.Length, caps, floors0, vars.Volatility, gearing_n, spread_n);
        var capLeg_n = new Swap(fixedLeg, cappedLeg_n);
        var floor_n = new Floor(floatLeg, [(capstrike - spread_n) / gearing_n]);
        npvVanilla = Npv(vanillaLeg_n);
        npvCappedLeg = Npv(capLeg_n);
        npvFloor = CapNpv(floor_n);
        error = Math.Abs(npvCappedLeg - (npvVanilla + (gearing_n * npvFloor)));
        if (error > tolerance)
        {
            failures.Add($"\nCapped Leg: gearing={gearing_n}, spread= {spread_n * 100}%, strike={capstrike * 100}%, "
                         + $"effective strike= {(capstrike - spread_n) / gearing_n * 100}%\n"
                         + $"  Capped Floating Leg NPV: {npvCappedLeg}\n"
                         + $"  npv Vanilla: {npvVanilla}\n"
                         + $"  npvFloor: {npvFloor}\n"
                         + $"  Floating Leg NPV - Cap NPV: {npvVanilla + (gearing_n * npvFloor)}\n"
                         + $"  Diff: {error}");
        }

        /* gearing = a and spread = b
           FLOORED coupon - Decomposition of payoff
           Payoff
           = Nom * Max(a*rate+b,strike) * accrualperiod =
           = Nom * [a*rate+b + Max(0,strike-a*rate-b)] * accrualperiod =
           = Nom * a*rate+b * accrualperiod + Nom * Max(strike-b-a*rate,0) * accrualperiod
           --> If a>0 (assuming positive effective strike):
               Payoff = VanillaFloatingLeg + Put(a*rate+b,strike)
           --> If a<0 (assuming positive effective strike):
               Payoff = VanillaFloatingLeg + Nom * Max(strike-b+|a|*rate+,0) * accrualperiod =
                      = VanillaFloatingLeg - Call(|a|*rate+b,strike)
        */

        // Positive gearing
        Leg flooredLeg_p1 =
            vars.MakeCapFlooredLeg(vars.StartDate, vars.Length, caps0, floors, vars.Volatility, gearing_p, spread_p);
        var floorLeg_p1 = new Swap(fixedLeg, flooredLeg_p1);
        var floor_p1 = new Floor(floatLeg_p, [floorstrike]);
        npvVanilla = Npv(vanillaLeg_p);
        npvFlooredLeg = Npv(floorLeg_p1);
        npvFloor = CapNpv(floor_p1);
        error = Math.Abs(npvFlooredLeg - (npvVanilla + npvFloor));
        if (error > tolerance)
        {
            failures.Add($"\nFloored Leg: gearing={gearing_p}, spread= {spread_p * 100}%, strike={floorstrike * 100}%, "
                         + $"effective strike= {(floorstrike - spread_p) / gearing_p * 100}%\n"
                         + $"  Floored Floating Leg NPV: {npvFlooredLeg}\n"
                         + $"  Floating Leg NPV + Floor NPV: {npvVanilla + npvFloor}\n"
                         + $"  Diff: {error}");
        }

        // Negative gearing
        Leg flooredLeg_n =
            vars.MakeCapFlooredLeg(vars.StartDate, vars.Length, caps0, floors, vars.Volatility, gearing_n, spread_n);
        var floorLeg_n = new Swap(fixedLeg, flooredLeg_n);
        var cap_n = new Cap(floatLeg, [(floorstrike - spread_n) / gearing_n]);
        npvVanilla = Npv(vanillaLeg_n);
        npvFlooredLeg = Npv(floorLeg_n);
        npvCap = CapNpv(cap_n);
        error = Math.Abs(npvFlooredLeg - (npvVanilla - (gearing_n * npvCap)));
        if (error > tolerance)
        {
            failures.Add($"\nCapped Leg: gearing={gearing_n}, spread= {spread_n * 100}%, strike={floorstrike * 100}%, "
                         + $"effective strike= {(floorstrike - spread_n) / gearing_n * 100}%\n"
                         + $"  Capped Floating Leg NPV: {npvFlooredLeg}\n"
                         + $"  Floating Leg NPV - Cap NPV: {npvVanilla - (gearing_n * npvCap)}\n"
                         + $"  Diff: {error}");
        }

        /* gearing = a and spread = b
           COLLARED coupon - Decomposition of payoff
           Payoff = Nom * Min(caprate,Max(a*rate+b,floorrate)) * accrualperiod
           --> If a>0 (assuming positive effective strike):
               Payoff = VanillaFloatingLeg - Collar(a*rate+b, floorrate, caprate)
           --> If a<0 (assuming positive effective strike):
               Payoff = VanillaFloatingLeg + Collar(|a|*rate+b, caprate, floorrate)
        */

        // Positive gearing
        Leg collaredLeg_p =
            vars.MakeCapFlooredLeg(vars.StartDate, vars.Length, caps, floors, vars.Volatility, gearing_p, spread_p);
        var collarLeg_p1 = new Swap(fixedLeg, collaredLeg_p);
        var collar_p = new Collar(floatLeg_p, [capstrike], [floorstrike]);
        npvVanilla = Npv(vanillaLeg_p);
        npvCollaredLeg = Npv(collarLeg_p1);
        npvCollar = CapNpv(collar_p);
        error = Math.Abs(npvCollaredLeg - (npvVanilla - npvCollar));
        if (error > tolerance)
        {
            failures.Add($"\nCollared Leg: gearing={gearing_p}, spread= {spread_p * 100}%, strike={floorstrike * 100}% "
                         + $"and {capstrike * 100}%, effective strike={(floorstrike - spread_p) / gearing_p * 100}% and "
                         + $"{(capstrike - spread_p) / gearing_p * 100}%\n"
                         + $"  Collared Floating Leg NPV: {npvCollaredLeg}\n"
                         + $"  Floating Leg NPV - Collar NPV: {npvVanilla - npvCollar}\n"
                         + $"  Diff: {error}");
        }

        // Negative gearing
        Leg collaredLeg_n =
            vars.MakeCapFlooredLeg(vars.StartDate, vars.Length, caps, floors, vars.Volatility, gearing_n, spread_n);
        var collarLeg_n1 = new Swap(fixedLeg, collaredLeg_n);
        var collar_n = new Collar(
            floatLeg, [(floorstrike - spread_n) / gearing_n], [(capstrike - spread_n) / gearing_n]);
        npvVanilla = Npv(vanillaLeg_n);
        npvCollaredLeg = Npv(collarLeg_n1);
        npvCollar = CapNpv(collar_n);
        error = Math.Abs(npvCollaredLeg - (npvVanilla - (gearing_n * npvCollar)));
        if (error > tolerance)
        {
            failures.Add($"\nCollared Leg: gearing={gearing_n}, spread= {spread_n * 100}%, strike={floorstrike * 100}% "
                         + $"and {capstrike * 100}%, effective strike={(floorstrike - spread_n) / gearing_n * 100}% and "
                         + $"{(capstrike - spread_n) / gearing_n * 100}%\n"
                         + $"  Collared Floating Leg NPV: {npvCollaredLeg}\n"
                         + $"  Floating Leg NPV - Collar NPV: {npvVanilla - (gearing_n * npvCollar)}\n"
                         + $"  Diff: {error}");
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private sealed class CommonVars
    {
        // setup
        public CommonVars()
        {
            Length = 20; // years
            Volatility = 0.20;
            Nominal = 100.0;
            Nominals = [.. Enumerable.Repeat(Nominal, Length)];
            Frequency = Frequency.Annual;
            Calendar = new TARGET();
            Convention = BusinessDayConvention.ModifiedFollowing;
            Today = Calendar.Adjust(Date.TodaysDate());
            Context = new EvaluationContext { EvaluationDate = Today };
            SettlementDays = 2;
            FixingDays = 2;
            Settlement = Calendar.Advance(Today, SettlementDays, TimeUnit.Days);
            StartDate = Settlement;
            TermStructure = new FlatForward(Settlement, 0.05, new ActualActual(ActualActualConvention.ISDA));
            Index = new Euribor1Y(TermStructure);
        }

        // global data
        public Date Today { get; }

        public Date Settlement { get; }

        public Date StartDate { get; }

        public Calendar Calendar { get; }

        public Real Nominal { get; }

        public ImmutableArray<Real> Nominals { get; }

        public BusinessDayConvention Convention { get; }

        public Frequency Frequency { get; }

        public IborIndex Index { get; }

        public Natural SettlementDays { get; }

        public Natural FixingDays { get; }

        public YieldTermStructure TermStructure { get; }

        public int Length { get; }

        public Volatility Volatility { get; }

        public EvaluationContext Context { get; }

        // utilities
        public Leg MakeFixedLeg(Date startDate, int length)
        {
            Date endDate = Calendar.Advance(startDate, length, TimeUnit.Years, Convention);
            var schedule = new Schedule(startDate, endDate, new Period(Frequency), Calendar,
                                        Convention, Convention,
                                        DateGenerationRule.Forward, false, Context);
            Rate[] coupons = [.. Enumerable.Repeat(0.0, length)];
            return new FixedRateLeg(schedule)
                .WithNotionals(Nominals)
                .WithCouponRates(coupons, new Thirty360(Thirty360Convention.BondBasis))
                .ToLeg();
        }

        public Leg MakeFloatingLeg(Date startDate, int length, Rate gearing = 1.0, Rate spread = 0.0)
        {
            Date endDate = Calendar.Advance(startDate, length, TimeUnit.Years, Convention);
            var schedule = new Schedule(startDate, endDate, new Period(Frequency), Calendar,
                                        Convention, Convention,
                                        DateGenerationRule.Forward, false, Context);
            Real[] gearingVector = [.. Enumerable.Repeat(gearing, length)];
            Spread[] spreadVector = [.. Enumerable.Repeat(spread, length)];
            return new IborLeg(schedule, Index)
                .WithNotionals(Nominals)
                .WithPaymentDayCounter(Index.DayCounter)
                .WithPaymentAdjustment(Convention)
                .WithFixingDays(FixingDays)
                .WithGearings(gearingVector)
                .WithSpreads(spreadVector)
                .ToLeg(Context);
        }

        public Leg MakeCapFlooredLeg(
            Date startDate,
            int length,
            double?[] caps,
            double?[] floors,
            Volatility volatility,
            Rate gearing = 1.0,
            Rate spread = 0.0)
        {
            Date endDate = Calendar.Advance(startDate, length, TimeUnit.Years, Convention);
            var schedule = new Schedule(startDate, endDate, new Period(Frequency), Calendar,
                                        Convention, Convention,
                                        DateGenerationRule.Forward, false, Context);
            OptionletVolatilityStructure vol = ConstantOptionletVolatility.Create(
                0, Calendar, BusinessDayConvention.Following, volatility, new Actual365Fixed(), Context);

            IborCouponPricer pricer = new BlackIborCouponPricer(Context, vol);
            Real[] gearingVector = [.. Enumerable.Repeat(gearing, length)];
            Spread[] spreadVector = [.. Enumerable.Repeat(spread, length)];

            Leg iborLeg = new IborLeg(schedule, Index)
                .WithNotionals(Nominals)
                .WithPaymentDayCounter(Index.DayCounter)
                .WithPaymentAdjustment(Convention)
                .WithFixingDays(FixingDays)
                .WithGearings(gearingVector)
                .WithSpreads(spreadVector)
                .WithCaps(caps)
                .WithFloors(floors)
                .ToLeg(Context);
            return CouponPricerFunctions.SetCouponPricer(iborLeg, pricer);
        }

        // QuantLib's BlackCapFloorEngine on the curve and a volatility quote, the static engine on a volatility.
        public Func<CapFloor, EvaluationContext, InstrumentResults> MakeEngine(Volatility volatility) =>
            (capFloor, context) => BlackCapFloorEngine.Calculate(capFloor, context, TermStructure, volatility);
    }
}
