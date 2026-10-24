// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/assetswap.cpp @ v1.44
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

// Adapted throughout: the fixture's relinkable handle is the curve itself, built first, and its evaluation date and the
// fixings each case adds, in its order, are its context; a bond's engine is the static DiscountingBondEngine.Calculate
// and an asset swap's DiscountingSwapEngine.Calculate on the curve, whose asset swap overload derives the fair values;
// setCouponPricer returns the bond with its pricers set. The eight bonds every case builds, alike in each, are built by
// one method each, as their specialized classes and as generic bonds on the same legs; QuantLib's empty schedule is a
// null one, the default floating schedule.
[Trait("Oracle", "QuantLib")]
public class AssetSwapTests
{
    private const Natural SettlementDays = 3;
    private const Natural FixingDays = 2;
    private const bool InArrearsFlag = false;

    private static readonly Calendar BondCalendar = new TARGET();

    [Fact]
    public void TestConsistency()
    {
        // Testing consistency between fair price and fair spread...
        var vars = new CommonVars();

        // Fixed Underlying bond (Isin: DE0001135275 DBR 4 01/04/37)
        // maturity doesn't occur on a business day
        var bondSchedule = new Schedule(
            new Date(4, January, 2005),
            new Date(4, January, 2037),
            new Period(Frequency.Annual),
            BondCalendar,
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            false,
            vars.Context);
        Bond bond = new FixedRateBond(
            SettlementDays,
            vars.FaceAmount,
            bondSchedule,
            [0.04],
            new ActualActual(ActualActualConvention.ISDA),
            BusinessDayConvention.Following,
            100.0,
            new Date(4, January, 2005));

        bool payFixedRate = true;
        Real bondPrice = 95.0;

        bool isPar = true;
        AssetSwap parAssetSwap = vars.AssetSwap(payFixedRate, bond, bondPrice, vars.IborIndex, vars.Spread, isPar);

        Date bondSettlement = bond.SettlementDate(vars.Context);
        Func<AssetSwap, AssetSwapResults> swapEngine = s => DiscountingSwapEngine.Calculate(
            s, vars.Context, vars.TermStructure, true, bondSettlement, vars.Context.EvaluationDate);

        AssetSwapResults parResults = swapEngine(parAssetSwap);
        Real fairCleanPrice = AssetSwap.FairCleanPrice(parResults);
        Spread fairSpread = AssetSwap.FairSpread(parResults);

        Real tolerance = 1.0e-13;

        CheckFairValues("par", vars.AssetSwap(payFixedRate, bond, fairCleanPrice, vars.IborIndex, vars.Spread, isPar), swapEngine, fairCleanPrice, vars.Spread, tolerance);
        CheckFairValues("par", vars.AssetSwap(payFixedRate, bond, bondPrice, vars.IborIndex, fairSpread, isPar), swapEngine, bondPrice, fairSpread, tolerance);

        // let's change the npv date
        swapEngine = s => DiscountingSwapEngine.Calculate(
            s, vars.Context, vars.TermStructure, true, bondSettlement, bondSettlement);

        parResults = swapEngine(parAssetSwap);

        // fair clean price and fair spread should not change
        Assert.False(
            Math.Abs(AssetSwap.FairCleanPrice(parResults) - fairCleanPrice) > tolerance,
            $"par asset swap fair clean price changed with NpvDate: expected {fairCleanPrice}, "
            + $"fair clean price {AssetSwap.FairCleanPrice(parResults)}, tolerance {tolerance}");
        Assert.False(
            Math.Abs(AssetSwap.FairSpread(parResults) - fairSpread) > tolerance,
            $"par asset swap fair spread changed with NpvDate: expected {fairSpread}, "
            + $"fair spread {AssetSwap.FairSpread(parResults)}, tolerance {tolerance}");

        CheckFairValues("par", vars.AssetSwap(payFixedRate, bond, fairCleanPrice, vars.IborIndex, vars.Spread, isPar), swapEngine, fairCleanPrice, vars.Spread, tolerance);
        CheckFairValues("par", vars.AssetSwap(payFixedRate, bond, bondPrice, vars.IborIndex, fairSpread, isPar), swapEngine, bondPrice, fairSpread, tolerance);

        // using overnight index
        var overnight = new Estr(vars.TermStructure);
        var overnightSchedule = new Schedule(
            bondSettlement,
            bond.MaturityDate(),
            new Period(6, TimeUnit.Months),
            overnight.FixingCalendar,
            overnight.BusinessDayConvention,
            overnight.BusinessDayConvention,
            DateGenerationRule.Backward,
            false,
            vars.Context);

        parAssetSwap = vars.AssetSwap(payFixedRate, bond, bondPrice, overnight, vars.Spread, isPar, overnightSchedule);

        swapEngine = s => DiscountingSwapEngine.Calculate(
            s, vars.Context, vars.TermStructure, true, bondSettlement, vars.Context.EvaluationDate);

        parResults = swapEngine(parAssetSwap);
        fairCleanPrice = AssetSwap.FairCleanPrice(parResults);
        fairSpread = AssetSwap.FairSpread(parResults);

        CheckFairValues("par", vars.AssetSwap(payFixedRate, bond, fairCleanPrice, overnight, vars.Spread, isPar, overnightSchedule), swapEngine, fairCleanPrice, vars.Spread, tolerance);
        CheckFairValues("par", vars.AssetSwap(payFixedRate, bond, bondPrice, overnight, fairSpread, isPar, overnightSchedule), swapEngine, bondPrice, fairSpread, tolerance);

        // now market asset swap
        isPar = false;
        AssetSwap mktAssetSwap = vars.AssetSwap(payFixedRate, bond, bondPrice, vars.IborIndex, vars.Spread, isPar);

        swapEngine = s => DiscountingSwapEngine.Calculate(
            s, vars.Context, vars.TermStructure, true, bondSettlement, vars.Context.EvaluationDate);

        AssetSwapResults mktResults = swapEngine(mktAssetSwap);
        fairCleanPrice = AssetSwap.FairCleanPrice(mktResults);
        fairSpread = AssetSwap.FairSpread(mktResults);

        CheckFairValues("market", vars.AssetSwap(payFixedRate, bond, fairCleanPrice, vars.IborIndex, vars.Spread, isPar), swapEngine, fairCleanPrice, vars.Spread, tolerance);
        CheckFairValues("market", vars.AssetSwap(payFixedRate, bond, bondPrice, vars.IborIndex, fairSpread, isPar), swapEngine, bondPrice, fairSpread, tolerance);

        // let's change the npv date
        swapEngine = s => DiscountingSwapEngine.Calculate(
            s, vars.Context, vars.TermStructure, true, bondSettlement, bondSettlement);

        mktResults = swapEngine(mktAssetSwap);

        // fair clean price and fair spread should not change
        Assert.False(
            Math.Abs(AssetSwap.FairCleanPrice(mktResults) - fairCleanPrice) > tolerance,
            $"market asset swap fair clean price changed with NpvDate: expected {fairCleanPrice}, "
            + $"fair clean price {AssetSwap.FairCleanPrice(mktResults)}, tolerance {tolerance}");
        Assert.False(
            Math.Abs(AssetSwap.FairSpread(mktResults) - fairSpread) > tolerance,
            $"market asset swap fair spread changed with NpvDate: expected {fairSpread}, "
            + $"fair spread {AssetSwap.FairSpread(mktResults)}, tolerance {tolerance}");

        CheckFairValues("market", vars.AssetSwap(payFixedRate, bond, fairCleanPrice, vars.IborIndex, vars.Spread, isPar), swapEngine, fairCleanPrice, vars.Spread, tolerance);
        CheckFairValues("market", vars.AssetSwap(payFixedRate, bond, bondPrice, vars.IborIndex, fairSpread, isPar), swapEngine, bondPrice, fairSpread, tolerance);

        // using overnight index
        mktAssetSwap = vars.AssetSwap(payFixedRate, bond, bondPrice, overnight, vars.Spread, isPar, overnightSchedule);

        swapEngine = s => DiscountingSwapEngine.Calculate(
            s, vars.Context, vars.TermStructure, true, bondSettlement, vars.Context.EvaluationDate);

        mktResults = swapEngine(mktAssetSwap);
        fairCleanPrice = AssetSwap.FairCleanPrice(mktResults);
        fairSpread = AssetSwap.FairSpread(mktResults);

        CheckFairValues("market", vars.AssetSwap(payFixedRate, bond, fairCleanPrice, overnight, vars.Spread, isPar, overnightSchedule), swapEngine, fairCleanPrice, vars.Spread, tolerance);
        CheckFairValues("market", vars.AssetSwap(payFixedRate, bond, bondPrice, overnight, fairSpread, isPar, overnightSchedule), swapEngine, bondPrice, fairSpread, tolerance);
    }

