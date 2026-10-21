// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The finite-difference Black-Scholes engine on structures that measure time differently. Its times are the risk-free
/// curve's, measured from its reference date in its day counter (<c>blackscholesprocess.cpp:149-152</c>), and QuantLib
/// reads the dividend curve, the volatility and the quanto helper's structures at those times
/// (<c>fdmblackscholesop.cpp:51-66</c>, <c>fdmblackscholesmesher.cpp:63-103</c>, <c>fdmquantohelper.cpp:43-55</c>),
/// each of which measures time from its own reference date in its own day counter. A one-year put on a risk-free curve
/// on Act/360, with the same discount factor at every date as one on Act/365, prices 0.93% above QuantLib's analytic
/// engine, which reads every structure by date and agrees on both; on a curve settling two business days after its
/// volatility's date, 0.74% below. nanoql's operator and mesher refuse structures that aren't on the risk-free curve's
/// day counter and reference date, "all term structures must share one day counter" and "... one reference date", as
/// <see cref="QuantoTermStructure"/> does for day counters. A dividend curve that is a flat forward at zero, as
/// QuantLib's <see cref="BlackScholesProcess"/> builds on Act/365 and the evaluation date, discounts by 1 at any time
/// and is taken however it is dated, so that process prices as QuantLib's does.
/// </summary>
public class FdmMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly DayCounter A360 = new Actual360();

    [Fact]
    public void ARiskFreeCurveOnAnotherDayCounterIsRefused()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + 365));
        GeneralizedBlackScholesProcess on365 = Process(new FlatForward(Today, 0.05, A365));
        GeneralizedBlackScholesProcess on360 = Process(new FlatForward(Today, 0.05 * 360.0 / 365.0, A360));

        // QuantLib's check: its analytic engine reads the two curves' equal discount factors, 8.2268370475 on both
        double analytic = AnalyticEuropeanEngine.Calculate(put, context, on365).NPV!.Value;
        Assert.Equal(analytic, AnalyticEuropeanEngine.Calculate(put, context, on360).NPV!.Value, 1e-12);
        double fd = FdBlackScholesVanillaEngine.Calculate(put, context, on365, tGrid: 400, xGrid: 400).NPV!.Value;
        Assert.Equal(0.0, (fd / analytic) - 1.0, 1e-4);

        // QuantLib's finite-difference engine reads 8.3036142945 on Act/360, 9.3e-3 above, where it reads 1.7e-5 above
        // on Act/365
        Assert.Throws<ArgumentException>(() => FdBlackScholesVanillaEngine.Calculate(put, context, on360, tGrid: 400, xGrid: 400));
    }

    [Fact]
    public void ARiskFreeCurveOnAnotherReferenceDateIsRefused()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + 365));
        YieldTermStructure settling = FlatForward.Create(2, new TARGET(), 0.05, A365, context);

        // QuantLib's analytic engine reads 8.2530320612 and its finite-difference engine (400 × 400) 8.1923368426,
        // 7.4e-3 below: the engine's maturity is the risk-free curve's 361 days, where the volatility accrues over 365
        Assert.Equal(Today + 4, settling.ReferenceDate);
        Assert.Equal(8.2530320612, AnalyticEuropeanEngine.Calculate(put, context, Process(settling)).NPV!.Value, 1e-10);
        Assert.Throws<ArgumentException>(() => FdBlackScholesVanillaEngine.Calculate(put, context, Process(settling), tGrid: 400, xGrid: 400));

        // dated alike, the structures are taken: a BlackScholesProcess's zero dividend curve, on the evaluation date,
        // too
        BlackScholesProcess dated = BlackScholesProcess.Create(
            100.0, settling, new BlackConstantVol(settling.ReferenceDate, null, 0.25, A365), context);
        var american = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new AmericanExercise(Today, Today + 365));

        // QuantLib reads 7.932370363490167
        Assert.Equal(7.932370363490167, FdBlackScholesVanillaEngine.Calculate(american, context, dated).NPV!.Value, 1e-12);
    }

    [Fact]
    public void EachStructureTheEngineReadsIsChecked()
    {
        var mesher = new FdmMesherComposite(new Uniform1dMesher(3.0, 6.0, 11));
        YieldTermStructure r365 = new FlatForward(Today, 0.05, A365);
        var dividendOn360 = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A360), r365, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        var volatilityOn360 = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), r365, new BlackConstantVol(Today, new NullCalendar(), 0.25, A360));
        GeneralizedBlackScholesProcess one = Process(r365);
        var foreignOn360 = new FdmQuantoHelper(
            r365, new FlatForward(Today, 0.03, A360), new BlackConstantVol(Today, new NullCalendar(), 0.1, A365), -0.5, 1.0);

        Assert.Throws<ArgumentException>(() => new FdmBlackScholesOp(mesher, dividendOn360, 100.0));
        Assert.Throws<ArgumentException>(() => new FdmBlackScholesOp(mesher, volatilityOn360, 100.0));
        Assert.Throws<ArgumentException>(() => new FdmBlackScholesOp(mesher, volatilityOn360, 100.0, localVol: true));
        Assert.Throws<ArgumentException>(() => new FdmBlackScholesOp(mesher, one, 100.0, quantoHelper: foreignOn360));
        Assert.Throws<ArgumentException>(() => new FdmBlackScholesMesher(11, dividendOn360, 1.0, 100.0));
        Assert.Throws<ArgumentException>(() => new FdmBlackScholesMesher(11, volatilityOn360, 1.0, 100.0));
        Assert.Throws<ArgumentException>(() => new FdmBlackScholesMesher(11, one, 1.0, 100.0, fdmQuantoHelper: foreignOn360));

        // and each one dated otherwise
        var dividendLater = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today + 2, 0.02, A365), r365, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        var volatilityLater = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), r365, new BlackConstantVol(Today + 2, new NullCalendar(), 0.25, A365));
        var foreignLater = new FdmQuantoHelper(
            r365, new FlatForward(Today + 2, 0.03, A365), new BlackConstantVol(Today, new NullCalendar(), 0.1, A365), -0.5, 1.0);
        Assert.Throws<ArgumentException>(() => new FdmBlackScholesOp(mesher, dividendLater, 100.0));
        Assert.Throws<ArgumentException>(() => new FdmBlackScholesOp(mesher, volatilityLater, 100.0));
        Assert.Throws<ArgumentException>(() => new FdmBlackScholesOp(mesher, one, 100.0, quantoHelper: foreignLater));
        Assert.Throws<ArgumentException>(() => new FdmBlackScholesMesher(11, dividendLater, 1.0, 100.0));
        Assert.Throws<ArgumentException>(() => new FdmBlackScholesMesher(11, volatilityLater, 1.0, 100.0));

        // one day counter and one reference date throughout is QuantLib's path
        _ = new FdmBlackScholesOp(mesher, one, 100.0, localVol: true);
        _ = new FdmBlackScholesMesher(11, one, 1.0, 100.0);
    }

    [Fact]
    public void AZeroDividendCurveOnAnotherDayCounterIsTaken()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        BlackScholesProcess process = BlackScholesProcess.Create(
            100.0, new FlatForward(Today, 0.05, A360), new BlackConstantVol(Today, null, 0.25, A360), context);
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new AmericanExercise(Today, Today + 365));

        // QuantLib reads 8.008860323024129, its dividend curve at zero on Act/365
        Assert.Equal(8.008860323024129, FdBlackScholesVanillaEngine.Calculate(put, context, process).NPV!.Value, 1e-12);

        // a dividend curve away from zero on another day counter is refused still
        var dividends = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.01, A365), new FlatForward(Today, 0.05, A360), new BlackConstantVol(Today, null, 0.25, A360));
        Assert.Throws<ArgumentException>(() => FdBlackScholesVanillaEngine.Calculate(put, context, dividends));
    }

    private static GeneralizedBlackScholesProcess Process(YieldTermStructure riskFree) =>
        new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), riskFree, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
}
