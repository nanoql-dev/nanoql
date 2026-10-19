// SPDX-FileCopyrightText: Copyright (C) 2018 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2021, 2022 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/callablebonds.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Replaced: testObservability (Vectors/Experimental/CallableBonds/CallableBondVectorTests.cs)

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class CallableBondsTests
{
    // Adapted: the engine (lines 109-111) is TreeCallableZeroCouponBondEngine.Calculate on the model and the curve,
    // and each bond's settlement value (lines 139, 160, 194, 215) is read from its results under the fixture's context.
    [Fact]
    public void TestInterplay()
    {
        // Testing interplay of callability and puttability for callable bonds...

        var vars = new Globals();

        YieldTermStructure termStructure = vars.MakeFlatCurve(0.03);
        var model = new HullWhite(termStructure);

        Size timeSteps = 240;

        CallableBondResults Engine(CallableBond bond) =>
            TreeCallableZeroCouponBondEngine.Calculate(bond, vars.Context, model, timeSteps, termStructure);

        /* case 1: an earlier out-of-the-money callability must prevent
                   a later in-the-money puttability
        */

        var callabilities = new List<Callability>
        {
            new(new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Call,
                vars.Calendar.Advance(vars.IssueDate(), 4, TimeUnit.Years)),
            new(new BondPrice(1000.0, BondPriceType.Clean), CallabilityType.Put,
                vars.Calendar.Advance(vars.IssueDate(), 6, TimeUnit.Years)),
        };

        var bond = new CallableZeroCouponBond(
            3, 100.0, vars.Calendar, vars.MaturityDate(), new Thirty360(Thirty360Convention.BondBasis),
            vars.RollingConvention, 100.0, vars.IssueDate(), callabilities);

        Real expected = callabilities[0].Price.Amount
                        * termStructure.Discount(callabilities[0].Date)
                        / termStructure.Discount(bond.SettlementDate(vars.Context));

        Real settlementValue = Engine(bond).SettlementValue!.Value;
        Assert.False(
            Math.Abs(settlementValue - expected) > 1.0e-2,
            $"callability not exercised correctly: calculated NPV {settlementValue}, expected {expected}, "
            + $"difference {settlementValue - expected}");

        /* case 2: same as case 1, with an added callability later on */

        callabilities.Add(new Callability(
            new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Call,
            vars.Calendar.Advance(vars.IssueDate(), 8, TimeUnit.Years)));

        bond = new CallableZeroCouponBond(
            3, 100.0, vars.Calendar, vars.MaturityDate(), new Thirty360(Thirty360Convention.BondBasis),
            vars.RollingConvention, 100.0, vars.IssueDate(), callabilities);

        settlementValue = Engine(bond).SettlementValue!.Value;
        Assert.False(
            Math.Abs(settlementValue - expected) > 1.0e-2,
            $"callability not exercised correctly: calculated NPV {settlementValue}, expected {expected}, "
            + $"difference {settlementValue - expected}");

        /* case 3: an earlier in-the-money puttability must prevent
                   a later in-the-money callability
        */

        callabilities.Clear();

        callabilities.Add(new Callability(
            new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Put,
            vars.Calendar.Advance(vars.IssueDate(), 4, TimeUnit.Years)));

        callabilities.Add(new Callability(
            new BondPrice(10.0, BondPriceType.Clean), CallabilityType.Call,
            vars.Calendar.Advance(vars.IssueDate(), 6, TimeUnit.Years)));

        bond = new CallableZeroCouponBond(
            3, 100.0, vars.Calendar, vars.MaturityDate(), new Thirty360(Thirty360Convention.BondBasis),
            vars.RollingConvention, 100.0, vars.IssueDate(), callabilities);

        expected = callabilities[0].Price.Amount
                   * termStructure.Discount(callabilities[0].Date)
                   / termStructure.Discount(bond.SettlementDate(vars.Context));

        settlementValue = Engine(bond).SettlementValue!.Value;
        Assert.False(
            Math.Abs(settlementValue - expected) > 1.0e-2,
            $"puttability not exercised correctly: calculated NPV {settlementValue}, expected {expected}, "
            + $"difference {settlementValue - expected}");

        /* case 4: same as case 3, with an added puttability later on */

        callabilities.Add(new Callability(
            new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Put,
            vars.Calendar.Advance(vars.IssueDate(), 8, TimeUnit.Years)));

        bond = new CallableZeroCouponBond(
            3, 100.0, vars.Calendar, vars.MaturityDate(), new Thirty360(Thirty360Convention.BondBasis),
            vars.RollingConvention, 100.0, vars.IssueDate(), callabilities);

        settlementValue = Engine(bond).SettlementValue!.Value;
        Assert.False(
            Math.Abs(settlementValue - expected) > 1.0e-2,
            $"puttability not exercised correctly: calculated NPV {settlementValue}, expected {expected}, "
            + $"difference {settlementValue - expected}");
    }

    // Adapted: the engines (lines 246-247, 265-267) are DiscountingBondEngine.Calculate and
    // TreeCallableFixedRateBondEngine.Calculate, and the clean prices (lines 283-297) are read from their results.
    [Fact]
    public void TestConsistency()
    {
        // Testing consistency of callable bonds...

        var vars = new Globals();

        YieldTermStructure termStructure = vars.MakeFlatCurve(0.032);
        var model = new HullWhite(termStructure);

        Schedule schedule = vars.MakeSchedule(vars.IssueDate(), vars.MaturityDate());

        Rate[] coupons = [0.05];

        var bond = new FixedRateBond(3, 100.0, schedule, coupons, new Thirty360(Thirty360Convention.BondBasis));
        Real bondPrice = bond.CleanPrice(DiscountingBondEngine.Calculate(bond, vars.Context, termStructure), vars.Context);

        ImmutableArray<Callability> callabilities =
        [
            .. vars.EvenYears().Select(d => new Callability(
                new BondPrice(110.0, BondPriceType.Clean), CallabilityType.Call, d)),
        ];

        ImmutableArray<Callability> puttabilities =
        [
            .. vars.OddYears().Select(d => new Callability(
                new BondPrice(90.0, BondPriceType.Clean), CallabilityType.Put, d)),
        ];

        Size timeSteps = 240;

        Real CleanPrice(CallableBond b) => b.CleanPrice(
            TreeCallableFixedRateBondEngine.Calculate(b, vars.Context, model, timeSteps, termStructure), vars.Context);

        var callable = new CallableFixedRateBond(
            3, 100.0, schedule, coupons, new Thirty360(Thirty360Convention.BondBasis), vars.RollingConvention, 100.0,
            vars.IssueDate(), callabilities);

        var puttable = new CallableFixedRateBond(
            3, 100.0, schedule, coupons, new Thirty360(Thirty360Convention.BondBasis), vars.RollingConvention, 100.0,
            vars.IssueDate(), puttabilities);

        Real callablePrice = CleanPrice(callable);
        Assert.False(
            bondPrice <= callablePrice,
            $"inconsistent prices: plain bond {bondPrice}, callable {callablePrice} (should be lower)");

        Real puttablePrice = CleanPrice(puttable);
        Assert.False(
            bondPrice >= puttablePrice,
            $"inconsistent prices: plain bond {bondPrice}, puttable {puttablePrice} (should be higher)");
    }

    // Adapted: the engines (lines 399-403, 407-412, 454-455) are DiscountingBondEngine.Calculate and
    // TreeCallableFixedRateBondEngine.Calculate, and the clean prices (lines 416-469) are read from their results.
    [Fact]
    public void TestDegenerate()
    {
        // Repricing bonds using degenerate callable bonds...

        var vars = new Globals();

        YieldTermStructure termStructure = vars.MakeFlatCurve(0.034);
        var model = new HullWhite(termStructure);

        Schedule schedule = vars.MakeSchedule(vars.IssueDate(), vars.MaturityDate());

        Rate[] coupons = [0.05];

        var zeroCouponBond = new ZeroCouponBond(3, vars.Calendar, 100.0, vars.MaturityDate(), vars.RollingConvention);
        var couponBond = new FixedRateBond(3, 100.0, schedule, coupons, new Thirty360(Thirty360Convention.BondBasis));

        // no callability
        var callabilities = new List<Callability>();

        var bond1 = new CallableZeroCouponBond(
            3, 100.0, vars.Calendar, vars.MaturityDate(), new Thirty360(Thirty360Convention.BondBasis),
            vars.RollingConvention, 100.0, vars.IssueDate(), callabilities);

        var bond2 = new CallableFixedRateBond(
            3, 100.0, schedule, coupons, new Thirty360(Thirty360Convention.BondBasis), vars.RollingConvention, 100.0,
            vars.IssueDate(), callabilities);

        Real Discounting(Bond b) =>
            b.CleanPrice(DiscountingBondEngine.Calculate(b, vars.Context, termStructure), vars.Context);

        Size timeSteps = 240;

        Real Tree(CallableBond b) => b.CleanPrice(
            TreeCallableFixedRateBondEngine.Calculate(b, vars.Context, model, timeSteps, termStructure), vars.Context);

        double tolerance = 1.0e-4;

        Assert.False(
            Math.Abs(Tree(bond1) - Discounting(zeroCouponBond)) > tolerance,
            $"failed to reproduce zero-coupon bond price: calculated {Tree(bond1)}, expected {Discounting(zeroCouponBond)}");

        Assert.False(
            Math.Abs(Tree(bond2) - Discounting(couponBond)) > tolerance,
            $"failed to reproduce fixed-rate bond price: calculated {Tree(bond2)}, expected {Discounting(couponBond)}");

        // out-of-the-money callability

        foreach (Date callabilityDate in vars.EvenYears())
        {
            callabilities.Add(new Callability(
                new BondPrice(10000.0, BondPriceType.Clean), CallabilityType.Call, callabilityDate));
        }

        foreach (Date puttabilityDate in vars.OddYears())
        {
            callabilities.Add(new Callability(
                new BondPrice(0.0, BondPriceType.Clean), CallabilityType.Put, puttabilityDate));
        }

        bond1 = new CallableZeroCouponBond(
            3, 100.0, vars.Calendar, vars.MaturityDate(), new Thirty360(Thirty360Convention.BondBasis),
            vars.RollingConvention, 100.0, vars.IssueDate(), callabilities);

        bond2 = new CallableFixedRateBond(
            3, 100.0, schedule, coupons, new Thirty360(Thirty360Convention.BondBasis), vars.RollingConvention, 100.0,
            vars.IssueDate(), callabilities);

        Assert.False(
            Math.Abs(Tree(bond1) - Discounting(zeroCouponBond)) > tolerance,
            $"failed to reproduce zero-coupon bond price: calculated {Tree(bond1)}, expected {Discounting(zeroCouponBond)}");

        Assert.False(
            Math.Abs(Tree(bond2) - Discounting(couponBond)) > tolerance,
            $"failed to reproduce fixed-rate bond price: calculated {Tree(bond2)}, expected {Discounting(couponBond)}");
    }

    // Adapted: the evaluation date the case sets (line 479) is the context's; the engine (lines 517-519) is
    // TreeCallableFixedRateBondEngine.Calculate, and the clean prices (lines 531-561) are read from its results.
    [Fact]
    public void TestCached()
    {
        // Testing callable-bond value against cached values...

        var vars = new Globals(new Date(3, June, 2004), 3);

        YieldTermStructure termStructure = vars.MakeFlatCurve(0.032);
        var model = new HullWhite(termStructure);

        Schedule schedule = vars.MakeSchedule(vars.IssueDate(), vars.MaturityDate());

        Rate[] coupons = [0.05];

        var callabilities = new List<Callability>();
        var puttabilities = new List<Callability>();
        var allExercises = new List<Callability>();

        foreach (Date callabilityDate in vars.EvenYears())
        {
            var exercise = new Callability(
                new BondPrice(110.0, BondPriceType.Clean), CallabilityType.Call, callabilityDate);
            callabilities.Add(exercise);
            allExercises.Add(exercise);
        }

        foreach (Date puttabilityDate in vars.OddYears())
        {
            var exercise = new Callability(
                new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Put, puttabilityDate);
            puttabilities.Add(exercise);
            allExercises.Add(exercise);
        }

        Size timeSteps = 240;

        Real CleanPrice(CallableBond b) => b.CleanPrice(
            TreeCallableFixedRateBondEngine.Calculate(b, vars.Context, model, timeSteps, termStructure), vars.Context);

        double tolerance = 1.0e-8;

        double storedPrice1 = 110.60975477;
        var bond1 = new CallableFixedRateBond(
            3, 10000.0, schedule, coupons, new Thirty360(Thirty360Convention.BondBasis), vars.RollingConvention,
            100.0, vars.IssueDate(), callabilities);

        Assert.False(
            Math.Abs(CleanPrice(bond1) - storedPrice1) > tolerance,
            $"failed to reproduce cached callable-bond price: calculated {CleanPrice(bond1)}, expected {storedPrice1}");

        double storedPrice2 = 115.16559362;
        var bond2 = new CallableFixedRateBond(
            3, 10000.0, schedule, coupons, new Thirty360(Thirty360Convention.BondBasis), vars.RollingConvention,
            100.0, vars.IssueDate(), puttabilities);

        Assert.False(
            Math.Abs(CleanPrice(bond2) - storedPrice2) > tolerance,
            $"failed to reproduce cached puttable-bond price: calculated {CleanPrice(bond2)}, expected {storedPrice2}");

        double storedPrice3 = 110.97509625;
        var bond3 = new CallableFixedRateBond(
            3, 10000.0, schedule, coupons, new Thirty360(Thirty360Convention.BondBasis), vars.RollingConvention,
            100.0, vars.IssueDate(), allExercises);

        Assert.False(
            Math.Abs(CleanPrice(bond3) - storedPrice3) > tolerance,
            $"failed to reproduce cached callable/puttable-bond price: calculated {CleanPrice(bond3)}, "
            + $"expected {storedPrice3}");
    }

    // Adapted: the evaluation date the case sets (line 580) is the context's; the engines (lines 618-619, 629-630) are
    // TreeCallableFixedRateBondEngine.Calculate and DiscountingBondEngine.Calculate, the NPVs and the clean price
    // (lines 647-648, 658) are read from their results, and the OAS (lines 659-660) takes the tree engine.
    [Fact]
    public void TestSnappingExerciseDate2ClosestCouponDate()
    {
        // Testing snap of callability dates to the closest coupon date...

        /* This is a test case inspired by
         * https://github.com/lballabio/QuantLib/issues/930#issuecomment-853886024 */

        var today = new Date(18, May, 2021);

        var context = new EvaluationContext { EvaluationDate = today };

        var calendar = new UnitedStates(UnitedStatesMarket.FederalReserve);
        var accrualDCC = new Thirty360(Thirty360Convention.USA);
        Frequency frequency = Frequency.Semiannual;
        YieldTermStructure termStructure = new FlatForward(today, 0.02, new Actual365Fixed());

        (FixedRateBond FixedRateBond, CallableFixedRateBond CallableBond, HullWhite Model) MakeBonds(Date callDate)
        {
            Natural settlementDays = 2;
            var settlementDate = new Date(20, May, 2021);
            double coupon = 0.05;
            double faceAmount = 100.00;
            double redemption = faceAmount;
            var maturityDate = new Date(14, February, 2026);
            Date issueDate = settlementDate - (2 * 366);
            Schedule schedule = new MakeSchedule()
                .From(issueDate)
                .To(maturityDate)
                .WithFrequency(frequency)
                .WithCalendar(calendar)
                .WithConvention(BusinessDayConvention.Unadjusted)
                .WithTerminationDateConvention(BusinessDayConvention.Unadjusted)
                .Backwards()
                .EndOfMonth(false)
                .ToSchedule();
            Rate[] coupons = [.. Enumerable.Repeat(coupon, schedule.Size - 1)];

            ImmutableArray<Callability> callabilitySchedule =
            [
                new Callability(new BondPrice(faceAmount, BondPriceType.Clean), CallabilityType.Call, callDate),
            ];

            var newCallableBond = new CallableFixedRateBond(
                settlementDays, faceAmount, schedule, coupons, accrualDCC, BusinessDayConvention.Following,
                redemption, issueDate, callabilitySchedule);

            var model = new HullWhite(termStructure, 1e-12, 0.003);

            Schedule fixedRateBondSchedule = schedule.Until(callDate);
            Rate[] fixedRateBondCoupons = [.. Enumerable.Repeat(coupon, schedule.Size - 1)];

            var newFixedRateBond = new FixedRateBond(
                settlementDays, faceAmount, fixedRateBondSchedule, fixedRateBondCoupons, accrualDCC,
                BusinessDayConvention.Following, redemption, issueDate);

            return (newFixedRateBond, newCallableBond, model);
        }

        var initialCallDate = new Date(14, February, 2022);
        Real tolerance = 1e-10;
        Real prevOAS = 0.0266;
        Real expectedOasStep = 0.00005;

        for (int i = -10; i < 11; i++)
        {
            Date callDate = initialCallDate + i;
            if (calendar.IsBusinessDay(callDate))
            {
                (FixedRateBond fixedRateBond, CallableFixedRateBond callableBond, HullWhite model) = MakeBonds(callDate);

                CallableBondResults Tree(CallableBond b, EvaluationContext x, double s) =>
                    TreeCallableFixedRateBondEngine.Calculate(b, x, model, 40, null, s);

                Real npvFixedRateBond = DiscountingBondEngine.Calculate(fixedRateBond, context, termStructure).NPV!.Value;
                Real npvCallable = Tree(callableBond, context, 0.0).NPV!.Value;

                Assert.False(
                    Math.Abs(npvCallable - npvFixedRateBond) > tolerance,
                    $"failed to reproduce bond price at {callDate}: calculated {npvCallable}, expected "
                    + $"{npvFixedRateBond} +/- {tolerance}");

                Real cleanPrice = callableBond.CleanPrice(Tree(callableBond, context, 0.0), context) - 2.0;
                Real oas = callableBond.OAS(
                    cleanPrice, termStructure, accrualDCC, Compounding.Continuous, frequency, Tree, context);
                Assert.False(
                    prevOAS - oas < expectedOasStep,
                    $"failed to get expected change in OAS at {callDate}: calculated {oas}, previous {prevOAS}, "
                    + $"should at least change by {expectedOasStep}");
                prevOAS = oas;
            }
        }
    }

    // Adapted: the evaluation date the case sets (line 680) is the context's; the engine (lines 696-697) is
    // TreeCallableFixedRateBondEngine.Calculate, and the clean prices (line 704) are read from its results.
    [Fact]
    public void TestUnexercisableCallability()
    {
        // Testing that an unexercisable callability leaves the price alone...

        var vars = new Globals(new Date(3, June, 2004), 3);

        YieldTermStructure termStructure = vars.MakeFlatCurve(0.032);
        var model = new HullWhite(termStructure);

        Schedule schedule = vars.MakeSchedule(vars.IssueDate(), vars.MaturityDate());

        Size timeSteps = 240;

        Real Price(IEnumerable<Callability> exercises)
        {
            var bond = new CallableFixedRateBond(
                3, 10000.0, schedule, [0.05], new Thirty360(Thirty360Convention.BondBasis), vars.RollingConvention,
                100.0, vars.IssueDate(), exercises);
            return bond.CleanPrice(
                TreeCallableFixedRateBondEngine.Calculate(bond, vars.Context, model, timeSteps, termStructure),
                vars.Context);
        }

        Date couponDate = schedule.Date(8);
        Real tolerance = 1e-10;

        foreach (CallabilityType type in new[] { CallabilityType.Call, CallabilityType.Put })
        {
            Real strike = (type == CallabilityType.Call) ? 100.0 : 110.0;
            Real unreachable = (type == CallabilityType.Call) ? 200.0 : 0.0;

            ImmutableArray<Callability> onCouponDate =
            [
                new Callability(new BondPrice(strike, BondPriceType.Clean), type, couponDate),
            ];

            Real expected = Price(onCouponDate);

            for (Integer days = 1; days <= 7; ++days)
            {
                ImmutableArray<Callability> exercises =
                [
                    new Callability(new BondPrice(unreachable, BondPriceType.Dirty), type, couponDate - days),
                    onCouponDate[0],
                ];

                Real calculated = Price(exercises);
                Assert.False(
                    Math.Abs(calculated - expected) > tolerance,
                    $"unexercisable {(type == CallabilityType.Call ? "call" : "put")} on {couponDate - days} "
                    + $"changed the price: calculated {calculated}, expected {expected}");
            }
        }
    }

    // Adapted: the evaluation date the case sets (line 744) is the context's; the engine (lines 761-762) is
    // BlackCallableZeroCouponBondEngine.Calculate, and the clean price (line 765) is read from its results.
    [Fact]
    public void TestBlackEngine()
    {
        // Testing Black engine for European callable bonds...

        var vars = new Globals(new Date(20, September, 2022), 3);

        YieldTermStructure termStructure = vars.MakeFlatCurve(0.03);

        ImmutableArray<Callability> callabilities =
        [
            new Callability(
                new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Call,
                vars.Calendar.Advance(vars.IssueDate(), 4, TimeUnit.Years)),
        ];

        var bond = new CallableZeroCouponBond(
            3, 10000.0, vars.Calendar, vars.MaturityDate(), new Thirty360(Thirty360Convention.BondBasis),
            vars.RollingConvention, 100.0, vars.IssueDate(), callabilities);

        CallableBondResults results = BlackCallableZeroCouponBondEngine.Calculate(bond, vars.Context, 0.3, termStructure);

        Real cached = 74.54521578;
        Real calculated = bond.CleanPrice(results, vars.Context);

        Assert.False(
            Math.Abs(calculated - cached) > 1.0e-4,
            $"failed to reproduce cached price: calculated NPV {calculated}, cached {cached}, difference "
            + $"{calculated - cached}");
    }

    // Adapted: the engines (lines 816-817, 835-836) are BlackCallableFixedRateBondEngine.Calculate, and the dirty and
    // clean prices (lines 819, 838) are read from its results under the fixture's context.
    [Fact]
    public void TestImpliedVol()
    {
        // Testing implied-volatility calculation for callable bonds...

        var vars = new Globals();

        YieldTermStructure termStructure = vars.MakeFlatCurve(0.03);

        Schedule schedule = vars.MakeSchedule(vars.IssueDate(), vars.MaturityDate());

        Rate[] coupons = [0.01];

        ImmutableArray<Callability> callabilities =
        [
            new Callability(new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Call, schedule.At(8)),
        ];

        var bond = new CallableFixedRateBond(
            3, 10000.0, schedule, coupons, new Thirty360(Thirty360Convention.BondBasis), vars.RollingConvention, 100.0,
            vars.IssueDate(), callabilities);

        var targetPrice = new BondPrice(78.50, BondPriceType.Dirty);
        Volatility volatility = bond.ImpliedVolatility(
            targetPrice,
            termStructure,
            vars.Context,
            1e-8,  // accuracy
            200,   // max evaluations
            1e-4,  // min vol
            1.0);  // max vol

        CallableBondResults results =
            BlackCallableFixedRateBondEngine.Calculate(bond, vars.Context, volatility, termStructure);

        Real dirtyPrice = bond.DirtyPrice(results, vars.Context);
        Assert.False(
            Math.Abs(dirtyPrice - targetPrice.Amount) > 1.0e-4,
            $"failed to reproduce target dirty price with implied volatility: calculated price {dirtyPrice}, "
            + $"expected {targetPrice.Amount}, difference {dirtyPrice - targetPrice.Amount}");

        targetPrice = new BondPrice(78.50, BondPriceType.Clean);
        volatility = bond.ImpliedVolatility(
            targetPrice,
            termStructure,
            vars.Context,
            1e-8,  // accuracy
            200,   // max evaluations
            1e-4,  // min vol
            1.0);  // max vol

        results = BlackCallableFixedRateBondEngine.Calculate(bond, vars.Context, volatility, termStructure);

        Real cleanPrice = bond.CleanPrice(results, vars.Context);
        Assert.False(
            Math.Abs(cleanPrice - targetPrice.Amount) > 1.0e-4,
            $"failed to reproduce target clean price with implied volatility: calculated price {cleanPrice}, "
            + $"expected {targetPrice.Amount}, difference {cleanPrice - targetPrice.Amount}");
    }

    // Adapted: the evaluation date the case sets (line 854) is the context's; the engine (lines 887-888) is
    // BlackCallableFixedRateBondEngine.Calculate, and the clean price (line 893) is read from its results.
    [Fact]
    public void TestBlackEngineDeepInTheMoney()
    {
        // Testing Black engine for deep ITM European callable bond...

        var vars = new Globals(new Date(20, September, 2022), 3);

        YieldTermStructure termStructure = vars.MakeFlatCurve(0.05);

        Schedule schedule = vars.MakeSchedule(vars.IssueDate(), vars.MaturityDate());

        Rate[] coupons = [0.0];

        Date callabilityDate = schedule.At(6);
        Real strike = 50.0;  // definitely ITM; see also the volatility value below

        ImmutableArray<Callability> callabilities =
        [
            new Callability(new BondPrice(50.0, BondPriceType.Clean), CallabilityType.Call, callabilityDate),
        ];

        var bond = new CallableFixedRateBond(
            3, 10000.0, schedule, coupons, new Thirty360(Thirty360Convention.BondBasis), vars.RollingConvention, 100.0,
            vars.IssueDate(), callabilities);

        Volatility vol = 1e-10;
        CallableBondResults results = BlackCallableFixedRateBondEngine.Calculate(bond, vars.Context, vol, termStructure);

        Real expected = strike * termStructure.Discount(callabilityDate)
                        / termStructure.Discount(bond.SettlementDate(vars.Context));
        Real calculated = bond.CleanPrice(results, vars.Context);

        Assert.False(
            Math.Abs(calculated - expected) > 1.0e-8,
            $"failed to reproduce expected price: calculated NPV {calculated}, expected {expected}, difference "
            + $"{calculated - expected}");
    }

    // Adapted: the evaluation date the case sets (line 911) is the context's; the engine (lines 918-919, 940) is
    // TreeCallableFixedRateBondEngine.Calculate, and the clean price (line 942) is read from its results.
    [Fact]
    public void TestCallableFixedRateBondWithArbitrarySchedule()
    {
        // Testing callable fixed-rate bond with arbitrary schedule...

        Natural settlementDays = 2;
        var vars = new Globals(new Date(10, January, 2020), settlementDays);

        YieldTermStructure termStructure = vars.MakeFlatCurve(0.03);
        var model = new HullWhite(termStructure);

        Size timeSteps = 240;

        Date[] dates = new Date[4];
        dates[0] = new Date(20, February, 2020);
        dates[1] = new Date(15, August, 2020);
        dates[2] = new Date(25, September, 2021);
        dates[3] = new Date(27, January, 2022);

        var schedule = new Schedule(dates, vars.Calendar, BusinessDayConvention.Unadjusted);

        ImmutableArray<Callability> callabilities =
        [
            new Callability(new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Call, dates[2]),
        ];

        Rate[] coupons = [0.06];

        var callableBond = new CallableFixedRateBond(
            settlementDays, 100.0, schedule, coupons, vars.DayCounter, vars.RollingConvention, 100.0, vars.IssueDate(),
            callabilities);

        CallableBondResults results =
            TreeCallableFixedRateBondEngine.Calculate(callableBond, vars.Context, model, timeSteps, termStructure);

        // BOOST_CHECK_NO_THROW(callableBond.cleanPrice())
        _ = callableBond.CleanPrice(results, vars.Context);
    }

    // Adapted: the evaluation date the case sets (line 952) is the context's; the engine (lines 964-965) is
    // TreeCallableFixedRateBondEngine.Calculate, which the OAS and the clean prices from it (lines 999-1009) take.
    [Fact]
    public void TestCallableBondOasWithDifferentNotinals()
    {
        // Testing callable fixed-rate bond OAS with different notionals...

        Natural settlementDays = 2;
        var vars = new Globals(new Date(10, January, 2020), settlementDays);

        Rate[] coupons = [0.055];
        DayCounter dc = vars.DayCounter;
        Compounding compounding = Compounding.Compounded;
        Frequency frequency = Frequency.Semiannual;

        YieldTermStructure termStructure = vars.MakeFlatCurve(0.03);
        var model = new HullWhite(termStructure);

        Size timeSteps = 240;
        CallableBondResults Engine(CallableBond b, EvaluationContext x, double s) =>
            TreeCallableFixedRateBondEngine.Calculate(b, x, model, timeSteps, termStructure, s);

        Schedule schedule = new MakeSchedule()
            .From(vars.IssueDate())
            .To(vars.MaturityDate())
            .WithCalendar(vars.Calendar)
            .WithFrequency(frequency)
            .WithConvention(vars.RollingConvention)
            .WithRule(DateGenerationRule.Backward)
            .ToSchedule();

        Date firstCallDate = schedule.At(schedule.Size - 5);
        Date lastCallDate = schedule.At(schedule.Size - 2);
        Schedule callabilityDates = schedule.After(firstCallDate);
        callabilityDates = callabilityDates.Until(lastCallDate);

        var callSchedule = new List<Callability>();
        foreach (Date callDate in callabilityDates)
        {
            var callPrice = new BondPrice(100, BondPriceType.Clean);
            callSchedule.Add(new Callability(callPrice, CallabilityType.Call, callDate));
        }

        var callableBond100 = new CallableFixedRateBond(
            settlementDays, 100.0, schedule, coupons, vars.DayCounter, vars.RollingConvention, 100.0, vars.IssueDate(),
            callSchedule);

        var callableBond25 = new CallableFixedRateBond(
            settlementDays, 25.0, schedule, coupons, vars.DayCounter, vars.RollingConvention, 100.0, vars.IssueDate(),
            callSchedule);

        Real cleanPrice = 96.0;
        Real oas100 = callableBond100.OAS(cleanPrice, termStructure, dc, compounding, frequency, Engine, vars.Context);
        Real oas25 = callableBond25.OAS(cleanPrice, termStructure, dc, compounding, frequency, Engine, vars.Context);
        Assert.False(
            oas100 != oas25,
            $"failed to reproduce equal OAS with different notionals: OAS(bps) with notional 100.0 {oas100 * 10000}, "
            + $"with notional 25.0 {oas25 * 10000}");

        Real oas = 0.0300;
        Real cleanPrice100 = callableBond100.CleanPriceOAS(
            oas, termStructure, dc, compounding, frequency, Engine, vars.Context);
        Real cleanPrice25 = callableBond25.CleanPriceOAS(
            oas, termStructure, dc, compounding, frequency, Engine, vars.Context);
        Assert.False(
            cleanPrice100 != cleanPrice25,
            $"failed to reproduce equal clean price given OAS with different notionals: clean price with notional "
            + $"100.0 {cleanPrice100}, with notional 25.0 {cleanPrice25}");
    }

    // Adapted: the evaluation date the case sets (line 1031) is the context's; the engine (lines 1085-1087) is
    // TreeCallableFixedRateBondEngine.Calculate, which the OAS (lines 1089-1090) takes.
    [Fact]
    public void TestOasContinuityThroughExCouponWindow()
    {
        // Testing OAS continuity when call date crosses ex-coupon period...

        /* This is a test case inspired by
         * https://github.com/lballabio/QuantLib/issues/2236
         *
         * When a call date falls between the ex-coupon date and the payment date,
         * OAS should vary smoothly (within tree discretization noise). Before the
         * fix, OAS jumped from ~+154 bps to ~-513 bps at the ex-coupon boundary
         * due to an inconsistency between how the tree continuation value includes
         * coupons and how the call price uses negative accrued during ex-coupon. */

        var today = new Date(31, January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };

        Natural settlementDays = 0;
        var calendar = new UnitedStates(UnitedStatesMarket.NYSE);
        var dc = new Thirty360(Thirty360Convention.BondBasis);
        BusinessDayConvention bdc = BusinessDayConvention.Unadjusted;
        Frequency frequency = Frequency.Quarterly;
        var exCouponPeriod = new Period(14, TimeUnit.Days);

        Date issueDate = today;
        var maturityDate = new Date(31, January, 2029);
        Real faceAmount = 100.0;
        Rate[] coupons = [0.06];
        Real redemption = 100.0;

        YieldTermStructure termStructure = new FlatForward(today, 0.04, dc);
        var model = new HullWhite(termStructure);

        Schedule schedule = new MakeSchedule()
            .From(issueDate)
            .To(maturityDate)
            .WithFrequency(frequency)
            .WithCalendar(calendar)
            .WithConvention(bdc)
            .WithTerminationDateConvention(bdc)
            .Backwards()
            .EndOfMonth(true)
            .ToSchedule();

        // First coupon payment date after today
        Date firstPaymentDate = schedule[1];
        Date exCouponDate = firstPaymentDate - exCouponPeriod;

        // Sweep call date from 1 week before ex-coupon to 1 week after payment
        Date sweepStart = exCouponDate - 7;
        Date sweepEnd = firstPaymentDate + 7;

        Real cleanPrice = 100.0;
        Compounding compounding = Compounding.Compounded;

        Real maxOas = -double.MaxValue;
        Real minOas = double.MaxValue;

        CallableBondResults Engine(CallableBond b, EvaluationContext x, double s) =>
            TreeCallableFixedRateBondEngine.Calculate(b, x, model, 100, termStructure, s);

        for (Date callDate = sweepStart; callDate <= sweepEnd; callDate++)
        {
            ImmutableArray<Callability> callSchedule =
            [
                new Callability(new BondPrice(redemption, BondPriceType.Clean), CallabilityType.Call, callDate),
            ];

            var bond = new CallableFixedRateBond(
                settlementDays, faceAmount, schedule, coupons, dc, bdc, redemption, issueDate, callSchedule,
                exCouponPeriod, new NullCalendar());

            Real oas = bond.OAS(cleanPrice, termStructure, dc, compounding, frequency, Engine, context) * 10000.0;

            // std::max and std::min
            maxOas = maxOas < oas ? oas : maxOas;
            minOas = oas < minOas ? oas : minOas;
        }

        Real oasRange = maxOas - minOas;

        // OAS should be reasonably continuous through the ex-coupon window.
        // A range of 50 bps allows for tree discretization noise; the bug
        // produced a range of ~667 bps.
        Real tolerance = 50.0;
        Assert.False(
            oasRange > tolerance,
            $"OAS discontinuity across ex-coupon window: min OAS {minOas:F2} bps, max OAS {maxOas:F2} bps, range "
            + $"{oasRange:F2} bps, tolerance {tolerance:F2} bps (sweep from {sweepStart} to {sweepEnd})");
    }

    // Adapted: the evaluation date the case sets (line 1117) is the context's, which the curve's two settlement days
    // (line 1154) count from, as its Create factory does; the engine (lines 1161-1162) is
    // TreeCallableFixedRateBondEngine.Calculate, which the OAS family (lines 1165-1175) takes.
    [Fact]
    public void TestEffectiveDurationAndConvexity()
    {
        // Testing effective duration and convexity of a callable fixed-rate bond using dirty price...

        var settlementDate = new Date(30, November, 2023);
        var context = new EvaluationContext { EvaluationDate = settlementDate };

        var effectiveDate = new Date(20, May, 2021);
        var maturityDate = new Date(1, June, 2029);
        var firstCouponDate = new Date(1, December, 2021);

        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        DayCounter dayCount = new Thirty360(Thirty360Convention.ISDA);

        var schedule = new Schedule(
            effectiveDate,
            maturityDate,
            new Period(6, TimeUnit.Months),
            calendar,
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            true,
            context,
            firstCouponDate);

        ImmutableArray<Callability> callSchedule =
        [
            new Callability(new BondPrice(102.438, BondPriceType.Clean), CallabilityType.Call, new Date(1, June, 2024)),
            new Callability(new BondPrice(101.219, BondPriceType.Clean), CallabilityType.Call, new Date(1, June, 2025)),
            new Callability(new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Call, new Date(1, June, 2026)),
            new Callability(new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Call, new Date(1, June, 2029)),
        ];

        Natural settlementDays = 2;
        Real faceAmount = 100.0;
        Rate[] coupons = [0.04875];
        Real redemption = 100.0;

        var callableBond = new CallableFixedRateBond(
            settlementDays, faceAmount, schedule, coupons, dayCount, BusinessDayConvention.Unadjusted, redemption,
            effectiveDate, callSchedule);

        YieldTermStructure flatRate = FlatForward.Create(settlementDays, calendar, 0.05, dayCount, context);

        Real alpha = 0.03;
        Real sigma = 0.012;
        var hw = new HullWhite(flatRate, alpha, sigma);

        Size gridSteps = (maturityDate - settlementDate) / 30;
        CallableBondResults Engine(CallableBond b, EvaluationContext x, double s) =>
            TreeCallableFixedRateBondEngine.Calculate(b, x, hw, gridSteps, null, s);

        Real cleanPrice = 70.926;
        Spread oas = callableBond.OAS(
            cleanPrice, flatRate, dayCount, Compounding.Compounded, Frequency.Semiannual, Engine, context,
            settlementDate);

        Real shift = 0.001;
        Real effDur = callableBond.EffectiveDuration(
            oas, flatRate, dayCount, Compounding.Compounded, Frequency.Semiannual, Engine, context, shift);
        Real effConv = callableBond.EffectiveConvexity(
            oas, flatRate, dayCount, Compounding.Compounded, Frequency.Semiannual, Engine, context, shift);

        // Compute expected duration and convexity manually using dirty price
        Real accrued = callableBond.AccruedAmount(context, settlementDate);
        Real p0 = callableBond.CleanPriceOAS(
            oas, flatRate, dayCount, Compounding.Compounded, Frequency.Semiannual, Engine, context, settlementDate)
            + accrued;
        Real pUp = callableBond.CleanPriceOAS(
            oas + shift, flatRate, dayCount, Compounding.Compounded, Frequency.Semiannual, Engine, context,
            settlementDate) + accrued;
        Real pDown = callableBond.CleanPriceOAS(
            oas - shift, flatRate, dayCount, Compounding.Compounded, Frequency.Semiannual, Engine, context,
            settlementDate) + accrued;

        Real expectedEffDur = (pDown - pUp) / (2.0 * p0 * shift);
        Real expectedEffConv = (pDown + pUp - (2.0 * p0)) / (p0 * shift * shift);

        // Value if clean price were incorrectly used in the denominator
        Real cleanP0 = p0 - accrued;
        Real incorrectEffDur = (pDown - pUp) / (2.0 * cleanP0 * shift);

        CheckClose(effDur, expectedEffDur, 1e-4);
        CheckClose(effConv, expectedEffConv, 1e-4);

        // Verify that the result differs significantly from the clean-price-denominator calculation
        Assert.True(Math.Abs(effDur - incorrectEffDur) > 0.01);
    }

    // BOOST_CHECK_CLOSE: the difference within the tolerance, in percent, of both values.
    private static void CheckClose(Real a, Real b, Real percent)
    {
        Real diff = Math.Abs(a - b);
        Assert.True(
            diff <= Math.Abs(a) * percent / 100.0 && diff <= Math.Abs(b) * percent / 100.0,
            $"difference between {a} and {b} exceeds {percent}%");
    }

    // QuantLib's Globals: the test runner's evaluation date unless a case sets its own, the curve from the settlement
    // date the case's settlement days later.
    private sealed class Globals
    {
        public Globals(Date? today = null, int settlementDays = 2)
        {
            Today = today ?? new Date(16, September, 2015);
            Settlement = Calendar.Advance(Today, settlementDays, TimeUnit.Days);
        }

        public Date Today { get; }

        public Date Settlement { get; }

        public Calendar Calendar { get; } = new TARGET();

        public DayCounter DayCounter { get; } = new Actual365Fixed();

        public BusinessDayConvention RollingConvention { get; } = BusinessDayConvention.ModifiedFollowing;

        public EvaluationContext Context => new() { EvaluationDate = Today };

        // ensure that we're in mid-coupon
        public Date IssueDate() => Calendar.Adjust(Today - 100);

        // ensure that we're in mid-coupon
        public Date MaturityDate() => Calendar.Advance(IssueDate(), 10, TimeUnit.Years);

        public ImmutableArray<Date> EvenYears() =>
            [.. Enumerable.Range(1, 4).Select(i => Calendar.Advance(IssueDate(), 2 * i, TimeUnit.Years))];

        public ImmutableArray<Date> OddYears() =>
            [.. Enumerable.Range(0, 5).Select(i => Calendar.Advance(IssueDate(), (2 * i) + 1, TimeUnit.Years))];

        public YieldTermStructure MakeFlatCurve(double r) => new FlatForward(Settlement, r, DayCounter);

        // The cases' schedule: semiannual on TARGET, the rolling convention, backward.
        public Schedule MakeSchedule(Date from, Date to) => new MakeSchedule()
            .From(from)
            .To(to)
            .WithCalendar(Calendar)
            .WithFrequency(Frequency.Semiannual)
            .WithConvention(RollingConvention)
            .WithRule(DateGenerationRule.Backward)
            .ToSchedule();
    }
}