    [Fact]
    public void TestImpliedValue()
    {
        // Testing implied bond value against asset-swap fair price with null spread...
        var vars = new CommonVars();
        bool usingAtParCoupons = vars.Context.UseAtParCoupons;

        bool payFixedRate = true;
        bool parAssetSwap = true;

        Real tolerance = 1.0e-13;

        // for indexed coupons the float leg will not be par, therefore we
        // have to relax the tolerance - note that the fair clean price is
        // correct though, only we can not compare it to the bond price
        // directly. The same kind of discrepancy will occur for a multi
        // curve set up, which we do not test here.
        Real tolerance2 = usingAtParCoupons ? 1.0e-13 : 1.0e-2;

        void Check(string kind, Bond bond)
        {
            Real bondPrice = vars.CleanPrice(bond);
            AssetSwap assetSwap = vars.AssetSwap(payFixedRate, bond, bondPrice, vars.IborIndex, vars.Spread, parAssetSwap);
            Real assetSwapPrice = AssetSwap.FairCleanPrice(vars.Price(assetSwap));
            Real error = Math.Abs(assetSwapPrice - bondPrice);
            Assert.False(
                error > tolerance2,
                $"wrong zero spread asset swap price for {kind}: bond's clean price {bondPrice}, "
                + $"asset swap fair price {assetSwapPrice}, error {error}, tolerance {tolerance2}");
        }

        // Fixed Underlying bond (Isin: DE0001135275 DBR 4 01/04/37)
        // maturity doesn't occur on a business day
        Check("fixed bond", FixedBond1(vars));

        // Fixed Underlying bond (Isin: IT0006527060 IBRD 5 02/05/19)
        // maturity occurs on a business day
        Check("fixed bond", FixedBond2(vars));

        // FRN Underlying bond (Isin: IT0003543847 ISPIM 0 09/29/13)
        // maturity doesn't occur on a business day
        Bond floatingBond1 = FloatingBond1(vars);
        vars.AddFixing(vars.IborIndex, new Date(27, March, 2007), 0.0402);
        Check("floater", floatingBond1);

        // FRN Underlying bond (Isin: XS0090566539 COE 0 09/24/18)
        // maturity occurs on a business day
        Bond floatingBond2 = FloatingBond2(vars);
        vars.AddFixing(vars.IborIndex, new Date(22, March, 2007), 0.04013);
        Real currentCoupon = 0.04013 + 0.0025;
        Rate floatingCurrentCoupon = floatingBond2.NextCouponRate(vars.Context);
        Real error4 = Math.Abs(floatingCurrentCoupon - currentCoupon);
        Assert.False(
            error4 > tolerance,
            $"wrong current coupon is returned for floater bond: bond's calculated current coupon {currentCoupon}, "
            + $"current coupon asked to the bond {floatingCurrentCoupon}, error {error4}, tolerance {tolerance}");
        Check("floater", floatingBond2);

        // CMS Underlying bond (Isin: XS0228052402 CRDIT 0 8/22/20)
        // maturity doesn't occur on a business day
        Bond cmsBond1 = CmsBond1(vars);
        vars.AddFixing(vars.SwapIndex, new Date(18, August, 2006), 0.04158);
        Check("cms bond", cmsBond1);

        // CMS Underlying bond (Isin: XS0218766664 ISPIM 0 5/6/15)
        // maturity occurs on a business day
        Bond cmsBond2 = CmsBond2(vars);
        vars.AddFixing(vars.SwapIndex, new Date(4, May, 2006), 0.04217);
        Check("cms bond", cmsBond2);

        // Adapted: QuantLib's two zero-coupon checks compare the CMS bonds' prices again (assetswap.cpp:996, :1028);
        // here each compares its zero-coupon bond's, as its message says and testGenericBondImplied's do.

        // Zero Coupon bond (Isin: DE0004771662 IBRD 0 12/20/15)
        // maturity doesn't occur on a business day
        Check("zero cpn bond", ZeroCpnBond1(vars));

        // Zero Coupon bond (Isin: IT0001200390 ISPIM 0 02/17/28)
        // maturity occurs on a business day
        Check("zero cpn bond", ZeroCpnBond2(vars));
    }

    [Fact]
    public void TestMarketASWSpread()
    {
        // Testing relationship between market asset swap and par asset swap...
        var vars = new CommonVars();
        CheckMarketAgainstParSpread(vars, generic: false);
    }

    [Fact]
    public void TestZSpread()
    {
        // Testing clean and dirty price with null Z-spread against theoretical prices...
        var vars = new CommonVars();
        CheckNullZSpread(vars, generic: false);
    }

