// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/capflooredcoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testDecomposition, which waits for S8's cap and floor instruments and their Black engine

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
    }
}
