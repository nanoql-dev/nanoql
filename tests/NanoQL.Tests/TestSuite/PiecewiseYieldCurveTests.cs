// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008, 2009, 2017 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/piecewiseyieldcurve.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Replaced: testObservability (Vectors/TermStructures/Yield/PiecewiseYieldCurveVectorTests.cs)

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class PiecewiseYieldCurveTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2), under which the cases that set
    // the evaluation date after building their helpers build them.
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, September, 2015) };

    private static readonly Datum[] DepositData =
    [
        new(1, TimeUnit.Weeks, 4.559),
        new(1, TimeUnit.Months, 4.581),
        new(2, TimeUnit.Months, 4.573),
        new(3, TimeUnit.Months, 4.557),
        new(6, TimeUnit.Months, 4.496),
        new(9, TimeUnit.Months, 4.490),
    ];

    private static readonly Datum[] FraData =
    [
        new(1, TimeUnit.Months, 4.581),
        new(2, TimeUnit.Months, 4.573),
        new(3, TimeUnit.Months, 4.557),
        new(6, TimeUnit.Months, 4.496),
        new(9, TimeUnit.Months, 4.490),
    ];

    private static readonly Datum[] ImmFutData =
    [
        new(1, TimeUnit.Months, 4.581),
        new(2, TimeUnit.Months, 4.573),
        new(3, TimeUnit.Months, 4.557),
    ];

    private static readonly Datum[] AsxFutData =
    [
        new(1, TimeUnit.Months, 4.581),
        new(2, TimeUnit.Months, 4.573),
        new(3, TimeUnit.Months, 4.557),
    ];

    private static readonly Datum[] SwapData =
    [
        new(1, TimeUnit.Years, 4.54),
        new(2, TimeUnit.Years, 4.63),
        new(3, TimeUnit.Years, 4.75),
        new(4, TimeUnit.Years, 4.86),
        new(5, TimeUnit.Years, 4.99),
        new(6, TimeUnit.Years, 5.11),
        new(7, TimeUnit.Years, 5.23),
        new(8, TimeUnit.Years, 5.33),
        new(9, TimeUnit.Years, 5.41),
        new(10, TimeUnit.Years, 5.47),
        new(12, TimeUnit.Years, 5.60),
        new(15, TimeUnit.Years, 5.75),
        new(20, TimeUnit.Years, 5.89),
        new(25, TimeUnit.Years, 5.95),
        new(30, TimeUnit.Years, 5.96),
    ];

    private static readonly BondDatum[] BondData =
    [
        new(6, TimeUnit.Months, 5, Frequency.Semiannual, 4.75, 101.320),
        new(1, TimeUnit.Years, 3, Frequency.Semiannual, 2.75, 100.590),
        new(2, TimeUnit.Years, 5, Frequency.Semiannual, 5.00, 105.650),
        new(5, TimeUnit.Years, 11, Frequency.Semiannual, 5.50, 113.610),
        new(10, TimeUnit.Years, 11, Frequency.Semiannual, 3.75, 104.070),
    ];

    private static readonly Datum[] BmaData =
    [
        new(1, TimeUnit.Years, 67.56),
        new(2, TimeUnit.Years, 68.00),
        new(3, TimeUnit.Years, 68.25),
        new(4, TimeUnit.Years, 68.50),
        new(5, TimeUnit.Years, 68.81),
        new(7, TimeUnit.Years, 69.50),
        new(10, TimeUnit.Years, 70.44),
        new(15, TimeUnit.Years, 71.69),
        new(20, TimeUnit.Years, 72.69),
        new(30, TimeUnit.Years, 73.81),
    ];

    [Fact]
    public void TestLogLinearDiscountConsistency()
    {
        // Testing consistency of piecewise-log-linear discount curve...
        var vars = new CommonVars();

        TestCurveConsistency<Discount, LogLinear, IterativeBootstrap>(vars);
        TestBMACurveConsistency<Discount, LogLinear, IterativeBootstrap>(vars);
    }

    [Fact]
    public void TestLinearDiscountConsistency()
    {
        // Testing consistency of piecewise-linear discount curve...
        var vars = new CommonVars();

        TestCurveConsistency<Discount, Linear, IterativeBootstrap>(vars);
        TestBMACurveConsistency<Discount, Linear, IterativeBootstrap>(vars);
    }

    [Fact]
    public void TestLinearZeroConsistency()
    {
        // Testing consistency of piecewise-linear zero-yield curve...
        var vars = new CommonVars();

        TestCurveConsistency<ZeroYield, Linear, IterativeBootstrap>(vars);
        TestBMACurveConsistency<ZeroYield, Linear, IterativeBootstrap>(vars);
    }

    [Fact]
    public void TestSplineZeroConsistency()
    {
        // Testing consistency of piecewise-cubic zero-yield curve...
        var vars = new CommonVars();

        TestCurveConsistency<ZeroYield, Cubic, IterativeBootstrap>(
            vars,
            new Cubic(
                CubicInterpolationDerivativeApprox.Spline,
                true,
                CubicInterpolationBoundaryCondition.SecondDerivative,
                0.0,
                CubicInterpolationBoundaryCondition.SecondDerivative,
                0.0));
        TestBMACurveConsistency<ZeroYield, Cubic, IterativeBootstrap>(
            vars,
            new Cubic(
                CubicInterpolationDerivativeApprox.Spline,
                true,
                CubicInterpolationBoundaryCondition.SecondDerivative,
                0.0,
                CubicInterpolationBoundaryCondition.SecondDerivative,
                0.0));
    }

    [Fact]
    public void TestLinearForwardConsistency()
    {
        // Testing consistency of piecewise-linear forward-rate curve...
        var vars = new CommonVars();

        TestCurveConsistency<ForwardRate, Linear, IterativeBootstrap>(vars);
        TestBMACurveConsistency<ForwardRate, Linear, IterativeBootstrap>(vars);
    }

    [Fact]
    public void TestFlatForwardConsistency()
    {
        // Testing consistency of piecewise-flat forward-rate curve...
        var vars = new CommonVars();

        TestCurveConsistency<ForwardRate, BackwardFlat, IterativeBootstrap>(vars);
        TestBMACurveConsistency<ForwardRate, BackwardFlat, IterativeBootstrap>(vars);
    }

    [Fact]
    public void TestConvexMonotoneForwardConsistency()
    {
        // Testing consistency of convex monotone forward-rate curve...
        var vars = new CommonVars();
        TestCurveConsistency<ForwardRate, ConvexMonotone, IterativeBootstrap>(vars);

        TestBMACurveConsistency<ForwardRate, ConvexMonotone, IterativeBootstrap>(vars);
    }

    [Fact]
    public void TestLocalBootstrapConsistency()
    {
        // Testing consistency of local-bootstrap algorithm...
        var vars = new CommonVars();
        TestCurveConsistency<ForwardRate, ConvexMonotone, LocalBootstrap>(vars, default(ConvexMonotone), 1.0e-6);
        TestBMACurveConsistency<ForwardRate, ConvexMonotone, LocalBootstrap>(vars, default(ConvexMonotone), 1.0e-7);
    }

    // Adapted: the curve's handle linked after the curve is built (line 806) becomes the index and the FRAs on the
    // curve; the FRAs' forward rates are read from their results.
    [Fact]
    public void TestParFraRegression()
    {
        // Testing regression for at-par FRA...

        var vars = new CommonVars(new Date(23, February, 2023));

        bool useIndexedFra = false;

        YieldTermStructure termStructure = new PiecewiseYieldCurve<ZeroYield, Linear, IterativeBootstrap>(
            vars.Settlement, vars.FraHelpers(useIndexedFra), new Actual360(), vars.Context);
        var euribor3m = new Euribor3M(termStructure);

        for (int i = 0; i < vars.Fras; i++)
        {
            Date start = vars.Calendar.Advance(
                vars.Settlement,
                FraData[i].N,
                FraData[i].Units,
                euribor3m.BusinessDayConvention,
                euribor3m.EndOfMonth);
            Assert.Equal(TimeUnit.Months, FraData[i].Units);

            Date end = vars.Calendar.Advance(
                vars.Settlement, 3 + FraData[i].N, TimeUnit.Months, euribor3m.BusinessDayConvention, euribor3m.EndOfMonth);
            var fra = new ForwardRateAgreement(
                euribor3m, start, end, PositionType.Long, FraData[i].Rate / 100, 100.0, termStructure);
            Rate expectedRate = FraData[i].Rate / 100;
            Rate estimatedRate = fra.Calculate(vars.Context).ForwardRate!.Rate;
            Real tolerance = 1.0e-6;
            Assert.True(
                Math.Abs(expectedRate - estimatedRate) <= tolerance,
                $"{NanoQL.Utilities.DataFormattersFunctions.Ordinal(i + 1)} FRA (at par) failure: estimated rate "
                + $"{estimatedRate}, expected rate {expectedRate}");
        }
    }

    // Adapted: today's fixing added after the curve was priced (line 936) becomes a second context holding it, the
    // curve bootstrapped and the swaps built under it; the observer checks (lines 932-934, 938-939) are dropped.
    [Fact]
    public void TestLiborFixing()
    {
        // Testing use of today's LIBOR fixings in swap curve...
        var vars = new CommonVars();

        var swapHelpers = new List<RateHelper>();
        var euribor6m = new Euribor6M();

        for (int i = 0; i < vars.Swaps; i++)
        {
            Rate r = vars.Rates[i + vars.Deposits];
            swapHelpers.Add(new SwapRateHelper(
                r,
                new Period(SwapData[i].N, SwapData[i].Units),
                vars.Calendar,
                vars.FixedLegFrequency,
                vars.FixedLegConvention,
                vars.FixedLegDayCounter,
                euribor6m,
                vars.Context));
        }

        CheckLiborFixing(vars, swapHelpers, vars.Context, "before LIBOR fixing");

        EvaluationContext fixed6m = vars.Context with
        {
            Fixings = vars.Context.Fixings.AddFixing(euribor6m, vars.Today, 0.0425),
        };

        CheckLiborFixing(vars, swapHelpers, fixed6m, "after LIBOR fixing");
    }

    // Adapted: the check that nodes() doesn't throw (line 1069) becomes a check that construction, which bootstraps,
    // doesn't throw; the fixture's futures helpers, which the case replaces (line 1059), are a list of its own.
    // Adapted: the fixture's evaluation date, calendar and settlement reset (lines 973-978) are the case's own, its
    // context on 4 October 2007; the curve's handle (lines 1005-1006) is the curve, the index built on it.
    [Fact]
    public void TestJpyLibor()
    {
        // Testing bootstrap over JPY LIBOR swaps...

        var vars = new CommonVars();

        var today = new Date(4, October, 2007);
        var context = new EvaluationContext { EvaluationDate = today };

        Calendar calendar = new Japan();
        Date settlement = calendar.Advance(today, vars.SettlementDays, TimeUnit.Days);

        // market elements
        var rates = new Rate[vars.Swaps];
        for (int i = 0; i < vars.Swaps; i++)
        {
            rates[i] = SwapData[i].Rate / 100;
        }

        // rate helpers
        var instruments = new List<RateHelper>();

        var index = new JPYLibor(new Period(6, TimeUnit.Months));
        for (int i = 0; i < vars.Swaps; i++)
        {
            instruments.Add(new SwapRateHelper(
                rates[i],
                new Period(SwapData[i].N, SwapData[i].Units),
                calendar,
                vars.FixedLegFrequency,
                vars.FixedLegConvention,
                vars.FixedLegDayCounter,
                index,
                context));
        }

        YieldTermStructure termStructure = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            settlement, instruments, new Actual360(), context);

        // check swaps
        var jpylibor6m = new JPYLibor(new Period(6, TimeUnit.Months), termStructure);
        for (int i = 0; i < vars.Swaps; i++)
        {
            var tenor = new Period(SwapData[i].N, SwapData[i].Units);

            VanillaSwap swap = new MakeVanillaSwap(tenor, jpylibor6m)
                .WithFixedRate(0.0)
                .WithEffectiveDate(settlement)
                .WithFixedLegDayCount(vars.FixedLegDayCounter)
                .WithFixedLegTenor(new Period(vars.FixedLegFrequency))
                .WithFixedLegConvention(vars.FixedLegConvention)
                .WithFixedLegTerminationDateConvention(vars.FixedLegConvention)
                .WithFixedLegCalendar(calendar)
                .WithFloatingLegCalendar(calendar)
                .ToVanillaSwap(context);

            Rate expectedRate = SwapData[i].Rate / 100, estimatedRate = FairRate(swap, context, termStructure);
            Spread error = Math.Abs(expectedRate - estimatedRate);
            Real tolerance = 1.0e-9;

            Assert.True(
                error <= tolerance,
                $"{SwapData[i].N} year(s) swap: estimated rate {estimatedRate}, expected rate {expectedRate}, error "
                + $"{error}, tolerance {tolerance}");
        }
    }

    [Fact]
    public void TestCA365Futures()
    {
        // Testing futures rate helpers with act/365 Canadian day counter...
        var vars = new CommonVars();

        var index = new IborIndex(
            "foo",
            new Period(3, TimeUnit.Months),
            2,
            null,
            new Canada(),
            BusinessDayConvention.ModifiedFollowing,
            true,
            new Actual365Fixed(Actual365FixedConvention.Canadian));

        var immFutHelpers = new List<RateHelper>();
        Date? immDate = null;
        for (int i = 0; i < vars.ImmFuts; i++)
        {
            Real r = vars.ImmFutPrices[i];
            immDate = IMM.NextDate(vars.Context, immDate, false);

            // if the fixing is before the evaluation date, we
            // just jump forward by one future maturity
            if (index.FixingDate(immDate.Value) < vars.Context.EvaluationDate)
            {
                immDate = IMM.NextDate(vars.Context, immDate, false);
            }

            immFutHelpers.Add(new FuturesRateHelper(r, immDate.Value, index, 0.0, FuturesType.IMM));
        }

        var termStructure = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            vars.Settlement, immFutHelpers, new Actual360(), vars.Context);

        _ = termStructure.Nodes();
    }

    // Adapted: QuantLib never bootstraps these curves (line 1087: the case tests that they compile); nanoql's
    // constructors bootstrap them. SplineLogCubic, a test-local subclass of LogCubic with a default constructor, is the
    // LogCubic it constructs, passed to the curve. The ForwardFlat curve (line 1093) is compiled and not invoked, as in
    // QuantLib: no iterative bootstrap can build it, since a forward-flat curve's discount at a node never reads that
    // node's forward.
    [Fact]
    public void TestDefaultInstantiation()
    {
        // Testing instantiation of curves without passing an interpolator...
        var vars = new CommonVars();

        var splineLogCubic = new LogCubic(
            CubicInterpolationDerivativeApprox.Spline,
            false,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0);

        _ = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            vars.Settlement, vars.Instruments, new Actual360(), vars.Context);
        _ = new PiecewiseYieldCurve<Discount, LogCubic, IterativeBootstrap>(
            vars.Settlement, vars.Instruments, new Actual360(), vars.Context, i: splineLogCubic);
        _ = new PiecewiseYieldCurve<Discount, MonotonicLogCubic, IterativeBootstrap>(
            vars.Settlement, vars.Instruments, new Actual360(), vars.Context);
        _ = new PiecewiseYieldCurve<Discount, KrugerLog, IterativeBootstrap>(
            vars.Settlement, vars.Instruments, new Actual360(), vars.Context);
        _ = new PiecewiseYieldCurve<ForwardRate, BackwardFlat, IterativeBootstrap>(
            vars.Settlement, vars.Instruments, new Actual360(), vars.Context);
        Func<PiecewiseYieldCurve<ForwardRate, ForwardFlat, IterativeBootstrap>> forwardFlat =
            () => new(vars.Settlement, vars.Instruments, new Actual360(), vars.Context);
        Assert.NotNull(forwardFlat);
        _ = new PiecewiseYieldCurve<ForwardRate, ConvexMonotone, IterativeBootstrap>(
            vars.Settlement, vars.Instruments, new Actual360(), vars.Context);
    }

    // Adapted: the helpers built under the test runner's evaluation date, the curve's first bootstrap under
    // 12 October 2017 (lines 1176-1180) is dropped: the curve is built under the second evaluation date (line 1183),
    // which re-dates its helpers, and has no previous curve to start from (a documented divergence). The handle linked
    // after the curve exists (lines 1185-1186) becomes the curve itself.
    // Adapted: the check that discount(1.0) doesn't throw (line 1115) is the curve's construction, which bootstraps,
    // and the discount.
    [Fact]
    public void TestSwapRateHelperLastRelevantDate()
    {
        // Testing SwapRateHelper last relevant date...

        var today = new Date(22, December, 2016);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure flat3m = new FlatForward(today, 0.02, new Actual365Fixed());
        IborIndex usdLibor3m = new USDLibor(new Period(3, TimeUnit.Months), flat3m);

        // note that the calendar should be US+UK here actually, but technically it should also work with
        // the US calendar only
        RateHelper helper = new SwapRateHelper(
            0.02,
            new Period(50, TimeUnit.Years),
            new UnitedStates(UnitedStatesMarket.GovernmentBond),
            Frequency.Semiannual,
            BusinessDayConvention.ModifiedFollowing,
            new Thirty360(Thirty360Convention.BondBasis),
            usdLibor3m,
            context);

        var curve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            today, [helper], new Actual365Fixed(), context);
        _ = curve.Discount(1.0);
    }

    // Adapted: the evaluation date set after the helper is built (line 1127), which re-dates it in QuantLib, is the
    // context the helper is built under.
    [Fact]
    public void TestSwapRateHelperSpotDate()
    {
        // Testing SwapRateHelper spot date...

        var context = new EvaluationContext { EvaluationDate = new Date(11, October, 2019) };

        IborIndex usdLibor3m = new USDLibor(new Period(3, TimeUnit.Months));

        var helper = new SwapRateHelper(
            0.02,
            new Period(5, TimeUnit.Years),
            new UnitedStates(UnitedStatesMarket.GovernmentBond),
            Frequency.Semiannual,
            BusinessDayConvention.ModifiedFollowing,
            new Thirty360(Thirty360Convention.BondBasis),
            usdLibor3m,
            context);

        // Advancing 2 days on the US calendar would yield October 16th (because October 14th
        // is Columbus day), but the LIBOR spot is calculated advancing on the UK calendar,
        // resulting in October 15th which is also a business day for the US calendar.
        var expected = new Date(15, October, 2019);
        Date calculated = helper.Swap.StartDate();
        Assert.True(calculated == expected, $"expected spot date: {expected}, calculated: {calculated}");
    }

    [Fact]
    public void TestBadPreviousCurve()
    {
        // Testing bootstrap starting from bad guess...
        Datum[] data =
        [
            new(1, TimeUnit.Weeks, -0.003488),
            new(2, TimeUnit.Weeks, -0.0033),
            new(6, TimeUnit.Months, -0.00339),
            new(2, TimeUnit.Years, -0.00336),
            new(8, TimeUnit.Years, 0.00302),
            new(50, TimeUnit.Years, 0.01185),
        ];

        var helpers = new List<RateHelper>();
        var euribor1m = new Euribor1M();
        foreach (Datum i in data)
        {
            helpers.Add(new SwapRateHelper(
                i.Rate,
                new Period(i.N, i.Units),
                new TARGET(),
                Frequency.Monthly,
                BusinessDayConvention.Unadjusted,
                new Thirty360(Thirty360Convention.BondBasis),
                euribor1m,
                RunnerContext));
        }

        var testDate = new Date(16, December, 2016);
        var context = new EvaluationContext { EvaluationDate = testDate };

        YieldTermStructure curve = new PiecewiseYieldCurve<ForwardRate, BackwardFlat, IterativeBootstrap>(
            testDate, helpers, new Actual360(), context);

        var index = new Euribor1M(curve);
        foreach (Datum i in data)
        {
            var tenor = new Period(i.N, i.Units);

            VanillaSwap swap = new MakeVanillaSwap(tenor, index)
                .WithFixedRate(0.0)
                .WithFixedLegDayCount(new Thirty360(Thirty360Convention.BondBasis))
                .WithFixedLegTenor(new Period(1, TimeUnit.Months))
                .WithFixedLegConvention(BusinessDayConvention.Unadjusted)
                .ToVanillaSwap(context);

            Rate expectedRate = i.Rate, estimatedRate = DiscountingSwapEngine.Calculate(swap, context, curve).FairRate!.Value;
            Spread error = Math.Abs(expectedRate - estimatedRate);
            Real tolerance = 1.0e-9;
            Assert.True(
                error <= tolerance,
                $"{tenor} swap:\n estimated rate: {estimatedRate}\n expected rate:  {expectedRate}\n"
                + $" error:          {error}\n tolerance:      {tolerance}");
        }
    }

    // Adapted: the checks that discounting doesn't throw (lines 1227, 1235) become checks that construction, which
    // bootstraps, doesn't throw.
    [Fact]
    public void TestConstructionWithExplicitBootstrap()
    {
        // Testing that construction with an explicit bootstrap succeeds...
        var vars = new CommonVars();

        // With an explicit IterativeBootstrap object
        YieldTermStructure yts = new PiecewiseYieldCurve<ForwardRate, Linear, IterativeBootstrap>(
            vars.Settlement, vars.Instruments, new Actual360(), vars.Context, i: default(Linear), bootstrap: new IterativeBootstrap());

        // Check anything to show that the construction succeeded
        _ = yts.Discount(1.0, true);

        // With an explicit LocalBootstrap object
        yts = new PiecewiseYieldCurve<ForwardRate, ConvexMonotone, LocalBootstrap>(
            vars.Settlement, vars.Instruments, new Actual360(), vars.Context, i: default(ConvexMonotone), bootstrap: new LocalBootstrap());

        _ = yts.Discount(1.0, true);
    }

    // Adapted: the helpers are built under the test runner's evaluation date and re-dated by the curve, built under
    // the case's (line 1255); the bootstrap that discount() forces (line 1269) happens at construction.
    [Fact]
    public void TestLargeRates()
    {
        // Testing bootstrap with large input rates...
        Datum[] data =
        [
            new(1, TimeUnit.Weeks, 2.418633),
            new(2, TimeUnit.Weeks, 1.361540),
            new(3, TimeUnit.Weeks, 1.195362),
            new(1, TimeUnit.Months, 0.829009),
        ];

        var helpers = new List<RateHelper>();
        foreach (Datum i in data)
        {
            helpers.Add(new DepositRateHelper(
                i.Rate,
                new Period(i.N, i.Units),
                0,
                new WeekendsOnly(),
                BusinessDayConvention.Following,
                false,
                new Actual360(),
                RunnerContext));
        }

        var today = new Date(12, October, 2017);
        var context = new EvaluationContext { EvaluationDate = today };

        double? accuracy = null; // use the default
        double? minValue = null; // use the default
        Real maxValue = 3.0; // override

        YieldTermStructure curve = new PiecewiseYieldCurve<ForwardRate, BackwardFlat, IterativeBootstrap>(
            today,
            helpers,
            new Actual360(),
            context,
            i: default(BackwardFlat),
            bootstrap: new IterativeBootstrap(accuracy, minValue, maxValue));

        // force bootstrap and check it worked
        _ = curve.Discount(0.01);
    }

    // Adapted: the curve is moving (line 1373), built by Create in the case's context, and enableExtrapolation
    // (line 1378) returns a copy; the additional dates' functor, which reads the global evaluation date inside the
    // bootstrap (line 1295), becomes the dates computed from the case's context; the additional errors' functor
    // (lines 1279-1291) prices the additional helpers the bootstrap has bound to the curve, which the state holds.
    [Fact]
    public void TestGlobalBootstrap()
    {
        // Testing global bootstrap...

        // the precondition usingAtParCoupons() (line 1311) is the context's default
        var today = new Date(26, September, 2019);
        var context = new EvaluationContext { EvaluationDate = today };

        // market rates
        Real[] refMktRate =
        [
            -0.373, -0.388, -0.402, -0.418, -0.431, -0.441, -0.45, -0.457, -0.463, -0.469, -0.461, -0.463, -0.479,
            -0.4511, -0.45418, -0.439, -0.4124, -0.37703, -0.3335, -0.28168, -0.22725, -0.1745, -0.12425, -0.07746,
            0.0385, 0.1435, 0.17525, 0.17275, 0.1515, 0.1225, 0.095, 0.0644,
        ];

        // expected outputs
        Date[] refDate = GlobalBootstrapRefDates;

        Real[] refZeroRate =
        [
            -0.00373354, -0.00381005, -0.00387689, -0.00394124, -0.00407706, -0.00413633, -0.00411935, -0.00416370,
            -0.00420557, -0.00424431, -0.00427824, -0.00430977, -0.00434401, -0.00445243, -0.00448506, -0.00433690,
            -0.00407401, -0.00372752, -0.00330050, -0.00279139, -0.00225477, -0.00173422, -0.00123688, -0.00077237,
            0.00038554, 0.00144248, 0.00175995, 0.00172873, 0.00150782, 0.00121145, 0.000933912, 0.000628946,
        ];

        // build ql helpers
        List<RateHelper> helpers = GlobalBootstrapHelpers(refMktRate, context, out IborIndex index);

        // global bootstrap constraints
        var additionalHelpers = new List<BootstrapHelper<YieldTermStructure>>();

        // set up the additional rate helpers we need in the cost function
        for (int i = 0; i < 7; ++i)
        {
            additionalHelpers.Add(new FraRateHelper(-0.004, new Period(12 + i, TimeUnit.Months), index, context));
        }

        // build curve with additional dates and constraints using a global bootstrapper
        var curve = PiecewiseYieldCurve<SimpleZeroYield, Linear, GlobalBootstrap>.Create(
            2,
            new TARGET(),
            helpers,
            new Actual365Fixed(),
            context,
            i: default(Linear),
            bootstrap: new GlobalBootstrap(
                accuracy: 1.0e-12,
                additionalHelpers: additionalHelpers,
                additionalDates: AdditionalDates(context),
                additionalPenalties: AdditionalErrors));
        YieldTermStructure extrapolating = curve.EnableExtrapolation();

        // check expected pillar dates
        for (int i = 0; i < refDate.Length; ++i)
        {
            Assert.Equal(refDate[i], helpers[i].PillarDate);
        }

        // check expected zero rates
        for (int i = 0; i < refZeroRate.Length; ++i)
        {
            // 0.01 basis points tolerance
            Real error = Math.Abs(
                refZeroRate[i]
                - extrapolating.ZeroRate(refDate[i], new Actual360(), Compounding.Continuous).Rate);
            Assert.True(error <= 1E-6, $"zero rate at {refDate[i]}: error {error:R}");
        }
    }

    // Adapted: the two curves are moving (lines 1450, 1477), built by Create in the case's context; the gradient
    // penalty of the times and data (lines 1469-1475) reads them from the bootstrap's state.
    [Fact]
    public void TestGlobalBootstrapPenalty()
    {
        // the precondition usingAtParCoupons() (line 1393) is the context's default
        var context = new EvaluationContext { EvaluationDate = new Date(26, September, 2019) };

        // market rates
        Real[] refMktRate =
        [
            -0.373, -0.388, -0.402, -0.418, -0.431, -0.441, -0.45, -0.457, -0.463, -0.469, -0.461, -0.463, -0.479,
            -0.4511, -0.45418, -0.439, -0.4124, -0.37703, -0.3335, -0.28168, -0.22725, -0.1745, -0.12425, -0.07746,
            0.0385, 0.1435, 0.17525, 0.17275, 0.1515, 0.1225, 0.095, 0.0644,
        ];

        // expected outputs
        Date[] refDate = GlobalBootstrapRefDates;

        Real[] refZeroRateNP =
        [
            -0.00373354, -0.00386194, -0.00395205, -0.00403303, -0.00408033, -0.00410875, -0.00411935, -0.00419161,
            -0.00424817, -0.00429923, -0.00428029, -0.00429178, -0.00434401, -0.00445243, -0.00448506, -0.0043369,
            -0.00407401, -0.00372752, -0.0033005, -0.00279139, -0.00225477, -0.00173422, -0.00123688, -0.00077236,
            0.00038550, 0.00144208, 0.00175947, 0.00172834, 0.00150757, 0.00121131, 0.00093384, 0.00062891,
        ];

        Real[] refZeroRateGP =
        [
            -0.00377892, -0.00386127, -0.00394737, -0.00402914, -0.00409541, -0.00413252, -0.00415463, -0.00419484,
            -0.00424238, -0.00427875, -0.00429712, -0.00431898, -0.00436027, -0.00445297, -0.00448502, -0.00433694,
            -0.00407406, -0.00372755, -0.00330018, -0.00279133, -0.00225491, -0.00173429, -0.00123643, -0.00077298,
            0.00038547, 0.00144206, 0.00175948, 0.00172834, 0.00150756, 0.00121135, 0.00093379, 0.00062895,
        ];

        // build ql helpers
        List<RateHelper> helpers = GlobalBootstrapHelpers(refMktRate, context, out _);

        // build the curve without penalties first
        YieldTermStructure curve = PiecewiseYieldCurve<ForwardRate, BackwardFlat, GlobalBootstrap>.Create(
            2,
            new TARGET(),
            helpers,
            new Actual365Fixed(),
            context,
            i: default(BackwardFlat),
            bootstrap: new GlobalBootstrap(accuracy: 1.0e-12));

        // check expected pillar dates
        for (int i = 0; i < refDate.Length; ++i)
        {
            Assert.Equal(refDate[i], helpers[i].PillarDate);
        }

        // check expected zero rates
        for (int i = 0; i < refZeroRateNP.Length; ++i)
        {
            // 0.01 basis points tolerance
            Real error = refZeroRateNP[i] - curve.ZeroRate(refDate[i], new Actual360(), Compounding.Continuous).Rate;
            Assert.True(Math.Abs(error) <= 1E-6, $"zero rate at {refDate[i]}, no penalty: error {error:R}");
        }

        // build the curve with gradient penalties
        static Vector GradientPenalty(IBootstrapState state, EvaluationContext context)
        {
            ReadOnlySpan<double> times = state.Times, data = state.Data;
            var errors = new Vector(times.Length - 1);
            for (int i = 0; i < times.Length - 1; ++i)
            {
                errors[i] = 0.01 * (data[i + 1] - data[i]) / (times[i + 1] - times[i]);
            }

            return errors;
        }

        curve = PiecewiseYieldCurve<ForwardRate, BackwardFlat, GlobalBootstrap>.Create(
            2,
            new TARGET(),
            helpers,
            new Actual365Fixed(),
            context,
            i: default(BackwardFlat),
            bootstrap: new GlobalBootstrap(accuracy: 1.0e-12, additionalPenalties: GradientPenalty));

        // check expected zero rates
        for (int i = 0; i < refZeroRateGP.Length; ++i)
        {
            // 0.01 basis points tolerance
            Real error = refZeroRateGP[i] - curve.ZeroRate(refDate[i], new Actual360(), Compounding.Continuous).Rate;
            Assert.True(Math.Abs(error) <= 1E-6, $"zero rate at {refDate[i]}, gradient penalty: error {error:R}");
        }
    }

    // ---- the private helpers, before TestCurveConsistency ----

    // Adapted: QuantLib's volatility quote, which its bootstrap optimizes and its futures helpers read through their
    // convexity-adjustment quotes (lines 1503-1528), is the additional variable's value, from which the variable
    // helpers build the futures at each evaluation; they follow the curve's helpers, as the futures follow the deposits
    // and swaps in QuantLib's helper vector. The penalty reads the first swap from the working state's additional
    // helpers, the copy the bootstrap has bound to the curve (line 1530 reads the fixture's own, which QuantLib's
    // bootstrap binds).
    [Fact]
    public void TestGlobalBootstrapVariables()
    {
        // Use fixed evaluationDate to make the test stable. It works for any date,
        // but the tolerance varies and also depends on usingAtParCoupons().
        var vars = new CommonVars(new Date(25, September, 2019));

        // First, build the curve without futures.
        var curve = new PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>(
            vars.Settlement, vars.Instruments, new Actual365Fixed(), vars.Context, i: default(LogLinear));

        // Now build the curve with futures adjusted to the same level as swaps.
        // Remove the first swap from helpers so it does not create a pillar.
        var helpers = new List<RateHelper>(vars.Instruments);
        RateHelper firstSwap = helpers[vars.Deposits];
        helpers.RemoveAt(vars.Deposits);

        var euribor3m = new Euribor3M();
        Date immDate = vars.Today;
        var immDates = new Date[vars.ImmFuts];
        for (int i = 0; i < vars.ImmFuts; i++)
        {
            immDate = IMM.NextDate(vars.Context, immDate);

            // if the fixing is before today, jump forward by one future maturity
            if (euribor3m.FixingDate(immDate) < vars.Today)
            {
                immDate = IMM.NextDate(vars.Context, immDate);
            }

            immDates[i] = immDate;
        }

        // We will optimize vol as an additional variable during bootstrapping.
        double mr = 0.03;
        ImmutableArray<IBootstrapHelper> Futures(ImmutableArray<double> vol, EvaluationContext context)
        {
            var futures = ImmutableArray.CreateBuilder<IBootstrapHelper>(vars.ImmFuts);
            for (int i = 0; i < vars.ImmFuts; i++)
            {
                double r = vars.ImmFutPrices[i];
                var convAdj = new FuturesConvAdjustmentQuote(euribor3m, immDates[i], r, vol[0], mr, context);
                futures.Add(new FuturesRateHelper(r, immDates[i], euribor3m, convAdj.Value, FuturesType.IMM));
            }

            return futures.MoveToImmutable();
        }

        static Vector Penalties(IBootstrapState state, EvaluationContext context) =>
            new(1, 1e4 * state.AdditionalHelpers[0].QuoteError(context));

        var curveFutures = new PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>(
            vars.Settlement,
            helpers,
            new Actual365Fixed(),
            vars.Context,
            i: default(LogLinear),
            bootstrap: new GlobalBootstrap(
                accuracy: 1e-12,
                additionalHelpers: [firstSwap],
                additionalPenalties: Penalties,
                additionalVariables: new SimpleQuoteVariables(1, [1.0], [0.0]),
                variableHelpers: Futures));

        // Check that the pillars are not the same.
        Assert.False(curve.Dates().SequenceEqual(curveFutures.Dates()));

        // Check that all deposit and swap rates are the same in both curves.
        foreach (RateHelper helper in vars.Instruments)
        {
            CheckClose(curve.Discount(helper.PillarDate!.Value), curveFutures.Discount(helper.PillarDate!.Value), 1e-6);
        }
    }

    // Adapted: the penalties read the fixture's instruments from the working state, whose additional helpers are the
    // copies the bootstrap has bound to the curve (lines 1571-1573 read the fixture's own, which QuantLib's bootstrap
    // binds); the dates functor (line 1594) becomes the dates it returns.
    [Fact]
    public void TestGlobalBootstrapInitialGuessFn()
    {
        var vars = new CommonVars(new Date(25, September, 2019));

        // Create a setup that is sensitive to the initial guess.
        Calendar calendar = new Euribor6M().FixingCalendar;
        var pillarDates = new List<Date> { new(1, October, 2019) };
        for (int i = 0; i < 8; ++i)
        {
            pillarDates.Add(calendar.Adjust(pillarDates[^1] + 45));
        }

        foreach (RateHelper helper in vars.Instruments)
        {
            if (helper.PillarDate > pillarDates[^1])
            {
                pillarDates.Add(helper.PillarDate!.Value);
            }
        }

        Func<IBootstrapState, EvaluationContext, Vector> penalties = (state, context) =>
        {
            IReadOnlyList<IBootstrapHelper> instruments = state.AdditionalHelpers;
            ReadOnlySpan<double> times = state.Times;
            ReadOnlySpan<double> data = state.Data;
            int nInst = instruments.Count;
            var errors = new Vector(nInst + times.Length - 2);

            // instruments errors
            for (int i = 0; i < nInst; ++i)
            {
                errors[i] = instruments[i].QuoteError(context);
            }

            // gradient penalties
            var rates = new Vector(data.Length - 1);
            for (int i = 0; i < times.Length - 1; ++i)
            {
                rates[i] = ((data[i] / data[i + 1]) - 1.0) / (times[i + 1] - times[i]);
            }

            for (int i = 0; i < times.Length - 2; ++i)
            {
                errors[nInst + i] = 1e-2 * (rates[i + 1] - rates[i]) / (times[i + 1] - times[i]);
            }

            return errors;
        };

        int initialGuessCalls = 0;
        Func<IBootstrapState, EvaluationContext, Vector> initialGuessFn = (state, _) =>
        {
            initialGuessCalls++;
            return new Vector(state.Times.Length - 1, 1.0);
        };

        var curve = new PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>(
            vars.Settlement,
            [],
            new Actual365Fixed(),
            vars.Context,
            bootstrap: new GlobalBootstrap(
                1e-12, null, null, null, vars.Instruments, pillarDates, penalties, initialGuessFn));

        Date[] expectedDates =
        [
            new(27, September, 2019), new(1, October, 2019), new(15, November, 2019), new(30, December, 2019),
            new(13, February, 2020), new(30, March, 2020), new(14, May, 2020), new(29, June, 2020),
            new(13, August, 2020), new(28, September, 2020), new(27, September, 2021), new(27, September, 2022),
            new(27, September, 2023), new(27, September, 2024), new(29, September, 2025), new(28, September, 2026),
            new(27, September, 2027), new(27, September, 2028), new(27, September, 2029), new(29, September, 2031),
            new(27, September, 2034), new(27, September, 2039), new(27, September, 2044), new(27, September, 2049),
        ];
        DiscountFactor[] expectedDFs =
        [
            1.0, 0.9994923431162580, 0.9938069118457880, 0.9882401711181712,
            0.9828381250775845, 0.9774079566030049, 0.9721182498392723, 0.9667951580943512,
            0.9617336341824015, 0.9564548068987965, 0.9135007024272714, 0.8698607442445871,
            0.8266650007699866, 0.7829682196126155, 0.7399209887960153, 0.6973589159124343,
            0.6565792747227167, 0.6180250998622848, 0.5818438569698231, 0.5127051907762759,
            0.4218543471337612, 0.3050840671400172, 0.2225943633033588, 0.165544906093695,
        ];

        var nodes = curve.Nodes();
        Assert.Equal(expectedDates.Length, nodes.Length);
        for (int i = 0; i < nodes.Length; ++i)
        {
            Assert.Equal(expectedDates[i], nodes[i].Key);
            Assert.True(
                Math.Abs(nodes[i].Value - expectedDFs[i]) <= 1e-10,
                $"node {i}: {nodes[i].Value:R} differs from {expectedDFs[i]:R}");
        }

        Assert.Equal(1, initialGuessCalls);
    }

    // Adapted: the empty internal handles (line 1634) are the cycle's members, each created with the reference date
    // and day counter of the curve it is linked to; the bootstrapped curves, which QuantLib builds and then adds (lines
    // 1673-1681), are added with their arguments, and Build builds them, links both members and returns the curves
    // QuantLib's adds return; the FRAs' forward rates are read from their results, and the swaps' engine (lines 1718,
    // 1745, 1757) is a static call.
    [Fact]
    public void TestMultiCurveTwoPiecewiseYieldCurves()
    {
        // Testing multicurve bootstrap with two piecewise yield curves...

        var vars = new CommonVars(new Date(23, October, 2025));

        const double accuracy = 1E-10;

        YieldTermStructure discountCurve = new FlatForward(vars.Settlement, 0.02, new Actual360());

        var intcurve3m = new MultiCurveMember(vars.Today, new Actual360());
        var intcurve6m = new MultiCurveMember(vars.Today, new Actual360());

        var euribor3m = new Euribor3M(intcurve3m);
        var euribor6m = new Euribor6M(intcurve6m);

        var helpers3m = new List<RateHelper>();
        var helpers6m = new List<RateHelper>();

        Real q = 0.03;
        Real b = 0.0020;

        for (int i = 1; i <= 9; ++i)
        {
            helpers3m.Add(new FraRateHelper(
                q,
                i,
                i + 3,
                euribor3m.FixingDays,
                euribor3m.FixingCalendar,
                euribor3m.BusinessDayConvention,
                euribor3m.EndOfMonth,
                euribor3m.DayCounter,
                vars.Context,
                PillarChoice.LastRelevantDate));
        }

        for (int i = 2; i <= 10; ++i)
        {
            helpers3m.Add(new IborIborBasisSwapRateHelper(
                b,
                new Period(i, TimeUnit.Years),
                euribor3m.FixingDays,
                euribor3m.FixingCalendar,
                euribor3m.BusinessDayConvention,
                euribor3m.EndOfMonth,
                euribor3m,
                euribor6m,
                discountCurve,
                true,
                vars.Context));
        }

        for (int i = 1; i <= 3; ++i)
        {
            helpers6m.Add(new IborIborBasisSwapRateHelper(
                b,
                new Period(i * 6, TimeUnit.Months),
                euribor3m.FixingDays,
                euribor3m.FixingCalendar,
                euribor3m.BusinessDayConvention,
                euribor3m.EndOfMonth,
                euribor3m,
                euribor6m,
                discountCurve,
                false,
                vars.Context));
        }

        for (int i = 2; i <= 10; ++i)
        {
            helpers6m.Add(new SwapRateHelper(
                q,
                new Period(i, TimeUnit.Years),
                euribor6m.FixingCalendar,
                Frequency.Annual,
                BusinessDayConvention.Following,
                new Thirty360(Thirty360Convention.BondBasis),
                euribor6m,
                vars.Context,
                fwdStart: new Period(0, TimeUnit.Days),
                discountingCurve: discountCurve));
        }

        var curves = new MultiCurve(accuracy)
            .AddBootstrappedCurve<Discount, LogLinear>(
                intcurve3m,
                vars.Today,
                helpers3m,
                new Actual360(),
                i: default(LogLinear),
                bootstrap: new GlobalBootstrap(accuracy))
            .AddBootstrappedCurve<Discount, LogLinear>(
                intcurve6m,
                vars.Today,
                helpers6m,
                new Actual360(),
                i: default(LogLinear),
                bootstrap: new GlobalBootstrap(accuracy))
            .Build(vars.Context);
        YieldTermStructure curve3m = curves[0];

        // check instrument npvs

        const double tolerance = 1E-10;

        for (int i = 1; i <= 9; ++i)
        {
            Date start = euribor3m.FixingCalendar.Advance(
                euribor3m.FixingCalendar.Advance(vars.Today, euribor3m.FixingDays, TimeUnit.Days),
                i,
                TimeUnit.Months,
                euribor3m.BusinessDayConvention,
                euribor3m.EndOfMonth);
            var fra = new ForwardRateAgreement(euribor3m, start, PositionType.Long, q, 1.0, curve3m);
            CheckClose(fra.Calculate(vars.Context).ForwardRate!.Rate, q, tolerance);
        }

        for (int i = 2; i <= 10; ++i)
        {
            Date start = euribor3m.FixingCalendar.Advance(vars.Today, euribor3m.FixingDays, TimeUnit.Days);
            Date maturity = euribor3m.FixingCalendar.Advance(
                start, new Period(i, TimeUnit.Years), euribor3m.BusinessDayConvention);
            CheckBasisSwapIsFair(vars, euribor3m, euribor6m, start, maturity, b, discountCurve, tolerance);
        }

        for (int i = 1; i <= 3; ++i)
        {
            Date start = euribor3m.FixingCalendar.Advance(vars.Today, euribor3m.FixingDays, TimeUnit.Days);
            Date maturity = euribor3m.FixingCalendar.Advance(
                start, new Period(i * 6, TimeUnit.Months), euribor3m.BusinessDayConvention);
            CheckBasisSwapIsFair(vars, euribor3m, euribor6m, start, maturity, b, discountCurve, tolerance);
        }

        for (int i = 2; i <= 10; ++i)
        {
            VanillaSwap swap = new MakeVanillaSwap(new Period(i, TimeUnit.Years), euribor6m)
                .WithFixedRate(q)
                .WithSettlementDays(euribor6m.FixingDays)
                .WithFixedLegDayCount(new Thirty360(Thirty360Convention.BondBasis))
                .WithFixedLegTenor(new Period(1, TimeUnit.Years))
                .WithFixedLegConvention(BusinessDayConvention.Following)
                .WithFixedLegTerminationDateConvention(BusinessDayConvention.Following)
                .ToVanillaSwap(vars.Context);
            Real npv = DiscountingSwapEngine.Calculate(swap, vars.Context, discountCurve).NPV!.Value;
            Assert.True(Math.Abs(npv) <= tolerance, $"swap {i}Y: NPV {npv:R} isn't within {tolerance:R} of 0");
        }

        // testMultiCurveTwoPiecewiseYieldCurves' basis swaps (lines 1699-1719, 1726-1746): the 3M leg plus the basis
        // against the 6M leg, on schedules generated forwards, priced at zero.
        static void CheckBasisSwapIsFair(
            CommonVars vars,
            IborIndex euribor3m,
            IborIndex euribor6m,
            Date start,
            Date maturity,
            Real b,
            YieldTermStructure discountCurve,
            double tolerance)
        {
            Schedule baseSchedule = new MakeSchedule()
                .From(start)
                .To(maturity)
                .WithTenor(new Period(3, TimeUnit.Months))
                .WithCalendar(euribor3m.FixingCalendar)
                .WithConvention(euribor3m.BusinessDayConvention)
                .EndOfMonth(euribor3m.EndOfMonth)
                .Forwards()
                .ToSchedule();
            Schedule otherSchedule = new MakeSchedule()
                .From(start)
                .To(maturity)
                .WithTenor(new Period(6, TimeUnit.Months))
                .WithCalendar(euribor6m.FixingCalendar)
                .WithConvention(euribor6m.BusinessDayConvention)
                .EndOfMonth(euribor6m.EndOfMonth)
                .Forwards()
                .ToSchedule();
            Leg baseLeg = new IborLeg(baseSchedule, euribor3m).WithSpreads(b).WithNotionals(1.0).ToLeg(vars.Context);
            Leg otherLeg = new IborLeg(otherSchedule, euribor6m).WithNotionals(1.0).ToLeg(vars.Context);
            var swap = new Swap(baseLeg, otherLeg);
            Real npv = DiscountingSwapEngine.Calculate(swap, vars.Context, discountCurve).NPV!.Value;
            Assert.True(Math.Abs(npv) <= tolerance, $"basis swap to {maturity}: NPV {npv:R} isn't within {tolerance:R} of 0");
        }
    }

    // Adapted: the empty internal handles (lines 1771-1772) are the cycle's members, each created with the reference
    // date and day counter of the curve it is linked to; the bootstrapped curve, which QuantLib builds and then adds
    // (lines 1791-1793), is added with its arguments, and Build builds it, links both members and returns the two
    // curves QuantLib's adds return (lines 1793, 1797); the swaps' engine (line 1816) is a static call.
    [Fact]
    public void TestMultiCurvePiecewiseYieldCurveAndSpreadedCurve()
    {
        // Testing multicurve bootstrap with piecewise yield curve and spreaded curve...

        var vars = new CommonVars(new Date(23, October, 2025));

        const double accuracy = 1E-10;

        var intcurveois = new MultiCurveMember(vars.Today, new Actual360());
        var intcurve3m = new MultiCurveMember(vars.Today, new Actual360());

        var euribor3m = new Euribor3M(intcurve3m);

        var helpers3m = new List<RateHelper>();

        Real q = 0.03;
        Real b = -0.01;

        for (int i = 1; i <= 10; ++i)
        {
            helpers3m.Add(new SwapRateHelper(
                q,
                new Period(i, TimeUnit.Years),
                euribor3m.FixingCalendar,
                Frequency.Annual,
                BusinessDayConvention.Following,
                new Thirty360(Thirty360Convention.BondBasis),
                euribor3m,
                vars.Context,
                fwdStart: new Period(0, TimeUnit.Days),
                discountingCurve: intcurveois));
        }

        // the spreaded curve reads its member's reference date and day counter now, before the cycle is built
        YieldTermStructure ptrois = new ZeroSpreadedTermStructure(intcurve3m, b);

        var curves = new MultiCurve(accuracy)
            .AddBootstrappedCurve<Discount, LogLinear>(
                intcurve3m,
                vars.Today,
                helpers3m,
                new Actual360(),
                i: default(LogLinear),
                bootstrap: new GlobalBootstrap(accuracy))
            .AddNonBootstrappedCurve(intcurveois, ptrois)
            .Build(vars.Context);
        YieldTermStructure curve3m = curves[0], curveois = curves[1];

        // check spread ois 3m

        const double tolerance = 1E-10;

        CheckClose(
            curveois.ZeroRate(1.0, Compounding.Continuous).Rate - curve3m.ZeroRate(1.0, Compounding.Continuous).Rate,
            b,
            tolerance);

        // check instrument npvs

        for (int i = 1; i <= 10; ++i)
        {
            VanillaSwap swap = new MakeVanillaSwap(new Period(i, TimeUnit.Years), euribor3m)
                .WithFixedRate(q)
                .WithSettlementDays(euribor3m.FixingDays)
                .WithFixedLegDayCount(new Thirty360(Thirty360Convention.BondBasis))
                .WithFixedLegTenor(new Period(1, TimeUnit.Years))
                .WithFixedLegConvention(BusinessDayConvention.Following)
                .WithFixedLegTerminationDateConvention(BusinessDayConvention.Following)
                .ToVanillaSwap(vars.Context);
            Real npv = DiscountingSwapEngine.Calculate(swap, vars.Context, curveois).NPV!.Value;
            Assert.True(Math.Abs(npv) <= tolerance, $"swap {i}Y: NPV {npv:R} isn't within {tolerance:R} of 0");
        }
    }

    // Adapted: the additional penalties read the helpers from the working state, whose additional helpers are the
    // copies the bootstrap has bound to the curve (line 1852 reads the case's own, which QuantLib's bootstrap binds);
    // the dates functor (lines 1848-1850) becomes the dates it returns; the unused Euribor 6M index (line 1827) is
    // dropped.
    [Fact]
    public void TestGlobalBootstrapInstrumentWeights()
    {
        var vars = new CommonVars(new Date(23, October, 2025));

        var helpers = new List<RateHelper>();

        // build a curve with overdetermined helper set
        helpers.Add(new DepositRateHelper(
            0.01, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, true,
            new Actual360(), vars.Context));
        helpers.Add(new DepositRateHelper(
            0.02, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, true,
            new Actual360(), vars.Context));

        // curve1 uses traditional helpers with weights w1 and w2
        Real w1 = 0.1, w2 = 0.9;

        var curve1 = new PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>(
            vars.Today,
            helpers,
            new Actual360(),
            vars.Context,
            i: default(LogLinear),
            bootstrap: new GlobalBootstrap(1E-10, null, null, [w1, w2]));

        // curve2 uses custom dates and penalties using the same weights
        Date[] addDates = [helpers[0].PillarDate!.Value, helpers[1].PillarDate!.Value];
        Func<IBootstrapState, EvaluationContext, Vector> addPenalties = (state, context) =>
            new Vector([
                w1 * state.AdditionalHelpers[0].QuoteError(context), w2 * state.AdditionalHelpers[1].QuoteError(context)
            ]);

        var curve2 = new PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>(
            vars.Today,
            [],
            new Actual360(),
            vars.Context,
            i: default(LogLinear),
            bootstrap: new GlobalBootstrap(
                1E-10, additionalHelpers: helpers, additionalDates: addDates, additionalPenalties: addPenalties));

        // check that both approaches result in the same curve
        CheckClose(curve1.Discount(0.3), curve2.Discount(0.3), 1E-13);
    }

    [Fact]
    public void TestPiecewiseSpreadYieldCurve()
    {
        // Testing PiecewiseSpreadYieldCurve...

        TestPiecewiseSpreadYieldCurveImpl<IterativeBootstrap>();
        TestPiecewiseSpreadYieldCurveImpl<GlobalBootstrap>();
    }

    // Adapted: the expected "1st iteration: failed at 1st alive instrument" from discount() (lines 2071-2072) comes
    // from the curve's construction, which bootstraps.
    [Fact]
    public void TestIterativeBootstrapRetries()
    {
        // Testing iterative bootstrap with retries...

        var asof = new Date(25, September, 2019);
        var context = new EvaluationContext { EvaluationDate = asof };
        var tsDayCounter = new Actual365Fixed();

        // USD discount curve built out of FedFunds OIS swaps.
        Date[] usdCurveDates =
        [
            new(25, September, 2019),
            new(26, September, 2019),
            new(8, October, 2019),
            new(16, October, 2019),
            new(22, October, 2019),
            new(30, October, 2019),
            new(2, December, 2019),
            new(31, December, 2019),
            new(29, January, 2020),
            new(2, March, 2020),
            new(31, March, 2020),
            new(29, April, 2020),
            new(29, May, 2020),
            new(1, July, 2020),
            new(29, July, 2020),
            new(31, August, 2020),
            new(30, September, 2020),
        ];

        DiscountFactor[] usdCurveDfs =
        [
            1.000000000,
            0.999940837,
            0.999309357,
            0.998894646,
            0.998574816,
            0.998162528,
            0.996552511,
            0.995197584,
            0.993915264,
            0.992530008,
            0.991329696,
            0.990179606,
            0.989005698,
            0.987751691,
            0.986703371,
            0.985495036,
            0.984413446,
        ];

        YieldTermStructure usdYts = new InterpolatedDiscountCurve<LogLinear>(usdCurveDates, usdCurveDfs, tsDayCounter);

        // USD/ARS forward points
        Real arsSpot = 56.881;
        (Period Tenor, Real Points)[] arsFwdPoints =
        [
            (new Period(1, TimeUnit.Months), 8.5157),
            (new Period(2, TimeUnit.Months), 12.7180),
            (new Period(3, TimeUnit.Months), 17.8310),
            (new Period(6, TimeUnit.Months), 30.3680),
            (new Period(9, TimeUnit.Months), 45.5520),
            (new Period(1, TimeUnit.Years), 60.7370),
        ];

        // Create the FX swap rate helpers for the ARS in USD curve.
        var instruments = new List<RateHelper>();
        var datedInstruments = new List<RateHelper>();
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        Date startDate = calendar.Advance(calendar.Adjust(asof), 2, TimeUnit.Days);
        foreach ((Period tenor, Real points) in arsFwdPoints)
        {
            Real arsFwd = points;
            instruments.Add(new FxSwapRateHelper(
                arsFwd, arsSpot, tenor, 2, calendar, BusinessDayConvention.Following, false, true, usdYts, context));
            datedInstruments.Add(new FxSwapRateHelper(
                arsFwd, arsSpot, startDate, calendar.Advance(startDate, tenor), true, usdYts));
        }

        // Create the ARS in USD curve with the default IterativeBootstrap.
        // Check that the ARS in USD curve throws by requesting a discount factor.
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
                asof, instruments, tsDayCounter, context));
        Assert.Contains("1st iteration: failed at 1st alive instrument", e.Message, StringComparison.Ordinal);

        // USD/ARS spot date. The date on which we check the ARS discount curve.
        var spotDate = new Date(27, September, 2019);

        // Create the ARS in USD curve with an IterativeBootstrap allowing for 4 retries.
        var ib = new IterativeBootstrap(null, null, null, 5);
        var arsYts = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            asof, instruments, tsDayCounter, context, bootstrap: ib);

        // Check that the ARS in USD curve builds and populate the spot ARS discount factor.
        DiscountFactor spotDfArs = arsYts.Discount(spotDate);

        // Additional dates and discount factors used in the final check i.e. that calculated 1Y FX forward equals input.
        var oneYearFwdDate = new Date(28, September, 2020);
        DiscountFactor spotDfUsd = usdYts.Discount(spotDate);
        DiscountFactor oneYearDfUsd = usdYts.Discount(oneYearFwdDate);

        // Given that the ARS in USD curve builds, check that the 1Y USD/ARS forward rate is as expected.
        DiscountFactor oneYearDfArs = arsYts.Discount(oneYearFwdDate);
        Real calcFwd = (spotDfArs * arsSpot / oneYearDfArs) / (spotDfUsd / oneYearDfUsd);
        Real expFwd = arsSpot + arsFwdPoints[^1].Points;
        Assert.True(Math.Abs(calcFwd - expFwd) <= 1e-10, $"calculated forward {calcFwd:R}, expected {expFwd:R}");

        // Check that datedInstruments give the same result.
        var datedArsYts = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            asof, datedInstruments, tsDayCounter, context, bootstrap: ib);
        Assert.True(arsYts.Dates().SequenceEqual(datedArsYts.Dates()));
        foreach (Date date in arsYts.Dates())
        {
            CheckClose(arsYts.Discount(date), datedArsYts.Discount(date), 1e-6);
        }
    }

    [Fact]
    public void TestCustomFuturesHelpers()
    {
        // Testing futures rate helpers with custom dates...
        var vars = new CommonVars();

        var helpers = new List<RateHelper>();

        Date startDate1 = vars.Today + 60;
        Real price1 = 97.0;
        Natural length1 = 2;
        BusinessDayConvention convention = BusinessDayConvention.ModifiedFollowing;
        bool endOfMonth = true;
        var dayCounter = new Actual360();

        helpers.Add(new FuturesRateHelper(
            price1, startDate1, length1, new TARGET(), convention, endOfMonth, dayCounter, 0.0, FuturesType.Custom));

        Date startDate2 = vars.Today + 120;
        Date endDate2 = startDate2 + 45;
        Real price2 = 96.5;

        helpers.Add(new FuturesRateHelper(price2, startDate2, endDate2, dayCounter, 0.0, FuturesType.Custom));

        Date startDate3 = vars.Today + 180;
        var index = new Euribor3M();
        Real price3 = 96.0;

        helpers.Add(new FuturesRateHelper(price3, startDate3, index, 0.0, FuturesType.Custom));

        YieldTermStructure curve = new PiecewiseYieldCurve<ForwardRate, BackwardFlat, IterativeBootstrap>(
            vars.Today, helpers, new Actual360(), vars.Context);

        Date endDate1 = new TARGET().Advance(startDate1, length1, TimeUnit.Months, convention, endOfMonth);

        Rate calculated = curve.ForwardRate(startDate1, endDate1, dayCounter, Compounding.Simple).Rate;
        Rate expected = (100 - price1) / 100;
        Real error = Math.Abs(expected - calculated);
        Real tolerance = 1e-8;
        Assert.True(
            error <= tolerance,
            $" first helper:\n\n estimated rate: {calculated:R}\n expected rate:  {expected:R}");

        calculated = curve.ForwardRate(startDate2, endDate2, dayCounter, Compounding.Simple).Rate;
        expected = (100 - price2) / 100;
        error = Math.Abs(expected - calculated);
        Assert.True(
            error <= tolerance,
            $" second helper:\n\n estimated rate: {calculated:R}\n expected rate:  {expected:R}");

        Date endDate3 = index.FixingCalendar.Advance(startDate3, index.Tenor, index.BusinessDayConvention);

        calculated = curve.ForwardRate(startDate3, endDate3, dayCounter, Compounding.Simple).Rate;
        expected = (100 - price3) / 100;
        error = Math.Abs(expected - calculated);
        Assert.True(
            error <= tolerance,
            $" third helper:\n\n estimated rate: {calculated:R}\n expected rate:  {expected:R}");
    }

    [Fact]
    public void TestSwapHelpersWithOnceFrequency()
    {
        // Testing single-coupon swap rate helpers...

        var context = new EvaluationContext { EvaluationDate = new Date(16, September, 2015) };

        var index = new IborIndex(
            "TestIndex", 4 * TimeUnit.Weeks, 1, new MXNCurrency(), new Mexico(), BusinessDayConvention.Following, false,
            new Actual360());

        Real r = 0.02;

        Assert.Null(Record.Exception(() => new SwapRateHelper(
            r, 4 * TimeUnit.Weeks, new Mexico(), Frequency.Once, BusinessDayConvention.Following, new Actual360(), index,
            context)));

        Assert.Null(Record.Exception(() => new OISRateHelper(
            2, 4 * TimeUnit.Weeks, r, new Estr(), context, null, false, 0, BusinessDayConvention.Following,
            Frequency.Once)));
    }

    [Fact]
    public void TestDepositForDates()
    {
        // Testing DepositRateHelper with custom fixingDate...
        var vars = new CommonVars();

        var helpers = new List<RateHelper>();
        Date fixingDate = new TARGET().Adjust(vars.Today);
        for (int i = 0; i < vars.Deposits; i++)
        {
            Rate r = vars.Rates[i];
            helpers.Add(new DepositRateHelper(
                r, fixingDate, new Euribor(new Period(DepositData[i].N, DepositData[i].Units))));
        }

        YieldTermStructure curve = new PiecewiseYieldCurve<ZeroYield, Linear, IterativeBootstrap>(
            vars.Settlement, helpers, new Actual365Fixed(), vars.Context);

        const Real tolerance = 1.0e-9;
        for (int i = 0; i < vars.Deposits; i++)
        {
            var index = new Euribor(new Period(DepositData[i].N, DepositData[i].Units), curve);
            Rate expectedRate = DepositData[i].Rate / 100, estimatedRate = index.Fixing(vars.Today, vars.Context);
            Assert.True(
                Math.Abs(expectedRate - estimatedRate) <= tolerance,
                $"{DepositData[i].N} {(DepositData[i].Units == TimeUnit.Weeks ? "week(s)" : "month(s)")} deposit:\n"
                + $"    estimated rate: {estimatedRate}\n    expected rate:  {expectedRate}");
        }
    }

    // Adapted: the fixings added to the index (lines 2297-2299) are held by the case's context; the index rebuilt on
    // the curve's handle (line 2321) is built on the curve.
    // Adapted: the curve's handle (line 2250) is the curve, the index built on it; the FRAs' forward rates are read
    // from their results.
    [Fact]
    public void TestFraForDates()
    {
        // Testing FraRateHelper with custom dates...

        var vars = new CommonVars();

        var helpers = new List<RateHelper>();
        var euribor6m = new Euribor6M();
        for (int i = 0; i < vars.Fras; i++)
        {
            Rate r = vars.FraRates[i];
            Date startDate = vars.Calendar.Advance(
                vars.Settlement, FraData[i].N, FraData[i].Units, euribor6m.BusinessDayConvention, euribor6m.EndOfMonth);
            Date endDate = vars.Calendar.Advance(
                vars.Settlement,
                FraData[i].N + 3,
                FraData[i].Units,
                euribor6m.BusinessDayConvention,
                euribor6m.EndOfMonth);
            helpers.Add(new FraRateHelper(r, startDate, endDate, euribor6m, PillarChoice.LastRelevantDate, null, false));
        }

        YieldTermStructure curve = new PiecewiseYieldCurve<ZeroYield, Linear, IterativeBootstrap>(
            vars.Settlement, helpers, new Actual365Fixed(), vars.Context);
        euribor6m = new Euribor6M(curve);

        const double tolerance = 1.0e-9;
        for (int i = 0; i < vars.Fras; i++)
        {
            Date start = vars.Calendar.Advance(
                vars.Settlement, FraData[i].N, FraData[i].Units, euribor6m.BusinessDayConvention, euribor6m.EndOfMonth);
            Date end = vars.Calendar.Advance(
                vars.Settlement,
                FraData[i].N + 3,
                FraData[i].Units,
                euribor6m.BusinessDayConvention,
                euribor6m.EndOfMonth);
            Assert.Equal(TimeUnit.Months, FraData[i].Units);

            var fra = new ForwardRateAgreement(euribor6m, start, end, PositionType.Long, FraData[i].Rate / 100, 100.0);
            Rate expectedRate = FraData[i].Rate / 100, estimatedRate = fra.Calculate(vars.Context).ForwardRate!.Rate;
            Assert.True(
                Math.Abs(expectedRate - estimatedRate) <= tolerance,
                $"{NanoQL.Utilities.DataFormattersFunctions.Ordinal(i + 1)} FRA failure: estimated rate {estimatedRate}, "
                + $"expected rate {expectedRate}");
        }
    }

    [Fact]
    public void TestDatedSwapHelpers()
    {
        // Testing dated swap rate helpers...
        var today = new Date(28, October, 2024);

        (Date Start, Date End, Rate Quote)[] swapData =
        [
            (new Date(1, November, 2024), new Date(1, November, 2025), 4.54),
            (new Date(15, October, 2024), new Date(15, October, 2026), 4.63),
            (new Date(28, October, 2024), new Date(1, November, 2029), 4.99),
            (new Date(4, November, 2024), new Date(4, November, 2034), 5.47),
            (new Date(11, October, 2024), new Date(11, October, 2044), 5.89),
        ];

        var euribor6m = new Euribor6M();
        IndexFixings fixings = IndexFixings.Empty
            .AddFixing(euribor6m, new Date(9, October, 2024), 0.0447)
            .AddFixing(euribor6m, new Date(11, October, 2024), 0.045)
            .AddFixing(euribor6m, new Date(24, October, 2024), 0.0442);
        var context = new EvaluationContext { EvaluationDate = today, Fixings = fixings };

        var calendar = new TARGET();
        const Frequency fixedLegFrequency = Frequency.Annual;
        const BusinessDayConvention fixedLegConvention = BusinessDayConvention.Unadjusted;
        var fixedLegDayCounter = new Thirty360(Thirty360Convention.BondBasis);

        var helpers = new List<RateHelper>();
        foreach ((Date start, Date end, Rate q) in swapData)
        {
            Rate r = q / 100;
            helpers.Add(new SwapRateHelper(
                r,
                start,
                end,
                calendar,
                fixedLegFrequency,
                fixedLegConvention,
                fixedLegDayCounter,
                euribor6m,
                context));
        }

        YieldTermStructure curve = new PiecewiseYieldCurve<ZeroYield, Linear, IterativeBootstrap>(
            today, helpers, new Actual365Fixed(), context);
        euribor6m = new Euribor6M(curve);

        foreach ((Date start, Date end, Rate q) in swapData)
        {
            VanillaSwap swap = new MakeVanillaSwap(new Period(), euribor6m)
                .WithFixedRate(0.0)
                .WithEffectiveDate(start)
                .WithTerminationDate(end)
                .WithFixedLegDayCount(fixedLegDayCounter)
                .WithFixedLegTenor(new Period(fixedLegFrequency))
                .WithFixedLegConvention(fixedLegConvention)
                .WithFixedLegTerminationDateConvention(fixedLegConvention)
                .ToVanillaSwap(context);

            Rate expectedRate = q / 100, estimatedRate = FairRate(swap, context, curve);
            Spread error = Math.Abs(expectedRate - estimatedRate);
            Real tolerance = 1e-9;
            Assert.True(
                error <= tolerance,
                $"swap from {start} to {end}:\n    estimated rate: {estimatedRate}\n    expected rate:  {expectedRate}\n"
                + $"    error:          {error}\n    tolerance:      {tolerance}");
        }
    }

    // Adapted: the index built on a handle linked to the curve once bootstrapped (lines 2366, 2392) becomes an index
    // built on the curve; setCouponPricer on the built swap's floating leg (line 2411) returns a copy.
    [Fact]
    public void TestSwapRateHelperWithCouponPricer()
    {
        // Testing SwapRateHelper with a custom coupon pricer...

        // Test for issue #1817: a coupon pricer (e.g. a BlackIborCouponPricer
        // carrying a volatility surface for timing/convexity adjustments) can be
        // attached to the floating leg of the swap built internally by
        // SwapRateHelper, so that the curve is bootstrapped with the same pricing
        // methodology that is later used to value swaps.
        var today = new Date(15, June, 2020);
        var context = new EvaluationContext { EvaluationDate = today };

        Datum[] swapData =
        [
            new(2, TimeUnit.Years, 0.015), new(5, TimeUnit.Years, 0.020), new(10, TimeUnit.Years, 0.025),
            new(20, TimeUnit.Years, 0.030),
        ];

        var vol = new ConstantOptionletVolatility(
            today, new TARGET(), BusinessDayConvention.Following, 0.20, new Actual365Fixed());
        var pricer = new BlackIborCouponPricer(context, vol);

        var index = new Euribor6M();

        var helpers = new List<SwapRateHelper>();
        foreach (Datum d in swapData)
        {
            helpers.Add(new SwapRateHelper(
                d.Rate,
                new Period(d.N, d.Units),
                new TARGET(),
                Frequency.Annual,
                BusinessDayConvention.ModifiedFollowing,
                new Thirty360(Thirty360Convention.BondBasis),
                index,
                context,
                0.0,
                new Period(0, TimeUnit.Days),
                null,
                null,
                PillarChoice.LastRelevantDate,
                null,
                false,
                null,
                null,
                pricer));
        }

        // the supplied pricer must be attached to every coupon of the floating leg
        // of the swap the helper builds internally
        foreach (SwapRateHelper helper in helpers)
        {
            foreach (CashFlow cf in helper.Swap.FloatingLeg)
            {
                var coupon = cf as FloatingRateCoupon;
                Assert.NotNull(coupon);
                Assert.True(
                    ReferenceEquals(coupon.Pricer, pricer),
                    "the coupon pricer was not attached to the floating leg of the rate helper's internal swap");
            }
        }

        // bootstrap and check self-consistency: a swap priced at its market rate
        // with the same coupon pricer must be at par on the resulting curve
        YieldTermStructure curve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            today, helpers, new Actual365Fixed(), context);
        var curveIndex = new Euribor6M(curve);

        Real tolerance = 1.0e-6;
        foreach (Datum d in swapData)
        {
            // built to match the helper's internal swap, priced with the same pricer
            VanillaSwap swap = new MakeVanillaSwap(new Period(d.N, d.Units), curveIndex)
                .WithFixedRate(d.Rate)
                .WithForwardStart(new Period(0, TimeUnit.Days))
                .WithDiscountingTermStructure(curve)
                .WithFixedLegDayCount(new Thirty360(Thirty360Convention.BondBasis))
                .WithFixedLegTenor(new Period(Frequency.Annual))
                .WithFixedLegConvention(BusinessDayConvention.ModifiedFollowing)
                .WithFixedLegTerminationDateConvention(BusinessDayConvention.ModifiedFollowing)
                .WithFixedLegCalendar(new TARGET())
                .WithFixedLegEndOfMonth(false)
                .WithFloatingLegCalendar(new TARGET())
                .WithFloatingLegEndOfMonth(false)
                .ToVanillaSwap(context);
            swap = CouponPricerFunctions.SetCouponPricer(swap, 1, pricer);

            Real npv = DiscountingSwapEngine.Calculate(swap, context, curve, includeSettlementDateFlows: false).NPV!.Value;
            Assert.True(
                Math.Abs(npv) <= tolerance,
                $"swap priced with the bootstrapping coupon pricer is not at par:\n    tenor:     {new Period(d.N, d.Units)}"
                + $"\n    NPV:       {npv}\n    tolerance: {tolerance}");
        }
    }

    [Fact]
    public void TestHelperDatesFromNonBusinessEvaluationDate()
    {
        // Testing that rate-helper dates are calculated from the actual evaluation date when the latter is not a
        // business day...

        // Saturday
        var today = new Date(20, June, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        var euribor6m = new Euribor6M();
        Calendar calendar = euribor6m.FixingCalendar;

        // fixing days are counted from the actual evaluation date,
        // not from the next business day
        Date expectedSpot = calendar.Advance(today, euribor6m.FixingDays, TimeUnit.Days);

        var depo = new DepositRateHelper(
            0.03,
            new Period(6, TimeUnit.Months),
            euribor6m.FixingDays,
            calendar,
            euribor6m.BusinessDayConvention,
            euribor6m.EndOfMonth,
            euribor6m.DayCounter,
            context);
        Assert.True(
            depo.EarliestDate == expectedSpot,
            $"deposit helper earliest date not calculated from the actual evaluation date:\n    expected: "
            + $"{expectedSpot}\n    obtained: {depo.EarliestDate}");

        var fra = new FraRateHelper(0.03, 3, euribor6m, context);
        Date expectedFraStart = calendar.Advance(
            expectedSpot, new Period(3, TimeUnit.Months), euribor6m.BusinessDayConvention, euribor6m.EndOfMonth);
        Assert.True(
            fra.EarliestDate == expectedFraStart,
            $"FRA helper earliest date not calculated from the actual evaluation date:\n    expected: "
            + $"{expectedFraStart}\n    obtained: {fra.EarliestDate}");

        Calendar bmaCalendar = new JointCalendar(
            new BMAIndex().FixingCalendar, new USDLibor(new Period(3, TimeUnit.Months)).FixingCalendar);
        var bmaHelper = new BMASwapRateHelper(
            0.75,
            new Period(5, TimeUnit.Years),
            3,
            bmaCalendar,
            new Period(Frequency.Quarterly),
            BusinessDayConvention.Following,
            new Actual360(),
            new BMAIndex(),
            new USDLibor(new Period(3, TimeUnit.Months)),
            context);
        Date expectedBmaStart = bmaCalendar.Advance(today, 3, TimeUnit.Days, BusinessDayConvention.Following);
        Assert.True(
            bmaHelper.EarliestDate == expectedBmaStart,
            $"BMA helper earliest date not calculated from the actual evaluation date:\n    expected: "
            + $"{expectedBmaStart}\n    obtained: {bmaHelper.EarliestDate}");
    }

    // Adapted: the evaluation date moved a month on (line 2493), which re-dates QuantLib's helper and rebuilds its
    // swap, is a second helper built under that date.
    [Fact]
    public void TestBmaHelperSwap()
    {
        // Testing the swap underlying a BMA swap-rate helper...

        var today = new Date(15, January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };

        Calendar bmaCalendar = new JointCalendar(
            new BMAIndex().FixingCalendar, new USDLibor(new Period(3, TimeUnit.Months)).FixingCalendar);
        var bmaIndex = new BMAIndex();
        BMASwapRateHelper Helper(EvaluationContext on) => new(
            0.75,
            new Period(5, TimeUnit.Years),
            2,
            bmaCalendar,
            new Period(Frequency.Quarterly),
            BusinessDayConvention.Following,
            new Actual360(),
            bmaIndex,
            new USDLibor(new Period(3, TimeUnit.Months)),
            on);
        BMASwapRateHelper helper = Helper(context);

        BMASwap swap = helper.Swap;
        Assert.NotNull(swap);
        Assert.Equal(helper.EarliestDate, swap.StartDate());
        Assert.Equal(20, swap.BmaLeg.Length);

        BMASwapRateHelper later = Helper(context with { EvaluationDate = bmaCalendar.Advance(today, new Period(1, TimeUnit.Months)) });
        Assert.NotSame(swap, later.Swap);
        Assert.Equal(later.EarliestDate, later.Swap.StartDate());
    }

    // testGlobalBootstrap's and testGlobalBootstrapPenalty's expected pillar dates.
    private static readonly Date[] GlobalBootstrapRefDates =
    [
        new(31, March, 2020), new(30, April, 2020), new(29, May, 2020), new(30, June, 2020),
        new(31, July, 2020), new(31, August, 2020), new(30, September, 2020), new(30, October, 2020),
        new(30, November, 2020), new(31, December, 2020), new(29, January, 2021), new(26, February, 2021),
        new(31, March, 2021), new(30, September, 2021), new(30, September, 2022), new(29, September, 2023),
        new(30, September, 2024), new(30, September, 2025), new(30, September, 2026), new(30, September, 2027),
        new(29, September, 2028), new(28, September, 2029), new(30, September, 2030), new(30, September, 2031),
        new(29, September, 2034), new(30, September, 2039), new(30, September, 2044), new(30, September, 2049),
        new(30, September, 2054), new(30, September, 2059), new(30, September, 2064), new(30, September, 2069),
    ];

    // testGlobalBootstrap's and testGlobalBootstrapPenalty's helpers: a deposit, 12 FRAs and 19 swaps on Euribor 6M.
    private static List<RateHelper> GlobalBootstrapHelpers(
        Real[] refMktRate, EvaluationContext context, out IborIndex index)
    {
        var helpers = new List<RateHelper>();
        index = new Euribor(new Period(6, TimeUnit.Months));

        helpers.Add(new DepositRateHelper(
            refMktRate[0] / 100.0,
            new Period(6, TimeUnit.Months),
            2,
            new TARGET(),
            BusinessDayConvention.ModifiedFollowing,
            true,
            new Actual360(),
            context));

        for (int i = 0; i < 12; ++i)
        {
            helpers.Add(new FraRateHelper(refMktRate[1 + i] / 100.0, new Period(i + 1, TimeUnit.Months), index, context));
        }

        Size[] swapTenors = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 15, 20, 25, 30, 35, 40, 45, 50];
        for (int i = 0; i < swapTenors.Length; ++i)
        {
            helpers.Add(new SwapRateHelper(
                refMktRate[13 + i] / 100.0,
                new Period(swapTenors[i], TimeUnit.Years),
                new TARGET(),
                Frequency.Annual,
                BusinessDayConvention.ModifiedFollowing,
                new Thirty360(Thirty360Convention.BondBasis),
                index,
                context));
        }

        return helpers;
    }

    // testGlobalBootstrap's additionalErrors: the additional errors for the cost function, from the implied quotes of
    // the additional helpers bound to the curve under construction.
    private static Vector AdditionalErrors(IBootstrapState state, EvaluationContext context)
    {
        IReadOnlyList<IBootstrapHelper> additionalHelpers = state.AdditionalHelpers;
        var errors = new Vector(5);
        Real a = additionalHelpers[0].ImpliedQuote(context);
        Real b = additionalHelpers[6].ImpliedQuote(context);
        for (int k = 0; k < 5; ++k)
        {
            errors[k] = ((5.0 - k) / 6.0 * a) + ((1.0 + k) / 6.0 * b) - additionalHelpers[1 + k].ImpliedQuote(context);
        }

        return errors;
    }

    // testGlobalBootstrap's additionalDates: additional dates used in the bootstrap.
    private static List<Date> AdditionalDates(EvaluationContext context)
    {
        Date today = context.EvaluationDate;
        Calendar cal = new TARGET();
        Date settl = cal.Advance(today, 2, TimeUnit.Days);
        var dates = new List<Date>(5);
        for (int i = 0; i < 5; ++i)
        {
            dates.Add(cal.Advance(settl, 1 + i, TimeUnit.Months));
        }

        // Add dates before the referenceDate and not in sorted order.
        // These should be skipped by GlobalBootstrap::initialize().
        dates.Insert(0, today - 1);
        dates.Add(today - 2);
        return dates;
    }

    // Adapted: the curve's handle linked after the curve is built (line 362), and the checking indexes built on it,
    // become indexes built on the curve; MakeVanillaSwap's default engine (makevanillaswap.cpp:178-189) prices the
    // fair rate on the index's curve. The handle relinked to the curve bootstrapped on the bonds (line 411) is that
    // curve, which the bonds' engine (lines 427-428), DiscountingBondEngine.Calculate, discounts on; their clean prices
    // (line 431) are read from its results.
    private static void TestCurveConsistency<TTraits, TInterpolator, TBootstrap>(
        CommonVars vars, TInterpolator interpolator = default, Real tolerance = 1.0e-9)
        where TTraits : struct, IYieldTraits
        where TInterpolator : struct, IInterpolator
        where TBootstrap : struct, IBootstrap
    {
        YieldTermStructure termStructure = new PiecewiseYieldCurve<TTraits, TInterpolator, TBootstrap>(
            vars.Settlement, vars.Instruments, new Actual360(), vars.Context, i: interpolator);

        // check deposits
        for (int i = 0; i < vars.Deposits; i++)
        {
            var index = new Euribor(new Period(DepositData[i].N, DepositData[i].Units), termStructure);
            Rate expectedRate = DepositData[i].Rate / 100, estimatedRate = index.Fixing(vars.Today, vars.Context);
            Assert.True(
                Math.Abs(expectedRate - estimatedRate) <= tolerance,
                $"{DepositData[i].N} {(DepositData[i].Units == TimeUnit.Weeks ? "week(s)" : "month(s)")} deposit:\n"
                + $"    estimated rate: {estimatedRate}\n    expected rate:  {expectedRate}");
        }

        // check swaps
        var euribor6m = new Euribor6M(termStructure);
        for (int i = 0; i < vars.Swaps; i++)
        {
            var tenor = new Period(SwapData[i].N, SwapData[i].Units);

            VanillaSwap swap = new MakeVanillaSwap(tenor, euribor6m)
                .WithFixedRate(0.0)
                .WithEffectiveDate(vars.Settlement)
                .WithFixedLegDayCount(vars.FixedLegDayCounter)
                .WithFixedLegTenor(new Period(vars.FixedLegFrequency))
                .WithFixedLegConvention(vars.FixedLegConvention)
                .WithFixedLegTerminationDateConvention(vars.FixedLegConvention)
                .ToVanillaSwap(vars.Context);

            Rate expectedRate = SwapData[i].Rate / 100, estimatedRate = FairRate(swap, vars.Context, termStructure);
            Spread error = Math.Abs(expectedRate - estimatedRate);
            Assert.True(
                error <= tolerance,
                $"{SwapData[i].N} year(s) swap:\n\n estimated rate: {estimatedRate}\n expected rate:  {expectedRate}\n"
                + $" error:          {error}\n tolerance:      {tolerance}");
        }

        // check bonds
        termStructure = new PiecewiseYieldCurve<TTraits, TInterpolator, TBootstrap>(
            vars.Settlement, vars.BondHelpers, new Actual360(), vars.Context, i: interpolator);

        for (int i = 0; i < vars.Bonds; i++)
        {
            Date maturity = vars.Calendar.Advance(vars.Today, BondData[i].N, BondData[i].Units);
            Date issue = vars.Calendar.Advance(maturity, -BondData[i].Length, TimeUnit.Years);
            Rate[] coupons = [BondData[i].Coupon / 100.0];

            var bond = new FixedRateBond(
                vars.BondSettlementDays, 100.0, vars.Schedules[i], coupons, vars.BondDayCounter, vars.BondConvention,
                vars.BondRedemption, issue);

            BondResults results = DiscountingBondEngine.Calculate(bond, vars.Context, termStructure);

            Real expectedPrice = BondData[i].Price, estimatedPrice = bond.CleanPrice(results, vars.Context);
            Real error = Math.Abs(expectedPrice - estimatedPrice);
            Assert.True(
                error <= tolerance,
                $"{NanoQL.Utilities.DataFormattersFunctions.Ordinal(i + 1)} bond failure:\n  estimated price: "
                + $"{estimatedPrice}\n  expected price:  {expectedPrice}\n  error:           {error}");
        }

        // check FRA, use indexed
        bool useIndexedFra = true;

        termStructure = new PiecewiseYieldCurve<TTraits, TInterpolator, IterativeBootstrap>(
            vars.Settlement, vars.FraHelpers(useIndexedFra), new Actual360(), vars.Context, i: interpolator);
        var euribor3m = new Euribor3M(termStructure);

        for (int i = 0; i < vars.Fras; i++)
        {
            Date start = vars.Calendar.Advance(
                vars.Settlement, FraData[i].N, FraData[i].Units, euribor3m.BusinessDayConvention, euribor3m.EndOfMonth);
            Assert.Equal(TimeUnit.Months, FraData[i].Units);

            var fra = new ForwardRateAgreement(
                euribor3m, start, PositionType.Long, FraData[i].Rate / 100, 100.0, termStructure);
            Rate expectedRate = FraData[i].Rate / 100, estimatedRate = fra.Calculate(vars.Context).ForwardRate!.Rate;
            Assert.True(
                Math.Abs(expectedRate - estimatedRate) <= tolerance,
                $"{NanoQL.Utilities.DataFormattersFunctions.Ordinal(i + 1)} FRA (indexed) failure: estimated rate "
                + $"{estimatedRate}, expected rate {expectedRate}");
        }

        // check FRA, don't use indexed
        useIndexedFra = false;

        termStructure = new PiecewiseYieldCurve<TTraits, TInterpolator, IterativeBootstrap>(
            vars.Settlement, vars.FraHelpers(useIndexedFra), new Actual360(), vars.Context, i: interpolator);
        euribor3m = new Euribor3M(termStructure);

        for (int i = 0; i < vars.Fras; i++)
        {
            Date start = vars.Calendar.Advance(
                vars.Settlement, FraData[i].N, FraData[i].Units, euribor3m.BusinessDayConvention, euribor3m.EndOfMonth);
            Assert.Equal(TimeUnit.Months, FraData[i].Units);

            Date end = vars.Calendar.Advance(
                vars.Settlement, 3 + FraData[i].N, TimeUnit.Months, euribor3m.BusinessDayConvention, euribor3m.EndOfMonth);
            var fra = new ForwardRateAgreement(
                euribor3m, start, end, PositionType.Long, FraData[i].Rate / 100, 100.0, termStructure);
            Rate expectedRate = FraData[i].Rate / 100, estimatedRate = fra.Calculate(vars.Context).ForwardRate!.Rate;
            Assert.True(
                Math.Abs(expectedRate - estimatedRate) <= tolerance,
                $"{NanoQL.Utilities.DataFormattersFunctions.Ordinal(i + 1)} FRA (at par) failure: estimated rate "
                + $"{estimatedRate}, expected rate {expectedRate}");
        }

        // check immFuts
        termStructure = new PiecewiseYieldCurve<TTraits, TInterpolator, IterativeBootstrap>(
            vars.Settlement, vars.ImmFutHelpers, new Actual360(), vars.Context, i: interpolator);
        euribor3m = new Euribor3M(termStructure);

        Date? immStart = null;
        for (int i = 0; i < vars.ImmFuts; i++)
        {
            immStart = IMM.NextDate(vars.Context, immStart, false);

            // if the fixing is before the evaluation date, we
            // just jump forward by one future maturity
            if (euribor3m.FixingDate(immStart.Value) < vars.Context.EvaluationDate)
            {
                immStart = IMM.NextDate(vars.Context, immStart, false);
            }

            var immFut = new ForwardRateAgreement(
                euribor3m, immStart.Value, PositionType.Long, ImmFutData[i].Rate / 100, 100.0, termStructure);
            Rate expectedRate = ImmFutData[i].Rate / 100, estimatedRate = immFut.Calculate(vars.Context).ForwardRate!.Rate;
            Assert.True(
                Math.Abs(expectedRate - estimatedRate) <= tolerance,
                $"{NanoQL.Utilities.DataFormattersFunctions.Ordinal(i + 1)} IMM futures failure: estimated rate "
                + $"{estimatedRate}, expected rate {expectedRate}");
        }

        // check asxFuts
        termStructure = new PiecewiseYieldCurve<TTraits, TInterpolator, IterativeBootstrap>(
            vars.Settlement, vars.AsxFutHelpers, new Actual360(), vars.Context, i: interpolator);
        euribor3m = new Euribor3M(termStructure);

        Date? asxStart = null;
        for (int i = 0; i < vars.AsxFuts; i++)
        {
            asxStart = ASX.NextDate(vars.Context, asxStart, false);

            // if the fixing is before the evaluation date, we
            // just jump forward by one future maturity
            if (euribor3m.FixingDate(asxStart.Value) < vars.Context.EvaluationDate)
            {
                asxStart = ASX.NextDate(vars.Context, asxStart, false);
            }

            if (euribor3m.FixingCalendar.IsHoliday(asxStart.Value))
            {
                continue;
            }

            var asxFut = new ForwardRateAgreement(
                euribor3m, asxStart.Value, PositionType.Long, AsxFutData[i].Rate / 100, 100.0, termStructure);
            Rate expectedRate = AsxFutData[i].Rate / 100, estimatedRate = asxFut.Calculate(vars.Context).ForwardRate!.Rate;
            Assert.True(
                Math.Abs(expectedRate - estimatedRate) <= tolerance,
                $"{NanoQL.Utilities.DataFormattersFunctions.Ordinal(i + 1)} ASX futures failure: estimated rate "
                + $"{estimatedRate}, expected rate {expectedRate}");
        }

        // end checks
    }

    // Adapted: the fixture's calendar, today and settlement, which QuantLib re-adjusts (lines 573-579), are the case's
    // own, and the BMA index's last fixing (line 604) is in its context, built before the helpers; the curve's handle
    // (lines 611-612) is the curve, the BMA index built on it; the swaps' engine (lines 641-642) is a static call on
    // the Libor index's curve, and the fair Libor fraction is read from its results.
    private static void TestBMACurveConsistency<TTraits, TInterpolator, TBootstrap>(
        CommonVars vars, TInterpolator interpolator = default, Real tolerance = 1.0e-9)
        where TTraits : struct, IYieldTraits
        where TInterpolator : struct, IInterpolator
        where TBootstrap : struct, IBootstrap
    {
        // re-adjust settlement
        Calendar calendar = new JointCalendar(
            new BMAIndex().FixingCalendar,
            new USDLibor(new Period(3, TimeUnit.Months)).FixingCalendar,
            JointCalendarRule.JoinHolidays);
        Date today = calendar.Adjust(Date.TodaysDate());
        Date settlement = calendar.Advance(today, vars.SettlementDays, TimeUnit.Days);

        YieldTermStructure riskFreeCurve = new FlatForward(settlement, 0.04, new Actual360());

        var bmaIndex = new BMAIndex();
        var liborIndex = new USDLibor(new Period(3, TimeUnit.Months), riskFreeCurve);

        int w = (int)today.Weekday;
        Date lastWednesday = w >= 4 ? today - (w - 4) : today + (4 - w - 7);
        Date lastFixing = bmaIndex.FixingCalendar.Adjust(lastWednesday);
        var context = new EvaluationContext
        {
            EvaluationDate = today,
            Fixings = IndexFixings.Empty.AddFixing(bmaIndex, lastFixing, 0.03),
        };

        var bmaHelpers = new List<RateHelper>();
        for (int i = 0; i < vars.Bmas; ++i)
        {
            Real f = vars.Fractions[i];
            bmaHelpers.Add(new BMASwapRateHelper(
                f,
                new Period(BmaData[i].N, BmaData[i].Units),
                vars.SettlementDays,
                calendar,
                new Period(vars.BmaFrequency),
                vars.BmaConvention,
                vars.BmaDayCounter,
                bmaIndex,
                liborIndex,
                context));
        }

        YieldTermStructure termStructure = new PiecewiseYieldCurve<TTraits, TInterpolator, TBootstrap>(
            today, bmaHelpers, new Actual360(), context, i: interpolator);

        // check BMA swaps
        var bma = new BMAIndex(termStructure);
        var libor3m = new USDLibor(new Period(3, TimeUnit.Months), riskFreeCurve);
        for (int i = 0; i < vars.Bmas; i++)
        {
            var tenor = new Period(BmaData[i].N, BmaData[i].Units);

            Schedule bmaSchedule = new MakeSchedule()
                .From(settlement)
                .To(settlement + tenor)
                .WithFrequency(vars.BmaFrequency)
                .WithCalendar(bma.FixingCalendar)
                .WithConvention(vars.BmaConvention)
                .Backwards()
                .ToSchedule();
            Schedule liborSchedule = new MakeSchedule()
                .From(settlement)
                .To(settlement + tenor)
                .WithTenor(libor3m.Tenor)
                .WithCalendar(libor3m.FixingCalendar)
                .WithConvention(libor3m.BusinessDayConvention)
                .EndOfMonth(libor3m.EndOfMonth)
                .Backwards()
                .ToSchedule();

            var swap = new BMASwap(
                SwapType.Payer,
                100.0,
                liborSchedule,
                0.75,
                0.0,
                libor3m,
                libor3m.DayCounter,
                bmaSchedule,
                bma,
                vars.BmaDayCounter,
                context);
            BMASwapResults results = DiscountingSwapEngine.Calculate(swap, context, libor3m.ForwardingTermStructure!);

            Real expectedFraction = BmaData[i].Rate / 100, estimatedFraction = results.FairLiborFraction!.Value;
            Real error = Math.Abs(expectedFraction - estimatedFraction);
            Assert.True(
                error <= tolerance,
                $"{BmaData[i].N} year(s) BMA swap: estimated libor fraction {estimatedFraction}, expected libor "
                + $"fraction {expectedFraction}, error {error}, tolerance {tolerance}");
        }
    }

    // testLiborFixing's checks, on a curve bootstrapped under the given context.
    private static void CheckLiborFixing(
        CommonVars vars, List<RateHelper> swapHelpers, EvaluationContext context, string when)
    {
        YieldTermStructure termStructure = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            vars.Settlement, swapHelpers, new Actual360(), context);

        var index = new Euribor6M(termStructure);
        for (int i = 0; i < vars.Swaps; i++)
        {
            var tenor = new Period(SwapData[i].N, SwapData[i].Units);

            VanillaSwap swap = new MakeVanillaSwap(tenor, index)
                .WithFixedRate(0.0)
                .WithEffectiveDate(vars.Settlement)
                .WithFixedLegDayCount(vars.FixedLegDayCounter)
                .WithFixedLegTenor(new Period(vars.FixedLegFrequency))
                .WithFixedLegConvention(vars.FixedLegConvention)
                .WithFixedLegTerminationDateConvention(vars.FixedLegConvention)
                .ToVanillaSwap(context);

            Rate expectedRate = SwapData[i].Rate / 100, estimatedRate = FairRate(swap, context, termStructure);
            Real tolerance = 1.0e-9;
            Assert.True(
                Math.Abs(expectedRate - estimatedRate) <= tolerance,
                $"{when}:\n{SwapData[i].N} year(s) swap:\n    estimated rate: {estimatedRate}\n"
                + $"    expected rate:  {expectedRate}");
        }
    }

    private static void TestPiecewiseSpreadYieldCurveImpl<TBootstrap>()
        where TBootstrap : struct, IBootstrap
    {
        // Use fixed evaluationDate to make the test stable. When usingAtParCoupons() == false the dates don't always
        // align between the 3M and 6M indexes, but most of the test is still valid.
        var vars = new CommonVars(new Date(23, Sep, 2019));
        var dc = new Actual365Fixed();

        // First, build the base curve. We can use any bootstrapping and interpolation.
        YieldTermStructure baseCurve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            vars.Settlement, vars.Instruments, dc, vars.Context, i: default(LogLinear)).EnableExtrapolation();

        // Now build the curve with fewer benchmarks as a spread to the base.
        Datum[] swapData =
        [
            new(1, TimeUnit.Years, 4.44),
            new(3, TimeUnit.Years, 4.55),
            new(6, TimeUnit.Years, 4.81),
            new(9, TimeUnit.Years, 5.01),
            new(15, TimeUnit.Years, 5.25),
            new(30, TimeUnit.Years, 5.36),
        ];

        var helpers = new List<RateHelper>();
        var euribor3m = new Euribor3M();
        foreach (Datum datum in swapData)
        {
            helpers.Add(new SwapRateHelper(
                datum.Rate / 100.0,
                new Period(datum.N, datum.Units),
                vars.Calendar,
                vars.FixedLegFrequency,
                vars.FixedLegConvention,
                vars.FixedLegDayCounter,
                euribor3m,
                vars.Context));
        }

        // We rely on LogLinear interpolation to check the curve's shape.
        PiecewiseSpreadYieldCurve<LogLinear, TBootstrap> curve =
            new PiecewiseSpreadYieldCurve<LogLinear, TBootstrap>(baseCurve, helpers, vars.Context, default(LogLinear))
                .EnableExtrapolation();

        // Check that we reprice the swaps.
        const Real tolerance = 1.0e-9;
        euribor3m = new Euribor3M(curve);
        foreach (Datum datum in swapData)
        {
            VanillaSwap swap = new MakeVanillaSwap(new Period(datum.N, datum.Units), euribor3m)
                .WithFixedRate(0.0)
                .WithEffectiveDate(vars.Settlement)
                .WithFixedLegDayCount(vars.FixedLegDayCounter)
                .WithFixedLegTenor(new Period(vars.FixedLegFrequency))
                .WithFixedLegConvention(vars.FixedLegConvention)
                .WithFixedLegTerminationDateConvention(vars.FixedLegConvention)
                .ToVanillaSwap(vars.Context);

            Rate expectedRate = datum.Rate / 100.0, estimatedRate = FairRate(swap, vars.Context, curve);
            Spread error = Math.Abs(expectedRate - estimatedRate);
            Assert.True(
                error <= tolerance,
                $"{datum.N} year(s) swap:\n"
                + $"\n estimated rate: {DataFormattersFunctions.Rate(estimatedRate)}"
                + $"\n expected rate:  {DataFormattersFunctions.Rate(expectedRate)}"
                + $"\n error:          {DataFormattersFunctions.Rate(error)}"
                + $"\n tolerance:      {DataFormattersFunctions.Rate(tolerance)}");
        }

        // Check that the curve has shape between pillars.
        Date prev = vars.Settlement;
        foreach (RateHelper helper in helpers)
        {
            Date pillar = helper.PillarDate!.Value;
            Rate rate1 = curve.ForwardRate(prev, pillar, dc, Compounding.Continuous).Rate;
            Rate rate2 = curve.ForwardRate(prev, prev + ((pillar - prev) / 2), dc, Compounding.Continuous).Rate;
            Assert.True(Math.Abs(rate1 - rate2) > 1e-4, $"no shape between {prev} and {pillar}");
            prev = pillar;
        }

        // Check that extrapolation preserves constant spread.
        Date maxDate = curve.MaxDate;
        Assert.Equal(baseCurve.MaxDate, maxDate);
        Rate r1 = curve.ForwardRate(maxDate - new Period(1, TimeUnit.Years), maxDate, dc, Compounding.Continuous).Rate;
        Rate r2 = curve.ForwardRate(maxDate, maxDate + new Period(1, TimeUnit.Years), dc, Compounding.Continuous).Rate;
        Rate baseRate1 =
            baseCurve.ForwardRate(maxDate - new Period(1, TimeUnit.Years), maxDate, dc, Compounding.Continuous).Rate;
        Rate baseRate2 =
            baseCurve.ForwardRate(maxDate, maxDate + new Period(1, TimeUnit.Years), dc, Compounding.Continuous).Rate;
        CheckClose(r1 - baseRate1, r2 - baseRate2, 1e-9);

        // Check accessors.
        Assert.Equal(helpers.Count + 1, curve.Dates().Length);
        Assert.Equal(helpers.Count + 1, curve.Times().Length);
        Assert.Equal(helpers.Count + 1, curve.Data().Length);
        var nodes = curve.Nodes();
        Assert.Equal(helpers.Count + 1, nodes.Length);

        Assert.Equal(vars.Settlement, curve.Dates()[0]);
        Assert.Equal(0.0, curve.Times()[0]);
        Assert.Equal(1.0, curve.Data()[0]);
        Assert.True(nodes[0].Key == vars.Settlement && nodes[0].Value == 1.0);
        for (int i = 0; i < helpers.Count; ++i)
        {
            Assert.Equal(helpers[i].PillarDate!.Value, curve.Dates()[i + 1]);
            Assert.Equal(curve.TimeFromReference(helpers[i].PillarDate!.Value), curve.Times()[i + 1]);
            Assert.True(nodes[i + 1].Key == curve.Dates()[i + 1] && nodes[i + 1].Value == curve.Data()[i + 1]);
        }

        // Check that we can rebuild the curve from raw data.
        SpreadDiscountCurve rawCurve =
            new SpreadDiscountCurve(curve.BaseCurve, curve.Dates(), curve.Data()).EnableExtrapolation();

        int maxSwapYears = swapData[^1].N;
        for (int i = 0; i < maxSwapYears + 3; ++i)
        {
            Date d = vars.Settlement + new Period(i, TimeUnit.Years);
            CheckClose(curve.Discount(d), rawCurve.Discount(d), 1e-9);
        }
    }

    // A swap's fair rate as MakeVanillaSwap's default engine computes it: discounted on the index's curve, without the
    // settlement date's flows (makevanillaswap.cpp:178-189).
    private static Rate FairRate(VanillaSwap swap, EvaluationContext context, YieldTermStructure curve) =>
        DiscountingSwapEngine.Calculate(swap, context, curve, includeSettlementDateFlows: false).FairRate!.Value;

    // QL_CHECK_CLOSE, Boost's BOOST_CHECK_CLOSE: |actual - expected| within the tolerance, in percent, of both values.
    private static void CheckClose(double actual, double expected, double tolerancePercent)
    {
        double diff = Math.Abs(actual - expected), fraction = tolerancePercent * 0.01;
        Assert.True(
            SafeDivision(diff, Math.Abs(expected)) <= fraction && SafeDivision(diff, Math.Abs(actual)) <= fraction,
            $"difference between {actual:R} and {expected:R} exceeds {tolerancePercent:R}%");
    }

    // Boost's safe_fpt_division: f1 / f2, without overflow or underflow.
    private static double SafeDivision(double f1, double f2)
    {
        const double NormalMin = 2.2250738585072014e-308;
        if (f2 < 1.0 && f1 > f2 * double.MaxValue)
        {
            return double.MaxValue;
        }

        if (Math.Abs(f1) <= NormalMin || (f2 > 1.0 && f1 < f2 * NormalMin))
        {
            return 0.0;
        }

        return f1 / f2;
    }

    private readonly record struct Datum(Integer N, TimeUnit Units, Rate Rate);

    private readonly record struct BondDatum(
        Integer N, TimeUnit Units, Integer Length, Frequency Frequency, Rate Coupon, Real Price);

    // The fixture without its futures, FRA and BMA helpers, which no ported case reads; testCA365Futures reads
    // its futures prices and builds its own futures helpers.
    private sealed class CommonVars
    {
        public CommonVars(Date? evaluationDate = null)
        {
            // data
            Calendar = new TARGET();
            SettlementDays = 2;
            Today = Calendar.Adjust(evaluationDate ?? Date.TodaysDate());
            Context = new EvaluationContext { EvaluationDate = Today };
            Settlement = Calendar.Advance(Today, SettlementDays, TimeUnit.Days);
            FixedLegConvention = BusinessDayConvention.Unadjusted;
            FixedLegFrequency = Frequency.Annual;
            FixedLegDayCounter = new Thirty360(Thirty360Convention.BondBasis);
            BondSettlementDays = 3;
            BondDayCounter = new ActualActual(ActualActualConvention.ISDA);
            BondConvention = BusinessDayConvention.Following;
            BondRedemption = 100.0;
            BmaFrequency = Frequency.Quarterly;
            BmaConvention = BusinessDayConvention.Following;
            BmaDayCounter = new ActualActual(ActualActualConvention.ISDA);

            Deposits = DepositData.Length;
            Fras = FraData.Length;
            ImmFuts = ImmFutData.Length;
            AsxFuts = AsxFutData.Length;
            Swaps = SwapData.Length;
            Bonds = BondData.Length;
            Bmas = BmaData.Length;

            // market elements
            Rates = new Rate[Deposits + Swaps];
            for (int i = 0; i < Deposits; i++)
            {
                Rates[i] = DepositData[i].Rate / 100;
            }

            for (int i = 0; i < Swaps; i++)
            {
                Rates[i + Deposits] = SwapData[i].Rate / 100;
            }

            FraRates = new Rate[Fras];
            for (int i = 0; i < Fras; i++)
            {
                FraRates[i] = FraData[i].Rate / 100;
            }

            ImmFutPrices = new Real[ImmFuts];
            for (int i = 0; i < ImmFuts; i++)
            {
                ImmFutPrices[i] = 100.0 - ImmFutData[i].Rate;
            }

            AsxFutPrices = new Real[AsxFuts];
            for (int i = 0; i < AsxFuts; i++)
            {
                AsxFutPrices[i] = 100.0 - AsxFutData[i].Rate;
            }

            Fractions = new Real[Bmas];
            for (int i = 0; i < Bmas; i++)
            {
                Fractions[i] = BmaData[i].Rate / 100;
            }

            // rate helpers
            var euribor6m = new Euribor6M();
            for (int i = 0; i < Deposits; i++)
            {
                Rate r = Rates[i];
                Instruments.Add(new DepositRateHelper(
                    r, new Euribor(new Period(DepositData[i].N, DepositData[i].Units)), Context));
            }

            for (int i = 0; i < Swaps; i++)
            {
                Rate r = Rates[i + Deposits];
                Instruments.Add(new SwapRateHelper(
                    r,
                    new Period(SwapData[i].N, SwapData[i].Units),
                    Calendar,
                    FixedLegFrequency,
                    FixedLegConvention,
                    FixedLegDayCounter,
                    euribor6m,
                    Context));
            }

            Date? immDate = null;
            var euribor3m = new Euribor3M();
            for (int i = 0; i < ImmFuts; i++)
            {
                Real r = ImmFutPrices[i];
                immDate = IMM.NextDate(Context, immDate, false);

                // if the fixing is before the evaluation date, we
                // just jump forward by one future maturity
                if (euribor3m.FixingDate(immDate.Value) < Context.EvaluationDate)
                {
                    immDate = IMM.NextDate(Context, immDate, false);
                }

                ImmFutHelpers.Add(new FuturesRateHelper(r, immDate.Value, euribor3m, 0.0, FuturesType.IMM));
            }

            Date? asxDate = null;
            for (int i = 0; i < AsxFuts; i++)
            {
                Real r = AsxFutPrices[i];
                asxDate = ASX.NextDate(Context, asxDate, false);

                // if the fixing is before the evaluation date, we
                // just jump forward by one future maturity
                if (euribor3m.FixingDate(asxDate.Value) < Context.EvaluationDate)
                {
                    asxDate = ASX.NextDate(Context, asxDate, false);
                }

                if (euribor3m.FixingCalendar.IsBusinessDay(asxDate.Value))
                {
                    AsxFutHelpers.Add(new FuturesRateHelper(r, asxDate.Value, euribor3m, 0.0, FuturesType.ASX));
                }
            }

            Schedules = new Schedule[Bonds];
            for (int i = 0; i < Bonds; i++)
            {
                Real p = BondData[i].Price;
                Date maturity = Calendar.Advance(Today, BondData[i].N, BondData[i].Units);
                Date issue = Calendar.Advance(maturity, -BondData[i].Length, TimeUnit.Years);
                Rate[] coupons = [BondData[i].Coupon / 100.0];
                Schedules[i] = new Schedule(
                    issue,
                    maturity,
                    new Period(BondData[i].Frequency),
                    Calendar,
                    BondConvention,
                    BondConvention,
                    DateGenerationRule.Backward,
                    false,
                    Context);
                BondHelpers.Add(new FixedRateBondHelper(
                    p,
                    BondSettlementDays,
                    BondRedemption,
                    Schedules[i],
                    coupons,
                    BondDayCounter,
                    Context,
                    BondConvention,
                    BondRedemption,
                    issue));
            }
        }

        public Calendar Calendar { get; }

        public Natural SettlementDays { get; }

        public Date Today { get; }

        public Date Settlement { get; }

        public EvaluationContext Context { get; }

        public BusinessDayConvention FixedLegConvention { get; }

        public Frequency FixedLegFrequency { get; }

        public DayCounter FixedLegDayCounter { get; }

        public Frequency BmaFrequency { get; }

        public BusinessDayConvention BmaConvention { get; }

        public DayCounter BmaDayCounter { get; }

        public int Deposits { get; }

        public int Fras { get; }

        public int ImmFuts { get; }

        public int AsxFuts { get; }

        public int Bmas { get; }

        public Rate[] FraRates { get; }

        public Real[] AsxFutPrices { get; }

        public Real[] Fractions { get; }

        public List<RateHelper> ImmFutHelpers { get; } = [];

        public List<RateHelper> AsxFutHelpers { get; } = [];

        public int Swaps { get; }

        public Rate[] Rates { get; }

        public Real[] ImmFutPrices { get; }

        public List<RateHelper> Instruments { get; } = [];

        public Natural BondSettlementDays { get; }

        public DayCounter BondDayCounter { get; }

        public BusinessDayConvention BondConvention { get; }

        public Real BondRedemption { get; }

        public int Bonds { get; }

        public Schedule[] Schedules { get; }

        public List<RateHelper> BondHelpers { get; } = [];

        // Adapted: the helpers take the fixture's context.
        public List<RateHelper> FraHelpers(bool useIndexedFra)
        {
            var helpers = new List<RateHelper>(Fras);
            var euribor3m = new Euribor3M();
            for (int i = 0; i < Fras; i++)
            {
                Rate r = FraRates[i];
                helpers.Add(new FraRateHelper(
                    r,
                    FraData[i].N,
                    FraData[i].N + 3,
                    euribor3m.FixingDays,
                    euribor3m.FixingCalendar,
                    euribor3m.BusinessDayConvention,
                    euribor3m.EndOfMonth,
                    euribor3m.DayCounter,
                    Context,
                    PillarChoice.LastRelevantDate,
                    null,
                    useIndexedFra));
            }

            return helpers;
        }
    }
}