    [Fact]
    public void TestGenericBondImplied()
    {
        // Testing implied generic-bond value against asset-swap fair price with null spread...
        var vars = new CommonVars();
        bool usingAtParCoupons = vars.Context.UseAtParCoupons;

        bool payFixedRate = true;
        bool parAssetSwap = true;

        Real tolerance = 1.0e-13;

        // see comment above
        Real tolerance2 = usingAtParCoupons ? 1.0e-13 : 1.0e-2;

        void Check(string kind, Bond bond)
        {
            Real bondPrice = vars.CleanPrice(bond);
            AssetSwap assetSwap = vars.AssetSwap(payFixedRate, bond, bondPrice, vars.IborIndex, vars.Spread, parAssetSwap);
            Real assetSwapPrice = AssetSwap.FairCleanPrice(vars.Price(assetSwap));
            Real error = Math.Abs(assetSwapPrice - bondPrice);
            Assert.False(
                error > tolerance2,
                $"wrong zero spread asset swap price for {kind}: bond's clean price {bondPrice}, "
                + $"asset swap fair price {assetSwapPrice}, error {error}, tolerance {tolerance2}");
        }

        // Fixed Underlying bond (Isin: DE0001135275 DBR 4 01/04/37)
        // maturity doesn't occur on a business day
        Check("fixed bond", GenericFixedBond1(vars));

        // Fixed Underlying bond (Isin: IT0006527060 IBRD 5 02/05/19)
        // maturity occurs on a business day
        Check("fixed bond", GenericFixedBond2(vars));

        // FRN Underlying bond (Isin: IT0003543847 ISPIM 0 09/29/13)
        // maturity doesn't occur on a business day
        Bond floatingBond1 = GenericFloatingBond1(vars);
        vars.AddFixing(vars.IborIndex, new Date(27, March, 2007), 0.0402);
        Check("floater", floatingBond1);

        // FRN Underlying bond (Isin: XS0090566539 COE 0 09/24/18)
        // maturity occurs on a business day
        Bond floatingBond2 = GenericFloatingBond2(vars);
        vars.AddFixing(vars.IborIndex, new Date(22, March, 2007), 0.04013);
        Real currentCoupon = 0.04013 + 0.0025;
        Rate floatingCurrentCoupon = floatingBond2.NextCouponRate(vars.Context);
        Real error4 = Math.Abs(floatingCurrentCoupon - currentCoupon);
        Assert.False(
            error4 > tolerance,
            $"wrong current coupon is returned for floater bond: bond's calculated current coupon {currentCoupon}, "
            + $"current coupon asked to the bond {floatingCurrentCoupon}, error {error4}, tolerance {tolerance}");
        Check("floater", floatingBond2);

        // CMS Underlying bond (Isin: XS0228052402 CRDIT 0 8/22/20)
        // maturity doesn't occur on a business day
        Bond cmsBond1 = GenericCmsBond1(vars);
        vars.AddFixing(vars.SwapIndex, new Date(18, August, 2006), 0.04158);
        Check("cms bond", cmsBond1);

        // CMS Underlying bond (Isin: XS0218766664 ISPIM 0 5/6/15)
        // maturity occurs on a business day
        Bond cmsBond2 = GenericCmsBond2(vars);
        vars.AddFixing(vars.SwapIndex, new Date(4, May, 2006), 0.04217);
        Check("cms bond", cmsBond2);

        // Zero Coupon bond (Isin: DE0004771662 IBRD 0 12/20/15)
        // maturity doesn't occur on a business day
        Check("zero cpn bond", GenericZeroCpnBond1(vars));

        // Zero Coupon bond (Isin: IT0001200390 ISPIM 0 02/17/28)
        // maturity occurs on a business day
        Check("zero cpn bond", GenericZeroCpnBond2(vars));
    }

    [Fact]
    public void TestMASWWithGenericBond()
    {
        // Testing market asset swap against par asset swap with generic bond...
        var vars = new CommonVars();
        CheckMarketAgainstParSpread(vars, generic: true);
    }

    [Fact]
    public void TestZSpreadWithGenericBond()
    {
        // Testing clean and dirty price with null Z-spread against theoretical prices...
        var vars = new CommonVars();
        CheckNullZSpread(vars, generic: true);
    }

    [Fact]
    public void TestSpecializedBondVsGenericBond()
    {
        // Testing clean and dirty prices for specialized bond against equivalent generic bond...
        var vars = new CommonVars();
        Real tolerance = 1.0e-13;

        void Check(string kind, Bond generic, Bond specialized)
        {
            Real genericTheoValue = vars.CleanPrice(generic);
            Real specializedTheoValue = vars.CleanPrice(specialized);
            Real error = Math.Abs(genericTheoValue - specializedTheoValue);
            Assert.False(
                error > tolerance,
                $"wrong clean price for {kind}: generic bond's theo clean price {genericTheoValue}, "
                + $"equivalent specialized bond's theo clean price {specializedTheoValue}, error {error}, "
                + $"tolerance {tolerance}");
            Real genericTheoDirty = genericTheoValue + generic.AccruedAmount(vars.Context);
            Real specializedTheoDirty = specializedTheoValue + specialized.AccruedAmount(vars.Context);
            Real dirtyError = Math.Abs(genericTheoDirty - specializedTheoDirty);
            Assert.False(
                dirtyError > tolerance,
                $"wrong dirty price for {kind}: generic bond's theo dirty price {genericTheoDirty}, "
                + $"equivalent specialized bond's theo dirty price {specializedTheoDirty}, error {dirtyError}, "
                + $"tolerance {tolerance}");
        }

        // Fixed Underlying bond (Isin: DE0001135275 DBR 4 01/04/37)
        // maturity doesn't occur on a business day
        Check("fixed bond", GenericFixedBond1(vars), FixedBond1(vars));

        // Fixed Underlying bond (Isin: IT0006527060 IBRD 5 02/05/19)
        // maturity occurs on a business day
        Check("fixed bond", GenericFixedBond2(vars), FixedBond2(vars));

        // FRN Underlying bond (Isin: IT0003543847 ISPIM 0 09/29/13)
        // maturity doesn't occur on a business day
        Bond floatingBond1 = GenericFloatingBond1(vars);
        Bond floatingSpecializedBond1 = FloatingBond1(vars);
        vars.AddFixing(vars.IborIndex, new Date(27, March, 2007), 0.0402);
        Check("frn bond", floatingBond1, floatingSpecializedBond1);

        // FRN Underlying bond (Isin: XS0090566539 COE 0 09/24/18)
        // maturity occurs on a business day
        Bond floatingBond2 = GenericFloatingBond2(vars);
        Bond floatingSpecializedBond2 = FloatingBond2(vars);
        vars.AddFixing(vars.IborIndex, new Date(22, March, 2007), 0.04013);
        Check("floater bond", floatingBond2, floatingSpecializedBond2);

        // CMS Underlying bond (Isin: XS0228052402 CRDIT 0 8/22/20)
        // maturity doesn't occur on a business day
        Bond cmsBond1 = GenericCmsBond1(vars);
        Bond cmsSpecializedBond1 = CmsBond1(vars);
        vars.AddFixing(vars.SwapIndex, new Date(18, August, 2006), 0.04158);
        Check("cms bond", cmsBond1, cmsSpecializedBond1);

        // CMS Underlying bond (Isin: XS0218766664 ISPIM 0 5/6/15)
        // maturity occurs on a business day
        Bond cmsBond2 = GenericCmsBond2(vars);
        Bond cmsSpecializedBond2 = CmsBond2(vars);
        vars.AddFixing(vars.SwapIndex, new Date(4, May, 2006), 0.04217);
        Check("cms bond", cmsBond2, cmsSpecializedBond2);

        // Zero Coupon bond (Isin: DE0004771662 IBRD 0 12/20/15)
        // maturity doesn't occur on a business day
        Check("zero coupon bond", GenericZeroCpnBond1(vars), ZeroCpnBond1(vars));

        // Zero Coupon bond (Isin: IT0001200390 ISPIM 0 02/17/28)
        // maturity occurs on a business day
        Check("zero coupon bond", GenericZeroCpnBond2(vars), ZeroCpnBond2(vars));
    }

