// SPDX-FileCopyrightText: Copyright (C) 2011 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2012 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/inflationcpibond.cpp @ v1.44
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

/// <summary>QuantLib's CPI bond tests, <c>test-suite/inflationcpibond.cpp</c>.</summary>
[Trait("Oracle", "QuantLib")]
public class InflationCPIBondTests
{
    // Adapted: the bond is priced by calling the engine, and its prices read the engine's results.
    [Fact]
    public void TestCleanPrice()
    {
        // Checking cached pricers for CPI bond...

        var common = new CommonVars();

        double notional = 1000000.0;
        double[] fixedRates = [0.1];
        DayCounter fixedDayCount = new Actual365Fixed();
        BusinessDayConvention fixedPaymentConvention = BusinessDayConvention.ModifiedFollowing;
        ZeroInflationIndex fixedIndex = common.Ii;
        var contractObservationLag = new Period(3, TimeUnit.Months);
        CPIInterpolationType observationInterpolation = CPIInterpolationType.Flat;
        int settlementDays = 3;
        double baseCPI = 206.1;

        // set the schedules
        var startDate = new Date(2, October, 2007);
        var endDate = new Date(2, October, 2052);
        Schedule fixedSchedule = new MakeSchedule()
            .From(startDate)
            .To(endDate)
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithCalendar(new UnitedKingdom())
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Backwards()
            .ToSchedule();

        var bond = new CPIBond(
            settlementDays,
            notional,
            baseCPI,
            contractObservationLag,
            fixedIndex,
            observationInterpolation,
            fixedSchedule,
            fixedRates,
            fixedDayCount,
            fixedPaymentConvention);

        BondResults results = DiscountingBondEngine.Calculate(bond, common.Context, common.YTS);

        double storedPrice = 396.47045891;
        double calculated = bond.DirtyPrice(results, common.Context);
        double tolerance = 1.0e-8;
        Assert.False(
            Math.Abs(calculated - storedPrice) > tolerance,
            $"failed to reproduce expected CPI-bond dirty price\n  expected:   {storedPrice:F12}\n  calculated: {calculated:F12}");

        storedPrice = 394.79676679;
        calculated = bond.CleanPrice(results, common.Context);
        Assert.False(
            Math.Abs(calculated - storedPrice) > tolerance,
            $"failed to reproduce expected CPI-bond clean price\n  expected:   {storedPrice:F12}\n  calculated: {calculated:F12}");
    }

    [Fact]
    public void TestCPILegWithoutBaseCPI()
    {
        // Checking CPI leg with or without explicit base CPI fixing...

        var common = new CommonVars();

        double notional = 1000000.0;
        double[] fixedRates = [0.1];
        DayCounter fixedDayCount = new Actual365Fixed();
        BusinessDayConvention fixedPaymentConvention = BusinessDayConvention.ModifiedFollowing;
        Calendar fixedPaymentCalendar = new UnitedKingdom();
        ZeroInflationIndex fixedIndex = common.Ii;
        var contractObservationLag = new Period(3, TimeUnit.Months);
        CPIInterpolationType observationInterpolation = CPIInterpolationType.Flat;
        int settlementDays = 3;
        bool growthOnly = false;
        double baseCPI = 206.1;

        // set the schedules
        var baseDate = new Date(1, July, 2007);
        var startDate = new Date(2, October, 2007);
        var endDate = new Date(2, October, 2052);
        Schedule fixedSchedule = new MakeSchedule()
            .From(startDate)
            .To(endDate)
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithCalendar(fixedPaymentCalendar)
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Backwards()
            .ToSchedule();

        ImmutableArray<CashFlow> legWithBaseDate = new CPILeg(fixedSchedule, fixedIndex, null, contractObservationLag)
            .WithSubtractInflationNominal(growthOnly)
            .WithNotionals(notional)
            .WithBaseDate(baseDate)
            .WithFixedRates(fixedRates)
            .WithPaymentDayCounter(fixedDayCount)
            .WithObservationInterpolation(observationInterpolation)
            .WithPaymentAdjustment(fixedPaymentConvention)
            .WithPaymentCalendar(fixedPaymentCalendar)
            .ToLeg();

        ImmutableArray<CashFlow> legWithBaseCPI = new CPILeg(fixedSchedule, fixedIndex, baseCPI, contractObservationLag)
            .WithSubtractInflationNominal(growthOnly)
            .WithNotionals(notional)
            .WithFixedRates(fixedRates)
            .WithPaymentDayCounter(fixedDayCount)
            .WithObservationInterpolation(observationInterpolation)
            .WithPaymentAdjustment(fixedPaymentConvention)
            .WithPaymentCalendar(fixedPaymentCalendar)
            .ToLeg();

        Date settlementDate = fixedPaymentCalendar.Advance(
            common.EvaluationDate, settlementDays, TimeUnit.Days, fixedPaymentConvention);

        double npvWithBaseDate =
            CashFlows.Npv(legWithBaseDate, common.YTS, common.Context, false, settlementDate, settlementDate);
        double accruedsBaseDate = CashFlows.AccruedAmount(legWithBaseDate, common.Context, false, settlementDate);

        double npvWithBaseCPI =
            CashFlows.Npv(legWithBaseCPI, common.YTS, common.Context, false, settlementDate, settlementDate);
        double accruedsBaseCPI = CashFlows.AccruedAmount(legWithBaseCPI, common.Context, false, settlementDate);

        double cleanPriceWithBaseDate = (npvWithBaseDate - accruedsBaseDate) * 100.0 / notional;
        double cleanPriceWithBaseCPI = (npvWithBaseCPI - accruedsBaseCPI) * 100.0 / notional;

        double tolerance = 1.0e-8;

        Assert.False(
            Math.Abs(cleanPriceWithBaseDate - cleanPriceWithBaseCPI) > tolerance,
            "prices of CPI leg with base date and explicit base CPI fixing are not equal "
            + $"\n  clean npv of leg with baseDate:   {cleanPriceWithBaseDate:F12}"
            + $"\n clean npv of leg with explicit baseCPI: {cleanPriceWithBaseCPI:F12}");

        // Compare to expected price
        double storedPrice = 394.79676680;

        Assert.False(
            Math.Abs(cleanPriceWithBaseDate - storedPrice) > tolerance,
            $"failed to reproduce expected CPI-bond clean price\n  expected:   {storedPrice:F12}"
            + $"\n  calculated: {cleanPriceWithBaseDate:F12}");
    }

