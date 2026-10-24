// SPDX-FileCopyrightText: Copyright (C) 2022 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/bondforward.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// Adapted throughout: the fixture's relinkable handle is the curve itself, and its evaluation date the context; the
// bond's engine, which QuantLib's bond holds, is the static DiscountingBondEngine.Calculate on the curve, passed to
// the forward's Calculate, whose results its prices read.
[Trait("Oracle", "QuantLib")]
public class BondForwardTests
{
    [Fact]
    public void TestFuturesPriceReplication()
    {
        // Testing futures price replication...
        var vars = new CommonVars();

        Real tolerance = 1.0e-2;

        var issue = new Date(15, August, 2015);
        var maturity = new Date(15, August, 2046);
        Rate cpn = 0.025;

        Bond bnd = BuildBond(issue, maturity, cpn, vars.Context);

        var delivery = new Date(10, March, 2022);
        Real conversionFactor = 0.76871;
        BondForward bndFwd = BuildBondForward(bnd, vars.Curve, delivery, PositionType.Long);

        ForwardResults results = bndFwd.Calculate(vars.Context, vars.BondEngine);
        Real futuresPrice = bndFwd.CleanForwardPrice(results, vars.Context) / conversionFactor;
        Real expectedFuturesPrice = 207.47;

        Assert.False(
            Math.Abs(futuresPrice - expectedFuturesPrice) > tolerance,
            $"unable to replicate bond futures price: calculated {futuresPrice}, expected {expectedFuturesPrice}");
    }

    [Fact]
    public void TestCleanForwardPriceReplication()
    {
        // Testing clean forward price replication...
        var vars = new CommonVars();

        Real tolerance = 1.0e-2;

        var issue = new Date(15, August, 2015);
        var maturity = new Date(15, August, 2046);
        Rate cpn = 0.025;

        Bond bnd = BuildBond(issue, maturity, cpn, vars.Context);

        var delivery = new Date(10, March, 2022);
        BondForward bndFwd = BuildBondForward(bnd, vars.Curve, delivery, PositionType.Long);

        ForwardResults results = bndFwd.Calculate(vars.Context, vars.BondEngine);
        Real fwdCleanPrice = bndFwd.CleanForwardPrice(results, vars.Context);
        Real expectedFwdCleanPrice = results.ForwardValue!.Value - bnd.AccruedAmount(vars.Context, delivery);

        Assert.False(
            Math.Abs(fwdCleanPrice - expectedFwdCleanPrice) > tolerance,
            $"unable to replicate clean forward price: calculated {fwdCleanPrice}, expected {expectedFwdCleanPrice}");
    }

    [Fact]
    public void TestThatForwardValueIsEqualToSpotValueIfNoIncome()
    {
        // Testing that forward value is equal to spot value if no income...
        var vars = new CommonVars();

        Real tolerance = 1.0e-2;

        var issue = new Date(15, August, 2015);
        var maturity = new Date(15, August, 2046);
        Rate cpn = 0.025;

        Bond bnd = BuildBond(issue, maturity, cpn, vars.Context);

        var delivery = new Date(10, March, 2022);
        BondForward bndFwd = BuildBondForward(bnd, vars.Curve, delivery, PositionType.Long);

        Real bndFwdValue = bndFwd.Calculate(vars.Context, vars.BondEngine).ForwardValue!.Value;
        Real underlyingDirtyPrice = bnd.DirtyPrice(vars.BondEngine(bnd, vars.Context), vars.Context);

        Assert.False(
            Math.Abs(bndFwdValue - underlyingDirtyPrice) > tolerance,
            $"unable to match the dirty price: bond forward {bndFwdValue}, underlying bond {underlyingDirtyPrice}");
    }

    private static Bond BuildBond(Date issue, Date maturity, Rate cpn, EvaluationContext context)
    {
        var sch = new Schedule(
            issue,
            maturity,
            new Period(Frequency.Annual),
            new TARGET(),
            BusinessDayConvention.Following,
            BusinessDayConvention.Following,
            DateGenerationRule.Backward,
            false,
            context);

        return new FixedRateBond(2, 1.0e5, sch, [cpn], new ActualActual(ActualActualConvention.ISDA));
    }

    private static BondForward BuildBondForward(
        Bond underlying, YieldTermStructure handle, Date delivery, PositionType type)
    {
        Date valueDt = handle.ReferenceDate;
        return new BondForward(
            valueDt,
            delivery,
            type,
            0.0,
            2,
            new ActualActual(ActualActualConvention.ISDA),
            new TARGET(),
            BusinessDayConvention.Following,
            underlying,
            handle,
            handle);
    }

    private sealed class CommonVars
    {
        // setup
        public CommonVars()
        {
            Today = new Date(7, March, 2022);
            Context = new EvaluationContext { EvaluationDate = Today };

            Curve = new FlatForward(Today, 0.0004977, new Actual365Fixed());
            BondEngine = (bond, context) => DiscountingBondEngine.Calculate(bond, context, Curve);
        }

        // common data
        public Date Today { get; }

        public EvaluationContext Context { get; }

        public YieldTermStructure Curve { get; }

        // QuantLib's DiscountingBondEngine on the curve, which the bond holds.
        public Func<Bond, EvaluationContext, BondResults> BondEngine { get; }
    }
}
