// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2019 Wojciech Ślusarski
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/capfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class CapFloorTests
{
    // Adapted: CommonVars' relinkable handle, linked to the curve (lines 72-74) after the index is built on it (line
    // 66), is the curve itself, built first, with the index built on it; the evaluation date it reads (line 69) is the
    // test runner's, the context; each engine (lines 90-100) is the static BlackCapFloorEngine.Calculate on the curve
    // and the volatility; and the "vega" result is read with InstrumentResults.Result.
    [Fact]
    public void TestVega()
    {
        // Testing cap/floor vega...

        var vars = new CommonVars();

        Integer[] lengths = [1, 2, 3, 4, 5, 6, 7, 10, 15, 20, 30];
        Volatility[] vols = [0.01, 0.05, 0.10, 0.15, 0.20];
        Rate[] strikes = [0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07, 0.08, 0.09];
        CapFloorType[] types = [CapFloorType.Cap, CapFloorType.Floor];

        Date startDate = vars.TermStructure.ReferenceDate;
        const Real shift = 1e-8;
        const Real tolerance = 0.005;

        foreach (int length in lengths)
        {
            foreach (Real vol in vols)
            {
                foreach (Real strike in strikes)
                {
                    foreach (CapFloorType type in types)
                    {
                        Leg leg = vars.MakeLeg(startDate, length);
                        CapFloor capFloor = vars.MakeCapFloor(type, leg, strike);
                        CapFloor shiftedCapFloor2 = vars.MakeCapFloor(type, leg, strike);
                        CapFloor shiftedCapFloor1 = vars.MakeCapFloor(type, leg, strike);
                        Real value1 = vars.Npv(shiftedCapFloor1, vol - shift);
                        Real value2 = vars.Npv(shiftedCapFloor2, vol + shift);
                        Real numericalVega = (value2 - value1) / (2 * shift);
                        if (numericalVega > 1.0e-4)
                        {
                            Real analyticalVega = vars.Price(capFloor, vol).Result<Real>("vega");
                            Real discrepancy = Math.Abs(numericalVega - analyticalVega);
                            discrepancy /= numericalVega;
                            if (discrepancy > tolerance)
                            {
                                Assert.Fail("failed to compute cap/floor vega:"
                                            + $"\n   lengths:     {new Period(length, TimeUnit.Years)}"
                                            + $"\n   strike:      {DataFormattersFunctions.Rate(strike)}"
                                            + $"\n   calculated:  {analyticalVega:F12}"
                                            + $"\n   expected:    {numericalVega:F12}"
                                            + $"\n   discrepancy: {DataFormattersFunctions.Rate(discrepancy)}"
                                            + $"\n   tolerance:   {DataFormattersFunctions.Rate(tolerance)}");
                            }
                        }
                    }
                }
            }
        }
    }

    // Adapted: as TestVega, the curve built first and the engine a static call; std::adjacent_find with std::less<>
    // (line 222) and std::greater<> (line 234) is a loop over adjacent pairs.
    [Fact]
    public void TestStrikeDependency()
    {
        // Testing cap/floor dependency on strike...

        var vars = new CommonVars();

        Integer[] lengths = [1, 2, 3, 5, 7, 10, 15, 20];
        Volatility[] vols = [0.01, 0.05, 0.10, 0.15, 0.20];
        Rate[] strikes = [0.03, 0.04, 0.05, 0.06, 0.07];

        Date startDate = vars.TermStructure.ReferenceDate;

        foreach (int length in lengths)
        {
            foreach (Real vol in vols)
            {
                // store the results for different strikes...
                var capValues = new List<Real>();
                var floorValues = new List<Real>();
                foreach (Real strike in strikes)
                {
                    Leg leg = vars.MakeLeg(startDate, length);
                    Instrument cap = vars.MakeCapFloor(CapFloorType.Cap, leg, strike);
                    capValues.Add(vars.Npv((CapFloor)cap, vol));
                    Instrument floor = vars.MakeCapFloor(CapFloorType.Floor, leg, strike);
                    floorValues.Add(vars.Npv((CapFloor)floor, vol));
                }

                // and check that they go the right way
                int it = AdjacentFind(capValues, (a, b) => a < b);
                if (it != capValues.Count)
                {
                    int n = it;
                    Assert.Fail("NPV is increasing with the strike in a cap: \n"
                                + $"    length:     {length} years\n"
                                + $"    volatility: {DataFormattersFunctions.Volatility(vol)}\n"
                                + $"    value:      {capValues[n]} at strike: {DataFormattersFunctions.Rate(strikes[n])}\n"
                                + $"    value:      {capValues[n + 1]} at strike: {DataFormattersFunctions.Rate(strikes[n + 1])}");
                }

                // same for floors
                it = AdjacentFind(floorValues, (a, b) => a > b);
                if (it != floorValues.Count)
                {
                    int n = it;
                    Assert.Fail("NPV is decreasing with the strike in a floor: \n"
                                + $"    length:     {length} years\n"
                                + $"    volatility: {DataFormattersFunctions.Volatility(vol)}\n"
                                + $"    value:      {floorValues[n]} at strike: {DataFormattersFunctions.Rate(strikes[n])}\n"
                                + $"    value:      {floorValues[n + 1]} at strike: {DataFormattersFunctions.Rate(strikes[n + 1])}");
                }
            }
        }
    }

    // Adapted: as TestVega, the curve built first and the engines static calls, the caplets' engines (lines 290-345)
    // among them; the re-composition by optionlets follows BOOST_FAIL (line 277), which ends the case, as Assert.Fail
    // does.
    [Fact]
    public void TestConsistency()
    {
        // Testing consistency between cap, floor and collar...

        var vars = new CommonVars();

        Integer[] lengths = [1, 2, 3, 5, 7, 10, 15, 20];
        Rate[] capRates = [0.03, 0.04, 0.05, 0.06, 0.07];
        Rate[] floorRates = [0.03, 0.04, 0.05, 0.06, 0.07];
        Volatility[] vols = [0.01, 0.05, 0.10, 0.15, 0.20];

        Date startDate = vars.TermStructure.ReferenceDate;

        foreach (int length in lengths)
        {
            foreach (Real capRate in capRates)
            {
                foreach (Real floorRate in floorRates)
                {
                    foreach (Real vol in vols)
                    {
                        Leg leg = vars.MakeLeg(startDate, length);
                        CapFloor cap = vars.MakeCapFloor(CapFloorType.Cap, leg, capRate);
                        CapFloor floor = vars.MakeCapFloor(CapFloorType.Floor, leg, floorRate);
                        var collar = new Collar(leg, [capRate], [floorRate]);

                        Real capNpv = vars.Npv(cap, vol), floorNpv = vars.Npv(floor, vol), collarNpv = vars.Npv(collar, vol);
                        if (Math.Abs((capNpv - floorNpv) - collarNpv) > 1e-10)
                        {
                            Assert.Fail("inconsistency between cap, floor and collar:\n"
                                        + $"    length:       {length} years\n"
                                        + $"    volatility:   {DataFormattersFunctions.Volatility(vol)}\n"
                                        + $"    cap value:    {capNpv} at strike: {DataFormattersFunctions.Rate(capRate)}\n"
                                        + $"    floor value:  {floorNpv} at strike: {DataFormattersFunctions.Rate(floorRate)}\n"
                                        + $"    collar value: {collarNpv}");

                            // test re-composition by optionlets, N.B. two per year
                            Real capletsNPV = 0.0;
                            var caplets = new List<CapFloor>();
                            for (Integer m = 0; m < length * 2; m++)
                            {
                                caplets.Add(cap.Optionlet(m));
                                capletsNPV += vars.Npv(caplets[m], vol);
                            }

                            if (Math.Abs(capNpv - capletsNPV) > 1e-10)
                            {
                                Assert.Fail(
                                    "sum ofcaplet NPVs does not equal cap NPV:\n"
                                    + $"    length:       {length} years\n"
                                    + $"    volatility:   {DataFormattersFunctions.Volatility(vol)}\n"
                                    + $"    cap value:    {capNpv} at strike: {DataFormattersFunctions.Rate(capRate)}\n"
                                    + $"    sum of caplets value:  {capletsNPV} at strike (first): "
                                    + $"{DataFormattersFunctions.Rate(caplets[0].CapRates[0])}\n");
                            }

                            Real floorletsNPV = 0.0;
                            var floorlets = new List<CapFloor>();
                            for (Integer m = 0; m < length * 2; m++)
                            {
                                floorlets.Add(floor.Optionlet(m));
                                floorletsNPV += vars.Npv(floorlets[m], vol);
                            }

                            if (Math.Abs(floorNpv - floorletsNPV) > 1e-10)
                            {
                                Assert.Fail(
                                    "sum offloorlet NPVs does not equal floor NPV:\n"
                                    + $"    length:       {length} years\n"
                                    + $"    volatility:   {DataFormattersFunctions.Volatility(vol)}\n"
                                    + $"    cap value:    {floorNpv} at strike: {DataFormattersFunctions.Rate(floorRate)}\n"
                                    + $"    sum of floorlets value:  {floorletsNPV} at strike (first): "
                                    + $"{DataFormattersFunctions.Rate(floorlets[0].FloorRates[0])}\n");
                            }

                            Real collarletsNPV = 0.0;
                            var collarlets = new List<CapFloor>();
                            for (Integer m = 0; m < length * 2; m++)
                            {
                                collarlets.Add(collar.Optionlet(m));
                                collarletsNPV += vars.Npv(collarlets[m], vol);
                            }

                            if (Math.Abs(collarNpv - collarletsNPV) > 1e-10)
                            {
                                Assert.Fail(
                                    "sum ofcollarlet NPVs does not equal floor NPV:\n"
                                    + $"    length:       {length} years\n"
                                    + $"    volatility:   {DataFormattersFunctions.Volatility(vol)}\n"
                                    + $"    cap value:    {collarNpv} at strike floor: {DataFormattersFunctions.Rate(floorRate)}"
                                    + $" at strike cap: {DataFormattersFunctions.Rate(capRate)}\n"
                                    + $"    sum of collarlets value:  {collarletsNPV} at strike floor (first): "
                                    + $"{DataFormattersFunctions.Rate(collarlets[0].FloorRates[0])} at strike cap (first): "
                                    + $"{DataFormattersFunctions.Rate(collarlets[0].CapRates[0])}\n");
                            }
                        }
                    }
                }
            }
        }
    }

    // Adapted: as TestVega, the curve built first and the engines static calls, the swap's DiscountingSwapEngine
    // (lines 380-381) among them; the swap's constructor takes the context, whose at-par switch its floating leg reads.
    [Fact]
    public void TestParity()
    {
        // Testing cap/floor parity...

        var vars = new CommonVars();

        Integer[] lengths = [1, 2, 3, 5, 7, 10, 15, 20];
        Rate[] strikes = [0.0, 0.03, 0.04, 0.05, 0.06, 0.07];
        Volatility[] vols = [0.01, 0.05, 0.10, 0.15, 0.20];

        Date startDate = vars.TermStructure.ReferenceDate;

        foreach (int length in lengths)
        {
            foreach (Real strike in strikes)
            {
                foreach (Real vol in vols)
                {
                    Leg leg = vars.MakeLeg(startDate, length);
                    CapFloor cap = vars.MakeCapFloor(CapFloorType.Cap, leg, strike);
                    CapFloor floor = vars.MakeCapFloor(CapFloorType.Floor, leg, strike);
                    Date maturity = vars.Calendar.Advance(startDate, length, TimeUnit.Years, vars.Convention);
                    var schedule = new Schedule(
                        startDate, maturity, new Period(vars.Frequency), vars.Calendar, vars.Convention, vars.Convention,
                        DateGenerationRule.Forward, false, vars.Context);
                    var swap = new VanillaSwap(
                        SwapType.Payer, vars.Nominals[0], schedule, strike, vars.Index.DayCounter, schedule, vars.Index, 0.0,
                        vars.Index.DayCounter, vars.Context);
                    Real capNpv = vars.Npv(cap, vol), floorNpv = vars.Npv(floor, vol);
                    Real swapNpv = DiscountingSwapEngine.Calculate(swap, vars.Context, vars.TermStructure).NPV!.Value;
                    if (Math.Abs((capNpv - floorNpv) - swapNpv) > 1.0e-10)
                    {
                        Assert.Fail("put/call parity violated:\n"
                                    + $"    length:      {length} years\n"
                                    + $"    volatility:  {DataFormattersFunctions.Volatility(vol)}\n"
                                    + $"    strike:      {DataFormattersFunctions.Rate(strike)}\n"
                                    + $"    cap value:   {capNpv}\n"
                                    + $"    floor value: {floorNpv}\n"
                                    + $"    swap value:  {swapNpv}");
                    }
                }
            }
        }
    }

    // Adapted: as TestParity; atmRate (lines 418-419) takes the context, whose fixings and forecasts the coupons' amounts
    // read.
    [Fact]
    public void TestATMRate()
    {
        // Testing cap/floor ATM rate...

        var vars = new CommonVars();

        Integer[] lengths = [1, 2, 3, 5, 7, 10, 15, 20];
        Rate[] strikes = [0.0, 0.03, 0.04, 0.05, 0.06, 0.07];
        Volatility[] vols = [0.01, 0.05, 0.10, 0.15, 0.20];

        Date startDate = vars.TermStructure.ReferenceDate;

        foreach (int length in lengths)
        {
            Leg leg = vars.MakeLeg(startDate, length);
            Date maturity = vars.Calendar.Advance(startDate, length, TimeUnit.Years, vars.Convention);
            var schedule = new Schedule(
                startDate, maturity, new Period(vars.Frequency), vars.Calendar, vars.Convention, vars.Convention,
                DateGenerationRule.Forward, false, vars.Context);

            foreach (Real strike in strikes)
            {
                foreach (Real vol in vols)
                {
                    CapFloor cap = vars.MakeCapFloor(CapFloorType.Cap, leg, strike);
                    CapFloor floor = vars.MakeCapFloor(CapFloorType.Floor, leg, strike);
                    Rate capATMRate = cap.AtmRate(vars.TermStructure, vars.Context);
                    Rate floorATMRate = floor.AtmRate(vars.TermStructure, vars.Context);
                    if (!CheckAbsError(floorATMRate, capATMRate, 1.0e-10))
                    {
                        Assert.Fail("Cap ATM Rate and floor ATM Rate should be equal :\n"
                                    + $"   length:        {length} years\n"
                                    + $"   volatility:    {DataFormattersFunctions.Volatility(vol)}\n"
                                    + $"   strike:        {DataFormattersFunctions.Rate(strike)}\n"
                                    + $"   cap ATM rate:  {capATMRate}\n"
                                    + $"   floor ATM rate:{floorATMRate}\n"
                                    + $"   relative Error:{Math.Abs(capATMRate - floorATMRate) / capATMRate * 100}%");
                    }

                    var swap = new VanillaSwap(
                        SwapType.Payer, vars.Nominals[0], schedule, floorATMRate, vars.Index.DayCounter, schedule,
                        vars.Index, 0.0, vars.Index.DayCounter, vars.Context);
                    Real swapNPV = DiscountingSwapEngine.Calculate(swap, vars.Context, vars.TermStructure).NPV!.Value;
                    if (!CheckAbsError(swapNPV, 0, 1.0e-10))
                    {
                        Assert.Fail("the NPV of a Swap struck at ATM rate should be equal to 0:\n"
                                    + $"   length:        {length} years\n"
                                    + $"   volatility:    {DataFormattersFunctions.Volatility(vol)}\n"
                                    + $"   ATM rate:      {DataFormattersFunctions.Rate(floorATMRate)}\n"
                                    + $"   swap NPV:      {swapNPV}");
                    }
                }
            }
        }
    }

    // Adapted: the handle relinked to each rate's curve (line 481), on which the index forecasts, is a curve, an index,
    // a leg and an instrument built per rate, the leg on the same schedule; the engine set per volatility (lines 482,
    // 506 and 518) is a static call; and impliedVolatility (lines 488-494) takes the context.
    [Fact]
    public void TestImpliedVolatility()
    {
        // Testing implied term volatility for cap and floor...

        var vars = new CommonVars();

        Size maxEvaluations = 100;
        Real tolerance = 1.0e-8;

        CapFloorType[] types = [CapFloorType.Cap, CapFloorType.Floor];
        Rate[] strikes = [0.02, 0.03, 0.04];
        Integer[] lengths = [1, 5, 10];

        // test data
        Rate[] rRates = [0.02, 0.03, 0.04, 0.05, 0.06, 0.07];
        Volatility[] vols = [0.01, 0.05, 0.10, 0.20, 0.30, 0.70, 0.90];

        foreach (int length in lengths)
        {
            foreach (CapFloorType type in types)
            {
                foreach (Real strike in strikes)
                {
                    foreach (Real r in rRates)
                    {
                        var rVars = new CommonVars(curve: new FlatForward(vars.Settlement, r, new Actual360()));
                        Leg leg = rVars.MakeLeg(rVars.Settlement, length);
                        CapFloor capfloor = rVars.MakeCapFloor(type, leg, strike);

                        foreach (Real v in vols)
                        {
                            Real value = rVars.Npv(capfloor, v);
                            Volatility implVol = 0.0;
                            try
                            {
                                implVol = capfloor.ImpliedVolatility(
                                    value, rVars.TermStructure, 0.10, rVars.Context, tolerance, maxEvaluations, 10.0e-7, 4.0,
                                    VolatilityType.ShiftedLognormal, 0.0);
                            }
                            catch (Exception e) when (e is NanoQLException or ArgumentException)
                            {
                                // couldn't bracket?
                                Real value2 = rVars.Npv(capfloor, 0.0);
                                if (Math.Abs(value - value2) < tolerance)
                                {
                                    // ok, just skip:
                                    continue;
                                }

                                // otherwise, report error
                                Assert.Fail($"implied vol failure: {TypeToString(type)}\n  strike:     "
                                            + $"{DataFormattersFunctions.Rate(strike)}\n  risk-free:  {DataFormattersFunctions.Rate(r)}"
                                            + $"\n  length:     {length}Y"
                                            + $"\n  volatility: {DataFormattersFunctions.Volatility(v)}"
                                            + $"\n  price:      {value}\n{e.Message}");
                            }

                            if (Math.Abs(implVol - v) > tolerance)
                            {
                                // the difference might not matter
                                Real value2 = rVars.Npv(capfloor, implVol);
                                if (Math.Abs(value - value2) > tolerance)
                                {
                                    Assert.Fail($"implied vol failure: {TypeToString(type)}"
                                                + $"\n  strike:        {DataFormattersFunctions.Rate(strike)}"
                                                + $"\n  risk-free:     {DataFormattersFunctions.Rate(r)}"
                                                + $"\n  length:        {length}Y"
                                                + $"\n  volatility:    {DataFormattersFunctions.Volatility(v)}"
                                                + $"\n  price:         {value}"
                                                + $"\n  implied vol:   {DataFormattersFunctions.Volatility(implVol)}"
                                                + $"\n  implied price: {value2}");
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // Adapted: the evaluation date set after CommonVars (line 544) and the handle relinked to the cached curve (line
    // 545) are the context and the curve CommonVars is built on; the engine is a static call; and the at-par switch
    // read (line 554) is the context's.
    [Fact]
    public void TestCachedValue()
    {
        // Testing Black cap/floor price against cached values...

        Date cachedToday = new(14, March, 2002), cachedSettlement = new(18, March, 2002);
        var vars = new CommonVars(cachedToday, new FlatForward(cachedSettlement, 0.05, new Actual360()));
        Date startDate = vars.TermStructure.ReferenceDate;
        Leg leg = vars.MakeLeg(startDate, 20);
        CapFloor cap = vars.MakeCapFloor(CapFloorType.Cap, leg, 0.07);
        CapFloor floor = vars.MakeCapFloor(CapFloorType.Floor, leg, 0.03);

        Real cachedCapNPV, cachedFloorNPV;
        if (!vars.Context.UseAtParCoupons)
        {
            // index fixing price
            cachedCapNPV = 6.87630307745;
            cachedFloorNPV = 2.65796764715;
        }
        else
        {
            // par coupon price
            cachedCapNPV = 6.87570026732;
            cachedFloorNPV = 2.65812927959;
        }

        // test Black cap price against cached value
        Real capNpv = vars.Npv(cap, 0.20), floorNpv = vars.Npv(floor, 0.20);
        Assert.False(
            Math.Abs(capNpv - cachedCapNPV) > 1.0e-11,
            $"failed to reproduce cached cap value:\n    calculated: {capNpv:G12}\n    expected:   {cachedCapNPV:G12}");

        // test Black floor price against cached value
        Assert.False(
            Math.Abs(floorNpv - cachedFloorNPV) > 1.0e-11,
            $"failed to reproduce cached floor value:\n    calculated: {floorNpv:G12}\n    expected:   {cachedFloorNPV:G12}");
    }

    // Adapted: as TestCachedValue; the optionlet prices (lines 614-615) are read with InstrumentResults.Result.
    [Fact]
    public void TestCachedValueFromOptionLets()
    {
        // Testing Black cap/floor price as a sum of optionlets prices against cached values...

        Date cachedToday = new(14, March, 2002), cachedSettlement = new(18, March, 2002);
        YieldTermStructure baseCurve = new FlatForward(cachedSettlement, 0.05, new Actual360());
        var vars = new CommonVars(cachedToday, baseCurve);
        Date startDate = vars.TermStructure.ReferenceDate;
        Leg leg = vars.MakeLeg(startDate, 20);

        CapFloor cap = vars.MakeCapFloor(CapFloorType.Cap, leg, 0.07);
        CapFloor floor = vars.MakeCapFloor(CapFloorType.Floor, leg, 0.03);
        Real calculatedCapletsNPV = 0.0, calculatedFloorletsNPV = 0.0;

        Real cachedCapNPV, cachedFloorNPV;
        if (vars.Context.UseAtParCoupons)
        {
            cachedCapNPV = 6.87570026732;
            cachedFloorNPV = 2.65812927959;
        }
        else
        {
            cachedCapNPV = 6.87630307745;
            cachedFloorNPV = 2.65796764715;
        }

        // test Black floor price against cached value
        ImmutableArray<Real> capletPrices = vars.Price(cap, 0.20).Result<ImmutableArray<Real>>("optionletsPrice");
        ImmutableArray<Real> floorletPrices = vars.Price(floor, 0.20).Result<ImmutableArray<Real>>("optionletsPrice");

        Assert.False(
            capletPrices.Length != 40,
            $"failed to produce prices for all caplets:\n    calculated: {capletPrices.Length} caplet prices\n    expected:   40");

        foreach (Real capletPrice in capletPrices)
        {
            calculatedCapletsNPV += capletPrice;
        }

        foreach (Real floorletPrice in floorletPrices)
        {
            calculatedFloorletsNPV += floorletPrice;
        }

        Assert.False(
            Math.Abs(calculatedCapletsNPV - cachedCapNPV) > 1.0e-11,
            "failed to reproduce cached cap value from its caplets' values:\n"
            + $"    calculated: {calculatedCapletsNPV:G12}\n    expected:   {cachedCapNPV:G12}");

        // test Black floor price against cached value
        Assert.False(
            Math.Abs(calculatedFloorletsNPV - cachedFloorNPV) > 1.0e-11,
            "failed to reproduce cached floor value from its floorlets' values:\n"
            + $"    calculated: {calculatedFloorletsNPV:G12}\n    expected:   {cachedFloorNPV:G12}");
    }

    // Adapted: the spread quote set to eps and to -eps (lines 680 and 689), on which the spreaded curve, the index, its
    // leg and the instruments price, is a spreaded curve, an index, a leg and an instrument built per spread, the leg on
    // the same schedule (OptionletDeltas); the engine is a static call; the per-optionlet results are read with
    // InstrumentResults.Result, the forwards as nullable reals, each of which a coupon paid on or after the evaluation
    // date has; and the coupons' nominal() is Nominal(context).
    [Fact]
    public void TestOptionLetsDelta()
    {
        // Testing Black caplet/floorlet delta coefficients against finite difference values...

        OptionletDeltas((vars, c) => vars.Price(c, 0.20));
    }

    // Adapted: as TestOptionLetsDelta, on BachelierCapFloorEngine.Calculate's static call at 1% normal volatility.
    [Fact]
    public void TestBachelierOptionLetsDelta()
    {
        // Testing Bachelier caplet/floorlet delta coefficients against finite difference values...

        // Use normal model (BachelierCapFloorEngine)
        OptionletDeltas((vars, c) => BachelierCapFloorEngine.Calculate(c, vars.Context, vars.TermStructure, 0.01));
    }

    private static bool CheckAbsError(Real x1, Real x2, Real tolerance) => Math.Abs(x1 - x2) < tolerance;

    private static string TypeToString(CapFloorType type) => type switch
    {
        CapFloorType.Cap => "cap",
        CapFloorType.Floor => "floor",
        CapFloorType.Collar => "collar",
        _ => throw new NanoQLException("unknown cap/floor type"),
    };

    // std::adjacent_find with a predicate: the first n with p(values[n], values[n + 1]), or the count when there is none.
    private static int AdjacentFind(List<Real> values, Func<Real, Real, bool> p)
    {
        for (int n = 0; n + 1 < values.Count; ++n)
        {
            if (p(values[n], values[n + 1]))
            {
                return n;
            }
        }

        return values.Count;
    }

    // testOptionLetsDelta's and testBachelierOptionLetsDelta's body (lines 647-764 and 766-886), on the given pricing
    // of an instrument; QuantLib's cases differ only in the engine.
    private static void OptionletDeltas(Func<CommonVars, CapFloor, InstrumentResults> price)
    {
        Date cachedToday = new(14, March, 2002), cachedSettlement = new(18, March, 2002);
        YieldTermStructure baseCurve = new FlatForward(cachedSettlement, 0.05, new Actual360());

        // Define spreaded curve with eps as spread used for FD sensitivities
        Real eps = 1.0e-6;

        (CommonVars Vars, CapFloor Cap, CapFloor Floor) Spreaded(Real spread)
        {
            YieldTermStructure spreadCurve = new ZeroSpreadedTermStructure(baseCurve, spread, Compounding.Continuous);
            var vars = new CommonVars(cachedToday, spreadCurve);
            Date startDate = vars.TermStructure.ReferenceDate;
            Leg leg = vars.MakeLeg(startDate, 20);
            return (vars, vars.MakeCapFloor(CapFloorType.Cap, leg, 0.05), vars.MakeCapFloor(CapFloorType.Floor, leg, 0.05));
        }

        (CommonVars atVars, CapFloor cap, CapFloor floor) = Spreaded(0.0);

        //so far tests pass, now try to get additional results and it will fail
        Size capletsNum = cap.CapRates.Length;
        var capletFDDelta = new Real[capletsNum];
        Size floorletNum = floor.FloorRates.Length;
        var floorletFDDelta = new Real[floorletNum];

        ImmutableArray<Real> capletAnalyticDelta = price(atVars, cap).Result<ImmutableArray<Real>>("optionletsDelta");
        ImmutableArray<Real> floorletAnalyticDelta = price(atVars, floor).Result<ImmutableArray<Real>>("optionletsDelta");

        (CommonVars upVars, CapFloor upCap, CapFloor upFloor) = Spreaded(eps);
        InstrumentResults capUp = price(upVars, upCap), floorUp = price(upVars, upFloor);
        ImmutableArray<Real> capletUpPrices = capUp.Result<ImmutableArray<Real>>("optionletsPrice");
        ImmutableArray<Real> floorletUpPrices = floorUp.Result<ImmutableArray<Real>>("optionletsPrice");
        ImmutableArray<Real> capletDiscountFactorsUp = capUp.Result<ImmutableArray<Real>>("optionletsDiscountFactor");
        ImmutableArray<Real> floorletDiscountFactorsUp = floorUp.Result<ImmutableArray<Real>>("optionletsDiscountFactor");
        ImmutableArray<Real?> capletForwardsUp = capUp.Result<ImmutableArray<Real?>>("optionletsAtmForward");
        ImmutableArray<Real?> floorletForwardsUp = floorUp.Result<ImmutableArray<Real?>>("optionletsAtmForward");

        (CommonVars downVars, CapFloor downCap, CapFloor downFloor) = Spreaded(-eps);
        InstrumentResults capDown = price(downVars, downCap), floorDown = price(downVars, downFloor);
        ImmutableArray<Real> capletDownPrices = capDown.Result<ImmutableArray<Real>>("optionletsPrice");
        ImmutableArray<Real> floorletDownPrices = floorDown.Result<ImmutableArray<Real>>("optionletsPrice");
        ImmutableArray<Real> capletDiscountFactorsDown = capDown.Result<ImmutableArray<Real>>("optionletsDiscountFactor");
        ImmutableArray<Real> floorletDiscountFactorsDown = floorDown.Result<ImmutableArray<Real>>("optionletsDiscountFactor");
        ImmutableArray<Real?> capletForwardsDown = capDown.Result<ImmutableArray<Real?>>("optionletsAtmForward");
        ImmutableArray<Real?> floorletForwardsDown = floorDown.Result<ImmutableArray<Real?>>("optionletsAtmForward");

        Real accrualFactor;
        Leg capLeg = cap.FloatingLeg;
        Leg floorLeg = floor.FloatingLeg;

        for (Size n = 1; n < capletUpPrices.Length; n++)
        {
            // calculating only caplet's FD sensitivity w.r.t. forward rate
            // without the effect of sensitivity related to changed discount factor
            var c = (FloatingRateCoupon)capLeg[n];
            accrualFactor = c.Nominal(atVars.Context) * c.AccrualPeriod() * c.Gearing;
            capletFDDelta[n] = ((capletUpPrices[n] / capletDiscountFactorsUp[n])
                                - (capletDownPrices[n] / capletDiscountFactorsDown[n]))
                               / (capletForwardsUp[n]!.Value - capletForwardsDown[n]!.Value)
                               / accrualFactor;
        }

        for (Size n = 0; n < floorletUpPrices.Length; n++)
        {
            // calculating only caplet's FD sensitivity w.r.t. forward rate
            // without the effect of sensitivity related to changed discount factor
            var c = (FloatingRateCoupon)floorLeg[n];
            accrualFactor = c.Nominal(atVars.Context) * c.AccrualPeriod() * c.Gearing;
            floorletFDDelta[n] = ((floorletUpPrices[n] / floorletDiscountFactorsUp[n])
                                  - (floorletDownPrices[n] / floorletDiscountFactorsDown[n]))
                                 / (floorletForwardsUp[n]!.Value - floorletForwardsDown[n]!.Value)
                                 / accrualFactor;
        }

        var failures = new List<string>();
        for (Size n = 0; n < capletAnalyticDelta.Length; n++)
        {
            if (Math.Abs(capletAnalyticDelta[n] - capletFDDelta[n]) > 1.0e-6)
            {
                failures.Add("failed to compare analytical and finite difference caplet delta:\n"
                             + $"caplet number:\t{n}\n"
                             + $"    finite difference: {capletFDDelta[n]:G12}\n"
                             + $"    analytical value:   {capletAnalyticDelta[n]:G12}\n"
                             + $"    resulting ratio: {capletFDDelta[n] / capletAnalyticDelta[n]:G12}");
            }
        }

        for (Size n = 0; n < floorletAnalyticDelta.Length; n++)
        {
            if (Math.Abs(floorletAnalyticDelta[n] - floorletFDDelta[n]) > 1.0e-6)
            {
                failures.Add("failed to compare analytical and finite difference floorlet delta:\n"
                             + $"floorlet number:\t{n}\n"
                             + $"    finite difference: {floorletFDDelta[n]:G12}\n"
                             + $"    analytical value:   {floorletAnalyticDelta[n]:G12}\n"
                             + $"    resulting ratio: {floorletFDDelta[n] / floorletAnalyticDelta[n]:G12}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private sealed class CommonVars
    {
        // setup: on the test runner's evaluation date and its flat 5% curve, or on the date and curve a case sets.
        public CommonVars(Date? today = null, YieldTermStructure? curve = null)
        {
            Nominals = [100];
            Frequency = Frequency.Semiannual;
            var conventions = new Euribor6M();
            Calendar = conventions.FixingCalendar;
            Convention = BusinessDayConvention.ModifiedFollowing;
            Today = today ?? new Date(16, September, 2015);
            Context = new EvaluationContext { EvaluationDate = Today };
            Natural settlementDays = 2;
            FixingDays = 2;
            Settlement = Calendar.Advance(Today, settlementDays, TimeUnit.Days);
            TermStructure = curve ?? new FlatForward(Settlement, 0.05, new ActualActual(ActualActualConvention.ISDA));
            Index = new Euribor6M(TermStructure);
        }

        // common data
        public Date Today { get; }

        public Date Settlement { get; }

        public ImmutableArray<Real> Nominals { get; }

        public BusinessDayConvention Convention { get; }

        public Frequency Frequency { get; }

        public IborIndex Index { get; }

        public Calendar Calendar { get; }

        public Natural FixingDays { get; }

        public YieldTermStructure TermStructure { get; }

        public EvaluationContext Context { get; }

        // utilities
        public Leg MakeLeg(Date startDate, Integer length)
        {
            Date endDate = Calendar.Advance(startDate, length, TimeUnit.Years, Convention);
            var schedule = new Schedule(startDate, endDate, new Period(Frequency), Calendar,
                                        Convention, Convention,
                                        DateGenerationRule.Forward, false, Context);
            return new IborLeg(schedule, Index)
                .WithNotionals(Nominals)
                .WithPaymentDayCounter(Index.DayCounter)
                .WithPaymentAdjustment(Convention)
                .WithFixingDays(FixingDays)
                .ToLeg(Context);
        }

        // makeCapFloor's instrument (lines 102-126); its engine, makeEngine's, is Price's static call.
        public CapFloor MakeCapFloor(CapFloorType type, Leg leg, Rate strike) => type switch
        {
            CapFloorType.Cap => new Cap(leg, [strike]),
            CapFloorType.Floor => new Floor(leg, [strike]),
            _ => throw new NanoQLException("unknown cap/floor type"),
        };

        // makeEngine (lines 90-94): BlackCapFloorEngine on the curve and a volatility.
        public InstrumentResults Price(CapFloor capFloor, Volatility volatility) =>
            BlackCapFloorEngine.Calculate(capFloor, Context, TermStructure, volatility);

        public Real Npv(CapFloor capFloor, Volatility volatility) => Price(capFloor, volatility).NPV!.Value;
    }
}