    // QuantLib's makeHelpers (lines 55-73).
    private static List<BootstrapHelper<ZeroInflationTermStructure>> MakeHelpers(
        Datum[] iiData,
        ZeroInflationIndex ii,
        Period observationLag,
        Calendar calendar,
        BusinessDayConvention bdc,
        DayCounter dc,
        EvaluationContext context)
    {
        var instruments = new List<BootstrapHelper<ZeroInflationTermStructure>>();
        foreach (Datum datum in iiData)
        {
            Date maturity = datum.Date;
            double quote = datum.Rate / 100.0;
            var h = new ZeroCouponInflationSwapHelper(
                quote, observationLag, maturity, calendar, bdc, dc, ii, CPIInterpolationType.Flat, context);
            instruments.Add(h);
        }

        return instruments;
    }

    // QuantLib's Datum (lines 48-51).
    private readonly record struct Datum(Date Date, double Rate);

    // QuantLib's CommonVars (lines 76-156), the fixings in its evaluation context.
    private sealed class CommonVars
    {
        public CommonVars()
        {
            // usual setup
            Calendar = new UnitedKingdom();
            Convention = BusinessDayConvention.ModifiedFollowing;
            var today = new Date(25, November, 2009);
            EvaluationDate = Calendar.Adjust(today);
            Context = new EvaluationContext { EvaluationDate = EvaluationDate };
            DayCounter = new ActualActual(ActualActualConvention.ISDA);

            var ii = new UKRPI();

            Schedule rpiSchedule = new MakeSchedule()
                .From(new Date(1, July, 2007))
                .To(new Date(1, September, 2009))
                .WithFrequency(Frequency.Monthly)
                .ToSchedule();

            double[] fixData =
            [
                206.1, 207.3, 208.0, 208.9, 209.7, 210.9,
                209.8, 211.4, 212.1, 214.0, 215.1, 216.8,
                216.5, 217.2, 218.4, 217.7, 216.0, 212.9,
                210.1, 211.4, 211.3, 211.5, 212.8, 213.4,
                213.4, 213.4, 214.4,
            ];
            for (int i = 0; i < fixData.Length; ++i)
            {
                Context = Context with { Fixings = Context.Fixings.AddFixing(ii, rpiSchedule[i], fixData[i]) };
            }

            YTS = new FlatForward(EvaluationDate, 0.05, DayCounter);

            // now build the zero inflation curve
            ObservationLag = new Period(2, TimeUnit.Months);

            Datum[] zciisData =
            [
                new(new Date(25, November, 2010), 3.0495),
                new(new Date(25, November, 2011), 2.93),
                new(new Date(26, November, 2012), 2.9795),
                new(new Date(25, November, 2013), 3.029),
                new(new Date(25, November, 2014), 3.1425),
                new(new Date(25, November, 2015), 3.211),
                new(new Date(25, November, 2016), 3.2675),
                new(new Date(25, November, 2017), 3.3625),
                new(new Date(25, November, 2018), 3.405),
                new(new Date(25, November, 2019), 3.48),
                new(new Date(25, November, 2021), 3.576),
                new(new Date(25, November, 2024), 3.649),
                new(new Date(26, November, 2029), 3.751),
                new(new Date(27, November, 2034), 3.77225),
                new(new Date(25, November, 2039), 3.77),
                new(new Date(25, November, 2049), 3.734),
                new(new Date(25, November, 2059), 3.714),
            ];

            List<BootstrapHelper<ZeroInflationTermStructure>> helpers =
                MakeHelpers(zciisData, ii, ObservationLag, Calendar, Convention, DayCounter, Context);

            Date baseDate = ii.LastFixingDate(Context);
            CpiTS = new PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits>(
                EvaluationDate, baseDate, ii.Frequency, DayCounter, helpers, Context);

            Ii = new UKRPI(CpiTS);
        }

        public Calendar Calendar { get; }

        public BusinessDayConvention Convention { get; }

        public Date EvaluationDate { get; }

        public EvaluationContext Context { get; }

        public Period ObservationLag { get; }

        public DayCounter DayCounter { get; }

        public UKRPI Ii { get; }

        public YieldTermStructure YTS { get; }

        public ZeroInflationTermStructure CpiTS { get; }
    }
}