    [Fact]
    public void TestSpecializedBondVsGenericBondUsingAsw()
    {
        // Testing asset-swap prices and spreads for specialized bond against equivalent generic bond...
        var vars = new CommonVars();

        bool payFixedRate = true;
        bool parAssetSwap = true;
        Real tolerance = 1.0e-13;

        void Check(string kind, Bond generic, Bond specialized, Real mktPrice)
        {
            Real genericPrice = vars.CleanPrice(generic);
            Real specializedPrice = vars.CleanPrice(specialized);
            AssetSwap genericAssetSwap = vars.AssetSwap(payFixedRate, generic, genericPrice, vars.IborIndex, vars.NonNullSpread, parAssetSwap);
            AssetSwap specializedAssetSwap = vars.AssetSwap(payFixedRate, specialized, specializedPrice, vars.IborIndex, vars.NonNullSpread, parAssetSwap);
            Real genericAssetSwapPrice = AssetSwap.FairCleanPrice(vars.Price(genericAssetSwap));
            Real specializedAssetSwapPrice = AssetSwap.FairCleanPrice(vars.Price(specializedAssetSwap));
            Real error = Math.Abs(genericAssetSwapPrice - specializedAssetSwapPrice);
            Assert.False(
                error > tolerance,
                $"wrong clean price for {kind}: generic bond's clean price {genericAssetSwapPrice}, "
                + $"equivalent specialized bond's clean price {specializedAssetSwapPrice}, error {error}, "
                + $"tolerance {tolerance}");

            // market executable price as of 4th sept 2007
            AssetSwap genericAsw = vars.AssetSwap(payFixedRate, generic, mktPrice, vars.IborIndex, vars.Spread, parAssetSwap);
            AssetSwap specializedAsw = vars.AssetSwap(payFixedRate, specialized, mktPrice, vars.IborIndex, vars.Spread, parAssetSwap);
            Real genericAswSpread = AssetSwap.FairSpread(vars.Price(genericAsw));
            Real specializedAswSpread = AssetSwap.FairSpread(vars.Price(specializedAsw));
            Real spreadError = Math.Abs(genericAswSpread - specializedAswSpread);
            Assert.False(
                spreadError > tolerance,
                $"wrong asw spread for {kind}: generic bond's asw spread {genericAswSpread}, "
                + $"equivalent specialized bond's asw spread {specializedAswSpread}, error {spreadError}, "
                + $"tolerance {tolerance}");
        }

        // Fixed bond (Isin: DE0001135275 DBR 4 01/04/37)
        // maturity doesn't occur on a business day
        Check("fixed bond", GenericFixedBond1(vars), FixedBond1(vars), 91.832);

        // Fixed bond (Isin: IT0006527060 IBRD 5 02/05/19)
        // maturity occurs on a business day
        Check("fixed bond", GenericFixedBond2(vars), FixedBond2(vars), 102.178);

        // FRN bond (Isin: IT0003543847 ISPIM 0 09/29/13)
        // maturity doesn't occur on a business day
        Bond floatingBond1 = GenericFloatingBond1(vars);
        Bond floatingSpecializedBond1 = FloatingBond1(vars);
        vars.AddFixing(vars.IborIndex, new Date(27, March, 2007), 0.0402);
        Check("frn bond", floatingBond1, floatingSpecializedBond1, 101.33);

        // FRN bond (Isin: XS0090566539 COE 0 09/24/18)
        // maturity occurs on a business day
        Bond floatingBond2 = GenericFloatingBond2(vars);
        Bond floatingSpecializedBond2 = FloatingBond2(vars);
        vars.AddFixing(vars.IborIndex, new Date(22, March, 2007), 0.04013);
        Check("frn bond", floatingBond2, floatingSpecializedBond2, 101.26);

        // CMS Underlying bond (Isin: XS0228052402 CRDIT 0 8/22/20)
        // maturity doesn't occur on a business day
        Bond cmsBond1 = GenericCmsBond1(vars);
        Bond cmsSpecializedBond1 = CmsBond1(vars);
        vars.AddFixing(vars.SwapIndex, new Date(18, August, 2006), 0.04158);
        Check("cms bond", cmsBond1, cmsSpecializedBond1, 87.02);

        // CMS Underlying bond (Isin: XS0218766664 ISPIM 0 5/6/15)
        // maturity occurs on a business day
        Bond cmsBond2 = GenericCmsBond2(vars);
        Bond cmsSpecializedBond2 = CmsBond2(vars);
        vars.AddFixing(vars.SwapIndex, new Date(4, May, 2006), 0.04217);
        Check("cms bond", cmsBond2, cmsSpecializedBond2, 94.35);

        // Zero-Coupon bond (Isin: DE0004771662 IBRD 0 12/20/15)
        // maturity doesn't occur on a business day
        Check("zero cpn bond", GenericZeroCpnBond1(vars), ZeroCpnBond1(vars), 72.277);

        // Zero Coupon bond (Isin: IT0001200390 ISPIM 0 02/17/28)
        // maturity doesn't occur on a business day
        Check("zero cpn bond", GenericZeroCpnBond2(vars), ZeroCpnBond2(vars), 72.277);
    }

