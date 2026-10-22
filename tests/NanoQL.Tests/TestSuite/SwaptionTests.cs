// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2020 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/swaption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Replaced: testBlackEngineCaching (Vectors/Instruments/SwaptionVectorTests.cs)

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class SwaptionTests
{
    private static readonly Period[] Exercises =
    [
        new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(3, TimeUnit.Years),
        new(5, TimeUnit.Years), new(7, TimeUnit.Years), new(10, TimeUnit.Years),
    ];

    private static readonly Period[] Lengths =
    [
        new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(3, TimeUnit.Years),
        new(5, TimeUnit.Years), new(7, TimeUnit.Years), new(10, TimeUnit.Years),
        new(15, TimeUnit.Years), new(20, TimeUnit.Years),
    ];

    private static readonly SwapType[] Type = [SwapType.Receiver, SwapType.Payer];

    [Fact]
    public void TestStrikeDependency()
    {
        // Testing swaption dependency on strike...

        var vars = new CommonVars();

        Rate[] strikes = [0.03, 0.04, 0.05, 0.06, 0.07];

        foreach (Period exercise in Exercises)
        {
            foreach (Period length in Lengths)
            {
                foreach (SwapType k in Type)
                {
                    Date exerciseDate = vars.Calendar.Advance(vars.Today, exercise);
                    Date startDate = vars.Calendar.Advance(exerciseDate, vars.SettlementDays, TimeUnit.Days);

                    // store the results for different rates...
                    var values = new List<Real>();
                    var valuesCash = new List<Real>();
                    Volatility vol = 0.20;
                    foreach (Rate strike in strikes)
                    {
                        VanillaSwap swap = new MakeVanillaSwap(length, vars.Index)
                            .WithFixedRate(strike)
                            .WithEffectiveDate(startDate)
                            .WithFixedLegTenor(new Period(1, TimeUnit.Years))
                            .WithFixedLegDayCount(vars.FixedDayCount)
                            .WithFloatingLegSpread(0.0)
                            .WithType(k)
                            .ToVanillaSwap(vars.Context);
                        Swaption swaption = vars.MakeSwaption(swap, exerciseDate);
                        values.Add(vars.Npv(swaption, vol));
                        Swaption swaptionCash = vars.MakeSwaption(
                            swap, exerciseDate, SettlementType.Cash, SettlementMethod.ParYieldCurve);
                        valuesCash.Add(vars.Npv(swaptionCash, vol));
                    }

                    // and check that they go the right way
                    if (k == SwapType.Payer)
                    {
                        int n = AdjacentFind(values, (a, b) => a < b);
                        Assert.True(
                            n == values.Count,
                            $"NPV of Payer swaption with delivery settlement is increasing with the strike: option "
                            + $"tenor {exercise}, option date {exerciseDate}, volatility {vol}, swap tenor {length}, "
                            + $"value {Show(values, n)} at strike {Show(strikes, n)}");
                        int nCash = AdjacentFind(valuesCash, (a, b) => a < b);
                        Assert.True(
                            nCash == valuesCash.Count,
                            $"NPV of Payer swaption with cash settlement is increasing with the strike: option tenor "
                            + $"{exercise}, option date {exerciseDate}, volatility {vol}, swap tenor {length}, value "
                            + $"{Show(valuesCash, nCash)} at strike {Show(strikes, nCash)}");
                    }
                    else
                    {
                        int n = AdjacentFind(values, (a, b) => a > b);
                        Assert.True(
                            n == values.Count,
                            $"NPV of Receiver swaption with delivery settlement is increasing with the strike: option "
                            + $"tenor {exercise}, option date {exerciseDate}, volatility {vol}, swap tenor {length}, "
                            + $"value {Show(values, n)} at strike {Show(strikes, n)}");
                        int nCash = AdjacentFind(valuesCash, (a, b) => a > b);
                        Assert.True(
                            nCash == valuesCash.Count,
                            $"NPV of Receiver swaption with cash settlement is increasing with the strike: option "
                            + $"tenor {exercise}, option date {exerciseDate}, volatility {vol}, swap tenor {length}, "
                            + $"value {Show(valuesCash, nCash)} at strike {Show(strikes, nCash)}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestSpreadDependency()
    {
        // Testing swaption dependency on spread...

        var vars = new CommonVars();

        Spread[] spreads = [-0.002, -0.001, 0.0, 0.001, 0.002];

        foreach (Period exercise in Exercises)
        {
            foreach (Period length in Lengths)
            {
                foreach (SwapType k in Type)
                {
                    Date exerciseDate = vars.Calendar.Advance(vars.Today, exercise);
                    Date startDate = vars.Calendar.Advance(exerciseDate, vars.SettlementDays, TimeUnit.Days);

                    // store the results for different rates...
                    var values = new List<Real>();
                    var valuesCash = new List<Real>();
                    foreach (Spread spread in spreads)
                    {
                        VanillaSwap swap = new MakeVanillaSwap(length, vars.Index)
                            .WithFixedRate(0.06)
                            .WithFixedLegTenor(new Period(1, TimeUnit.Years))
                            .WithFixedLegDayCount(vars.FixedDayCount)
                            .WithEffectiveDate(startDate)
                            .WithFloatingLegSpread(spread)
                            .WithType(k)
                            .ToVanillaSwap(vars.Context);
                        Swaption swaption = vars.MakeSwaption(swap, exerciseDate);
                        values.Add(vars.Npv(swaption, 0.20));
                        Swaption swaptionCash = vars.MakeSwaption(
                            swap, exerciseDate, SettlementType.Cash, SettlementMethod.ParYieldCurve);
                        valuesCash.Add(vars.Npv(swaptionCash, 0.20));
                    }

                    // and check that they go the right way
                    if (k == SwapType.Payer)
                    {
                        int n = AdjacentFind(values, (a, b) => a > b);
                        Assert.True(
                            n == values.Count,
                            $"NPV is decreasing with the spread in a payer swaption (physical delivered): exercise "
                            + $"date {exerciseDate}, length {length}, value {Show(values, n)} for spread "
                            + $"{Show(spreads, n)}");
                        int nCash = AdjacentFind(valuesCash, (a, b) => a > b);
                        Assert.True(
                            nCash == valuesCash.Count,
                            $"NPV is decreasing with the spread in a payer swaption (cash delivered): exercise date "
                            + $"{exerciseDate}, length {length}, value {Show(valuesCash, nCash)} for spread "
                            + $"{Show(spreads, nCash)}");
                    }
                    else
                    {
                        int n = AdjacentFind(values, (a, b) => a < b);
                        Assert.True(
                            n == values.Count,
                            $"NPV is increasing with the spread in a receiver swaption (physical delivered): exercise "
                            + $"date {exerciseDate}, length {length}, value {Show(values, n)} for spread "
                            + $"{Show(spreads, n)}");
                        int nCash = AdjacentFind(valuesCash, (a, b) => a < b);
                        Assert.True(
                            nCash == valuesCash.Count,
                            $"NPV is increasing with the spread in a receiver swaption (cash delivered): exercise date "
                            + $"{exerciseDate}, length {length}, value {Show(valuesCash, nCash)} for spread "
                            + $"{Show(spreads, nCash)}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestSpreadTreatment()
    {
        // Testing swaption treatment of spread...

        var vars = new CommonVars();

        Spread[] spreads = [-0.002, -0.001, 0.0, 0.001, 0.002];

        foreach (Period exercise in Exercises)
        {
            foreach (Period length in Lengths)
            {
                foreach (SwapType k in Type)
                {
                    Date exerciseDate = vars.Calendar.Advance(vars.Today, exercise);
                    Date startDate = vars.Calendar.Advance(exerciseDate, vars.SettlementDays, TimeUnit.Days);
                    foreach (Spread spread in spreads)
                    {
                        VanillaSwap swap = new MakeVanillaSwap(length, vars.Index)
                            .WithFixedRate(0.06)
                            .WithFixedLegTenor(new Period(1, TimeUnit.Years))
                            .WithFixedLegDayCount(vars.FixedDayCount)
                            .WithEffectiveDate(startDate)
                            .WithFloatingLegSpread(spread)
                            .WithType(k)
                            .ToVanillaSwap(vars.Context);

                        // MakeVanillaSwap's default engine: the index's curve, without the settlement date's flows
                        FixedVsFloatingSwapResults swapResults = DiscountingSwapEngine.Calculate(
                            swap, vars.Context, vars.TermStructure, includeSettlementDateFlows: false);
                        Spread correction = spread * swapResults.FloatingLegBPS!.Value / swapResults.FixedLegBPS!.Value;
                        VanillaSwap equivalentSwap = new MakeVanillaSwap(length, vars.Index)
                            .WithFixedRate(0.06 + correction)
                            .WithFixedLegTenor(new Period(1, TimeUnit.Years))
                            .WithFixedLegDayCount(vars.FixedDayCount)
                            .WithEffectiveDate(startDate)
                            .WithFloatingLegSpread(0.0)
                            .WithType(k)
                            .ToVanillaSwap(vars.Context);
                        Swaption swaption1 = vars.MakeSwaption(swap, exerciseDate);
                        Swaption swaption2 = vars.MakeSwaption(equivalentSwap, exerciseDate);
                        Swaption swaption1Cash = vars.MakeSwaption(
                            swap, exerciseDate, SettlementType.Cash, SettlementMethod.ParYieldCurve);
                        Swaption swaption2Cash = vars.MakeSwaption(
                            equivalentSwap, exerciseDate, SettlementType.Cash, SettlementMethod.ParYieldCurve);
                        Real npv1 = vars.Npv(swaption1, 0.20), npv2 = vars.Npv(swaption2, 0.20);
                        Assert.True(
                            Math.Abs(npv1 - npv2) <= 1.0e-6,
                            $"wrong spread treatment: exercise {exerciseDate}, length {length}, type {k}, spread "
                            + $"{spread}, original swaption value {npv1}, equivalent swaption value {npv2}");

                        Real npv1Cash = vars.Npv(swaption1Cash, 0.20), npv2Cash = vars.Npv(swaption2Cash, 0.20);
                        Assert.True(
                            Math.Abs(npv1Cash - npv2Cash) <= 1.0e-6,
                            $"wrong spread treatment: exercise date {exerciseDate}, length {length}, pay "
                            + $"{(k == SwapType.Payer ? "fixed" : "floating")}, spread {spread}, value of original "
                            + $"swaption {npv1Cash}, value of equivalent swaption {npv2Cash}");
                    }
                }
            }
        }
    }

    // Adapted: the evaluation date and settlement date set after the fixture read today's date, and the curve
    // relinked under the index (lines 424-427), become the fixture built on 13 March 2002, its indexes on the curve.
    [Fact]
    public void TestCachedValue()
    {
        // Testing swaption values against cached values...

        var vars = new CommonVars(new Date(13, March, 2002), new Date(15, March, 2002));
        bool usingAtParCoupons = vars.Context.UseAtParCoupons;

        Date exerciseDate = vars.Calendar.Advance(vars.Settlement, new Period(5, TimeUnit.Years));
        Date startDate = vars.Calendar.Advance(exerciseDate, vars.SettlementDays, TimeUnit.Days);
        VanillaSwap swap = new MakeVanillaSwap(new Period(10, TimeUnit.Years), vars.Index)
            .WithFixedRate(0.06)
            .WithEffectiveDate(startDate)
            .WithFixedLegTenor(new Period(1, TimeUnit.Years))
            .WithFixedLegDayCount(vars.FixedDayCount)
            .ToVanillaSwap(vars.Context);

        Swaption swaption = vars.MakeSwaption(swap, exerciseDate);

        Real cachedNPV = usingAtParCoupons ? 0.036418158579 : 0.036421429684;

        Real npv = vars.Npv(swaption, 0.20);
        Assert.True(
            Math.Abs(npv - cachedNPV) <= 1.0e-12,
            $"failed to reproduce cached swaption value: calculated {npv:F12}, expected {cachedNPV:F12}");

        OvernightIndexedSwap oiswap = new MakeOIS(new Period(10, TimeUnit.Years), vars.OisIndex)
            .WithFixedRate(0.06)
            .WithEffectiveDate(startDate)
            .WithPaymentFrequency(Frequency.Annual)
            .WithFixedLegDayCount(vars.FixedDayCount)
            .ToOvernightIndexedSwap(vars.Context);

        Swaption oiswaption = vars.MakeOISwaption(oiswap, exerciseDate);

        cachedNPV = 0.014101075767;

        Real oisNpv = vars.Npv(oiswaption, 0.20);
        Assert.True(
            Math.Abs(oisNpv - cachedNPV) <= 1.0e-12,
            $"failed to reproduce cached overnight-indexed swaption value: calculated {oisNpv:F12}, expected "
            + $"{cachedNPV:F12}");
    }

    [Fact]
    public void TestVega()
    {
        // Testing swaption vega...

        var vars = new CommonVars();

        SettlementType[] types = [SettlementType.Physical, SettlementType.Cash];
        SettlementMethod[] methods = [SettlementMethod.PhysicalOTC, SettlementMethod.ParYieldCurve];
        Rate[] strikes = [0.03, 0.04, 0.05, 0.06, 0.07];
        Volatility[] vols = [0.01, 0.20, 0.30, 0.70, 0.90];
        Volatility shift = 1e-8;
        foreach (Period exercise in Exercises)
        {
            Date exerciseDate = vars.Calendar.Advance(vars.Today, exercise);
            Date startDate = vars.Calendar.Advance(exerciseDate, new Period(vars.SettlementDays, TimeUnit.Days));
            foreach (Period length in Lengths)
            {
                foreach (Rate strike in strikes)
                {
                    for (int h = 0; h < Type.Length; h++)
                    {
                        VanillaSwap swap = new MakeVanillaSwap(length, vars.Index)
                            .WithFixedRate(strike)
                            .WithEffectiveDate(startDate)
                            .WithFixedLegTenor(new Period(1, TimeUnit.Years))
                            .WithFixedLegDayCount(vars.FixedDayCount)
                            .WithFloatingLegSpread(0.0)
                            .WithType(Type[h])
                            .ToVanillaSwap(vars.Context);
                        foreach (Volatility vol in vols)
                        {
                            Swaption swaption = vars.MakeSwaption(swap, exerciseDate, types[h], methods[h]);
                            InstrumentResults results = vars.Price(swaption, vol);
                            Real swaption1NPV = vars.Npv(swaption, vol - shift);
                            Real swaption2NPV = vars.Npv(swaption, vol + shift);

                            Real swaptionNPV = results.NPV!.Value;
                            Real numericalVegaPerPoint = (swaption2NPV - swaption1NPV) / (200.0 * shift);

                            // check only relevant vega
                            if (numericalVegaPerPoint / swaptionNPV > 1.0e-7)
                            {
                                Real analyticalVegaPerPoint = results.Result<Real>("vega") / 100.0;
                                Real discrepancy = Math.Abs(analyticalVegaPerPoint - numericalVegaPerPoint);
                                discrepancy /= numericalVegaPerPoint;
                                Real tolerance = 0.015;
                                Assert.True(
                                    discrepancy <= tolerance,
                                    $"failed to compute swaption vega: option tenor {exercise}, volatility {vol}, "
                                    + $"option type {swaption.Type}, swap tenor {length}, strike {strike}, settlement "
                                    + $"{SwaptionFunctions.ToString(types[h])}, nominal {swaption.Underlying.Nominal()}, "
                                    + $"npv {swaptionNPV}, calculated vega {analyticalVegaPerPoint}, expected vega "
                                    + $"{numericalVegaPerPoint}, discrepancy {discrepancy}, tolerance {tolerance}");
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TestCashSettledSwaptions()
    {
        // Testing cash settled swaptions modified annuity...

        var vars = new CommonVars();

        Rate strike = 0.05;

        foreach (Period exercise in Exercises)
        {
            foreach (Period length in Lengths)
            {
                Date exerciseDate = vars.Calendar.Advance(vars.Today, exercise);
                Date startDate = vars.Calendar.Advance(exerciseDate, vars.SettlementDays, TimeUnit.Days);
                Date maturity = vars.Calendar.Advance(startDate, length, vars.FloatingConvention);
                var floatSchedule = new Schedule(
                    startDate,
                    maturity,
                    vars.FloatingTenor,
                    vars.Calendar,
                    vars.FloatingConvention,
                    vars.FloatingConvention,
                    DateGenerationRule.Forward,
                    false,
                    vars.Context);

                // Swap with fixed leg conventions: Business Days = Unadjusted, DayCount = 30/360
                var fixedScheduleU = new Schedule(
                    startDate,
                    maturity,
                    new Period(vars.FixedFrequency),
                    vars.Calendar,
                    BusinessDayConvention.Unadjusted,
                    BusinessDayConvention.Unadjusted,
                    DateGenerationRule.Forward,
                    true,
                    vars.Context);
                var swapU360 = new VanillaSwap(
                    Type[0],
                    vars.Nominal,
                    fixedScheduleU,
                    strike,
                    new Thirty360(Thirty360Convention.BondBasis),
                    floatSchedule,
                    vars.Index,
                    0.0,
                    vars.Index.DayCounter,
                    vars.Context);

                // Swap with fixed leg conventions: Business Days = Unadjusted, DayCount = Act/365
                var swapU365 = new VanillaSwap(
                    Type[0],
                    vars.Nominal,
                    fixedScheduleU,
                    strike,
                    new Actual365Fixed(),
                    floatSchedule,
                    vars.Index,
                    0.0,
                    vars.Index.DayCounter,
                    vars.Context);

                // Swap with fixed leg conventions: Business Days = Modified Following, DayCount = 30/360
                var fixedScheduleA = new Schedule(
                    startDate,
                    maturity,
                    new Period(vars.FixedFrequency),
                    vars.Calendar,
                    BusinessDayConvention.ModifiedFollowing,
                    BusinessDayConvention.ModifiedFollowing,
                    DateGenerationRule.Forward,
                    true,
                    vars.Context);
                var swapA360 = new VanillaSwap(
                    Type[0],
                    vars.Nominal,
                    fixedScheduleA,
                    strike,
                    new Thirty360(Thirty360Convention.BondBasis),
                    floatSchedule,
                    vars.Index,
                    0.0,
                    vars.Index.DayCounter,
                    vars.Context);

                // Swap with fixed leg conventions: Business Days = Modified Following, DayCount = Act/365
                var swapA365 = new VanillaSwap(
                    Type[0],
                    vars.Nominal,
                    fixedScheduleA,
                    strike,
                    new Actual365Fixed(),
                    floatSchedule,
                    vars.Index,
                    0.0,
                    vars.Index.DayCounter,
                    vars.Context);

                FixedVsFloatingSwapResults resultsU360 =
                    DiscountingSwapEngine.Calculate(swapU360, vars.Context, vars.TermStructure);
                FixedVsFloatingSwapResults resultsA360 =
                    DiscountingSwapEngine.Calculate(swapA360, vars.Context, vars.TermStructure);
                FixedVsFloatingSwapResults resultsU365 =
                    DiscountingSwapEngine.Calculate(swapU365, vars.Context, vars.TermStructure);
                FixedVsFloatingSwapResults resultsA365 =
                    DiscountingSwapEngine.Calculate(swapA365, vars.Context, vars.TermStructure);

                // FlatForward curves
                var termStructureU360 = new FlatForward(
                    vars.Settlement,
                    resultsU360.FairRate!.Value,
                    new Thirty360(Thirty360Convention.BondBasis),
                    Compounding.Compounded,
                    vars.FixedFrequency);
                var termStructureA360 = new FlatForward(
                    vars.Settlement,
                    resultsA360.FairRate!.Value,
                    new Thirty360(Thirty360Convention.BondBasis),
                    Compounding.Compounded,
                    vars.FixedFrequency);
                var termStructureU365 = new FlatForward(
                    vars.Settlement,
                    resultsU365.FairRate!.Value,
                    new Actual365Fixed(),
                    Compounding.Compounded,
                    vars.FixedFrequency);
                var termStructureA365 = new FlatForward(
                    vars.Settlement,
                    resultsA365.FairRate!.Value,
                    new Actual365Fixed(),
                    Compounding.Compounded,
                    vars.FixedFrequency);

                // Annuity calculated by swap method fixedLegBPS().
                // Fixed leg conventions: Unadjusted, 30/360
                Real annuityU360 = resultsU360.FixedLegBPS!.Value / 0.0001;
                annuityU360 = swapU360.Type == SwapType.Payer ? -annuityU360 : annuityU360;

                // Fixed leg conventions: ModifiedFollowing, act/365
                Real annuityA365 = resultsA365.FixedLegBPS!.Value / 0.0001;
                annuityA365 = swapA365.Type == SwapType.Payer ? -annuityA365 : annuityA365;

                // Fixed leg conventions: ModifiedFollowing, 30/360
                Real annuityA360 = resultsA360.FixedLegBPS!.Value / 0.0001;
                annuityA360 = swapA360.Type == SwapType.Payer ? -annuityA360 : annuityA360;

                // Fixed leg conventions: Unadjusted, act/365
                Real annuityU365 = resultsU365.FixedLegBPS!.Value / 0.0001;
                annuityU365 = swapU365.Type == SwapType.Payer ? -annuityU365 : annuityU365;

                // Calculation of Modified Annuity (cash settlement)
                Real cashAnnuityU360 = CashAnnuity(swapU360, termStructureU360, strike, vars.Context);
                Real cashAnnuityU365 = CashAnnuity(swapU365, termStructureU365, strike, vars.Context);
                Real cashAnnuityA360 = CashAnnuity(swapA360, termStructureA360, strike, vars.Context);
                Real cashAnnuityA365 = CashAnnuity(swapA365, termStructureA365, strike, vars.Context);

                // Swaptions: underlying swap fixed leg conventions:
                // unadjusted, 30/360
                (Real npvRatioU360, Real annuityRatioU360) =
                    Ratios(vars, swapU360, exerciseDate, cashAnnuityU360, annuityU360);

                // modified following, act/365
                (Real npvRatioA365, Real annuityRatioA365) =
                    Ratios(vars, swapA365, exerciseDate, cashAnnuityA365, annuityA365);

                // modified following, 30/360
                (Real npvRatioA360, Real annuityRatioA360) =
                    Ratios(vars, swapA360, exerciseDate, cashAnnuityA360, annuityA360);

                // unadjusted, act/365
                (Real npvRatioU365, Real annuityRatioU365) =
                    Ratios(vars, swapU365, exerciseDate, cashAnnuityU365, annuityU365);

                CheckRatio(npvRatioU360, annuityRatioU360, "Unadjusted, 30/360", exercise, length);
                CheckRatio(npvRatioA365, annuityRatioA365, "Modified Following, act/365", exercise, length);
                CheckRatio(npvRatioA360, annuityRatioA360, "Unadjusted, 30/360", exercise, length);
                CheckRatio(npvRatioU365, annuityRatioU365, "Unadjusted, act/365", exercise, length);
            }
        }

        static Real CashAnnuity(VanillaSwap swap, YieldTermStructure termStructure, Rate strike, EvaluationContext context)
        {
            Real cashAnnuity = 0.0;
            foreach (CashFlow cf in swap.FixedLeg)
            {
                cashAnnuity += cf.Amount(context) / strike * termStructure.Discount(cf.Date);
            }

            return cashAnnuity;
        }

        // The physical and cash swaptions' values: the NPVs' ratio must be equal to the annuities' ratio.
        static (Real NpvRatio, Real AnnuityRatio) Ratios(
            CommonVars vars, VanillaSwap swap, Date exerciseDate, Real cashAnnuity, Real annuity)
        {
            // Physical settled swaption
            Real valueP = vars.Npv(vars.MakeSwaption(swap, exerciseDate), 0.20);

            // Cash settled swaption
            Real valueC = vars.Npv(
                vars.MakeSwaption(swap, exerciseDate, SettlementType.Cash, SettlementMethod.ParYieldCurve), 0.20);
            return (valueC / valueP, cashAnnuity / annuity);
        }

        static void CheckRatio(Real npvRatio, Real annuityRatio, string conventions, Period exercise, Period length) =>
            Assert.True(
                Math.Abs(annuityRatio - npvRatio) <= 1e-10,
                $"The npv's ratio must be equal to annuities ratio: swaption {exercise} x {length} (underlying swap "
                + $"fixed leg {conventions}), npv ratio {npvRatio}, annuity ratio {annuityRatio}, difference "
                + $"{annuityRatio - npvRatio}");
    }

    [Fact]
    public void TestImpliedVolatility()
    {
        // Testing implied volatility for swaptions...

        var vars = new CommonVars();

        Size maxEvaluations = 100;
        Real tolerance = 1.0e-08;

        SettlementType[] types = [SettlementType.Physical, SettlementType.Cash];
        SettlementMethod[] methods = [SettlementMethod.PhysicalOTC, SettlementMethod.ParYieldCurve];
        SwaptionPriceType[] priceTypes = [SwaptionPriceType.Spot, SwaptionPriceType.Forward];

        // test data
        Rate[] strikes = [0.02, 0.03, 0.04, 0.05, 0.06, 0.07];
        Volatility[] vols = [0.01, 0.05, 0.10, 0.20, 0.30, 0.70, 0.90];

        foreach (Period exercise in Exercises)
        {
            foreach (Period length in Lengths)
            {
                Date exerciseDate = vars.Calendar.Advance(vars.Today, exercise);
                Date startDate = vars.Calendar.Advance(exerciseDate, vars.SettlementDays, TimeUnit.Days);

                foreach (Rate strike in strikes)
                {
                    foreach (SwapType k in Type)
                    {
                        VanillaSwap swap = new MakeVanillaSwap(length, vars.Index)
                            .WithFixedRate(strike)
                            .WithEffectiveDate(startDate)
                            .WithFixedLegTenor(new Period(1, TimeUnit.Years))
                            .WithFixedLegDayCount(vars.FixedDayCount)
                            .WithFloatingLegSpread(0.0)
                            .WithType(k)
                            .ToVanillaSwap(vars.Context);
                        for (int h = 0; h < types.Length; h++)
                        {
                            foreach (SwaptionPriceType priceType in priceTypes)
                            {
                                foreach (Volatility vol in vols)
                                {
                                    Swaption swaption = vars.MakeSwaption(swap, exerciseDate, types[h], methods[h]);

                                    // Black price
                                    Real value = Value(
                                        vars.Price(swaption, vol, CashAnnuityModel.DiscountCurve), priceType);
                                    Volatility implVol = 0.0;
                                    try
                                    {
                                        implVol = swaption.ImpliedVolatility(
                                            value,
                                            vars.TermStructure,
                                            0.10,
                                            vars.Context,
                                            tolerance,
                                            maxEvaluations,
                                            1.0e-7,
                                            4.0,
                                            VolatilityType.ShiftedLognormal,
                                            0.0,
                                            priceType);
                                    }
                                    catch (Exception e) when (e is ArgumentException or NanoQLException)
                                    {
                                        // couldn't bracket?
                                        Real value0 = Value(
                                            vars.Price(swaption, 0.0, CashAnnuityModel.DiscountCurve), priceType);
                                        if (Math.Abs(value - value0) < tolerance)
                                        {
                                            // ok, just skip:
                                            continue;
                                        }

                                        // otherwise, report error
                                        Assert.Fail(
                                            $"implied vol failure: {exercise}x{length} {k}, settlement "
                                            + $"{SwaptionFunctions.ToString(types[h])}, strike {strike}, vol {vol}, "
                                            + $"price {value}, type {priceType}: {e.Message}");
                                    }

                                    if (Math.Abs(implVol - vol) > tolerance)
                                    {
                                        // the difference might not matter
                                        Real value2 = Value(
                                            vars.Price(swaption, implVol, CashAnnuityModel.DiscountCurve), priceType);
                                        Assert.True(
                                            Math.Abs(value - value2) <= tolerance,
                                            $"implied vol failure: {exercise}x{length} {k}, settlement "
                                            + $"{SwaptionFunctions.ToString(types[h])}, strike {strike}, vol {vol}, "
                                            + $"price {value}, type {priceType}, implied vol {implVol}, implied price "
                                            + $"{value2}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        static Real Value(InstrumentResults results, SwaptionPriceType priceType) =>
            priceType == SwaptionPriceType.Spot ? results.NPV!.Value : results.Result<Real>("forwardPrice");
    }

    [Fact]
    public void TestImpliedVolatilityOis()
    {
        // Testing implied volatility for overnight-indexed swaptions...

        var vars = new CommonVars();

        Size maxEvaluations = 100;
        Real tolerance = 1.0e-08;

        SettlementType[] types = [SettlementType.Physical, SettlementType.Cash];
        SettlementMethod[] methods = [SettlementMethod.PhysicalOTC, SettlementMethod.ParYieldCurve];
        SwaptionPriceType[] priceTypes = [SwaptionPriceType.Spot, SwaptionPriceType.Forward];

        // test data
        Rate[] strikes = [0.02, 0.03, 0.04, 0.05, 0.06, 0.07];
        Volatility[] vols = [0.01, 0.05, 0.10, 0.20, 0.30, 0.70, 0.90];

        foreach (Period exercise in Exercises)
        {
            foreach (Period length in Lengths)
            {
                Date exerciseDate = vars.Calendar.Advance(vars.Today, exercise);
                Date startDate = vars.Calendar.Advance(exerciseDate, vars.SettlementDays, TimeUnit.Days);

                foreach (Rate strike in strikes)
                {
                    foreach (SwapType k in Type)
                    {
                        OvernightIndexedSwap swap = new MakeOIS(length, vars.OisIndex)
                            .WithFixedRate(strike)
                            .WithEffectiveDate(startDate)
                            .WithPaymentFrequency(Frequency.Annual)
                            .WithFixedLegDayCount(vars.FixedDayCount)
                            .WithType(k)
                            .ToOvernightIndexedSwap(vars.Context);
                        for (int h = 0; h < types.Length; h++)
                        {
                            foreach (SwaptionPriceType priceType in priceTypes)
                            {
                                foreach (Volatility vol in vols)
                                {
                                    Swaption swaption = vars.MakeOISwaption(swap, exerciseDate, types[h], methods[h]);

                                    // Black price
                                    Real value = Value(
                                        vars.Price(swaption, vol, CashAnnuityModel.DiscountCurve), priceType);
                                    Volatility implVol = 0.0;
                                    try
                                    {
                                        implVol = swaption.ImpliedVolatility(
                                            value,
                                            vars.TermStructure,
                                            0.10,
                                            vars.Context,
                                            tolerance,
                                            maxEvaluations,
                                            1.0e-7,
                                            4.0,
                                            VolatilityType.ShiftedLognormal,
                                            0.0,
                                            priceType);
                                    }
                                    catch (Exception e) when (e is ArgumentException or NanoQLException)
                                    {
                                        // couldn't bracket?
                                        Real value0 = Value(
                                            vars.Price(swaption, 0.0, CashAnnuityModel.DiscountCurve), priceType);
                                        if (Math.Abs(value - value0) < tolerance)
                                        {
                                            // ok, just skip:
                                            continue;
                                        }

                                        // otherwise, report error
                                        Assert.Fail(
                                            $"implied vol failure: {exercise}x{length} {k}, settlement "
                                            + $"{SwaptionFunctions.ToString(types[h])}, strike {strike}, vol {vol}, "
                                            + $"price {value}, type {priceType}: {e.Message}");
                                    }

                                    if (Math.Abs(implVol - vol) > tolerance)
                                    {
                                        // the difference might not matter
                                        Real value2 = Value(
                                            vars.Price(swaption, implVol, CashAnnuityModel.DiscountCurve), priceType);
                                        Assert.True(
                                            Math.Abs(value - value2) <= tolerance,
                                            $"implied vol failure: {exercise}x{length} {k}, settlement "
                                            + $"{SwaptionFunctions.ToString(types[h])}, strike {strike}, vol {vol}, "
                                            + $"price {value}, type {priceType}, implied vol {implVol}, implied price "
                                            + $"{value2}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        static Real Value(InstrumentResults results, SwaptionPriceType priceType) =>
            priceType == SwaptionPriceType.Spot ? results.NPV!.Value : results.Result<Real>("forwardPrice");
    }

    // Adapted: the projection quote relinked to the bumped rate (lines 1083 and 1107), with the swap's fair rate, the
    // swaption's NPV and its delta read again, becomes a second curve, index, swap and swaption built on the bumped
    // rate.
    [Fact]
    public void TestSwaptionDeltaInBlackModel()
    {
        // Testing swaption delta in Black model...

        CheckSwaptionDelta(
            (swaption, context, discountCurve, volatility) =>
                BlackSwaptionEngine.Calculate(swaption, context, discountCurve, volatility),
            false);
    }

    // Adapted: as TestSwaptionDeltaInBlackModel, through the same helper (lines 1083 and 1107).
    [Fact]
    public void TestSwaptionDeltaInBachelierModel()
    {
        // Testing swaption delta in Bachelier model...

        CheckSwaptionDelta(
            (swaption, context, discountCurve, volatility) =>
                BachelierSwaptionEngine.Calculate(swaption, context, discountCurve, volatility),
            true);
    }

    [Fact]
    public void TestMakeSwaptionWithExerciseCalendar()
    {
        // Testing MakeSwaption with exercise calendar override...

        // Use a specific date where TARGET and US Settlement diverge:
        // 1Y advance from Oct 9, 2015 gives Oct 10, 2016 (TARGET)
        // vs Oct 11, 2016 (US), because Oct 10 is Columbus Day.
        var today = new Date(9, October, 2015);
        var context = new EvaluationContext { EvaluationDate = today };
        var termStructure = new FlatForward(today, 0.05, new Actual365Fixed());

        var swapIndex = new EuriborSwapIsdaFixA(new Period(5, TimeUnit.Years), termStructure);
        Calendar targetCalendar = swapIndex.FixingCalendar;
        Calendar usCalendar = new UnitedStates(UnitedStatesMarket.Settlement);

        // Default uses swap index's fixing calendar (TARGET)
        Swaption defaultSwaption =
            new MakeSwaption(swapIndex, new Period(1, TimeUnit.Years), 0.05).ToSwaption(context);
        Date defaultExercise = defaultSwaption.Exercise.Dates[0];

        Date expected = targetCalendar.Advance(
            targetCalendar.Adjust(today), new Period(1, TimeUnit.Years), BusinessDayConvention.ModifiedFollowing);
        Assert.Equal(expected, defaultExercise);

        // With custom calendar, exercise date differs
        Swaption customSwaption = new MakeSwaption(swapIndex, new Period(1, TimeUnit.Years), 0.05)
            .WithExerciseCalendar(usCalendar)
            .ToSwaption(context);
        Date customExercise = customSwaption.Exercise.Dates[0];

        Date expectedCustom = usCalendar.Advance(
            usCalendar.Adjust(today), new Period(1, TimeUnit.Years), BusinessDayConvention.ModifiedFollowing);
        Assert.Equal(expectedCustom, customExercise);
        Assert.NotEqual(defaultExercise, customExercise);

        // Explicit withExerciseDate takes precedence over calendar
        Date explicitDate = targetCalendar.Advance(today, new Period(6, TimeUnit.Months));
        Date fixingDate = targetCalendar.Advance(today, new Period(1, TimeUnit.Years));
        Swaption explicitSwaption = new MakeSwaption(swapIndex, fixingDate, 0.05)
            .WithExerciseCalendar(usCalendar)
            .WithExerciseDate(explicitDate)
            .ToSwaption(context);
        Assert.Equal(explicitDate, explicitSwaption.Exercise.Dates[0]);
    }

    [Fact]
    public void TestMakeSwaptionFixingDateNominal()
    {
        // Testing MakeSwaption nominal with fixing-date-based constructor...

        var today = new Date(9, October, 2015);
        var context = new EvaluationContext { EvaluationDate = today };
        var termStructure = new FlatForward(today, 0.05, new Actual365Fixed());

        var swapIndex = new EuriborSwapIsdaFixA(new Period(5, TimeUnit.Years), termStructure);
        Date fixingDate = swapIndex.FixingCalendar.Advance(today, new Period(1, TimeUnit.Years));

        // The fixingDate-based constructor should default the nominal to 1.0,
        // just like the optionTenor-based constructor does.
        Swaption swaption = new MakeSwaption(swapIndex, fixingDate, 0.05).ToSwaption(context);
        Assert.Equal(1.0, swaption.Underlying.Nominal());

        // withNominal should still override the default.
        Swaption customNominalSwaption =
            new MakeSwaption(swapIndex, fixingDate, 0.05).WithNominal(1234.0).ToSwaption(context);
        Assert.Equal(1234.0, customNominalSwaption.Underlying.Nominal());
    }

    // QuantLib's checkSwaptionDelta, on an engine taking a constant volatility (makeConstVolEngine).
    private static void CheckSwaptionDelta(
        Func<Swaption, EvaluationContext, YieldTermStructure, Volatility, InstrumentResults> engine,
        bool useBachelierVol)
    {
        var vars = new CommonVars();
        Date today = vars.Today;
        Calendar calendar = vars.Calendar;

        const Real bump = 1.0e-4;
        const Real epsilon = 1.0e-10;

        const Real projectionRate = 0.01;

        var projectionCurve = new FlatForward(today, projectionRate, new Actual365Fixed());
        var bumpedProjectionCurve = new FlatForward(today, projectionRate + bump, new Actual365Fixed());

        var discountHandle = new FlatForward(today, 0.0085, new Actual365Fixed());

        var idx = new Euribor6M(projectionCurve);
        var bumpedIdx = new Euribor6M(bumpedProjectionCurve);

        SettlementType[] types = [SettlementType.Physical, SettlementType.Cash];
        SettlementMethod[] methods = [SettlementMethod.PhysicalOTC, SettlementMethod.CollateralizedCashPrice];

        Rate[] strikes = [0.03, 0.04, 0.05, 0.06, 0.07];
        Volatility[] vols = [0.0, 0.10, 0.20, 0.30, 0.70, 0.90];

        foreach (Volatility vol in vols)
        {
            foreach (Period exercise in Exercises)
            {
                foreach (Period length in Lengths)
                {
                    foreach (Rate strike in strikes)
                    {
                        for (int h = 0; h < Type.Length; h++)
                        {
                            Volatility volatility = useBachelierVol ? vol / 100.0 : vol;

                            Date exerciseDate = calendar.Advance(today, exercise);
                            Date startDate = calendar.Advance(exerciseDate, new Period(2, TimeUnit.Days));

                            VanillaSwap underlying = Underlying(idx);
                            Real fairRate = DiscountingSwapEngine.Calculate(underlying, vars.Context, discountHandle)
                                .FairRate!.Value;

                            var swaption = new Swaption(
                                underlying, new EuropeanExercise(exerciseDate), types[h], methods[h]);
                            InstrumentResults results = engine(swaption, vars.Context, discountHandle, volatility);

                            Real value = results.NPV!.Value;
                            Real delta = results.Result<Real>("delta") * bump;

                            VanillaSwap bumpedUnderlying = Underlying(bumpedIdx);
                            Real bumpedFairRate =
                                DiscountingSwapEngine.Calculate(bumpedUnderlying, vars.Context, discountHandle)
                                    .FairRate!.Value;
                            var bumpedSwaption = new Swaption(
                                bumpedUnderlying, new EuropeanExercise(exerciseDate), types[h], methods[h]);
                            InstrumentResults bumpedResults =
                                engine(bumpedSwaption, vars.Context, discountHandle, volatility);
                            Real bumpedValue = bumpedResults.NPV!.Value;
                            Real bumpedDelta = bumpedResults.Result<Real>("delta") * bump;

                            Real deltaBump = bumpedFairRate - fairRate;
                            Real approxDelta = (bumpedValue - value) / deltaBump * bump;

                            Real lowerBound = (bumpedDelta < delta ? bumpedDelta : delta) - epsilon;
                            Real upperBound = (delta < bumpedDelta ? bumpedDelta : delta) + epsilon;

                            /* Based on the Mean Value Theorem, the below inequality
                               should hold for any function that is monotonic in the
                               area of the bump.
                            */
                            bool checkIsCorrect = (lowerBound < approxDelta) && (approxDelta < upperBound);

                            Assert.True(
                                checkIsCorrect,
                                $"failed to compute swaption delta: option tenor {exerciseDate}, volatility "
                                + $"{volatility}, option type {swaption.Type}, swap tenor {length}, strike {strike}, "
                                + $"settlement {SwaptionFunctions.ToString(types[h])}, method {methods[h]}, nominal "
                                + $"{swaption.Underlying.Nominal()}, npv {value}, calculated delta {delta}, expected "
                                + $"delta {approxDelta}");

                            VanillaSwap Underlying(IborIndex index) =>
                                new MakeVanillaSwap(length, index)
                                    .WithFixedRate(strike)
                                    .WithEffectiveDate(startDate)
                                    .WithFixedLegTenor(new Period(1, TimeUnit.Years))
                                    .WithFixedLegDayCount(new Thirty360(Thirty360Convention.BondBasis))
                                    .WithFloatingLegSpread(0.0)
                                    .WithType(Type[h])
                                    .ToVanillaSwap(vars.Context);
                        }
                    }
                }
            }
        }
    }

    // std::adjacent_find with a predicate: the first n with pred(values[n], values[n + 1]), or the count when none.
    private static int AdjacentFind(List<Real> values, Func<Real, Real, bool> pred)
    {
        for (int n = 0; n + 1 < values.Count; ++n)
        {
            if (pred(values[n], values[n + 1]))
            {
                return n;
            }
        }

        return values.Count;
    }

    private static string Show(IReadOnlyList<Real> values, int n) =>
        n + 1 < values.Count ? $"{values[n]} then {values[n + 1]}" : string.Empty;

    // The fixture, with its overnight index (Eonia on a zero-spreaded curve) and makeOISwaption. QuantLib's makeSwaption
    // sets a Black engine on a volatility quote with the SwapRate model; nanoql's swaption holds no engine, so the cases
    // call it (Price).
    private sealed class CommonVars
    {
        // The fixture on today's date, or on the given dates, as the cases that set them after the fixture.
        public CommonVars(Date? today = null, Date? settlement = null)
        {
            SettlementDays = 2;
            Nominal = 1000000.0;
            FixedConvention = BusinessDayConvention.Unadjusted;
            FixedFrequency = Frequency.Annual;
            FixedDayCount = new Thirty360(Thirty360Convention.BondBasis);

            var conventions = new Euribor6M();
            FloatingConvention = conventions.BusinessDayConvention;
            FloatingTenor = conventions.Tenor;
            Calendar = conventions.FixingCalendar;
            Today = today ?? Calendar.Adjust(Date.TodaysDate());
            Context = new EvaluationContext { EvaluationDate = Today };
            Settlement = settlement ?? Calendar.Advance(Today, SettlementDays, TimeUnit.Days);
            TermStructure = new FlatForward(Settlement, 0.05, new Actual365Fixed());
            Index = new Euribor6M(TermStructure);
            OisIndex = new Eonia(new ZeroSpreadedTermStructure(TermStructure, -0.01));
        }

        // global data
        public Date Today { get; }

        public Date Settlement { get; }

        public Real Nominal { get; }

        public Calendar Calendar { get; }

        public BusinessDayConvention FixedConvention { get; }

        public Frequency FixedFrequency { get; }

        public DayCounter FixedDayCount { get; }

        public BusinessDayConvention FloatingConvention { get; }

        public Period FloatingTenor { get; }

        public IborIndex Index { get; }

        public OvernightIndex OisIndex { get; }

        public Natural SettlementDays { get; }

        public YieldTermStructure TermStructure { get; }

        public EvaluationContext Context { get; }

        // utilities
        public Swaption MakeSwaption(
            VanillaSwap swap,
            Date exercise,
            SettlementType settlementType = SettlementType.Physical,
            SettlementMethod settlementMethod = SettlementMethod.PhysicalOTC) =>
            new(swap, new EuropeanExercise(exercise), settlementType, settlementMethod);

        public Swaption MakeOISwaption(
            OvernightIndexedSwap swap,
            Date exercise,
            SettlementType settlementType = SettlementType.Physical,
            SettlementMethod settlementMethod = SettlementMethod.PhysicalOTC) =>
            new(swap, new EuropeanExercise(exercise), settlementType, settlementMethod);

        // The engine QuantLib's makeSwaption, makeOISwaption and makeEngine set: a Black engine on a volatility quote.
        public InstrumentResults Price(
            Swaption swaption, Volatility volatility, CashAnnuityModel model = CashAnnuityModel.SwapRate) =>
            BlackSwaptionEngine.Calculate(swaption, Context, TermStructure, volatility, new Actual365Fixed(), 0.0, model);

        public Real Npv(Swaption swaption, Volatility volatility) => Price(swaption, volatility).NPV!.Value;
    }
}
