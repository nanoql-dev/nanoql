// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2012 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Piter Dias
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/bonds.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class BondsTests
{
    private static readonly Integer[] IssueMonths = [-24, -18, -12, -6, 0, 6, 12, 18, 24];
    private static readonly Integer[] Lengths = [3, 5, 10, 15, 20];
    private static readonly Real[] Coupons = [0.02, 0.05, 0.08];
    private static readonly Frequency[] Frequencies = [Frequency.Semiannual, Frequency.Annual];

    // Adapted: the evaluation date the fixture sets (line 79) is the context the bonds' settlement dates read.
    [Fact]
    public void TestYield()
    {
        // Testing consistency of bond price/yield calculation...

        var vars = new CommonVars();

        Real tolerance = 1.0e-7;
        Size maxEvaluations = 100;

        Natural settlementDays = 3;
        DayCounter bondDayCount = new Thirty360(Thirty360Convention.BondBasis);
        BusinessDayConvention accrualConvention = BusinessDayConvention.Unadjusted;
        BusinessDayConvention paymentConvention = BusinessDayConvention.ModifiedFollowing;
        Real redemption = 100.0;

        Rate[] yields = [0.03, 0.04, 0.05, 0.06, 0.07];
        Compounding[] compounding = [Compounding.Compounded, Compounding.Continuous];

        foreach (int issueMonth in IssueMonths)
        {
            foreach (int length in Lengths)
            {
                foreach (Real coupon in Coupons)
                {
                    foreach (Frequency frequency in Frequencies)
                    {
                        foreach (Compounding n in compounding)
                        {
                            Date dated = vars.Calendar.Advance(vars.Today, issueMonth, TimeUnit.Months);
                            Date issue = dated;
                            Date maturity = vars.Calendar.Advance(issue, length, TimeUnit.Years);

                            Schedule sch = vars.MakeSchedule(dated, maturity, frequency, accrualConvention);

                            var bond = new FixedRateBond(
                                settlementDays, vars.FaceAmount, sch, [coupon], bondDayCount, paymentConvention,
                                redemption, issue);

                            foreach (Real m in yields)
                            {
                                var price = new BondPrice(
                                    BondFunctions.CleanPrice(bond, m, bondDayCount, n, frequency, vars.Context),
                                    BondPriceType.Clean);

                                Rate calculated = BondFunctions.Yield(
                                    bond, price, bondDayCount, n, frequency, vars.Context, null, tolerance,
                                    maxEvaluations, 0.05);

                                if (Math.Abs(m - calculated) > tolerance)
                                {
                                    // the difference might not matter
                                    Real price2 = BondFunctions.CleanPrice(
                                        bond, calculated, bondDayCount, n, frequency, vars.Context);
                                    Assert.False(
                                        Math.Abs(price.Amount - price2) / price.Amount > tolerance,
                                        $"yield recalculation failed: issue {issue}, maturity {maturity}, coupon {coupon}, "
                                        + $"frequency {frequency}, yield {m} {n}, clean price {price.Amount}, yield' "
                                        + $"{calculated}, clean price' {price2}");
                                }

                                price = new BondPrice(
                                    BondFunctions.DirtyPrice(bond, m, bondDayCount, n, frequency, vars.Context),
                                    BondPriceType.Dirty);

                                calculated = BondFunctions.Yield(
                                    bond, price, bondDayCount, n, frequency, vars.Context, null, tolerance,
                                    maxEvaluations, 0.05);

                                if (Math.Abs(m - calculated) > tolerance)
                                {
                                    // the difference might not matter
                                    Real price2 = BondFunctions.DirtyPrice(
                                        bond, calculated, bondDayCount, n, frequency, vars.Context);
                                    Assert.False(
                                        Math.Abs(price.Amount - price2) / price.Amount > tolerance,
                                        $"yield recalculation failed: issue {issue}, maturity {maturity}, coupon {coupon}, "
                                        + $"frequency {frequency}, yield {m} {n}, dirty price {price.Amount}, yield' "
                                        + $"{calculated}, dirty price' {price2}");
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // Adapted: the bonds' engine (lines 220, 238) is DiscountingBondEngine.Calculate, and their clean and dirty prices
    // (lines 239, 255) are read from its results under the fixture's context.
    [Fact]
    public void TestAtmRate()
    {
        // Testing consistency of bond price/ATM rate calculation...

        var vars = new CommonVars();

        Real tolerance = 1.0e-7;

        Natural settlementDays = 3;
        DayCounter bondDayCount = new Thirty360(Thirty360Convention.BondBasis);
        BusinessDayConvention accrualConvention = BusinessDayConvention.Unadjusted;
        BusinessDayConvention paymentConvention = BusinessDayConvention.ModifiedFollowing;
        Real redemption = 100.0;
        var disc = new FlatForward(vars.Today, 0.03, new Actual360());

        foreach (int issueMonth in IssueMonths)
        {
            foreach (int length in Lengths)
            {
                foreach (Real coupon in Coupons)
                {
                    foreach (Frequency frequency in Frequencies)
                    {
                        Date dated = vars.Calendar.Advance(vars.Today, issueMonth, TimeUnit.Months);
                        Date issue = dated;
                        Date maturity = vars.Calendar.Advance(issue, length, TimeUnit.Years);

                        Schedule sch = vars.MakeSchedule(dated, maturity, frequency, accrualConvention);

                        var bond = new FixedRateBond(
                            settlementDays, vars.FaceAmount, sch, [coupon], bondDayCount, paymentConvention,
                            redemption, issue);

                        BondResults results = DiscountingBondEngine.Calculate(bond, vars.Context, disc);
                        var price = new BondPrice(bond.CleanPrice(results, vars.Context), BondPriceType.Clean);
                        Rate calculated = BondFunctions.AtmRate(
                            bond, disc, vars.Context, bond.SettlementDate(vars.Context), price);

                        Assert.False(
                            Math.Abs(coupon - calculated) > tolerance,
                            $"atm rate recalculation failed: today {vars.Today}, settlement date "
                            + $"{bond.SettlementDate(vars.Context)}, issue {issue}, maturity {maturity}, coupon {coupon}, "
                            + $"frequency {frequency}, clean price {price.Amount}, atm rate {calculated}");

                        price = new BondPrice(bond.DirtyPrice(results, vars.Context), BondPriceType.Dirty);
                        calculated = BondFunctions.AtmRate(
                            bond, disc, vars.Context, bond.SettlementDate(vars.Context), price);

                        Assert.False(
                            Math.Abs(coupon - calculated) > tolerance,
                            $"atm rate recalculation failed: today {vars.Today}, settlement date "
                            + $"{bond.SettlementDate(vars.Context)}, issue {issue}, maturity {maturity}, coupon {coupon}, "
                            + $"frequency {frequency}, dirty price {price.Amount}, atm rate {calculated}");
                    }
                }
            }
        }
    }

    // Adapted: the evaluation date the fixture sets (line 79) is the context the bonds' settlement dates read.
    [Fact]
    public void TestZspread()
    {
        // Testing consistency of bond price/z-spread calculation...

        var vars = new CommonVars();

        Real tolerance = 1.0e-7;
        Size maxEvaluations = 100;

        var discountCurve = new FlatForward(vars.Today, 0.03, new Actual360());

        Natural settlementDays = 3;
        DayCounter bondDayCount = new Thirty360(Thirty360Convention.BondBasis);
        BusinessDayConvention accrualConvention = BusinessDayConvention.Unadjusted;
        BusinessDayConvention paymentConvention = BusinessDayConvention.ModifiedFollowing;
        Real redemption = 100.0;

        Spread[] spreads = [-0.01, -0.005, 0.0, 0.005, 0.01];
        Compounding[] compounding = [Compounding.Compounded, Compounding.Continuous];

        foreach (int issueMonth in IssueMonths)
        {
            foreach (int length in Lengths)
            {
                foreach (Real coupon in Coupons)
                {
                    foreach (Frequency frequency in Frequencies)
                    {
                        foreach (Compounding n in compounding)
                        {
                            Date dated = vars.Calendar.Advance(vars.Today, issueMonth, TimeUnit.Months);
                            Date issue = dated;
                            Date maturity = vars.Calendar.Advance(issue, length, TimeUnit.Years);

                            Schedule sch = vars.MakeSchedule(dated, maturity, frequency, accrualConvention);

                            var bond = new FixedRateBond(
                                settlementDays, vars.FaceAmount, sch, [coupon], bondDayCount, paymentConvention,
                                redemption, issue);

                            foreach (Real spread in spreads)
                            {
                                // Clean price
                                var price = new BondPrice(
                                    BondFunctions.CleanPrice(bond, discountCurve, spread, n, frequency, vars.Context),
                                    BondPriceType.Clean);
                                Spread calculated = BondFunctions.ZSpread(
                                    bond, price, discountCurve, n, frequency, vars.Context, null, tolerance,
                                    maxEvaluations);

                                if (Math.Abs(spread - calculated) > tolerance)
                                {
                                    // the difference might not matter
                                    Real price2 = BondFunctions.CleanPrice(
                                        bond, discountCurve, calculated, n, frequency, vars.Context);
                                    Assert.False(
                                        Math.Abs(price.Amount - price2) / price.Amount > tolerance,
                                        $"Z-spread recalculation failed: issue {issue}, maturity {maturity}, coupon "
                                        + $"{coupon}, frequency {frequency}, Z-spread {spread} {n}, clean price "
                                        + $"{price.Amount}, Z-spread' {calculated}, clean price' {price2}");
                                }

                                // Dirty price
                                price = new BondPrice(
                                    BondFunctions.DirtyPrice(bond, discountCurve, spread, n, frequency, vars.Context),
                                    BondPriceType.Dirty);

                                calculated = BondFunctions.ZSpread(
                                    bond, price, discountCurve, n, frequency, vars.Context, null, tolerance,
                                    maxEvaluations);

                                if (Math.Abs(spread - calculated) > tolerance)
                                {
                                    // the difference might not matter
                                    Real price2 = BondFunctions.DirtyPrice(
                                        bond, discountCurve, calculated, n, frequency, vars.Context);
                                    Assert.False(
                                        Math.Abs(price.Amount - price2) / price.Amount > tolerance,
                                        $"Z-spread recalculation failed: issue {issue}, maturity {maturity}, coupon "
                                        + $"{coupon}, frequency {frequency}, Z-spread {spread} {n}, dirty price "
                                        + $"{price.Amount}, Z-spread' {calculated}, dirty price' {price2}");
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // Adapted: the quote the curve reads (line 431), changed for each yield, becomes a curve built on each yield; the
    // engine (lines 426-427) is DiscountingBondEngine.Calculate on it, and the clean and dirty prices (lines 436, 469)
    // are read from its results under the fixture's context.
    [Fact]
    public void TestTheoretical()
    {
        // Testing theoretical bond price/yield calculation...

        var vars = new CommonVars();

        Real tolerance = 1.0e-7;
        Size maxEvaluations = 100;

        Natural settlementDays = 3;
        DayCounter bondDayCount = new Actual360();
        BusinessDayConvention accrualConvention = BusinessDayConvention.Unadjusted;
        BusinessDayConvention paymentConvention = BusinessDayConvention.ModifiedFollowing;
        Real redemption = 100.0;

        Rate[] yields = [0.03, 0.04, 0.05, 0.06, 0.07];

        foreach (int length in Lengths)
        {
            foreach (Real coupon in Coupons)
            {
                foreach (Frequency frequency in Frequencies)
                {
                    Date dated = vars.Today;
                    Date issue = dated;
                    Date maturity = vars.Calendar.Advance(issue, length, TimeUnit.Years);

                    Schedule sch = vars.MakeSchedule(dated, maturity, frequency, accrualConvention);

                    var bond = new FixedRateBond(
                        settlementDays, vars.FaceAmount, sch, [coupon], bondDayCount, paymentConvention, redemption,
                        issue);

                    foreach (Real m in yields)
                    {
                        var discountCurve = new FlatForward(vars.Today, m, bondDayCount);
                        BondResults results = DiscountingBondEngine.Calculate(bond, vars.Context, discountCurve);

                        // Test yield vs clean price
                        Real price = BondFunctions.CleanPrice(
                            bond, m, bondDayCount, Compounding.Continuous, frequency, vars.Context);
                        Real calculatedPrice = bond.CleanPrice(results, vars.Context);

                        Assert.False(
                            Math.Abs(price - calculatedPrice) > tolerance,
                            $"price calculation failed: issue {issue}, maturity {maturity}, coupon {coupon}, "
                            + $"frequency {frequency}, yield {m}, expected {price}, calculated' {calculatedPrice}, "
                            + $"error' {price - calculatedPrice}");

                        Rate calculatedYield = BondFunctions.Yield(
                            bond, new BondPrice(calculatedPrice, BondPriceType.Clean), bondDayCount,
                            Compounding.Continuous, frequency, vars.Context, bond.SettlementDate(vars.Context),
                            tolerance, maxEvaluations);
                        Assert.False(
                            Math.Abs(m - calculatedYield) > tolerance,
                            $"yield calculation failed: issue {issue}, maturity {maturity}, coupon {coupon}, "
                            + $"frequency {frequency}, yield {m}, clean price {price}, yield' {calculatedYield}");

                        // Test yield vs dirty price
                        price = BondFunctions.DirtyPrice(
                            bond, m, bondDayCount, Compounding.Continuous, frequency, vars.Context);
                        calculatedPrice = bond.DirtyPrice(results, vars.Context);

                        Assert.False(
                            Math.Abs(price - calculatedPrice) > tolerance,
                            $"price calculation failed: issue {issue}, maturity {maturity}, coupon {coupon}, "
                            + $"frequency {frequency}, yield {m}, expected {price}, calculated' {calculatedPrice}, "
                            + $"error' {price - calculatedPrice}");

                        calculatedYield = BondFunctions.Yield(
                            bond, new BondPrice(calculatedPrice, BondPriceType.Dirty), bondDayCount,
                            Compounding.Continuous, frequency, vars.Context, bond.SettlementDate(vars.Context),
                            tolerance, maxEvaluations, 0.05);
                        Assert.False(
                            Math.Abs(m - calculatedYield) > tolerance,
                            $"yield calculation failed: issue {issue}, maturity {maturity}, coupon {coupon}, "
                            + $"frequency {frequency}, yield {m}, dirty price {price}, yield' {calculatedYield}");
                    }
                }
            }
        }
    }

    // Adapted: the evaluation date the case sets (line 514, set again at line 737) is the context; the bonds' engine
    // (lines 548-551, 572-573, 713-714) is DiscountingBondEngine.Calculate, and their clean prices are read from its
    // results.
    [Fact]
    public void TestCached()
    {
        // Testing bond price/yield calculation against cached values...

        var vars = new CommonVars();

        // with implicit settlement calculation:

        var today = new Date(22, November, 2004);
        var context = new EvaluationContext { EvaluationDate = today };

        Calendar bondCalendar = new NullCalendar();

        Natural settlementDays = 1;

        var discountCurve = new FlatForward(today, 0.03, new Actual360());
        Real CleanPrice(Bond bond) => bond.CleanPrice(DiscountingBondEngine.Calculate(bond, context, discountCurve), context);

        // actual market values from the evaluation date

        Frequency freq = Frequency.Semiannual;

        // This means that this bond has a short first coupon, as the
        // first coupon payment is april 30th and therefore the notional
        // first coupon is on October 30th 2004. Changing the EOM
        // convention to true will correct this so that the coupon starts
        // on October 31st and the first coupon is complete. This is
        // effectively assumed by the no-schedule daycounter.
        var sch1 = new Schedule(
            new Date(31, October, 2004), new Date(31, October, 2006), new Period(freq), bondCalendar,
            BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, true,
            context);
        DayCounter bondDayCount1 = new ActualActual(ActualActualConvention.ISMA, sch1);
        DayCounter bondDayCount1NoSchedule = new ActualActual(ActualActualConvention.ISMA);

        var bond1 = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch1, [0.025], bondDayCount1, BusinessDayConvention.ModifiedFollowing,
            100.0, new Date(1, November, 2004));
        var bond1NoSchedule = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch1, [0.025], bondDayCount1NoSchedule,
            BusinessDayConvention.ModifiedFollowing, 100.0, new Date(1, November, 2004));

        Real marketPrice1 = 99.203125;
        Rate marketYield1 = 0.02925;

        var sch2 = new Schedule(
            new Date(15, November, 2004), new Date(15, November, 2009), new Period(freq), bondCalendar,
            BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false,
            context);
        DayCounter bondDayCount2 = new ActualActual(ActualActualConvention.ISMA, sch2);
        DayCounter bondDayCount2NoSchedule = new ActualActual(ActualActualConvention.ISMA);

        var bond2 = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch2, [0.035], bondDayCount2, BusinessDayConvention.ModifiedFollowing,
            100.0, new Date(15, November, 2004));
        var bond2NoSchedule = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch2, [0.035], bondDayCount2NoSchedule,
            BusinessDayConvention.ModifiedFollowing, 100.0, new Date(15, November, 2004));

        Real marketPrice2 = 99.6875;
        Rate marketYield2 = 0.03569;

        // calculated values

        Real cachedPrice1a = 99.204505, cachedPrice2a = 99.687192;
        Real cachedPrice1b = 98.943393, cachedPrice2b = 101.986794;
        Rate cachedYield1a = 0.029257, cachedYield2a = 0.035689;
        Rate cachedYield1b = 0.029045, cachedYield2b = 0.035375;
        Rate cachedYield1c = 0.030423, cachedYield2c = 0.030432;

        // check
        Real tolerance = 1.0e-6;
        Compounding compounded = Compounding.Compounded, continuous = Compounding.Continuous;

        CheckValue(
            BondFunctions.CleanPrice(bond1, marketYield1, bondDayCount1, compounded, freq, context),
            cachedPrice1a,
            tolerance,
            "failed to reproduce cached price with schedule for bond 1:");
        CheckValue(
            BondFunctions.CleanPrice(bond1NoSchedule, marketYield1, bondDayCount1NoSchedule, compounded, freq, context),
            cachedPrice1a,
            tolerance,
            "failed to reproduce cached price with no schedule for bond 1:");
        CheckValue(
            CleanPrice(bond1),
            cachedPrice1b,
            tolerance,
            "failed to reproduce cached clean price with schedule for bond 1:");
        CheckValue(
            CleanPrice(bond1NoSchedule),
            cachedPrice1b,
            tolerance,
            "failed to reproduce cached clean price with no schdule for bond 1:");
        CheckValue(
            BondFunctions.Yield(
                bond1, new BondPrice(marketPrice1, BondPriceType.Clean), bondDayCount1, compounded, freq, context),
            cachedYield1a,
            tolerance,
            "failed to reproduce cached compounded yield with schedule for bond 1:");
        CheckValue(
            BondFunctions.Yield(
                bond1NoSchedule, new BondPrice(marketPrice1, BondPriceType.Clean), bondDayCount1NoSchedule,
                compounded, freq, context),
            cachedYield1a,
            tolerance,
            "failed to reproduce cached compounded yield with no schedule for bond 1:");
        CheckValue(
            BondFunctions.Yield(
                bond1, new BondPrice(marketPrice1, BondPriceType.Clean), bondDayCount1, continuous, freq, context),
            cachedYield1b,
            tolerance,
            "failed to reproduce cached continuous yield with schedule for bond 1:");
        CheckValue(
            BondFunctions.Yield(
                bond1NoSchedule, new BondPrice(marketPrice1, BondPriceType.Clean), bondDayCount1NoSchedule,
                continuous, freq, context),
            cachedYield1b,
            tolerance,
            "failed to reproduce cached continuous yield with no schedule for bond 1:");
        CheckValue(
            BondFunctions.Yield(
                bond1, new BondPrice(CleanPrice(bond1), BondPriceType.Clean), bondDayCount1, continuous, freq,
                context, bond1.SettlementDate(context)),
            cachedYield1c,
            tolerance,
            "failed to reproduce cached continuous yield with schedule for bond 1:");
        CheckValue(
            BondFunctions.Yield(
                bond1NoSchedule, new BondPrice(CleanPrice(bond1NoSchedule), BondPriceType.Clean),
                bondDayCount1NoSchedule, continuous, freq, context, bond1.SettlementDate(context)),
            cachedYield1c,
            tolerance,
            "failed to reproduce cached continuous yield with no schedule for bond 1:");

        // Now bond 2
        CheckValue(
            BondFunctions.CleanPrice(bond2, marketYield2, bondDayCount2, compounded, freq, context),
            cachedPrice2a,
            tolerance,
            "failed to reproduce cached price with schedule for bond 2");
        CheckValue(
            BondFunctions.CleanPrice(bond2NoSchedule, marketYield2, bondDayCount2NoSchedule, compounded, freq, context),
            cachedPrice2a,
            tolerance,
            "failed to reproduce cached price with no schedule for bond 2:");
        CheckValue(
            CleanPrice(bond2),
            cachedPrice2b,
            tolerance,
            "failed to reproduce cached clean price with schedule for bond 2:");
        CheckValue(
            CleanPrice(bond2NoSchedule),
            cachedPrice2b,
            tolerance,
            "failed to reproduce cached clean price with no schedule for bond 2:");
        CheckValue(
            BondFunctions.Yield(
                bond2, new BondPrice(marketPrice2, BondPriceType.Clean), bondDayCount2, compounded, freq, context),
            cachedYield2a,
            tolerance,
            "failed to reproduce cached compounded yield with schedule for bond 2:");
        CheckValue(
            BondFunctions.Yield(
                bond2NoSchedule, new BondPrice(marketPrice2, BondPriceType.Clean), bondDayCount2NoSchedule,
                compounded, freq, context),
            cachedYield2a,
            tolerance,
            "failed to reproduce cached compounded yield with no schedule for bond 2:");
        CheckValue(
            BondFunctions.Yield(
                bond2, new BondPrice(marketPrice2, BondPriceType.Clean), bondDayCount2, continuous, freq, context),
            cachedYield2b,
            tolerance,
            "failed to reproduce chached continuous yield with schedule for bond 2:");
        CheckValue(
            BondFunctions.Yield(
                bond2NoSchedule, new BondPrice(marketPrice2, BondPriceType.Clean), bondDayCount2NoSchedule,
                continuous, freq, context),
            cachedYield2b,
            tolerance,
            "failed to reproduce cached continuous yield with schedule for bond 2:");
        CheckValue(
            BondFunctions.Yield(
                bond2, new BondPrice(CleanPrice(bond2), BondPriceType.Clean), bondDayCount2, continuous, freq,
                context, bond2.SettlementDate(context)),
            cachedYield2c,
            tolerance,
            "failed to reproduce cached continuous yield for bond 2 with schedule:");
        CheckValue(
            BondFunctions.Yield(
                bond2NoSchedule, new BondPrice(CleanPrice(bond2NoSchedule), BondPriceType.Clean),
                bondDayCount2NoSchedule, continuous, freq, context, bond2NoSchedule.SettlementDate(context)),
            cachedYield2c,
            tolerance,
            "failed to reproduce cached continuous yield for bond 2 with no schedule:");

        // with explicit settlement date:

        var sch3 = new Schedule(
            new Date(30, November, 2004), new Date(30, November, 2006), new Period(freq),
            new UnitedStates(UnitedStatesMarket.GovernmentBond), BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false, context);
        DayCounter bondDayCount3 = new ActualActual(ActualActualConvention.ISMA, sch3);
        DayCounter bondDayCount3NoSchedule = new ActualActual(ActualActualConvention.ISMA);

        var bond3 = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch3, [0.02875], bondDayCount3, BusinessDayConvention.ModifiedFollowing,
            100.0, new Date(30, November, 2004));
        var bond3NoSchedule = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch3, [0.02875], bondDayCount3NoSchedule,
            BusinessDayConvention.ModifiedFollowing, 100.0, new Date(30, November, 2004));

        Rate marketYield3 = 0.02997;

        var settlementDate = new Date(30, November, 2004);
        Real cachedPrice3 = 99.764759;

        CheckValue(
            BondFunctions.CleanPrice(bond3, marketYield3, bondDayCount3, compounded, freq, context, settlementDate),
            cachedPrice3,
            tolerance,
            "Failed to reproduce cached price for bond 3 with schedule");
        CheckValue(
            BondFunctions.CleanPrice(
                bond3NoSchedule, marketYield3, bondDayCount3NoSchedule, compounded, freq, context, settlementDate),
            cachedPrice3,
            tolerance,
            "Failed to reproduce cached price for bond 3 with no schedule");

        // this should give the same result since the issue date is the
        // earliest possible settlement date

        context = new EvaluationContext { EvaluationDate = new Date(22, November, 2004) };
        CheckValue(
            BondFunctions.CleanPrice(bond3, marketYield3, bondDayCount3, compounded, freq, context),
            cachedPrice3,
            tolerance,
            "Failed to reproduce the cached price for bond 3 with schedule and the earlierst possible settlment date");
        CheckValue(
            BondFunctions.CleanPrice(bond3NoSchedule, marketYield3, bondDayCount3NoSchedule, compounded, freq, context),
            cachedPrice3,
            tolerance,
            "Failed to reproduce the cached price for bond 3 with no schedule and the earlierst possible settlment date");
    }

    // Adapted: the evaluation date the case sets (line 759) is the context; the bonds' engine (lines 776-778, 798, 818)
    // is DiscountingBondEngine.Calculate, and their clean prices are read from its results.
    [Fact]
    public void TestCachedZero()
    {
        // Testing zero-coupon bond prices against cached values...

        var vars = new CommonVars();

        var today = new Date(22, November, 2004);
        var context = new EvaluationContext { EvaluationDate = today };

        Natural settlementDays = 1;

        var discountCurve = new FlatForward(today, 0.03, new Actual360());

        Real tolerance = 1.0e-6;

        // plain

        var bond1 = new ZeroCouponBond(
            settlementDays, new UnitedStates(UnitedStatesMarket.GovernmentBond), vars.FaceAmount,
            new Date(30, November, 2008), BusinessDayConvention.ModifiedFollowing, 100.0, new Date(30, November, 2004));

        Real cachedPrice1 = 88.551726;

        Real price = bond1.CleanPrice(DiscountingBondEngine.Calculate(bond1, context, discountCurve), context);
        Assert.False(
            Math.Abs(price - cachedPrice1) > tolerance,
            $"failed to reproduce cached price: calculated {price}, expected {cachedPrice1}, error {price - cachedPrice1}");

        var bond2 = new ZeroCouponBond(
            settlementDays, new UnitedStates(UnitedStatesMarket.GovernmentBond), vars.FaceAmount,
            new Date(30, November, 2007), BusinessDayConvention.ModifiedFollowing, 100.0, new Date(30, November, 2004));

        Real cachedPrice2 = 91.278949;

        price = bond2.CleanPrice(DiscountingBondEngine.Calculate(bond2, context, discountCurve), context);
        Assert.False(
            Math.Abs(price - cachedPrice2) > tolerance,
            $"failed to reproduce cached price: calculated {price}, expected {cachedPrice2}, error {price - cachedPrice2}");

        var bond3 = new ZeroCouponBond(
            settlementDays, new UnitedStates(UnitedStatesMarket.GovernmentBond), vars.FaceAmount,
            new Date(30, November, 2006), BusinessDayConvention.ModifiedFollowing, 100.0, new Date(30, November, 2004));

        Real cachedPrice3 = 94.098006;

        price = bond3.CleanPrice(DiscountingBondEngine.Calculate(bond3, context, discountCurve), context);
        Assert.False(
            Math.Abs(price - cachedPrice3) > tolerance,
            $"failed to reproduce cached price: calculated {price}, expected {cachedPrice3}, error {price - cachedPrice3}");
    }

    // Adapted: the evaluation date the case sets (line 839) is the context; the bonds' engine (lines 860-862, 889, 915)
    // is DiscountingBondEngine.Calculate, and their clean prices are read from its results.
    [Fact]
    public void TestCachedFixed()
    {
        // Testing fixed-coupon bond prices against cached values...

        var vars = new CommonVars();

        var today = new Date(22, November, 2004);
        var context = new EvaluationContext { EvaluationDate = today };

        Natural settlementDays = 1;

        var discountCurve = new FlatForward(today, 0.03, new Actual360());
        Real CleanPrice(Bond bond) => bond.CleanPrice(DiscountingBondEngine.Calculate(bond, context, discountCurve), context);

        Real tolerance = 1.0e-6;

        // plain

        var sch = new Schedule(
            new Date(30, November, 2004), new Date(30, November, 2008), new Period(Frequency.Semiannual),
            new UnitedStates(UnitedStatesMarket.GovernmentBond), BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false, context);

        var bond1 = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch, [0.02875], new ActualActual(ActualActualConvention.ISMA),
            BusinessDayConvention.ModifiedFollowing, 100.0, new Date(30, November, 2004));

        Real cachedPrice1 = 99.298100;

        Real price = CleanPrice(bond1);
        Assert.False(
            Math.Abs(price - cachedPrice1) > tolerance,
            $"failed to reproduce cached price: calculated {price}, expected {cachedPrice1}, error {price - cachedPrice1}");

        // varying coupons

        Rate[] couponRates = [0.02875, 0.03, 0.03125, 0.0325];

        var bond2 = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch, couponRates, new ActualActual(ActualActualConvention.ISMA),
            BusinessDayConvention.ModifiedFollowing, 100.0, new Date(30, November, 2004));

        Real cachedPrice2 = 100.334149;

        price = CleanPrice(bond2);
        Assert.False(
            Math.Abs(price - cachedPrice2) > tolerance,
            $"failed to reproduce cached price: calculated {price}, expected {cachedPrice2}, error {price - cachedPrice2}");

        // stub date

        var sch3 = new Schedule(
            new Date(30, November, 2004), new Date(30, March, 2009), new Period(Frequency.Semiannual),
            new UnitedStates(UnitedStatesMarket.GovernmentBond), BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false, context, null,
            new Date(30, November, 2008));

        var bond3 = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch3, couponRates, new ActualActual(ActualActualConvention.ISMA),
            BusinessDayConvention.ModifiedFollowing, 100.0, new Date(30, November, 2004));

        Real cachedPrice3 = 100.382794;

        price = CleanPrice(bond3);
        Assert.False(
            Math.Abs(price - cachedPrice3) > tolerance,
            $"failed to reproduce cached price: calculated {price}, expected {cachedPrice3}, error {price - cachedPrice3}");
    }

    // Adapted: the evaluation date the case sets (line 938) is the context, whose at-par switch is QuantLib's
    // usingAtParCoupons; the bonds' engines (lines 970-972, 997-999, 1030, 1053) are DiscountingBondEngine.Calculate,
    // their clean prices read from its results; setCouponPricer returns the bond with its coupons' pricer set; the
    // fixing added to the index (line 1052) is stored in the context bond4 is priced in.
    [Fact]
    public void TestCachedFloating()
    {
        // Testing floating-rate bond prices against cached values...

        var vars = new CommonVars();

        var today = new Date(22, November, 2004);
        var context = new EvaluationContext { EvaluationDate = today };
        bool usingAtParCoupons = context.UseAtParCoupons;

        Natural settlementDays = 1;

        var riskFreeRate = new FlatForward(today, 0.025, new Actual360());
        var discountCurve = new FlatForward(today, 0.03, new Actual360());

        IborIndex index = new USDLibor(6 * TimeUnit.Months, riskFreeRate);
        Natural fixingDays = 1;

        Real tolerance = 1.0e-6;

        IborCouponPricer pricer = new BlackIborCouponPricer(context);

        Real CleanPrice(Bond bond, YieldTermStructure curve, EvaluationContext ctx) =>
            bond.CleanPrice(DiscountingBondEngine.Calculate(bond, ctx, curve), ctx);

        // plain

        var sch = new Schedule(
            new Date(30, November, 2004), new Date(30, November, 2008), new Period(Frequency.Semiannual),
            new UnitedStates(UnitedStatesMarket.GovernmentBond), BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward, false, context);

        var bond1 = new FloatingRateBond(
            settlementDays, vars.FaceAmount, sch, index, new ActualActual(ActualActualConvention.ISMA), context,
            BusinessDayConvention.ModifiedFollowing, fixingDays, [], [], [], [], false, 100.0,
            new Date(30, November, 2004));

        bond1 = CouponPricerFunctions.SetCouponPricer(bond1, pricer);

        Real cachedPrice1 = usingAtParCoupons ? 99.874646 : 99.874645;

        Real price = CleanPrice(bond1, riskFreeRate, context);
        Assert.False(
            Math.Abs(price - cachedPrice1) > tolerance,
            $"failed to reproduce cached price: calculated {price}, expected {cachedPrice1}, error {price - cachedPrice1}");

        // different risk-free and discount curve

        var bond2 = new FloatingRateBond(
            settlementDays, vars.FaceAmount, sch, index, new ActualActual(ActualActualConvention.ISMA), context,
            BusinessDayConvention.ModifiedFollowing, fixingDays, [], [], [], [], false, 100.0,
            new Date(30, November, 2004));

        bond2 = CouponPricerFunctions.SetCouponPricer(bond2, pricer);

        Real cachedPrice2 = 97.955904;

        price = CleanPrice(bond2, discountCurve, context);
        Assert.False(
            Math.Abs(price - cachedPrice2) > tolerance,
            $"failed to reproduce cached price: calculated {price}, expected {cachedPrice2}, error {price - cachedPrice2}");

        // varying spread

        Rate[] spreads = [0.001, 0.0012, 0.0014, 0.0016];

        var bond3 = new FloatingRateBond(
            settlementDays, vars.FaceAmount, sch, index, new ActualActual(ActualActualConvention.ISMA), context,
            BusinessDayConvention.ModifiedFollowing, fixingDays, [], spreads, [], [], false, 100.0,
            new Date(30, November, 2004));

        bond3 = CouponPricerFunctions.SetCouponPricer(bond3, pricer);

        Real cachedPrice3 = usingAtParCoupons ? 98.495459 : 98.495458;

        price = CleanPrice(bond3, discountCurve, context);
        Assert.False(
            Math.Abs(price - cachedPrice3) > tolerance,
            $"failed to reproduce cached price: calculated {price}, expected {cachedPrice3}, error {price - cachedPrice3}");

        var sch2 = new Schedule(
            new Date(26, November, 2003), new Date(26, November, 2007), new Period(Frequency.Semiannual),
            new UnitedStates(UnitedStatesMarket.GovernmentBond), BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward, false, context);
        var bond4 = new FloatingRateBond(
            settlementDays, vars.FaceAmount, sch2, index, new ActualActual(ActualActualConvention.ISMA), context,
            BusinessDayConvention.ModifiedFollowing, fixingDays, [], spreads, [], [], false, 100.0,
            new Date(29, October, 2004), new Period(6, TimeUnit.Days));

        EvaluationContext withFixing = context with
        {
            Fixings = context.Fixings.AddFixing(index, new Date(25, May, 2004), 0.0402),
        };

        bond4 = CouponPricerFunctions.SetCouponPricer(bond4, pricer);

        Real cachedPrice4 = usingAtParCoupons ? 98.892055 : 98.892346;

        price = CleanPrice(bond4, discountCurve, withFixing);
        Assert.False(
            Math.Abs(price - cachedPrice4) > tolerance,
            $"failed to reproduce cached price: calculated {price}, expected {cachedPrice4}, error {price - cachedPrice4}");
    }

    // Adapted: the evaluation date the case sets (line 1084) is the context; CommonVars (line 1073) is unused.
    [Fact]
    public void TestBrazilianCached()
    {
        // Testing Brazilian public bond prices against Andima cached values...
        Natural settlementDays = 1;
        Real faceAmount = 1000.0;
        var today = new Date(6, June, 2007);
        var issueDate = new Date(1, January, 2007);

        // The tolerance is high because Andima truncate yields
        Real tolerance = 1.0e-4;

        // Reset evaluation date
        var context = new EvaluationContext { EvaluationDate = today };

        // NTN-F maturity dates
        Date[] maturityDates =
        [
            new(1, January, 2008),
            new(1, January, 2010),
            new(1, July, 2010),
            new(1, January, 2012),
            new(1, January, 2014),
            new(1, January, 2017),
        ];

        // Andima NTN-F yields
        Rate[] yields = [0.114614, 0.105726, 0.105328, 0.104283, 0.103218, 0.102948];

        // Andima NTN-F prices
        Rate[] prices = [1034.63031372, 1030.09919487, 1029.98307160, 1028.13585068, 1028.33383817, 1026.19716497];

        InterestRate[] couponRates =
        [
            new(0.1, new Thirty360(Thirty360Convention.BondBasis), Compounding.Compounded, Frequency.Annual),
        ];

        for (int bondIndex = 0; bondIndex < maturityDates.Length; bondIndex++)
        {
            var yield = new InterestRate(
                yields[bondIndex], new Business252(new Brazil()), Compounding.Compounded, Frequency.Annual);

            var schedule = new Schedule(
                new Date(1, January, 2007), maturityDates[bondIndex], new Period(Frequency.Semiannual),
                new Brazil(BrazilMarket.Settlement), BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted,
                DateGenerationRule.Backward, false, context);

            ImmutableArray<CashFlow> coupons = new FixedRateLeg(schedule)
                .WithNotionals(faceAmount)
                .WithCouponRates(couponRates)
                .WithPaymentAdjustment(BusinessDayConvention.Following)
                .ToLeg();

            var bond = new Bond(settlementDays, schedule.Calendar, issueDate, coupons);

            Real cachedPrice = prices[bondIndex];
            Real price = faceAmount
                * (BondFunctions.CleanPrice(
                    bond, yield.Rate, yield.DayCounter, yield.Compounding, yield.Frequency, context, today)
                    + bond.AccruedAmount(context, today)) / 100.0;

            Assert.False(
                Math.Abs(price - cachedPrice) > tolerance,
                $"failed to reproduce Andima cached price: calculated {price}, expected {cachedPrice}, "
                + $"error {price - cachedPrice}");
        }
    }

    // Adapted: no evaluation date is read, every call taking an explicit settlement date; the context is the test
    // runner's.
    [Fact]
    public void TestExCouponGilt()
    {
        // Testing ex-coupon UK Gilt price against market values...

        // UK Gilts have an exCouponDate 7 business days before the coupon
        // is due (see <http://www.dmo.gov.uk/index.aspx?page=Gilts/Gilt_Faq>).
        // On the exCouponDate the bond still trades cum-coupon so we use
        // 6 days below and UK calendar
        //
        // Output verified with Bloomberg:
        //
        // ISIN: GB0009997999
        // Issue Date: February 29th, 1996
        // Interest Accrue: February 29th, 1996
        // First Coupon: June 7th, 1996
        // Maturity: June 7th, 2021
        // coupon: 8
        // period: 6M
        //
        // Settlement date: May 29th, 2013
        // Test Price : 103
        // Accrued : 38021.97802
        // NPV : 106.8021978
        // Yield : 7.495180593
        // Yield->NPV : 106.8021978
        // Yield->NPV->Price : 103
        // Mod duration : 5.676044458
        // Convexity : 0.4215314859
        // PV 0.01 : 0.0606214023
        //
        // Settlement date: May 30th, 2013
        // Test Price : 103
        // Accrued : -1758.241758
        // NPV : 102.8241758
        // Yield : 7.496183543
        // Yield->NPV : 102.8241758
        // Yield->NPV->Price : 103
        // Mod duration : 5.892816328
        // Convexity : 0.4375621862
        // PV 0.01 : 0.06059239822
        //
        // Settlement date: May 31st, 2013
        // Test Price : 103
        // Accrued : -1538.461538
        // NPV : 102.8461538
        // Yield : 7.495987492
        // Yield->NPV : 102.8461539
        // Yield->NPV->Price : 103
        // Mod duration : 5.890186028
        // Convexity : 0.4372394381
        // PV 0.01 : 0.06057829784
        Calendar calendar = new UnitedKingdom();

        Natural settlementDays = 3;

        var issueDate = new Date(29, February, 1996);
        var startDate = new Date(29, February, 1996);
        var firstCouponDate = new Date(07, June, 1996);
        var maturityDate = new Date(07, June, 2021);

        Rate coupon = 0.08;

        Period tenor = 6 * TimeUnit.Months;
        Period exCouponPeriod = 6 * TimeUnit.Days;

        Compounding comp = Compounding.Compounded;
        Frequency freq = Frequency.Semiannual;

        var schedule = new Schedule(
            startDate, maturityDate, tenor, new NullCalendar(), BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, DateGenerationRule.Forward, true, RunnerContext, firstCouponDate);
        DayCounter dc = new ActualActual(ActualActualConvention.ISMA, schedule);

        var bond = new FixedRateBond(
            settlementDays, 100.0, schedule, [coupon], dc, BusinessDayConvention.Unadjusted, 100.0, issueDate,
            calendar, exCouponPeriod, calendar);

        ImmutableArray<CashFlow> leg = bond.Cashflows;

        ExCouponCase[] cases =
        [
            new(new Date(29, May, 2013), 103.0, 3.8021978, 106.8021978, 0.0749518, 5.6760445, 42.1531486),
            new(new Date(30, May, 2013), 103.0, -0.1758242, 102.8241758, 0.0749618, 5.8928163, 43.7562186),
            new(new Date(31, May, 2013), 103.0, -0.1538462, 102.8461538, 0.0749599, 5.8901860, 43.7239438),
        ];

        CheckExCouponCases(bond, leg, dc, comp, freq, cases, [1e-6, 1e-6, 1e-6, 1e-6, 1e-6, 1e-6, 1e-6]);
    }

    // Adapted: no evaluation date is read, every call taking an explicit settlement date; the context is the test
    // runner's.
    [Fact]
    public void TestExCouponAustralianBond()
    {
        // Testing ex-coupon Australian bond price against market values...

        // Australian Government Bonds have an exCouponDate 7 calendar
        // days before the coupon is due.  On the exCouponDate the bond
        // trades ex-coupon so we use 7 days below and NullCalendar.
        // AGB accrued interest is rounded to 3dp.
        //
        // Output verified with Bloomberg:
        //
        // ISIN: AU300TB01208
        // Issue Date: June 10th, 2004
        // Interest Accrue: February 15th, 2004
        // First Coupon: August 15th, 2004
        // Maturity: February 15th, 2017
        // coupon: 6
        // period: 6M
        //
        // Settlement date: August 7th, 2014
        // Test Price : 103
        // Accrued : 28670
        // NPV : 105.867
        // Yield : 4.723814867
        // Yield->NPV : 105.867
        // Yield->NPV->Price : 103
        // Mod duration : 2.262763296
        // Convexity : 0.0654870275
        // PV 0.01 : 0.02395519619
        //
        // Settlement date: August 8th, 2014
        // Test Price : 103
        // Accrued : -1160
        // NPV : 102.884
        // Yield : 4.72354833
        // Yield->NPV : 102.884
        // Yield->NPV->Price : 103
        // Mod duration : 2.325360055
        // Convexity : 0.06725307785
        // PV 0.01 : 0.02392423439
        //
        // Settlement date: August 11th, 2014
        // Test Price : 103
        // Accrued : -660
        // NPV : 102.934
        // Yield : 4.719277687
        // Yield->NPV : 102.934
        // Yield->NPV->Price : 103
        // Mod duration : 2.317320093
        // Convexity : 0.06684074058
        // PV 0.01 : 0.02385310264
        Calendar calendar = new Australia();

        Natural settlementDays = 3;

        var issueDate = new Date(10, June, 2004);
        var startDate = new Date(15, February, 2004);
        var firstCouponDate = new Date(15, August, 2004);
        var maturityDate = new Date(15, February, 2017);

        Rate coupon = 0.06;

        Period tenor = 6 * TimeUnit.Months;
        Period exCouponPeriod = 7 * TimeUnit.Days;

        Compounding comp = Compounding.Compounded;
        Frequency freq = Frequency.Semiannual;

        var schedule = new Schedule(
            startDate, maturityDate, tenor, new NullCalendar(), BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, DateGenerationRule.Forward, true, RunnerContext, firstCouponDate);
        DayCounter dc = new ActualActual(ActualActualConvention.ISMA, schedule);

        var bond = new FixedRateBond(
            settlementDays, 100.0, schedule, [coupon], dc, BusinessDayConvention.Unadjusted, 100.0, issueDate,
            calendar, exCouponPeriod, new NullCalendar());

        ImmutableArray<CashFlow> leg = bond.Cashflows;

        ExCouponCase[] cases =
        [
            new(new Date(7, August, 2014), 103.0, 2.8670, 105.867, 0.04723, 2.26276, 6.54870),
            new(new Date(8, August, 2014), 103.0, -0.1160, 102.884, 0.047235, 2.32536, 6.72531),
            new(new Date(11, August, 2014), 103.0, -0.0660, 102.934, 0.04719, 2.31732, 6.68407),
        ];

        CheckExCouponCases(bond, leg, dc, comp, freq, cases, [1e-3, 1e-3, 1e-5, 1e-5, 1e-4, 1e-3, 1e-3]);
    }

    // Test calculation of South African R2048 bond
    // This requires the use of the Schedule to be constructed
    // with a custom date vector
    [Fact]
    public void TestBondFromScheduleWithDateVector()
    {
        // Testing South African R2048 bond price using Schedule constructor with Date vector...

        // When pricing bond from Yield To Maturity, use NullCalendar()
        Calendar calendar = new NullCalendar();

        Natural settlementDays = 3;

        var issueDate = new Date(29, June, 2012);
        var today = new Date(7, September, 2015);
        Date evaluationDate = calendar.Adjust(today);
        Date settlementDate = calendar.Advance(evaluationDate, settlementDays * TimeUnit.Days);
        var context = new EvaluationContext { EvaluationDate = evaluationDate };

        // For the schedule to generate correctly for Feb-28's, make maturity date on Feb 29
        var maturityDate = new Date(29, February, 2048);

        Rate coupon = 0.0875;
        Compounding comp = Compounding.Compounded;
        Frequency freq = Frequency.Semiannual;

        Period tenor = 6 * TimeUnit.Months;
        Period exCouponPeriod = 10 * TimeUnit.Days;

        // Generate coupon dates for 31 Aug and end of Feb each year
        // For leap years, this will generate 29 Feb, but the bond
        // actually pays coupons on 28 Feb, regardsless of whether
        // it is a leap year or not.
        var schedule = new Schedule(
            issueDate, maturityDate, tenor, new NullCalendar(), BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, true, context);

        // Adjust the 29 Feb's to 28 Feb
        var dates = new List<Date>();
        for (Size i = 0; i < schedule.Size; ++i)
        {
            Date d = schedule.Date(i);
            if (d.Month == February && d.DayOfMonth == 29)
            {
                dates.Add(new Date(28, February, d.Year));
            }
            else
            {
                dates.Add(d);
            }
        }

        schedule = new Schedule(
            dates,
            schedule.Calendar,
            schedule.BusinessDayConvention,
            schedule.TerminationDateBusinessDayConvention(),
            schedule.Tenor(),
            schedule.Rule(),
            schedule.EndOfMonth(),
            schedule.IsRegular());
        DayCounter dc = new ActualActual(ActualActualConvention.Bond, schedule);
        var bond = new FixedRateBond(
            0, 100.0, schedule, [coupon], dc, BusinessDayConvention.Following, 100.0, issueDate, calendar,
            exCouponPeriod, calendar, BusinessDayConvention.Unadjusted, false);

        // Yield as quoted in market
        var yield = new InterestRate(0.09185, dc, comp, freq);

        Real calculatedPrice = BondFunctions.DirtyPrice(bond, yield, context, settlementDate);
        Real expectedPrice = 95.75706;
        Real tolerance = 1e-5;
        Assert.False(
            Math.Abs(calculatedPrice - expectedPrice) > tolerance,
            $"failed to reproduce R2048 dirty price: expected {expectedPrice}, calculated {calculatedPrice}");
    }

    // Adapted: the evaluation date the case sets (line 1500) is the context; the bonds' engine (lines 1508-1509) is
    // DiscountingBondEngine.Calculate, and their clean prices are read from its results.
    [Fact]
    public void TestFixedBondWithGivenDates()
    {
        // Testing fixed-coupon bond built on schedule with given dates...

        var vars = new CommonVars();

        var today = new Date(22, November, 2004);
        var context = new EvaluationContext { EvaluationDate = today };

        Natural settlementDays = 1;

        var discountCurve = new FlatForward(today, 0.03, new Actual360());

        Real tolerance = 1.0e-6;

        Real CleanPrice(Bond bond) => bond.CleanPrice(DiscountingBondEngine.Calculate(bond, context, discountCurve), context);

        // plain

        var sch1 = new Schedule(
            new Date(30, November, 2004), new Date(30, November, 2008), new Period(Frequency.Semiannual),
            new UnitedStates(UnitedStatesMarket.GovernmentBond), BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false, context);
        var bond1 = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch1, [0.02875], new ActualActual(ActualActualConvention.ISMA),
            BusinessDayConvention.ModifiedFollowing, 100.0, new Date(30, November, 2004));

        var sch1Copy = new Schedule(
            sch1.Dates, new UnitedStates(UnitedStatesMarket.GovernmentBond), BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, new Period(Frequency.Semiannual), DateGenerationRule.Backward, false,
            Enumerable.Repeat(true, sch1.Size - 1));
        var bond1Copy = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch1Copy, [0.02875], new ActualActual(ActualActualConvention.ISMA),
            BusinessDayConvention.ModifiedFollowing, 100.0, new Date(30, November, 2004));

        Real expected = CleanPrice(bond1);
        Real calculated = CleanPrice(bond1Copy);
        Assert.False(
            Math.Abs(expected - calculated) > tolerance,
            $"failed to reproduce cached price: calculated {calculated}, expected {expected}, error {expected - calculated}");

        // varying coupons

        Rate[] couponRates = [0.02875, 0.03, 0.03125, 0.0325];

        var bond2 = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch1, couponRates, new ActualActual(ActualActualConvention.ISMA),
            BusinessDayConvention.ModifiedFollowing, 100.0, new Date(30, November, 2004));

        var bond2Copy = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch1Copy, couponRates, new ActualActual(ActualActualConvention.ISMA),
            BusinessDayConvention.ModifiedFollowing, 100.0, new Date(30, November, 2004));

        expected = CleanPrice(bond2);
        calculated = CleanPrice(bond2Copy);
        Assert.False(
            Math.Abs(expected - calculated) > tolerance,
            $"failed to reproduce cached price: calculated {calculated}, expected {expected}, error {expected - calculated}");

        // stub date

        var sch3 = new Schedule(
            new Date(30, November, 2004), new Date(30, March, 2009), new Period(Frequency.Semiannual),
            new UnitedStates(UnitedStatesMarket.GovernmentBond), BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false, context, null,
            new Date(30, November, 2008));
        var bond3 = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch3, couponRates, new Actual360(), BusinessDayConvention.ModifiedFollowing,
            100.0, new Date(30, November, 2004));

        var sch3Copy = new Schedule(
            sch3.Dates, new UnitedStates(UnitedStatesMarket.GovernmentBond), BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, new Period(Frequency.Semiannual), DateGenerationRule.Backward, false,
            Enumerable.Repeat(true, sch3.Size - 1));
        var bond3Copy = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch3Copy, couponRates, new Actual360(),
            BusinessDayConvention.ModifiedFollowing, 100.0, new Date(30, November, 2004));

        expected = CleanPrice(bond3);
        calculated = CleanPrice(bond3Copy);
        Assert.False(
            Math.Abs(expected - calculated) > tolerance,
            $"failed to reproduce cached price: calculated {calculated}, expected {expected}, error {expected - calculated}");
    }

    // Adapted: the evaluation date the case sets (line 1620) is the context; the bond's engine (lines 1651-1652) is
    // RiskyBondEngine.Calculate, and its NPV and clean price are read from its results. QuantLib's notionals (line
    // 1637) are never used, and are left out.
    [Fact]
    public void TestRiskyBondWithGivenDates()
    {
        // Testing risky bond engine...

        var vars = new CommonVars();

        var today = new Date(22, November, 2005);
        var context = new EvaluationContext { EvaluationDate = today };

        // Probability Structure
        FlatHazardRate defaultProbability = FlatHazardRate.Create(0, new TARGET(), 0.1, new Actual360(), context);

        // Yield term structure
        var riskFree = new FlatForward(today, 0.02, new Actual360());
        var sch1 = new Schedule(
            new Date(30, November, 2004), new Date(30, November, 2008), new Period(Frequency.Semiannual),
            new UnitedStates(UnitedStatesMarket.GovernmentBond), BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false, context);

        // Create Bond
        Natural settlementDays = 1;

        Rate[] couponRates = [0.02875, 0.03, 0.03125, 0.0325];
        Real recoveryRate = 0.4;

        var bond = new FixedRateBond(
            settlementDays, vars.FaceAmount, sch1, couponRates, new ActualActual(ActualActualConvention.ISMA),
            BusinessDayConvention.ModifiedFollowing, 100.0, new Date(20, November, 2004));

        // Create Engine
        BondResults results = RiskyBondEngine.Calculate(bond, context, defaultProbability, recoveryRate, riskFree);

        // Calculate and validate NPV and price
        Real expected = 888458.819055;
        Real calculated = results.NPV!.Value;
        Real tolerance = 1.0e-6;
        Assert.False(
            Math.Abs(expected - calculated) > tolerance,
            "Failed to reproduce risky bond NPV:\n"
            + $"    calculated: {calculated:F6}\n    expected:   {expected:F6}\n    error:      {expected - calculated:F6}");

        expected = 87.407883;
        calculated = bond.CleanPrice(results, context);
        Assert.False(
            Math.Abs(expected - calculated) > tolerance,
            "Failed to reproduce risky bond price:\n"
            + $"    calculated: {calculated:F6}\n    expected:   {expected:F6}\n    error:      {expected - calculated:F6}");
    }

    // Adapted: the bond's engine (lines 1709-1710) is DiscountingBondEngine.Calculate under the case's evaluation date
    // (line 1684); its clean price (line 1712) is read from its results.
    [Fact]
    public void TestFixedRateBondWithArbitrarySchedule()
    {
        // Testing fixed-rate bond with arbitrary schedule...
        Calendar calendar = new NullCalendar();

        Natural settlementDays = 3;

        var today = new Date(1, January, 2019);
        var context = new EvaluationContext { EvaluationDate = today };

        // For the schedule to generate correctly for Feb-28's, make maturity date on Feb 29
        Date[] dates =
        [
            new(1, February, 2019),
            new(7, February, 2019),
            new(1, April, 2019),
            new(27, May, 2019),
        ];

        var schedule = new Schedule(dates, calendar, BusinessDayConvention.Unadjusted);

        Rate coupon = 0.01;
        DayCounter dc = new Actual365Fixed();

        var bond = new FixedRateBond(
            settlementDays, 100.0, schedule, [coupon], dc, BusinessDayConvention.Following, 100.0);

        Assert.True(bond.Frequency == Frequency.NoFrequency, $"unexpected frequency: {bond.Frequency}");

        var discountCurve = new FlatForward(today, 0.03, new Actual360());
        BondResults results = DiscountingBondEngine.Calculate(bond, context, discountCurve);

        _ = bond.CleanPrice(results, context);
    }

    // Adapted: the evaluation date the case sets (line 1720) is the context.
    [Fact]
    public void TestThirty360BondWithSettlementOn31st()
    {
        // Testing Thirty/360 bond with settlement on 31st of the month...

        // cusip 3130A0X70, data is from Bloomberg
        var context = new EvaluationContext { EvaluationDate = new Date(28, July, 2017) };

        var datedDate = new Date(13, February, 2014);
        var settlement = new Date(31, July, 2017);
        var maturity = new Date(13, August, 2018);

        DayCounter dayCounter = new Thirty360(Thirty360Convention.USA);
        Compounding compounding = Compounding.Compounded;

        var fixedBondSchedule = new Schedule(
            datedDate, maturity, new Period(Frequency.Semiannual), new UnitedStates(UnitedStatesMarket.GovernmentBond),
            BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Forward, false,
            context);

        var fixedRateBond = new FixedRateBond(
            1, 100, fixedBondSchedule, [0.015], dayCounter, BusinessDayConvention.Unadjusted, 100.0);

        var cleanPrice = new BondPrice(100.0, BondPriceType.Clean);

        Real yield = BondFunctions.Yield(
            fixedRateBond, cleanPrice, dayCounter, compounding, Frequency.Semiannual, context, settlement);
        AssertClose("yield", settlement, yield, 0.015, 1e-4);

        Real duration = BondFunctions.Duration(
            fixedRateBond, new InterestRate(yield, dayCounter, compounding, Frequency.Semiannual), context,
            DurationType.Macaulay, settlement);
        AssertClose("duration", settlement, duration, 1.022, 1e-3);

        Real convexity = BondFunctions.Convexity(
            fixedRateBond, new InterestRate(yield, dayCounter, compounding, Frequency.Semiannual), context,
            settlement) / 100;
        AssertClose("convexity", settlement, convexity, 0.015, 1e-3);

        Real accrued = BondFunctions.AccruedAmount(fixedRateBond, context, settlement);
        AssertClose("accrued", settlement, accrued, 0.7, 1e-6);
    }

    // Adapted: the evaluation date the case sets (line 1766) is the context; QuantLib's null settlement date (line
    // 1806) is null.
    [Fact]
    public void TestBasisPointValue()
    {
        // Testing consistency of bond basisPointValue and yieldValueBasisPoint calculations...

        var vars = new CommonVars();

        var today = new Date(29, January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };

        var datedDate = new Date(15, November, 2023);
        var maturity = new Date(15, August, 2033);

        DayCounter dayCounter = new Thirty360(Thirty360Convention.USA);
        Compounding compounding = Compounding.Compounded;
        Frequency frequency = Frequency.Semiannual;
        var period = new Period(frequency);

        var fixedBondSchedule = new Schedule(
            datedDate, maturity, period, new UnitedStates(UnitedStatesMarket.GovernmentBond),
            BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Forward, false,
            context);

        var fixedRateBond = new FixedRateBond(
            1, vars.FaceAmount, fixedBondSchedule, [0.045], dayCounter, BusinessDayConvention.Unadjusted, 100.0);

        Date defaultSettlement = fixedRateBond.SettlementDate(context);
        var cleanPrice = new BondPrice(102.890625, BondPriceType.Clean);

        Real tolerance = 1e-6;
        Real bpvTolerance = 1e-3;

        Real yield = BondFunctions.Yield(fixedRateBond, cleanPrice, dayCounter, compounding, frequency, context);
        AssertClose("yield", defaultSettlement, yield, 0.041301, tolerance);

        (Date? Settlement, Real Bpv, Real Yvbp)[] cases =
        [
            (null, -795.098043, -0.0012571287),
            (defaultSettlement, -795.098043, -0.0012571287),
            (new Date(12, February, 2024), -792.789400, -0.0012607913),
        ];
        foreach ((Date? settlement, Real bpv, Real yvbp) in cases)
        {
            Real bvp1 = BondFunctions.BasisPointValue(
                fixedRateBond, yield, dayCounter, compounding, frequency, context, settlement);
            AssertClose("basisPointValue from yield", settlement, bvp1, bpv, tolerance);
            Real bvp2 = BondFunctions.BasisPointValue(
                fixedRateBond, new InterestRate(yield, dayCounter, compounding, frequency), context, settlement);
            AssertClose("basisPointValue from InterestRate", settlement, bvp2, bpv, tolerance);

            Real cleanPrice1 = BondFunctions.CleanPrice(
                fixedRateBond, yield, dayCounter, compounding, frequency, context, settlement);
            Real cleanPrice2 = BondFunctions.CleanPrice(
                fixedRateBond, yield + 0.0001, dayCounter, compounding, frequency, context, settlement);
            Real npvChange = (cleanPrice2 - cleanPrice1) * vars.FaceAmount / 100.0;
            AssertClose("basisPointValue against 1bp reprice", settlement, bvp1, npvChange, bpvTolerance);

            Real yvbp1 = BondFunctions.YieldValueBasisPoint(
                fixedRateBond, yield, dayCounter, compounding, frequency, context, settlement);
            yvbp1 *= vars.FaceAmount;
            AssertClose("yieldValueBasisPoint from yield", settlement, yvbp1, yvbp, tolerance);
            Real yvbp2 = BondFunctions.YieldValueBasisPoint(
                fixedRateBond, new InterestRate(yield, dayCounter, compounding, frequency), context, settlement);
            yvbp2 *= vars.FaceAmount;
            AssertClose("yieldValueBasisPoint from InterestRate", settlement, yvbp2, yvbp, tolerance);
        }
    }

    // Adapted: the evaluation date the case sets (line 1842) is the context the bonds' coupons are built with.
    [Fact]
    public void TestFixingConvention()
    {
        // Testing floating-rate bond fixing convention...

        // June 22, 2024 is a Saturday. With an unadjusted quarterly schedule
        // and fixingDays=0, the fixing date depends on the convention:
        //   Preceding  -> Friday June 21, 2024
        //   Following  -> Monday June 24, 2024
        var today = new Date(1, January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };

        Calendar calendar = new Australia();
        Natural settlementDays = 0;
        Real faceAmount = 100.0;

        var schedule = new Schedule(
            new Date(22, March, 2024), new Date(22, December, 2024), new Period(Frequency.Quarterly), calendar,
            BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Forward, false,
            context);

        var curve = new FlatForward(today, 0.05, new Actual365Fixed());
        var index = new AUDLibor(3 * TimeUnit.Months, curve);

        // Default (Preceding) convention
        var bondPreceding = new FloatingRateBond(
            settlementDays, faceAmount, schedule, index, new Actual365Fixed(), context,
            BusinessDayConvention.Following, 0);

        // Following convention
        var bondFollowing = new FloatingRateBond(
            settlementDays, faceAmount, schedule, index, new Actual365Fixed(), context,
            BusinessDayConvention.Following, 0, [1.0], [0.0], [], [], false, 100.0, null, default, null,
            BusinessDayConvention.Unadjusted, false, BusinessDayConvention.Following);

        // Extract the coupon whose accrual start date is June 22, 2024
        static FloatingRateCoupon? FindCouponStarting(FloatingRateBond bond, Date d)
        {
            foreach (CashFlow cf in bond.Cashflows)
            {
                if (cf is FloatingRateCoupon coupon && coupon.AccrualStartDate == d)
                {
                    return coupon;
                }
            }

            return null;
        }

        var june22 = new Date(22, June, 2024); // Saturday
        FloatingRateCoupon? couponP = FindCouponStarting(bondPreceding, june22);
        FloatingRateCoupon? couponF = FindCouponStarting(bondFollowing, june22);

        Assert.NotNull(couponP);
        Assert.NotNull(couponF);

        var expectedPreceding = new Date(21, June, 2024); // Friday
        var expectedFollowing = new Date(24, June, 2024); // Monday

        Assert.Equal(expectedPreceding, couponP.FixingDate());
        Assert.Equal(expectedFollowing, couponF.FixingDate());
    }

    [Fact]
    public void TestYieldInFinalCouponPeriod()
    {
        // Testing that yield conventions agree in the final coupon period...

        // The bond settles inside its final coupon period, so that the only
        // remaining payments are the last coupon and the redemption, both
        // falling at maturity.  With a single period left, "simple in the first
        // period" (SimpleThenCompounded) and "simple in the last period"
        // (CompoundedThenSimple) describe the same period, so both conventions
        // must return the same simple yield.  Excel and Bloomberg agree on
        // 1.349867886% for this bond.
        var vars = new FinalCouponPeriodVars();

        var bond = new FixedRateBond(
            0, 100.0, vars.Schedule, [vars.CouponRate], vars.DayCounter, BusinessDayConvention.Unadjusted, 100.0,
            vars.Issue);

        Real yieldSTC = BondFunctions.Yield(
            bond, vars.CleanPrice, vars.DayCounter, Compounding.SimpleThenCompounded, Frequency.Semiannual,
            RunnerContext, vars.Settlement);
        AssertClose("simple-then-compounded yield", vars.Settlement, yieldSTC, 0.013498678862, 1e-9);

        Real yieldCTS = BondFunctions.Yield(
            bond, vars.CleanPrice, vars.DayCounter, Compounding.CompoundedThenSimple, Frequency.Semiannual,
            RunnerContext, vars.Settlement);
        AssertClose("compounded-then-simple yield", vars.Settlement, yieldCTS, 0.013498678862, 1e-9);
    }

    [Fact]
    public void TestYieldInFinalCouponPeriodWithLateRedemption()
    {
        // Testing yield conventions in and after a final coupon period with a late redemption...

        // The bond of the previous test, except that the redemption is paid
        // three days after the final coupon rather than alongside it, so that
        // the two remaining payments no longer share a date.
        //
        // SimpleThenCompounded discounts the interval to the final coupon
        // simply and compounds over the three-day gap.  CompoundedThenSimple
        // compounds to the final coupon and applies its simple rule to the
        // terminal three-day interval instead.
        var vars = new FinalCouponPeriodVars();

        ImmutableArray<CashFlow> leg = new FixedRateLeg(vars.Schedule)
            .WithNotionals(100.0)
            .WithCouponRates(vars.CouponRate, vars.DayCounter)
            .WithPaymentAdjustment(BusinessDayConvention.Unadjusted)
            .ToLeg();
        Date redemptionDate = vars.Maturity + 3;
        leg = leg.Add(new Redemption(100.0, redemptionDate));

        var bond = new Bond(0, new NullCalendar(), 100.0, redemptionDate, vars.Issue, leg);

        Real yieldSTC = BondFunctions.Yield(
            bond, vars.CleanPrice, vars.DayCounter, Compounding.SimpleThenCompounded, Frequency.Semiannual,
            RunnerContext, vars.Settlement);
        AssertClose("simple-then-compounded yield", vars.Settlement, yieldSTC, 0.013059382713, 1e-9);

        Real yieldCTS = BondFunctions.Yield(
            bond, vars.CleanPrice, vars.DayCounter, Compounding.CompoundedThenSimple, Frequency.Semiannual,
            RunnerContext, vars.Settlement);

        // Solving the convention-correct closed form independently,
        //   dirtyPrice = discountCompounded(t1) *
        //                (coupon + 100 * discountSimple(t2)),
        // where t1 ends at the final coupon and t2 is the three-day redemption
        // delay, gives 1.3079316928%.
        AssertClose("compounded-then-simple yield", vars.Settlement, yieldCTS, 0.013079316928, 1e-9);

        // The delayed redemption leaves a three-day terminal interval, where the
        // simple and the compounded rule genuinely differ.  The long-first-coupon
        // bond checked below cannot cover this: its final interval is a whole
        // semiannual period, on which the two rules and their derivatives agree.
        CheckSensitivities(
            bond, vars.DayCounter, vars.Settlement, yieldSTC, Compounding.SimpleThenCompounded, "simple-then-compounded");
        CheckSensitivities(
            bond, vars.DayCounter, vars.Settlement, yieldCTS, Compounding.CompoundedThenSimple, "compounded-then-simple");

        // Settling after the final coupon leaves the redemption as the only
        // remaining flow.  Its interval is both the first and the last, so both
        // conventions apply the simple rule and agree again on the same bond.
        // Two days from a par
        // redemption a clean 99.99 is the simple yield 365/2 * 0.01/99.99.
        // The 365 denominator comes from the faked one-year quasi-period ending
        // on the redemption date, and the clean quote is the discounted amount
        // because accrued interest is zero once the next flow is the redemption.
        var afterFinalCoupon = new Date(2, June, 2026);
        var latePrice = new BondPrice(99.99, BondPriceType.Clean);

        Real lateSTC = BondFunctions.Yield(
            bond, latePrice, vars.DayCounter, Compounding.SimpleThenCompounded, Frequency.Semiannual, RunnerContext,
            afterFinalCoupon);
        AssertClose("simple-then-compounded yield after the final coupon", afterFinalCoupon, lateSTC, 0.018251825183, 1e-9);

        Real lateCTS = BondFunctions.Yield(
            bond, latePrice, vars.DayCounter, Compounding.CompoundedThenSimple, Frequency.Semiannual, RunnerContext,
            afterFinalCoupon);
        AssertClose(
            "compounded-then-simple yield with one interval remaining", afterFinalCoupon, lateCTS, lateSTC, 1e-12);
    }

    [Fact]
    public void TestYieldWithLongFirstCoupon()
    {
        // Testing yield conventions on a bond with a long first coupon...

        // The bond settles on its dated date, so the first coupon spans a full
        // year, that is w = 2 periods at m = 2, and accrued interest is zero.
        // At a yield of y = 5% the flows remaining at the first coupon date
        // come to exactly 105, so each convention collapses to one discounting
        // step over the stub, with a closed form:
        //   US Treasury (simple over the stub):    105 / (1 + w*y/m)  = 100.0
        //   US street (compounded over the stub):  105 * (1+y/m)^(-w) = 99.9405116002
        var issue = new Date(1, January, 2026);
        Date settlement = issue;

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISMA);

        var schedule = new Schedule(LongFirstCouponDates());

        var bond = new FixedRateBond(
            0, 100.0, schedule, [0.05], dayCounter, BusinessDayConvention.Unadjusted, 100.0, issue);

        CheckLongFirstCouponBond(
            bond,
            dayCounter,
            settlement,
            0.0, // accrued
            100.0, // clean price STC
            99.9405116002); // clean price CTS

        // The second settlement is 90 days into the long first coupon.  The
        // remaining time to the first coupon date is t = 275/365, so w = 2t
        // ~ 1.5068 quasi-periods is fractional.  The same closed forms give
        // the dirty prices
        //   simple over the stub:      105 / (1 + w*y/m)  = 101.1881188119
        //   compounded over the stub:  105 * (1+y/m)^(-w) = 101.1649450226
        // with accrued interest of 100 * 5% * 90/365 = 1.2328767123.
        // Note: the schedule is built from bare dates, so ActualActual(ISMA)
        // has no reference periods for the long first coupon and falls back
        // to Act/365-like fractions; the pinned values are the closed forms
        // evaluated at these fractions.  The next test covers the same bond
        // with the ICMA quasi-period fractions.
        var midSettlement = new Date(1, April, 2026);

        CheckLongFirstCouponBond(
            bond,
            dayCounter,
            midSettlement,
            1.2328767123, // accrued
            99.9552420996, // clean price STC
            99.9320683103); // clean price CTS
    }

    [Fact]
    public void TestYieldWithLongFirstCouponAndQuasiPeriods()
    {
        // Testing yield conventions on a long first coupon with quasi-coupon periods...

        // Same bond as above, but with the schedule tagged with its tenor and
        // its first period marked irregular.  ActualActual(ISMA) then has the
        // quasi-coupon periods of the long first coupon (January 1st 2026 to
        // July 1st 2026 to January 1st 2027) and returns true ICMA fractions
        // rather than the Act/365-like fallback of the previous test.
        var issue = new Date(1, January, 2026);
        var settlement = new Date(1, April, 2026);

        var schedule = new Schedule(
            LongFirstCouponDates(), new NullCalendar(), BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, new Period(Frequency.Semiannual), DateGenerationRule.Backward, false,
            [false, true, true, true, true]);

        DayCounter dayCounter = new ActualActual(ActualActualConvention.ISMA, schedule);

        var bond = new FixedRateBond(
            0, 100.0, schedule, [0.05], dayCounter, BusinessDayConvention.Unadjusted, 100.0, issue);

        // 90 of the 181 days of the first quasi-coupon period have run, so
        // accrued interest is 100 * 5% * 90/362 = 1.2430939227 and the time
        // remaining to the first coupon date is t = 1 - 90/362, that is
        // w = 2t ~ 1.5028 quasi-periods rather than the 1.5068 of the
        // fallback fractions.  The flows at and after the first coupon date
        // still come to exactly 105 at a 5% yield, so the closed forms give
        // the dirty prices
        //   US Treasury (simple over the stub):   105 / (1 + w*y/m)  = 101.1980830671
        //   US street (compounded over the stub): 105 * (1+y/m)^(-w) = 101.1751546838
        CheckLongFirstCouponBond(
            bond,
            dayCounter,
            settlement,
            1.2430939227, // accrued
            99.9549891444, // clean price STC
            99.9320607612); // clean price CTS
    }

    // The context of the test runner, which sets the evaluation date globally (quantlibglobalfixture.cpp).
    private static EvaluationContext RunnerContext { get; } =
        new() { EvaluationDate = new Date(16, September, 2015) };

    private static void AssertClose(string name, Date? settlement, Real calculated, Real expected, Real tolerance) =>
        Assert.False(
            Math.Abs(calculated - expected) > tolerance,
            $"Failed to reproduce {name} at {settlement?.ToString() ?? "null date"}: calculated {calculated:R}, "
            + $"expected {expected:R}");

    private static void CheckValue(Real value, Real expectedValue, Real tolerance, string msg) =>
        Assert.False(
            Math.Abs(value - expectedValue) > tolerance,
            $"{msg} calculated {value:R}, expected {expectedValue:R}, tolerance {tolerance}, error "
            + $"{value - expectedValue}");

    // The ex-coupon cases' checks, with their tolerances in order: accrued amount, NPV, yield, duration, convexity,
    // NPV from yield and price from yield.
    private static void CheckExCouponCases(
        Bond bond,
        ImmutableArray<CashFlow> leg,
        DayCounter dc,
        Compounding comp,
        Frequency freq,
        ExCouponCase[] cases,
        Real[] tolerances)
    {
        EvaluationContext context = RunnerContext;
        foreach (ExCouponCase i in cases)
        {
            Real accrued = bond.AccruedAmount(context, i.SettlementDate);
            AssertClose("accrued amount", i.SettlementDate, accrued, i.AccruedAmount, tolerances[0]);

            Real npv = i.TestPrice + accrued;
            AssertClose("NPV", i.SettlementDate, npv, i.Npv, tolerances[1]);

            Rate yield = CashFlows.Yield(leg, npv, dc, comp, freq, context, false, i.SettlementDate);
            AssertClose("yield", i.SettlementDate, yield, i.Yield, tolerances[2]);

            Real duration = CashFlows.Duration(
                leg, yield, dc, comp, freq, DurationType.Modified, context, false, i.SettlementDate);
            AssertClose("duration", i.SettlementDate, duration, i.Duration, tolerances[3]);

            Real convexity = CashFlows.Convexity(leg, yield, dc, comp, freq, context, false, i.SettlementDate);
            AssertClose("convexity", i.SettlementDate, convexity, i.Convexity, tolerances[4]);

            Real calcnpv = CashFlows.Npv(leg, yield, dc, comp, freq, context, false, i.SettlementDate);
            AssertClose("NPV from yield", i.SettlementDate, calcnpv, i.Npv, tolerances[5]);

            Real calcprice = calcnpv - accrued;
            AssertClose("price from yield", i.SettlementDate, calcprice, i.TestPrice, tolerances[6]);
        }
    }

    // Checks the public risk measures against independent central finite
    // differences of the same dirty-price function.
    private static void CheckSensitivities(
        Bond bond,
        DayCounter dayCounter,
        Date settlement,
        Rate rate,
        Compounding compounding,
        string convention)
    {
        const Rate shift = 1.0e-4;
        Real DirtyPrice(Rate yield) => BondFunctions.DirtyPrice(
            bond, new InterestRate(yield, dayCounter, compounding, Frequency.Semiannual), RunnerContext, settlement);

        Real price = DirtyPrice(rate);
        Real priceUp = DirtyPrice(rate + shift);
        Real priceDown = DirtyPrice(rate - shift);
        Real expectedDuration = -(priceUp - priceDown) / (2.0 * shift * price);
        Real expectedConvexity = (priceUp - (2.0 * price) + priceDown) / (shift * shift * price);

        var yield = new InterestRate(rate, dayCounter, compounding, Frequency.Semiannual);
        Real duration = BondFunctions.Duration(bond, yield, RunnerContext, DurationType.Modified, settlement);
        AssertClose(convention + " modified duration", settlement, duration, expectedDuration, 1e-7);

        Real convexity = BondFunctions.Convexity(bond, yield, RunnerContext, settlement);
        AssertClose(convention + " convexity", settlement, convexity, expectedConvexity, 1e-5);
    }

    // The dates of the bond used by the long-first-coupon tests below: a first
    // coupon running a full year from January 1st 2026, then semiannual coupons
    // out to maturity.  The two tests differ only in how the schedule built
    // from these dates is tagged.
    private static Date[] LongFirstCouponDates() =>
    [
        new(1, January, 2026), new(1, January, 2027), new(1, July, 2027),
        new(1, January, 2028), new(1, July, 2028), new(1, January, 2029),
    ];

    // Checks the accrued interest, the clean price and the risk measures under
    // each yield convention and the round trip back to the 5% yield from those
    // same prices.  The solver guess is kept off the 5% root so that it has to
    // iterate to it.
    private static void CheckLongFirstCouponBond(
        Bond bond,
        DayCounter dayCounter,
        Date settlement,
        Real expectedAccrued,
        Real expectedPriceSTC,
        Real expectedPriceCTS)
    {
        EvaluationContext context = RunnerContext;
        Real accrued = BondFunctions.AccruedAmount(bond, context, settlement);
        AssertClose("accrued interest", settlement, accrued, expectedAccrued, 1e-8);

        Real priceSTC = BondFunctions.CleanPrice(
            bond, new InterestRate(0.05, dayCounter, Compounding.SimpleThenCompounded, Frequency.Semiannual), context,
            settlement);
        AssertClose("simple-then-compounded price", settlement, priceSTC, expectedPriceSTC, 1e-8);

        Real priceCTS = BondFunctions.CleanPrice(
            bond, new InterestRate(0.05, dayCounter, Compounding.CompoundedThenSimple, Frequency.Semiannual), context,
            settlement);
        AssertClose("compounded-then-simple price", settlement, priceCTS, expectedPriceCTS, 1e-8);

        Real yieldSTC = BondFunctions.Yield(
            bond, new BondPrice(expectedPriceSTC, BondPriceType.Clean), dayCounter, Compounding.SimpleThenCompounded,
            Frequency.Semiannual, context, settlement, 1e-10, 100, 0.02);
        AssertClose("simple-then-compounded yield", settlement, yieldSTC, 0.05, 1e-9);

        Real yieldCTS = BondFunctions.Yield(
            bond, new BondPrice(expectedPriceCTS, BondPriceType.Clean), dayCounter, Compounding.CompoundedThenSimple,
            Frequency.Semiannual, context, settlement, 1e-10, 100, 0.02);
        AssertClose("compounded-then-simple yield", settlement, yieldCTS, 0.05, 1e-9);

        CheckSensitivities(bond, dayCounter, settlement, 0.05, Compounding.SimpleThenCompounded, "simple-then-compounded");
        CheckSensitivities(bond, dayCounter, settlement, 0.05, Compounding.CompoundedThenSimple, "compounded-then-simple");
    }

    private readonly record struct ExCouponCase(
        Date SettlementDate,
        Real TestPrice,
        Real AccruedAmount,
        Real Npv,
        Rate Yield,
        Real Duration,
        Real Convexity);

    private sealed class CommonVars
    {
        // setup
        public CommonVars()
        {
            Calendar = new TARGET();
            Today = Calendar.Adjust(Date.TodaysDate());
            Context = new EvaluationContext { EvaluationDate = Today };
            FaceAmount = 1000000.0;
        }

        // common data
        public Calendar Calendar { get; }

        public Date Today { get; }

        public EvaluationContext Context { get; }

        public Real FaceAmount { get; }

        public Schedule MakeSchedule(Date dated, Date maturity, Frequency frequency, BusinessDayConvention convention) =>
            new(dated, maturity, new Period(frequency), Calendar, convention, convention, DateGenerationRule.Backward,
                false, Context);
    }

    // The bond shared by the two final-coupon-period tests below: a ten-year
    // 2.125% semiannual bond whose last coupon falls on June 1st 2026.  The
    // tests differ in the redemption date, and the second test adds a settlement
    // after the final coupon with its own quote.
    private sealed class FinalCouponPeriodVars
    {
        public FinalCouponPeriodVars()
        {
            Issue = new Date(1, June, 2016);
            Maturity = new Date(1, June, 2026);
            Settlement = new Date(5, March, 2026);
            DayCounter = new ActualActual(ActualActualConvention.ISMA);
            Schedule = new Schedule(
                Issue, Maturity, new Period(Frequency.Semiannual), new NullCalendar(), BusinessDayConvention.Unadjusted,
                BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false, RunnerContext);
            CleanPrice = new BondPrice(100.185, BondPriceType.Clean);
        }

        public Date Issue { get; }

        public Date Maturity { get; }

        public Date Settlement { get; }

        public Rate CouponRate { get; } = 0.02125;

        public DayCounter DayCounter { get; }

        public Schedule Schedule { get; }

        public BondPrice CleanPrice { get; }
    }
}