    // testMarketASWSpread and testMASWWithGenericBond: a market asset swap's spread is the par one's over the bond's
    // full market price, per 100.
    private static void CheckMarketAgainstParSpread(CommonVars vars, bool generic)
    {
        bool usingAtParCoupons = vars.Context.UseAtParCoupons;
        bool payFixedRate = true;
        bool parAssetSwap = true;
        bool mktAssetSwap = false;

        // see comment above
        Real tolerance2 = usingAtParCoupons ? 1.0e-13 : 1.0e-4;

        void Check(string kind, Bond bond, Real mktPrice)
        {
            Real mktFullPrice = mktPrice + bond.AccruedAmount(vars.Context);
            AssetSwap parSwap = vars.AssetSwap(payFixedRate, bond, mktPrice, vars.IborIndex, vars.Spread, parAssetSwap);
            Real parSpread = AssetSwap.FairSpread(vars.Price(parSwap));
            AssetSwap mktSwap = vars.AssetSwap(payFixedRate, bond, mktPrice, vars.IborIndex, vars.Spread, mktAssetSwap);
            Real mktSpread = AssetSwap.FairSpread(vars.Price(mktSwap));
            Real error = Math.Abs(mktSpread - (100 * parSpread / mktFullPrice));
            Assert.False(
                error > tolerance2,
                $"wrong asset swap spreads for {kind}: market ASW spread {mktSpread}, par ASW spread {parSpread}, "
                + $"error {error}, tolerance {tolerance2}");
        }

        // Fixed Underlying bond (Isin: DE0001135275 DBR 4 01/04/37)
        // maturity doesn't occur on a business day
        Check("fixed bond", generic ? GenericFixedBond1(vars) : FixedBond1(vars), 89.22); // market price observed on 7th June 2007

        // Fixed Underlying bond (Isin: IT0006527060 IBRD 5 02/05/19)
        // maturity occurs on a business day
        Check("fixed bond", generic ? GenericFixedBond2(vars) : FixedBond2(vars), 99.98); // market price observed on 7th June 2007

        // FRN Underlying bond (Isin: IT0003543847 ISPIM 0 09/29/13)
        // maturity doesn't occur on a business day
        Bond floatingBond1 = generic ? GenericFloatingBond1(vars) : FloatingBond1(vars);
        vars.AddFixing(vars.IborIndex, new Date(27, March, 2007), 0.0402);

        // market price observed on 7th June 2007
        Check("floating bond", floatingBond1, 101.64);

        // FRN Underlying bond (Isin: XS0090566539 COE 0 09/24/18)
        // maturity occurs on a business day
        Bond floatingBond2 = generic ? GenericFloatingBond2(vars) : FloatingBond2(vars);
        vars.AddFixing(vars.IborIndex, new Date(22, March, 2007), 0.04013);

        // market price observed on 7th June 2007
        Check("floating bond", floatingBond2, 101.248);

        // CMS Underlying bond (Isin: XS0228052402 CRDIT 0 8/22/20)
        // maturity doesn't occur on a business day
        Bond cmsBond1 = generic ? GenericCmsBond1(vars) : CmsBond1(vars);
        vars.AddFixing(vars.SwapIndex, new Date(18, August, 2006), 0.04158);
        Check("cms bond", cmsBond1, 88.45); // market price observed on 7th June 2007

        // CMS Underlying bond (Isin: XS0218766664 ISPIM 0 5/6/15)
        // maturity occurs on a business day
        Bond cmsBond2 = generic ? GenericCmsBond2(vars) : CmsBond2(vars);
        vars.AddFixing(vars.SwapIndex, new Date(4, May, 2006), 0.04217);
        Check("cms bond", cmsBond2, 94.08); // market price observed on 7th June 2007

        // Zero Coupon bond (Isin: DE0004771662 IBRD 0 12/20/15)
        // maturity doesn't occur on a business day

        // market price observed on 12th June 2007
        Check("zero cpn bond", generic ? GenericZeroCpnBond1(vars) : ZeroCpnBond1(vars), 70.436);

        // Zero Coupon bond (Isin: IT0001200390 ISPIM 0 02/17/28)
        // maturity occurs on a business day

        // market price observed on 12th June 2007
        Check("zero cpn bond", generic ? GenericZeroCpnBond2(vars) : ZeroCpnBond2(vars), 35.160);
    }

    // testZSpread and testZSpreadWithGenericBond: a null Z-spread gives the bond's theoretical clean price.
    private static void CheckNullZSpread(CommonVars vars, bool generic)
    {
        Real tolerance = 1.0e-13;

        void Check(string kind, Bond bond, Frequency frequency, Date settlementDate)
        {
            Real impliedValue = vars.CleanPrice(bond);

            // standard market conventions:
            // bond's frequency + coumpounding and daycounter of the YieldCurve
            Real cleanPrice = BondFunctions.CleanPrice(
                bond, vars.TermStructure, vars.Spread, vars.Compounding, frequency, vars.Context, settlementDate);
            Real error = Math.Abs(impliedValue - cleanPrice);
            Assert.False(
                error > tolerance,
                $"wrong clean price for {kind}: implied value {impliedValue}, clean price {cleanPrice}, "
                + $"error {error}, tolerance {tolerance}");
        }

        // Fixed bond (Isin: DE0001135275 DBR 4 01/04/37)
        // maturity doesn't occur on a business day
        Bond fixedBond1 = generic ? GenericFixedBond1(vars) : FixedBond1(vars);
        Date fixedBondSettlementDate1 = fixedBond1.SettlementDate(vars.Context);
        Check("fixed bond", fixedBond1, Frequency.Annual, fixedBondSettlementDate1);

        // Fixed bond (Isin: IT0006527060 IBRD 5 02/05/19)
        // maturity occurs on a business day
        Bond fixedBond2 = generic ? GenericFixedBond2(vars) : FixedBond2(vars);
        Check("fixed bond", fixedBond2, Frequency.Annual, fixedBond2.SettlementDate(vars.Context));

        // FRN bond (Isin: IT0003543847 ISPIM 0 09/29/13)
        // maturity doesn't occur on a business day
        Bond floatingBond1 = generic ? GenericFloatingBond1(vars) : FloatingBond1(vars);
        vars.AddFixing(vars.IborIndex, new Date(27, March, 2007), 0.0402);
        Check("floating bond", floatingBond1, Frequency.Semiannual, fixedBondSettlementDate1);

        // FRN bond (Isin: XS0090566539 COE 0 09/24/18)
        // maturity occurs on a business day
        Bond floatingBond2 = generic ? GenericFloatingBond2(vars) : FloatingBond2(vars);
        vars.AddFixing(vars.IborIndex, new Date(22, March, 2007), 0.04013);
        Check("floating bond", floatingBond2, Frequency.Semiannual, fixedBondSettlementDate1);

        // CMS bond (Isin: XS0228052402 CRDIT 0 8/22/20)
        // maturity doesn't occur on a business day
        Bond cmsBond1 = generic ? GenericCmsBond1(vars) : CmsBond1(vars);
        vars.AddFixing(vars.SwapIndex, new Date(18, August, 2006), 0.04158);
        Check("cms bond", cmsBond1, Frequency.Annual, cmsBond1.SettlementDate(vars.Context));

        // CMS bond (Isin: XS0218766664 ISPIM 0 5/6/15)
        // maturity occurs on a business day
        Bond cmsBond2 = generic ? GenericCmsBond2(vars) : CmsBond2(vars);
        vars.AddFixing(vars.SwapIndex, new Date(4, May, 2006), 0.04217);
        Check("cms bond", cmsBond2, Frequency.Annual, cmsBond2.SettlementDate(vars.Context));

        // Zero-Coupon bond (Isin: DE0004771662 IBRD 0 12/20/15)
        // maturity doesn't occur on a business day
        Bond zeroCpnBond1 = generic ? GenericZeroCpnBond1(vars) : ZeroCpnBond1(vars);
        Check("zero coupon bond", zeroCpnBond1, Frequency.Annual, zeroCpnBond1.SettlementDate(vars.Context));

        // Zero Coupon bond (Isin: IT0001200390 ISPIM 0 02/17/28)
        // maturity doesn't occur on a business day
        Bond zeroCpnBond2 = generic ? GenericZeroCpnBond2(vars) : ZeroCpnBond2(vars);
        Check("zero coupon bond", zeroCpnBond2, Frequency.Annual, zeroCpnBond2.SettlementDate(vars.Context));
    }

