// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The finite-difference Black-Scholes engine on structures that measure time differently. Its times are the risk-free
/// curve's, measured from its reference date in its day counter (<c>blackscholesprocess.cpp:149-152</c>), and QuantLib
/// reads the dividend curve, the volatility, the local volatility's curves, the quanto helper's structures and the
/// escrowed dividends' curve at those times (<c>fdmblackscholesop.cpp:51-66</c>,
/// <c>fdmblackscholesmesher.cpp:63-103</c>, <c>fdmquantohelper.cpp:43-55</c>, <c>localvolsurface.cpp:82-118</c>,
/// <c>escroweddividendadjustment.cpp</c>), each of which measures time from its own reference date in its own day
/// counter: a one-year put on a risk-free curve on Act/360, with the discount of one on Act/365 at every date, prices
/// 0.93% above the same market restated on one day counter, and one on a risk-free curve settling two business days
/// after its other structures 0.74% below QuantLib's analytic engine, which counts each structure from its own
/// reference date. nanoql's grid runs on the risk-free curve's day counter from the earliest reference date of the
/// structures it reads, and reads each structure at the instants its times denote, on its own clock, accruing nothing
/// before its own reference date, as the analytic engine counts it: the days before a later-dated structure's date are
/// calendar time on the grid. So the engine prices the market restated from the earliest date, each structure accruing
/// nothing before its own, and QuantLib's own engine on that market is the check, price and theta: exact between
/// Actual/N day counters, and on others, read through the dates and linear within a day, its analytic engine within the
/// grid's error. The theta is the change over the grid's first calendar day, QuantLib's engine's on the restated
/// market: where a structure is dated after the evaluation date, that day accrues none of its dividends or variance, so
/// the theta differs from the formula theta of the analytic and binomial engines, even in sign. The clock is public
/// (<c>FdmBlackScholesOp.Clock</c>), and a solver assembled from the engine's types on it is the engine. A market whose
/// structures are dated with the risk-free curve, on its day counter, is read as QuantLib reads it, and keeps
/// QuantLib's bits.
/// </summary>
public class FdmMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly DayCounter A360 = new Actual360();
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void ARiskFreeCurveOnAnotherDayCounterPricesTheRestatedMarket()
    {
        var mixed = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05 * 360.0 / 365.0, A360), Vol(0.25, A365));

        // QuantLib's engine on the market restated on Act/360, the same discounts and variances at every date:
        // 8.226975823291562 and 8.564052404158435; on the mixed market it reads 8.303614294485753 and 8.638625987525602
        Assert.Equal(8.226975823291562, Fd(mixed, European()), 1e-12);
        Assert.Equal(8.564052404158435, Fd(mixed, American()), 1e-12);

        // QuantLib's analytic engine, 8.226837047453996, within the grid's error
        Assert.Equal(0.0, (Fd(mixed, European()) / 8.226837047453996) - 1.0, 2e-5);
    }

    [Fact]
    public void ARiskFreeCurveSettlingLaterIsPricedFromTheEarliestDate()
    {
        YieldTermStructure settling = FlatForward.Create(2, new TARGET(), 0.05, A365, Context);
        Assert.Equal(Today + 4, settling.ReferenceDate);

        // a dividend curve dated today with a term structure, log-linear in its discounts, and the volatility from
        // today: QuantLib's engine on the market restated from today, the risk-free curve discounting nothing before
        // 19 October, 9.069915216399108 and 9.118356664168223; its analytic engine 9.0697578690748, within the grid's
        // error; on the market as given it reads 8.99046049372568 and 9.037857235925607
        var dividends = new DiscountCurve(
            [Today, Today + 91, Today + 182, Today + 365, Today + 730],
            [1.0, 0.9975099546340253, 0.9900769587737215, 0.9607367945979871, 0.9230657660548007],
            A365);
        var mixed = new BlackScholesMertonProcess(100.0, dividends, settling, Vol(0.25, A365));
        Assert.Equal(9.069915216399108, Fd(mixed, European()), 1e-12);
        Assert.Equal(9.118356664168223, Fd(mixed, American()), 1e-12);
        Assert.Equal(0.0, (Fd(mixed, European()) / 9.0697578690748) - 1.0, 2e-5);

        // flat structures: QuantLib's engine on the restated market 8.25317133964261 and 8.591736231082914, its
        // analytic engine 8.253032061229066; on the market as given, 8.19233684260132 and 8.524824836750945
        var flat = new BlackScholesMertonProcess(100.0, new FlatForward(Today, 0.02, A365), settling, Vol(0.25, A365));
        Assert.Equal(8.25317133964261, Fd(flat, European()), 1e-12);
        Assert.Equal(8.591736231082914, Fd(flat, American()), 1e-12);
        Assert.Equal(0.0, (Fd(flat, European()) / 8.253032061229066) - 1.0, 2e-5);
    }

    [Fact]
    public void EveryMaturityAndGridRunsFromTheEarliestDate()
    {
        // European puts at 100 on the flat market with the risk-free curve settling T+2, and with the dividend curve
        // and the volatility dated four days after the risk-free curve: QuantLib's engine (400 x 400) on the markets
        // restated from today, its NPV and theta, the theta a calendar day's (the analytic engine's formula has no days
        // without accrual); the analytic engine within the grid's error on 400 x 400 and on the default grid
        YieldTermStructure settling = FlatForward.Create(2, new TARGET(), 0.05, A365, Context);
        var early = new BlackScholesMertonProcess(100.0, new FlatForward(Today, 0.02, A365), settling, Vol(0.25, A365));
        Date later = Today + 4;
        var late = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(later, 0.02, A365),
            new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(later, new NullCalendar(), 0.25, A365));
        (GeneralizedBlackScholesProcess Process, int Days, double Npv, double Theta, double Analytic)[] cases =
        [
            (early, 7, 1.3792344269608297, -38.34791693378193, 1.3792011546154954),
            (early, 14, 1.9211016235577243, -26.86258270892491, 1.9210564393409622),
            (early, 91, 4.598887003295121, -10.797396154443657, 4.598791746183207),
            (late, 7, 0.864545678487887, 2.4565411411868805, 0.8645239875432063),
            (late, 14, 1.5812156636969885, 2.4740028094672937, 1.5811780500724646),
            (late, 91, 4.452573436502405, 2.4663197266772805, 4.452480951538876),
        ];
        foreach ((GeneralizedBlackScholesProcess process, int days, double npv, double theta, double analytic) in cases)
        {
            var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + days));
            OneAssetOptionResults fd = new MakeFdBlackScholesVanillaEngine(process).WithTGrid(400).WithXGrid(400).ToEngine()(put, Context);
            Assert.Equal(npv, fd.NPV!.Value, 1e-11);
            Assert.Equal(theta, fd.Theta!.Value, 1e-9);
            Assert.Equal(0.0, (fd.NPV!.Value / analytic) - 1.0, 3e-5);
            double coarse = FdBlackScholesVanillaEngine.Calculate(put, Context, process).NPV!.Value;
            Assert.Equal(0.0, (coarse / analytic) - 1.0, 5e-4);
        }
    }

    [Fact]
    public void ASolverAssembledOnTheEnginesClockIsTheEngine()
    {
        // QuantLib's other finite-difference engines on these types take their times from the process; one assembled on
        // the engine's clock, its maturity and the conditions' date and day counter, is the engine: a 14-day put with
        // the risk-free curve settling T+2, QuantLib's engine on the market restated from today 1.9211016235577243
        YieldTermStructure settling = FlatForward.Create(2, new TARGET(), 0.05, A365, Context);
        var process = new BlackScholesMertonProcess(100.0, new FlatForward(Today, 0.02, A365), settling, Vol(0.25, A365));
        var payoff = new PlainVanillaPayoff(OptionType.Put, 100.0);
        var put = new VanillaOption(payoff, new EuropeanExercise(Today + 14));
        TermStructure clock = FdmBlackScholesOp.Clock(process);
        Assert.Equal(Today, clock.ReferenceDate);

        double maturity = clock.TimeFromReference(Today + 14);
        var mesher = new FdmMesherComposite(new FdmBlackScholesMesher(400, process, maturity, 100.0, cPoint: (100.0, 0.1)));
        var calculator = new FdmLogInnerValue(payoff, mesher, 0);
        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            [], put.Exercise, mesher, calculator, clock.ReferenceDate, clock.DayCounter, Context);
        var solver = new FdmBlackScholesSolver(process, 100.0, new FdmSolverDesc(mesher, [], conditions, calculator, maturity, 400, 0));

        double engine = new MakeFdBlackScholesVanillaEngine(process).WithTGrid(400).WithXGrid(400).ToEngine()(put, Context).NPV!.Value;
        Assert.Equal(engine, solver.ValueAt(100.0));
        Assert.Equal(1.9211016235577243, solver.ValueAt(100.0), 1e-11);
    }

    [Fact]
    public void EachStructureTheEngineReadsIsReadAtItsInstants()
    {
        YieldTermStructure riskFree = new FlatForward(Today, 0.05, A365);
        GeneralizedBlackScholesProcess plain = new BlackScholesMertonProcess(100.0, new FlatForward(Today, 0.02, A365), riskFree, Vol(0.25, A365));

        // the quanto helper's foreign curve on Act/360: QuantLib's engine on it restated on Act/365, 8.526564135734667
        // and 8.800846862538465; on the mixed helper, 8.543161422060974 and 8.814078743110262
        var foreign360 = new FdmQuantoHelper(riskFree, new FlatForward(Today, 0.03 * 360.0 / 365.0, A360), Vol(0.1, A365), -0.5, 1.0);
        Assert.Equal(8.526564135734667, Fd(plain, European(), foreign360), 1e-12);
        Assert.Equal(8.800846862538465, Fd(plain, American(), foreign360), 1e-12);

        // the local volatility of a variance surface on Act/360: QuantLib's engine (200 x 200) on the surface restated
        // on Act/365, the same variances at every date, 7.38695933906903 and 7.67040457723432; on the mixed surface,
        // 7.319158202229472 and 7.602373027364987. Dupire's time derivative steps 1e-4 on each surface's own clock, so
        // the two differentiate over slightly different instants: agreement to 1e-6, far inside the grid's error
        var local = new BlackScholesMertonProcess(100.0, new FlatForward(Today, 0.02, A365), riskFree, Surface(A360, 1.0));
        Assert.Equal(0.0, (LocalVolFd(local, European()) / 7.38695933906903) - 1.0, 1e-6);
        Assert.Equal(0.0, (LocalVolFd(local, American()) / 7.67040457723432) - 1.0, 1e-6);

        // the escrowed dividends' curve on Act/360: QuantLib's engine on it restated on Act/365, 10.636574340932707; on
        // the mixed curve, 10.625498199458868
        var escrowed = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02 * 360.0 / 365.0, A360), riskFree, Vol(0.25, A365));
        double price = new MakeFdBlackScholesVanillaEngine(escrowed)
            .WithTGrid(400)
            .WithXGrid(400)
            .WithCashDividends([Today + 91, Today + 273], [2.0, 3.0])
            .WithCashDividendModel(FdBlackScholesVanillaEngineCashDividendModel.Escrowed)
            .ToEngine()(American(), Context).NPV!.Value;
        Assert.Equal(10.636574340932707, price, 1e-12);
    }

    [Fact]
    public void OtherDayCountersAgreeWithTheAnalyticEngine()
    {
        // a 200-day put; QuantLib's engine reads 6.454682301331813 with the volatility, and 6.9286813188392715 with
        // the dividend curve, on any of these day counters, as if they were the risk-free curve's
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + 200));
        YieldTermStructure riskFree = new FlatForward(Today, 0.05, A365);
        foreach (DayCounter dc in new DayCounter[] { new Thirty360(Thirty360Convention.BondBasis), new ActualActual(ActualActualConvention.ISDA) })
        {
            var vol = new BlackScholesMertonProcess(100.0, new FlatForward(Today, 0.02, A365), riskFree, Vol(0.25, dc));
            var dividend = new BlackScholesMertonProcess(100.0, new FlatForward(Today, 0.04, dc), riskFree, Vol(0.25, A365));
            foreach (GeneralizedBlackScholesProcess process in new[] { vol, dividend })
            {
                double analytic = AnalyticEuropeanEngine.Calculate(put, Context, process).NPV!.Value;
                double fd = FdBlackScholesVanillaEngine.Calculate(put, Context, process, tGrid: 400, xGrid: 400).NPV!.Value;
                Assert.True(Math.Abs((fd / analytic) - 1.0) < 2e-5, $"{dc.Name}: {fd} against {analytic}");
            }
        }
    }

    [Fact]
    public void AStructureDatedAfterTheRiskFreeCurveAccruesNothingBeforeItsDate()
    {
        YieldTermStructure riskFree = new FlatForward(Today, 0.05, A365);
        Date later = Today + 4;

        // flat structures dated four days later: QuantLib's engine on the market restated from today, the dividend
        // curve discounting and the volatility accruing nothing before their date, 8.166187663409342 and
        // 8.497194253431704; its analytic engine 8.166049638607317, within the grid's error; on the market as given it
        // reads 8.22697582310168 and 8.56405240397323
        var flat = new BlackScholesMertonProcess(
            100.0, new FlatForward(later, 0.02, A365), riskFree, new BlackConstantVol(later, new NullCalendar(), 0.25, A365));
        Assert.Equal(8.166187663409342, Fd(flat, European()), 1e-12);
        Assert.Equal(8.497194253431704, Fd(flat, American()), 1e-12);
        Assert.Equal(0.0, (Fd(flat, European()) / 8.166049638607317) - 1.0, 2e-5);

        // a dividend curve with a term structure dated four days later: QuantLib's engine on it restated from today,
        // 9.014624798343355 and 9.063485451287942; its analytic engine 9.0144684476227; on the curve as given it reads
        // it four days late, 9.04205338006562 and 9.090207762957172
        var dividends = new DiscountCurve(
            [later, later + 91, later + 182, later + 365, later + 730],
            [1.0, 0.9975099546340253, 0.9900769587737215, 0.9607367945979871, 0.9230657660548007],
            A365);
        var curve = new BlackScholesMertonProcess(100.0, dividends, riskFree, Vol(0.25, A365));
        Assert.Equal(9.014624798343355, Fd(curve, European()), 1e-12);
        Assert.Equal(9.063485451287942, Fd(curve, American()), 1e-12);
        Assert.Equal(0.0, (Fd(curve, European()) / 9.0144684476227) - 1.0, 2e-5);
    }

    [Fact]
    public void AZeroDividendCurveOnAnotherDayCounterIsQuantLibs()
    {
        BlackScholesProcess process = BlackScholesProcess.Create(100.0, new FlatForward(Today, 0.05, A360), Vol(0.25, A360), Context);

        // QuantLib reads 8.008860323024129 on the default grid, its dividend curve at zero on Act/365
        Assert.Equal(8.008860323024129, FdBlackScholesVanillaEngine.Calculate(American(), Context, process).NPV!.Value, 1e-12);
    }

    private static VanillaOption European() =>
        new(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + 365));

    private static VanillaOption American() =>
        new(new PlainVanillaPayoff(OptionType.Put, 100.0), new AmericanExercise(Today, Today + 365));

    private static BlackConstantVol Vol(double v, DayCounter dc) => new(Today, new NullCalendar(), v, dc);

    private static double Fd(GeneralizedBlackScholesProcess process, VanillaOption option, FdmQuantoHelper? quanto = null) =>
        new MakeFdBlackScholesVanillaEngine(process).WithQuantoHelper(quanto).WithTGrid(400).WithXGrid(400)
            .ToEngine()(option, Context).NPV!.Value;

    private static double LocalVolFd(GeneralizedBlackScholesProcess process, VanillaOption option) =>
        new MakeFdBlackScholesVanillaEngine(process).WithTGrid(200).WithXGrid(200).WithLocalVol(true)
            .ToEngine()(option, Context).NPV!.Value;

    private static BlackVarianceSurface Surface(DayCounter dc, double scale)
    {
        double[,] vols =
        {
            { 0.32, 0.30, 0.28, 0.27 }, { 0.26, 0.25, 0.24, 0.235 }, { 0.22, 0.215, 0.21, 0.205 },
            { 0.21, 0.205, 0.20, 0.198 }, { 0.23, 0.22, 0.215, 0.21 },
        };
        var matrix = new Matrix(5, 4);
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                matrix[i, j] = vols[i, j] * scale;
            }
        }

        return new BlackVarianceSurface(
            Today, new TARGET(), [Today + 91, Today + 182, Today + 365, Today + 730], [70.0, 85.0, 100.0, 115.0, 130.0], matrix, dc);
    }
}