    // QuantLib's six checks of an asset swap priced at a fair value: its NPV is zero, and its fair clean price and
    // spread are its own.
    private static void CheckFairValues(
        string kind,
        AssetSwap assetSwap,
        Func<AssetSwap, AssetSwapResults> swapEngine,
        Real expectedCleanPrice,
        Spread expectedSpread,
        Real tolerance)
    {
        AssetSwapResults results = swapEngine(assetSwap);
        Real npv = results.NPV!.Value;
        Assert.False(
            Math.Abs(npv) > tolerance,
            $"{kind} asset swap fair value doesn't zero the NPV: NPV {npv}, tolerance {tolerance}");
        Real fairCleanPrice = AssetSwap.FairCleanPrice(results);
        Assert.False(
            Math.Abs(fairCleanPrice - expectedCleanPrice) > tolerance,
            $"{kind} asset swap fair clean price doesn't equal input clean price at zero NPV: input clean price "
            + $"{expectedCleanPrice}, fair clean price {fairCleanPrice}, NPV {npv}, tolerance {tolerance}");
        Spread fairSpread = AssetSwap.FairSpread(results);
        Assert.False(
            Math.Abs(fairSpread - expectedSpread) > tolerance,
            $"{kind} asset swap fair spread doesn't equal input spread at zero NPV: input spread {expectedSpread}, "
            + $"fair spread {fairSpread}, NPV {npv}, tolerance {tolerance}");
    }

    private static Schedule BondSchedule(
        CommonVars vars, Date start, Date maturity, Frequency frequency, BusinessDayConvention convention) =>
        new(start, maturity, new Period(frequency), BondCalendar, convention, convention, DateGenerationRule.Backward, false, vars.Context);

    // Fixed Underlying bond (Isin: DE0001135275 DBR 4 01/04/37)
    private static Bond FixedBond1(CommonVars vars) =>
        new FixedRateBond(
            SettlementDays,
            vars.FaceAmount,
            BondSchedule(vars, new Date(4, January, 2005), new Date(4, January, 2037), Frequency.Annual, BusinessDayConvention.Unadjusted),
            [0.04],
            new ActualActual(ActualActualConvention.ISDA),
            BusinessDayConvention.Following,
            100.0,
            new Date(4, January, 2005));

    private static Bond GenericFixedBond1(CommonVars vars)
    {
        Date fixedBondStartDate1 = new(4, January, 2005);
        Date fixedBondMaturityDate1 = new(4, January, 2037);
        Schedule fixedBondSchedule1 = BondSchedule(vars, fixedBondStartDate1, fixedBondMaturityDate1, Frequency.Annual, BusinessDayConvention.Unadjusted);
        ImmutableArray<CashFlow> fixedBondLeg1 = new FixedRateLeg(fixedBondSchedule1)
            .WithNotionals(vars.FaceAmount)
            .WithCouponRates(0.04, new ActualActual(ActualActualConvention.ISDA))
            .ToLeg();
        Date fixedbondRedemption1 = BondCalendar.Adjust(fixedBondMaturityDate1, BusinessDayConvention.Following);
        return new Bond(
            SettlementDays,
            BondCalendar,
            vars.FaceAmount,
            fixedBondMaturityDate1,
            fixedBondStartDate1,
            [.. fixedBondLeg1, new SimpleCashFlow(100.0, fixedbondRedemption1)]);
    }

    // Fixed Underlying bond (Isin: IT0006527060 IBRD 5 02/05/19)
    private static Bond FixedBond2(CommonVars vars) =>
        new FixedRateBond(
            SettlementDays,
            vars.FaceAmount,
            BondSchedule(vars, new Date(5, February, 2005), new Date(5, February, 2019), Frequency.Annual, BusinessDayConvention.Unadjusted),
            [0.05],
            new Thirty360(Thirty360Convention.BondBasis),
            BusinessDayConvention.Following,
            100.0,
            new Date(5, February, 2005));

    private static Bond GenericFixedBond2(CommonVars vars)
    {
        Date fixedBondStartDate2 = new(5, February, 2005);
        Date fixedBondMaturityDate2 = new(5, February, 2019);
        Schedule fixedBondSchedule2 = BondSchedule(vars, fixedBondStartDate2, fixedBondMaturityDate2, Frequency.Annual, BusinessDayConvention.Unadjusted);
        ImmutableArray<CashFlow> fixedBondLeg2 = new FixedRateLeg(fixedBondSchedule2)
            .WithNotionals(vars.FaceAmount)
            .WithCouponRates(0.05, new Thirty360(Thirty360Convention.BondBasis))
            .ToLeg();
        Date fixedbondRedemption2 = BondCalendar.Adjust(fixedBondMaturityDate2, BusinessDayConvention.Following);
        return new Bond(
            SettlementDays,
            BondCalendar,
            vars.FaceAmount,
            fixedBondMaturityDate2,
            fixedBondStartDate2,
            [.. fixedBondLeg2, new SimpleCashFlow(100.0, fixedbondRedemption2)]);
    }

    // FRN Underlying bond (Isin: IT0003543847 ISPIM 0 09/29/13), with the Ibor pricer set
    private static Bond FloatingBond1(CommonVars vars) =>
        CouponPricerFunctions.SetCouponPricer<Bond>(
            new FloatingRateBond(
                SettlementDays,
                vars.FaceAmount,
                BondSchedule(vars, new Date(29, September, 2003), new Date(29, September, 2013), Frequency.Semiannual, BusinessDayConvention.Unadjusted),
                vars.IborIndex,
                new Actual360(),
                vars.Context,
                BusinessDayConvention.Following,
                FixingDays,
                [1],
                [0.0056],
                [],
                [],
                InArrearsFlag,
                100.0,
                new Date(29, September, 2003)),
            vars.Pricer);

    private static Bond GenericFloatingBond1(CommonVars vars)
    {
        Date floatingBondStartDate1 = new(29, September, 2003);
        Date floatingBondMaturityDate1 = new(29, September, 2013);
        Schedule floatingBondSchedule1 = BondSchedule(vars, floatingBondStartDate1, floatingBondMaturityDate1, Frequency.Semiannual, BusinessDayConvention.Unadjusted);
        ImmutableArray<CashFlow> floatingBondLeg1 = new IborLeg(floatingBondSchedule1, vars.IborIndex)
            .WithNotionals(vars.FaceAmount)
            .WithPaymentDayCounter(new Actual360())
            .WithFixingDays(FixingDays)
            .WithSpreads(0.0056)
            .InArrears(InArrearsFlag)
            .ToLeg(vars.Context);
        Date floatingbondRedemption1 = BondCalendar.Adjust(floatingBondMaturityDate1, BusinessDayConvention.Following);
        var bond = new Bond(
            SettlementDays,
            BondCalendar,
            vars.FaceAmount,
            floatingBondMaturityDate1,
            floatingBondStartDate1,
            [.. floatingBondLeg1, new SimpleCashFlow(100.0, floatingbondRedemption1)]);
        return CouponPricerFunctions.SetCouponPricer(bond, vars.Pricer);
    }

    // FRN Underlying bond (Isin: XS0090566539 COE 0 09/24/18), with the Ibor pricer set
    private static Bond FloatingBond2(CommonVars vars) =>
        CouponPricerFunctions.SetCouponPricer<Bond>(
            new FloatingRateBond(
                SettlementDays,
                vars.FaceAmount,
                BondSchedule(vars, new Date(24, September, 2004), new Date(24, September, 2018), Frequency.Semiannual, BusinessDayConvention.ModifiedFollowing),
                vars.IborIndex,
                new Actual360(),
                vars.Context,
                BusinessDayConvention.ModifiedFollowing,
                FixingDays,
                [1],
                [0.0025],
                [],
                [],
                InArrearsFlag,
                100.0,
                new Date(24, September, 2004)),
            vars.Pricer);

    private static Bond GenericFloatingBond2(CommonVars vars)
    {
        Date floatingBondStartDate2 = new(24, September, 2004);
        Date floatingBondMaturityDate2 = new(24, September, 2018);
        Schedule floatingBondSchedule2 = BondSchedule(vars, floatingBondStartDate2, floatingBondMaturityDate2, Frequency.Semiannual, BusinessDayConvention.ModifiedFollowing);
        ImmutableArray<CashFlow> floatingBondLeg2 = new IborLeg(floatingBondSchedule2, vars.IborIndex)
            .WithNotionals(vars.FaceAmount)
            .WithPaymentDayCounter(new Actual360())
            .WithPaymentAdjustment(BusinessDayConvention.ModifiedFollowing)
            .WithFixingDays(FixingDays)
            .WithSpreads(0.0025)
            .InArrears(InArrearsFlag)
            .ToLeg(vars.Context);
        Date floatingbondRedemption2 = BondCalendar.Adjust(floatingBondMaturityDate2, BusinessDayConvention.ModifiedFollowing);
        var bond = new Bond(
            SettlementDays,
            BondCalendar,
            vars.FaceAmount,
            floatingBondMaturityDate2,
            floatingBondStartDate2,
            [.. floatingBondLeg2, new SimpleCashFlow(100.0, floatingbondRedemption2)]);
        return CouponPricerFunctions.SetCouponPricer(bond, vars.Pricer);
    }

    // CMS Underlying bond (Isin: XS0228052402 CRDIT 0 8/22/20), with the CMS pricer set
    private static Bond CmsBond1(CommonVars vars) =>
        CouponPricerFunctions.SetCouponPricer<Bond>(
            new CmsRateBond(
                SettlementDays,
                vars.FaceAmount,
                BondSchedule(vars, new Date(22, August, 2005), new Date(22, August, 2020), Frequency.Annual, BusinessDayConvention.Unadjusted),
                vars.SwapIndex,
                new Thirty360(Thirty360Convention.BondBasis),
                BusinessDayConvention.Following,
                FixingDays,
                [1.0],
                [0.0],
                [0.055],
                [0.025],
                InArrearsFlag,
                100.0,
                new Date(22, August, 2005)),
            vars.CmsPricer);

    private static Bond GenericCmsBond1(CommonVars vars)
    {
        Date cmsBondStartDate1 = new(22, August, 2005);
        Date cmsBondMaturityDate1 = new(22, August, 2020);
        Schedule cmsBondSchedule1 = BondSchedule(vars, cmsBondStartDate1, cmsBondMaturityDate1, Frequency.Annual, BusinessDayConvention.Unadjusted);
        ImmutableArray<CashFlow> cmsBondLeg1 = new CmsLeg(cmsBondSchedule1, vars.SwapIndex)
            .WithNotionals(vars.FaceAmount)
            .WithPaymentDayCounter(new Thirty360(Thirty360Convention.BondBasis))
            .WithFixingDays(FixingDays)
            .WithCaps(0.055)
            .WithFloors(0.025)
            .InArrears(InArrearsFlag)
            .ToLeg();
        Date cmsbondRedemption1 = BondCalendar.Adjust(cmsBondMaturityDate1, BusinessDayConvention.Following);
        var bond = new Bond(
            SettlementDays,
            BondCalendar,
            vars.FaceAmount,
            cmsBondMaturityDate1,
            cmsBondStartDate1,
            [.. cmsBondLeg1, new SimpleCashFlow(100.0, cmsbondRedemption1)]);
        return CouponPricerFunctions.SetCouponPricer(bond, vars.CmsPricer);
    }

    // CMS Underlying bond (Isin: XS0218766664 ISPIM 0 5/6/15), with the CMS pricer set
    private static Bond CmsBond2(CommonVars vars) =>
        CouponPricerFunctions.SetCouponPricer<Bond>(
            new CmsRateBond(
                SettlementDays,
                vars.FaceAmount,
                BondSchedule(vars, new Date(6, May, 2005), new Date(6, May, 2015), Frequency.Annual, BusinessDayConvention.Unadjusted),
                vars.SwapIndex,
                new Thirty360(Thirty360Convention.BondBasis),
                BusinessDayConvention.Following,
                FixingDays,
                [0.84],
                [0.0],
                [],
                [],
                InArrearsFlag,
                100.0,
                new Date(6, May, 2005)),
            vars.CmsPricer);

    private static Bond GenericCmsBond2(CommonVars vars)
    {
        Date cmsBondStartDate2 = new(6, May, 2005);
        Date cmsBondMaturityDate2 = new(6, May, 2015);
        Schedule cmsBondSchedule2 = BondSchedule(vars, cmsBondStartDate2, cmsBondMaturityDate2, Frequency.Annual, BusinessDayConvention.Unadjusted);
        ImmutableArray<CashFlow> cmsBondLeg2 = new CmsLeg(cmsBondSchedule2, vars.SwapIndex)
            .WithNotionals(vars.FaceAmount)
            .WithPaymentDayCounter(new Thirty360(Thirty360Convention.BondBasis))
            .WithFixingDays(FixingDays)
            .WithGearings(0.84)
            .InArrears(InArrearsFlag)
            .ToLeg();
        Date cmsbondRedemption2 = BondCalendar.Adjust(cmsBondMaturityDate2, BusinessDayConvention.Following);
        var bond = new Bond(
            SettlementDays,
            BondCalendar,
            vars.FaceAmount,
            cmsBondMaturityDate2,
            cmsBondStartDate2,
            [.. cmsBondLeg2, new SimpleCashFlow(100.0, cmsbondRedemption2)]);
        return CouponPricerFunctions.SetCouponPricer(bond, vars.CmsPricer);
    }

    // Zero Coupon bond (Isin: DE0004771662 IBRD 0 12/20/15)
    private static Bond ZeroCpnBond1(CommonVars vars) =>
        new ZeroCouponBond(
            SettlementDays,
            BondCalendar,
            vars.FaceAmount,
            new Date(20, December, 2015),
            BusinessDayConvention.Following,
            100.0,
            new Date(19, December, 1985));

    private static Bond GenericZeroCpnBond1(CommonVars vars)
    {
        Date zeroCpnBondStartDate1 = new(19, December, 1985);
        Date zeroCpnBondMaturityDate1 = new(20, December, 2015);
        Date zeroCpnBondRedemption1 = BondCalendar.Adjust(zeroCpnBondMaturityDate1, BusinessDayConvention.Following);
        return new Bond(
            SettlementDays,
            BondCalendar,
            vars.FaceAmount,
            zeroCpnBondMaturityDate1,
            zeroCpnBondStartDate1,
            [new SimpleCashFlow(100.0, zeroCpnBondRedemption1)]);
    }

    // Zero Coupon bond (Isin: IT0001200390 ISPIM 0 02/17/28)
    private static Bond ZeroCpnBond2(CommonVars vars) =>
        new ZeroCouponBond(
            SettlementDays,
            BondCalendar,
            vars.FaceAmount,
            new Date(17, February, 2028),
            BusinessDayConvention.Following,
            100.0,
            new Date(17, February, 1998));

    private static Bond GenericZeroCpnBond2(CommonVars vars)
    {
        Date zeroCpnBondStartDate2 = new(17, February, 1998);
        Date zeroCpnBondMaturityDate2 = new(17, February, 2028);
        Date zerocpbondRedemption2 = BondCalendar.Adjust(zeroCpnBondMaturityDate2, BusinessDayConvention.Following);
        return new Bond(
            SettlementDays,
            BondCalendar,
            vars.FaceAmount,
            zeroCpnBondMaturityDate2,
            zeroCpnBondStartDate2,
            [new SimpleCashFlow(100.0, zerocpbondRedemption2)]);
    }

    private sealed class CommonVars
    {
        // initial setup
        public CommonVars()
        {
            Natural swapSettlementDays = 2;
            FaceAmount = 100.0;
            BusinessDayConvention fixedConvention = BusinessDayConvention.Unadjusted;
            Compounding = Compounding.Continuous;
            Frequency fixedFrequency = Frequency.Annual;
            Frequency floatingFrequency = Frequency.Semiannual;
            var today = new Date(24, April, 2007);
            Context = new EvaluationContext { EvaluationDate = today };

            TermStructure = new FlatForward(today, 0.05, new Actual365Fixed());
            IborIndex = new Euribor(new Period(floatingFrequency), TermStructure);
            Calendar calendar = IborIndex.FixingCalendar;
            SwapIndex = new SwapIndex(
                "EuriborSwapIsdaFixA",
                new Period(10, TimeUnit.Years),
                swapSettlementDays,
                IborIndex.Currency,
                calendar,
                new Period(fixedFrequency),
                fixedConvention,
                IborIndex.DayCounter,
                IborIndex);
            Spread = 0.0;
            NonNullSpread = 0.003;

            Pricer = new BlackIborCouponPricer(Context);
            SwaptionVolatilityStructure swaptionVolatilityStructure = new ConstantSwaptionVolatility(
                today, new NullCalendar(), BusinessDayConvention.Following, 0.2, new Actual365Fixed());
            CmsPricer = new AnalyticHaganPricer(swaptionVolatilityStructure, GFunctionFactoryYieldCurveModel.Standard, 0.01);
        }

        // common data
        public IborIndex IborIndex { get; }

        public SwapIndex SwapIndex { get; }

        public IborCouponPricer Pricer { get; }

        public CmsCouponPricer CmsPricer { get; }

        public Spread Spread { get; }

        public Spread NonNullSpread { get; }

        public Real FaceAmount { get; }

        public Compounding Compounding { get; }

        public YieldTermStructure TermStructure { get; }

        public EvaluationContext Context { get; private set; }

        // QuantLib's index->addFixing, into the context the later calls read.
        public void AddFixing(InterestRateIndex index, Date fixingDate, Real fixing) =>
            Context = Context with { Fixings = Context.Fixings.AddFixing(index, fixingDate, fixing) };

        // QuantLib's bond->cleanPrice() with a DiscountingBondEngine on the term structure.
        public Real CleanPrice(Bond bond) =>
            bond.CleanPrice(DiscountingBondEngine.Calculate(bond, Context, TermStructure), Context);

        // QuantLib's DiscountingSwapEngine on the term structure, with its defaults.
        public AssetSwapResults Price(AssetSwap swap) => DiscountingSwapEngine.Calculate(swap, Context, TermStructure);

        // QuantLib's AssetSwap(payBondCoupon, bond, price, index, spread, schedule, index->dayCounter(), parSwap).
        public AssetSwap AssetSwap(
            bool payFixedRate, Bond bond, Real price, IborIndex index, Spread spread, bool parSwap, Schedule? schedule = null) =>
            new(payFixedRate, bond, price, index, spread, Context, schedule, index.DayCounter, parSwap);
    }
}
