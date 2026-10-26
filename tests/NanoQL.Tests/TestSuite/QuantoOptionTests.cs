// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008 Paul Farrington
// SPDX-FileCopyrightText: Copyright (C) 2014 Thema Consulting SA
// SPDX-FileCopyrightText: Copyright (C) 2019 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/quantooption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testBarrierValues (S-bar's quanto barrier option); testDoubleBarrierValues (S-xeq's quanto double-barrier option)

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class QuantoOptionTests
{
    [Fact]
    public void TestValues()
    {
        // Testing quanto option values...

        /* The data below are from
           from "Option pricing formulas", E.G. Haug, McGraw-Hill 1998
        */
        QuantoOptionData[] values =
        [
            // type, strike,  spot,  div, rate,   t, vol, fx risk-free rate, fx volatility, correlation,     result, tol
            // "Option pricing formulas", pag 105-106
            new(OptionType.Call, 105.0, 100.0, 0.04, 0.08, 0.5, 0.2, 0.05, 0.10, 0.3, 5.3280 / 1.5, 1.0e-4),

            // "Option pricing formulas", VBA code
            new(OptionType.Put, 105.0, 100.0, 0.04, 0.08, 0.5, 0.2, 0.05, 0.10, 0.3, 8.1636, 1.0e-4),
        ];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (QuantoOptionData value in values)
        {
            var payoff = new PlainVanillaPayoff(value.Type, value.Strike);
            Date exDate = today + TimeToDays(value.T);
            var exercise = new EuropeanExercise(exDate);

            Market market = Market.On(today, dc, value.S, value.Q, value.R, value.V, value.Fxr, value.Fxv);

            var option = new QuantoVanillaOption(payoff, exercise);

            Real calculated = QuantoEngine.Calculate(
                option, context, market.Process, market.ForeignTS, market.FxVolTS, value.Corr,
                AnalyticEuropeanEngine.Calculate).NPV!.Value;
            Real error = Math.Abs(calculated - value.Result);
            Real tolerance = 1e-4;
            if (error > tolerance)
            {
                QuantoReportFailure(
                    "value", payoff, exercise, value.S, value.Q, value.R, today, value.V, value.Fxr, value.Fxv, value.Corr,
                    value.Result, calculated, error, tolerance);
            }
        }
    }

    [Fact]
    public void TestGreeks()
    {
        // Testing quanto option greeks...
        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] strikes = [50.0, 99.5, 100.0, 100.5, 150.0];
        Integer[] lengths = [2];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        foreach (OptionType type in types)
        {
            foreach (Real strike in strikes)
            {
                foreach (Integer length in lengths)
                {
                    Date exDate = today + new Period(length, TimeUnit.Years);
                    var exercise = new EuropeanExercise(exDate);

                    var payoff = new PlainVanillaPayoff(type, strike);

                    var option = new QuantoVanillaOption(payoff, exercise);
                    TestQuantoGreeks(
                        today,
                        dc,
                        (market, correlation, context) => QuantoEngine.Calculate(
                            option, context, market.Process, market.ForeignTS, market.FxVolTS, correlation,
                            AnalyticEuropeanEngine.Calculate),
                        (greek, u, q, r, v, fxr, fxv, corr, expct, calcl, error, tol) => QuantoReportFailure(
                            greek, payoff, exercise, u, q, r, today, v, fxr, fxv, corr, expct, calcl, error, tol));
                }
            }
        }
    }

    [Fact]
    public void TestForwardValues()
    {
        // Testing quanto-forward option values...
        QuantoForwardOptionData[] values =
        [
            // type, moneyness,  spot,  div, risk-free rate, reset, maturity,  vol, fx risk-free rate, fx vol, corr,     result, tol
            // reset=0.0, quanto (not-forward) options
            new(OptionType.Call, 1.05, 100.0, 0.04, 0.08, 0.00, 0.5, 0.20, 0.05, 0.10, 0.3, 5.3280 / 1.5, 1.0e-4),
            new(OptionType.Put, 1.05, 100.0, 0.04, 0.08, 0.00, 0.5, 0.20, 0.05, 0.10, 0.3, 8.1636, 1.0e-4),

            // reset!=0.0, quanto-forward options (cursory checked against FinCAD 7)
            new(OptionType.Call, 1.05, 100.0, 0.04, 0.08, 0.25, 0.5, 0.20, 0.05, 0.10, 0.3, 2.0171, 1.0e-4),
            new(OptionType.Put, 1.05, 100.0, 0.04, 0.08, 0.25, 0.5, 0.20, 0.05, 0.10, 0.3, 6.7296, 1.0e-4),
        ];

        CheckForwardValues(values, ForwardVanillaEngine.Calculate);
    }

    [Fact]
    public void TestForwardGreeks()
    {
        // Testing quanto-forward option greeks...
        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] moneyness = [0.9, 1.0, 1.1];
        Integer[] lengths = [2];
        Integer[] startMonths = [6, 9];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        foreach (OptionType type in types)
        {
            foreach (Real moneynes in moneyness)
            {
                foreach (Integer length in lengths)
                {
                    foreach (Integer startMonth in startMonths)
                    {
                        Date exDate = today + new Period(length, TimeUnit.Years);
                        var exercise = new EuropeanExercise(exDate);

                        Date reset = today + new Period(startMonth, TimeUnit.Months);

                        var payoff = new PlainVanillaPayoff(type, 0.0);

                        var option = new QuantoForwardVanillaOption(moneynes, reset, payoff, exercise);
                        TestQuantoGreeks(
                            today,
                            dc,
                            (market, correlation, context) => QuantoEngine.Calculate(
                                option, context, market.Process, market.ForeignTS, market.FxVolTS, correlation,
                                (o, c, p) => ForwardVanillaEngine.Calculate(o, c, p, AnalyticEuropeanEngine.Calculate)),
                            (greek, u, q, r, v, fxr, fxv, corr, expct, calcl, error, tol) => QuantoForwardReportFailure(
                                greek, payoff, moneynes, exercise, u, q, r, today, reset, v, fxr, fxv, corr, expct, calcl,
                                error, tol));
                    }
                }
            }
        }
    }

    [Fact]
    public void TestForwardPerformanceValues()
    {
        // Testing quanto-forward-performance option values...
        QuantoForwardOptionData[] values =
        [
            // type, moneyness,  spot,  div, risk-free rate, reset, maturity,  vol, fx risk-free rate, fx vol, corr,     result, tol
            // reset=0.0, quanto-(not-forward)-performance options
            // exactly one hundredth of the non-performance version
            new(OptionType.Call, 1.05, 100.0, 0.04, 0.08, 0.00, 0.5, 0.20, 0.05, 0.10, 0.3, 5.3280 / 150, 1.0e-4),
            new(OptionType.Put, 1.05, 100.0, 0.04, 0.08, 0.00, 0.5, 0.20, 0.05, 0.10, 0.3, 0.0816, 1.0e-4),

            // reset!=0.0, quanto-forward-performance options (roughly one hundredth of the non-performance version)
            new(OptionType.Call, 1.05, 100.0, 0.04, 0.08, 0.25, 0.5, 0.20, 0.05, 0.10, 0.3, 0.0201, 1.0e-4),
            new(OptionType.Put, 1.05, 100.0, 0.04, 0.08, 0.25, 0.5, 0.20, 0.05, 0.10, 0.3, 0.0672, 1.0e-4),
        ];

        CheckForwardValues(values, ForwardPerformanceVanillaEngine.Calculate);
    }

    [Fact]
    public void TestFDMQuantoHelper()
    {
        // Testing FDM quanto helper...

        DayCounter dc = new Actual360();
        var today = new Date(22, Month.April, 2019);

        Real s = 100;
        Rate domesticR = 0.1;
        Rate foreignR = 0.2;
        Rate q = 0.3;
        Volatility vol = 0.3;
        Volatility fxVol = 0.2;

        Real exchRateATMlevel = 1.0;
        Real equityFxCorrelation = -0.75;

        YieldTermStructure domesticTS = new FlatForward(today, domesticR, dc);

        YieldTermStructure divTS = new FlatForward(today, q, dc);

        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), vol, dc);

        var bsmProcess = new BlackScholesMertonProcess(s, divTS, domesticTS, volTS);

        YieldTermStructure foreignTS = new FlatForward(today, foreignR, dc);

        BlackVolTermStructure fxVolTS = new BlackConstantVol(today, new NullCalendar(), fxVol, dc);

        var fdmQuantoHelper =
            new FdmQuantoHelper(domesticTS, foreignTS, fxVolTS, equityFxCorrelation, exchRateATMlevel);

        Real calculatedQuantoAdj = fdmQuantoHelper.QuantoAdjustment(vol, 0.0, 1.0);

        Real expectedQuantoAdj = domesticR - foreignR + (equityFxCorrelation * vol * fxVol);

        Real tol = 1e-10;
        if (Math.Abs(calculatedQuantoAdj - expectedQuantoAdj) > tol)
        {
            Assert.Fail(
                $"failed to reproduce quanto drift rate\n    calculated: {calculatedQuantoAdj}\n    expected:   "
                + $"{expectedQuantoAdj}");
        }

        Date maturityDate = today + new Period(6, TimeUnit.Months);
        double maturityTime = dc.YearFraction(today, maturityDate);

        Real eps = 0.0002;
        Real scalingFactor = 1.25;

        var mesher = new FdmBlackScholesMesher(
            3, bsmProcess, maturityTime, s, null, null, eps, scalingFactor, (null, null), [], fdmQuantoHelper);

        Real normInvEps = new InverseCumulativeNormal().Evaluate(1 - eps);
        Real sigmaSqrtT = vol * Math.Sqrt(maturityTime);

        Real qQuanto = q + expectedQuantoAdj;
        Real expectedDriftRate = domesticR - qQuanto;

        Real logFwd = Math.Log(s) + (expectedDriftRate * maturityTime);
        Real xMin = logFwd - (sigmaSqrtT * normInvEps * scalingFactor);
        Real xMax = Math.Log(s) + (sigmaSqrtT * normInvEps * scalingFactor);

        System.Collections.Immutable.ImmutableArray<double> loc = mesher.Locations();

        if (Math.Abs(loc[0] - xMin) > tol || Math.Abs(loc[^1] - xMax) > tol)
        {
            Assert.Fail(
                $"failed to reproduce FDM grid boundaries\n    calculated: ({loc[0]}, {loc[^1]})\n    expected:   "
                + $"({xMin}, {xMax})");
        }
    }

    [Fact]
    public void TestPDEOptionValues()
    {
        // Testing quanto-option values with PDEs...
        DayCounter dc = new Actual360();
        var today = new Date(21, Month.April, 2019);
        var context = new EvaluationContext { EvaluationDate = today };

        QuantoOptionData[] values =
        [
            // type,    strike,  spot,   div, domestic rate,  t,   vol, foreign rate, fx vol, correlation, result,     tol
            new(OptionType.Call, 105.0, 100.0, 0.04, 0.08, 0.5, 0.2, 0.05, 0.10, 0.3, double.NaN, double.NaN),
            new(OptionType.Call, 100.0, 100.0, 0.16, 0.08, 0.25, 0.15, 0.05, 0.20, -0.3, double.NaN, double.NaN),
            new(OptionType.Call, 105.0, 100.0, 0.04, 0.08, 0.5, 0.2, 0.05, 0.10, 0.3, double.NaN, double.NaN),
            new(OptionType.Put, 105.0, 100.0, 0.04, 0.08, 0.5, 0.2, 0.05, 0.10, 0.3, double.NaN, double.NaN),
            new(OptionType.Call, 0.0, 100.0, 0.04, 0.08, 0.3, 0.3, 0.05, 0.10, 0.75, double.NaN, double.NaN),
        ];

        foreach (QuantoOptionData value in values)
        {
            var tolerance = new Dictionary<string, Real>
            {
                ["npv"] = 2e-4,
                ["delta"] = 1e-4,
                ["gamma"] = 1e-4,
                ["theta"] = 1e-4,
            };

            Real strike = value.Strike;

            YieldTermStructure domesticTS = new FlatForward(today, value.R, dc);

            YieldTermStructure divTS = new FlatForward(today, value.Q, dc);

            BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), value.V, dc);

            var bsmProcess = new BlackScholesMertonProcess(value.S, divTS, domesticTS, volTS);

            YieldTermStructure foreignTS = new FlatForward(today, value.Fxr, dc);

            BlackVolTermStructure fxVolTS = new BlackConstantVol(today, new NullCalendar(), value.Fxv, dc);

            const Real exchRateATMlevel = 1.0;
            Real equityFxCorrelation = value.Corr;

            var quantoHelper = new FdmQuantoHelper(domesticTS, foreignTS, fxVolTS, equityFxCorrelation, exchRateATMlevel);

            var payoff = new PlainVanillaPayoff(value.Type, strike);
            Date exDate = today + TimeToDays(value.T);
            var exercise = new EuropeanExercise(exDate);

            var option = new VanillaOption(payoff, exercise);

            OneAssetOptionResults pde = FdBlackScholesVanillaEngine.Calculate(
                option, context, bsmProcess, [], quantoHelper, (Size)(value.T * 200), 500, 1);

            var calculated = new Dictionary<string, Real>
            {
                ["npv"] = pde.NPV!.Value,
                ["delta"] = pde.Delta!.Value,
                ["gamma"] = pde.Delta!.Value,
                ["theta"] = pde.Delta!.Value,
            };

            // QuantLib's QuantoEngine<VanillaOption, AnalyticEuropeanEngine> on the vanilla option itself, the generic
            // core with the option its own terms
            OneAssetOptionResults analytic = QuantoEngine.Calculate(
                option, option, context, bsmProcess, foreignTS, fxVolTS, equityFxCorrelation, AnalyticEuropeanEngine.Calculate);

            var expected = new Dictionary<string, Real>
            {
                ["npv"] = analytic.NPV!.Value,
                ["delta"] = analytic.Delta!.Value,
                ["gamma"] = analytic.Delta!.Value,
                ["theta"] = analytic.Delta!.Value,
            };

            foreach (string greek in calculated.Keys)
            {
                Real expct = expected[greek];
                Real calcl = calculated[greek];
                Real error = Math.Abs(expct - calcl);
                Real tol = tolerance[greek];

                if (error > tol)
                {
                    QuantoReportFailure(
                        greek, payoff, exercise, value.S, value.Q, value.R, today, value.V, value.Fxr, value.Fxv, value.Corr,
                        expct, calcl, error, tol);
                }
            }
        }
    }

    [Fact]
    public void TestAmericanQuantoOption()
    {
        // Testing American quanto-option values with PDEs...
        DayCounter dc = new Actual365Fixed();
        var today = new Date(21, Month.April, 2019);
        var context = new EvaluationContext { EvaluationDate = today };
        Date maturity = today + new Period(9, TimeUnit.Months);

        const Real s = 100;
        const Rate domesticR = 0.025;
        const Rate foreignR = 0.075;
        const Rate q = 0.03;
        const Volatility vol = 0.3;
        const Volatility fxVol = 0.15;

        const Real exchRateATMlevel = 1.0;
        const Real equityFxCorrelation = -0.75;

        YieldTermStructure domesticTS = new FlatForward(today, domesticR, dc);

        YieldTermStructure divTS = new FlatForward(today, q, dc);

        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), vol, dc);

        var bsmProcess = new BlackScholesMertonProcess(s, divTS, domesticTS, volTS);

        YieldTermStructure foreignTS = new FlatForward(today, foreignR, dc);

        BlackVolTermStructure fxVolTS = new BlackConstantVol(today, new NullCalendar(), fxVol, dc);

        var quantoHelper = new FdmQuantoHelper(domesticTS, foreignTS, fxVolTS, equityFxCorrelation, exchRateATMlevel);

        const Real strike = 105.0;

        Date[] dividendDates = [today + new Period(6, TimeUnit.Months)];
        Real[] dividendAmounts = [8.0];
        System.Collections.Immutable.ImmutableArray<NanoQL.Coupons.Dividend> dividends =
            NanoQL.Coupons.DividendFunctions.DividendVector(dividendDates, dividendAmounts);

        var option = new NanoQL.Instruments.VanillaOption(
            new NanoQL.Instruments.PlainVanillaPayoff(OptionType.Call, strike), new AmericanExercise(maturity));

        const Real tol = 1e-4;
        const Real expected = 8.90611734;
        Real bsCalculated = NanoQL.PricingEngines.FdBlackScholesVanillaEngine.Calculate(
            option, context, bsmProcess, dividends, quantoHelper, 100, 400, 1).NPV!.Value;

        Assert.False(
            Math.Abs(expected - bsCalculated) > tol,
            $"failed to reproduce American quanto option prices with the Black-Scholes-Merton model\n    calculated: {bsCalculated}\n    expected:   {expected}");

        Real localVolCalculated = NanoQL.PricingEngines.FdBlackScholesVanillaEngine.Calculate(
            option, context, bsmProcess, dividends, quantoHelper, 100, 400, 1).NPV!.Value;
        Assert.False(
            Math.Abs(expected - localVolCalculated) > tol,
            $"failed to reproduce American quanto option prices with the Local Volatility model\n    calculated: {localVolCalculated}\n    expected:   {expected}");

        const Real tolBetweenBSandLocalVol = 1e-6;
        Assert.False(
            Math.Abs(bsCalculated - localVolCalculated) > tolBetweenBSandLocalVol,
            $"difference between American quanto option prices for Local Volatility and Black-Scholes model\n    calculated Local Vol    : {localVolCalculated}\n    calculated Black-Scholes: {bsCalculated}");

        var divOption = new NanoQL.Instruments.VanillaOption(
            new NanoQL.Instruments.PlainVanillaPayoff(OptionType.Call, strike), new AmericanExercise(maturity));

        const Real v0 = vol * vol;
        const Real kappa = 1.0;
        const Real theta = v0;
        const Real sigma = 1e-4;
        const Real rho = 0.0;

        var hestonModel = new NanoQL.Models.HestonModel(
            new HestonProcess(domesticTS, divTS, s, v0, kappa, theta, sigma, rho));

        Real hestonCalculated = NanoQL.PricingEngines.FdHestonVanillaEngine.Calculate(
            divOption, context, hestonModel, dividends, quantoHelper, 100, 400, 3, 1).NPV!.Value;

        Assert.False(
            Math.Abs(expected - hestonCalculated) > tol,
            $"failed to reproduce American quanto option prices with the Heston model\n    calculated: {hestonCalculated}\n    expected:   {expected}");

        LocalVolTermStructure localConstVol = new LocalConstantVol(today, 2.0, dc);

        var hestonModel05 = new NanoQL.Models.HestonModel(
            new HestonProcess(domesticTS, divTS, s, 0.25 * v0, kappa, 0.25 * theta, sigma, rho));

        Real hestoSlvCalculated = NanoQL.PricingEngines.FdHestonVanillaEngine.Calculate(
            divOption, context, hestonModel05, dividends, quantoHelper, 100, 400, 3, 1, FdmSchemeDesc.Hundsdorfer(), localConstVol)
            .NPV!.Value;

        Assert.False(
            Math.Abs(expected - hestoSlvCalculated) > tol,
            $"failed to reproduce American quanto option prices with the Heston Local Volatility model\n    calculated: {hestoSlvCalculated}\n    expected:   {expected}");
    }

    // testForwardValues' and testForwardPerformanceValues' body, the forward engine template's.
    private static void CheckForwardValues(
        QuantoForwardOptionData[] values,
        Func<ForwardVanillaOption, EvaluationContext, GeneralizedBlackScholesProcess,
            Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults>,
            OneAssetOptionResults> forwardEngine)
    {
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (QuantoForwardOptionData value in values)
        {
            var payoff = new PlainVanillaPayoff(value.Type, 0.0);
            Date exDate = today + TimeToDays(value.T);
            var exercise = new EuropeanExercise(exDate);
            Date reset = today + TimeToDays(value.Start);

            Market market = Market.On(today, dc, value.S, value.Q, value.R, value.V, value.Fxr, value.Fxv);

            var option = new QuantoForwardVanillaOption(value.Moneyness, reset, payoff, exercise);

            Real calculated = QuantoEngine.Calculate(
                option, context, market.Process, market.ForeignTS, market.FxVolTS, value.Corr,
                (o, c, p) => forwardEngine(o, c, p, AnalyticEuropeanEngine.Calculate)).NPV!.Value;
            Real error = Math.Abs(calculated - value.Result);
            Real tolerance = 1e-4;
            if (error > tolerance)
            {
                QuantoForwardReportFailure(
                    "value", payoff, value.Moneyness, exercise, value.S, value.Q, value.R, today, reset, value.V, value.Fxr,
                    value.Fxv, value.Corr, value.Result, calculated, error, tolerance);
            }
        }
    }

    // testGreeks' and testForwardGreeks' loops over the market, an engine pricing the case's option on a market at a
    // correlation under a context; QuantLib's quotes set to each value.
    private static void TestQuantoGreeks(
        Date today,
        DayCounter dc,
        Func<Market, Real, EvaluationContext, QuantoOptionResults> price,
        Action<string, Real, Rate, Rate, Volatility, Rate, Volatility, Real, Real, Real, Real, Real> reportFailure)
    {
        var tolerance = new Dictionary<string, Real>
        {
            ["delta"] = 1.0e-5,
            ["gamma"] = 1.0e-5,
            ["theta"] = 1.0e-5,
            ["rho"] = 1.0e-5,
            ["divRho"] = 1.0e-5,
            ["vega"] = 1.0e-5,
            ["qrho"] = 1.0e-5,
            ["qvega"] = 1.0e-5,
            ["qlambda"] = 1.0e-5,
        };

        Real[] underlyings = [100.0];
        Rate[] qRates = [0.04, 0.05];
        Rate[] rRates = [0.01, 0.05, 0.15];
        Volatility[] vols = [0.11, 1.20];
        Real[] correlations = [0.10, 0.90];
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (Real u in underlyings)
        {
            foreach (Real m in qRates)
            {
                foreach (Real n in rRates)
                {
                    foreach (Volatility v in vols)
                    {
                        foreach (Rate fxr in rRates)
                        {
                            foreach (Volatility fxv in vols)
                            {
                                foreach (Real corr in correlations)
                                {
                                    Rate q = m, r = n;
                                    Real Npv(Real s, Rate qq, Rate rr, Volatility vv, Rate ff, Volatility fv, Real cc, EvaluationContext c) =>
                                        price(Market.Floating(c, dc, s, qq, rr, vv, ff, fv), cc, c).NPV!.Value;

                                    QuantoOptionResults results = price(Market.Floating(context, dc, u, q, r, v, fxr, fxv), corr, context);
                                    Real value = results.NPV!.Value;
                                    var calculated = new Dictionary<string, Real>
                                    {
                                        ["delta"] = results.Delta!.Value,
                                        ["gamma"] = results.Gamma!.Value,
                                        ["theta"] = results.Theta!.Value,
                                        ["rho"] = results.Rho!.Value,
                                        ["divRho"] = results.DividendRho!.Value,
                                        ["vega"] = results.Vega!.Value,
                                        ["qrho"] = results.Qrho!.Value,
                                        ["qvega"] = results.Qvega!.Value,
                                        ["qlambda"] = results.Qlambda!.Value,
                                    };
                                    var expected = new Dictionary<string, Real>();

                                    if (value > u * 1.0e-5)
                                    {
                                        // perturb spot and get delta and gamma
                                        Real du = u * 1.0e-4;
                                        QuantoOptionResults up = price(Market.Floating(context, dc, u + du, q, r, v, fxr, fxv), corr, context);
                                        Real valueP = up.NPV!.Value, deltaP = up.Delta!.Value;
                                        QuantoOptionResults down = price(Market.Floating(context, dc, u - du, q, r, v, fxr, fxv), corr, context);
                                        Real valueM = down.NPV!.Value, deltaM = down.Delta!.Value;
                                        expected["delta"] = (valueP - valueM) / (2 * du);
                                        expected["gamma"] = (deltaP - deltaM) / (2 * du);

                                        // perturb rates and get rho and dividend rho
                                        Spread dr = r * 1.0e-4;
                                        valueP = Npv(u, q, r + dr, v, fxr, fxv, corr, context);
                                        valueM = Npv(u, q, r - dr, v, fxr, fxv, corr, context);
                                        expected["rho"] = (valueP - valueM) / (2 * dr);

                                        Spread dq = q * 1.0e-4;
                                        valueP = Npv(u, q + dq, r, v, fxr, fxv, corr, context);
                                        valueM = Npv(u, q - dq, r, v, fxr, fxv, corr, context);
                                        expected["divRho"] = (valueP - valueM) / (2 * dq);

                                        // perturb volatility and get vega
                                        Volatility dv = v * 1.0e-4;
                                        valueP = Npv(u, q, r, v + dv, fxr, fxv, corr, context);
                                        valueM = Npv(u, q, r, v - dv, fxr, fxv, corr, context);
                                        expected["vega"] = (valueP - valueM) / (2 * dv);

                                        // perturb fx rate and get qrho
                                        Spread dfxr = fxr * 1.0e-4;
                                        valueP = Npv(u, q, r, v, fxr + dfxr, fxv, corr, context);
                                        valueM = Npv(u, q, r, v, fxr - dfxr, fxv, corr, context);
                                        expected["qrho"] = (valueP - valueM) / (2 * dfxr);

                                        // perturb fx volatility and get qvega
                                        Volatility dfxv = fxv * 1.0e-4;
                                        valueP = Npv(u, q, r, v, fxr, fxv + dfxv, corr, context);
                                        valueM = Npv(u, q, r, v, fxr, fxv - dfxv, corr, context);
                                        expected["qvega"] = (valueP - valueM) / (2 * dfxv);

                                        // perturb correlation and get qlambda
                                        Real dcorr = corr * 1.0e-4;
                                        valueP = Npv(u, q, r, v, fxr, fxv, corr + dcorr, context);
                                        valueM = Npv(u, q, r, v, fxr, fxv, corr - dcorr, context);
                                        expected["qlambda"] = (valueP - valueM) / (2 * dcorr);

                                        // perturb date and get theta
                                        double dT = dc.YearFraction(today - 1, today + 1);
                                        var yesterday = new EvaluationContext { EvaluationDate = today - 1 };
                                        var tomorrow = new EvaluationContext { EvaluationDate = today + 1 };
                                        valueM = Npv(u, q, r, v, fxr, fxv, corr, yesterday);
                                        valueP = Npv(u, q, r, v, fxr, fxv, corr, tomorrow);
                                        expected["theta"] = (valueP - valueM) / dT;

                                        // compare
                                        foreach (string greek in calculated.Keys)
                                        {
                                            Real expct = expected[greek],
                                                calcl = calculated[greek],
                                                tol = tolerance[greek];
                                            Real error = RelativeError(expct, calcl, u);
                                            if (error > tol)
                                            {
                                                reportFailure(greek, u, q, r, v, fxr, fxv, corr, expct, calcl, error, tol);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // The test suite's relativeError: the difference relative to the reference, or absolute for a zero reference.
    private static Real RelativeError(Real x1, Real x2, Real reference) =>
        reference != 0.0 ? Math.Abs(x1 - x2) / reference : Math.Abs(x1 - x2);

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static Integer TimeToDays(double t, Integer daysPerYear = 360) =>
        (Integer)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    private static void QuantoReportFailure(
        string greekName,
        PlainVanillaPayoff payoff,
        Exercise exercise,
        Real s,
        Rate q,
        Rate r,
        Date today,
        Volatility v,
        Rate fxr,
        Volatility fxv,
        Real corr,
        Real expected,
        Real calculated,
        Real error,
        Real tolerance) =>
        Assert.Fail(
            $"Quanto {exercise.Type} {payoff.OptionType} option with {payoff.Name} payoff:\n"
            + $"    spot value:        {s}\n    strike:            {payoff.Strike}\n    dividend yield:    {q}\n"
            + $"    risk-free rate:    {r}\n    fx risk-free rate: {fxr}\n    reference date:    {today}\n"
            + $"    maturity:          {exercise.LastDate()}\n    volatility:        {v}\n    fx volatility:     {fxv}\n"
            + $"    correlation:       {corr}\n\n    expected   {greekName}: {expected}\n"
            + $"    calculated {greekName}: {calculated}\n    error:            {error}\n    tolerance:        {tolerance}");

    private static void QuantoForwardReportFailure(
        string greekName,
        PlainVanillaPayoff payoff,
        Real moneyness,
        Exercise exercise,
        Real s,
        Rate q,
        Rate r,
        Date today,
        Date reset,
        Volatility v,
        Rate fxr,
        Volatility fxv,
        Real corr,
        Real expected,
        Real calculated,
        Real error,
        Real tolerance) =>
        Assert.Fail(
            $"Quanto {exercise.Type} {payoff.OptionType} option with {payoff.Name} payoff:\n"
            + $"    spot value:        {s}\n    strike:            {payoff.Strike}\n    moneyness:         {moneyness}\n"
            + $"    dividend yield:    {q}\n    risk-free rate:    {r}\n    fx risk-free rate: {fxr}\n"
            + $"    reference date:    {today}\n    reset date:        {reset}\n    maturity:          {exercise.LastDate()}\n"
            + $"    volatility:        {v}\n    fx volatility:     {fxv}\n    correlation:       {corr}\n\n"
            + $"    expected   {greekName}: {expected}\n    calculated {greekName}: {calculated}\n"
            + $"    error:            {error}\n    tolerance:        {tolerance}");

    // The case's market: the underlying's process, the foreign curve and the exchange rate's volatility, flat, from a
    // date (QuantLib's flatRate(today, …)) or from the evaluation date (its flatRate(…), which moves with it).
    private sealed record Market(GeneralizedBlackScholesProcess Process, YieldTermStructure ForeignTS, BlackVolTermStructure FxVolTS)
    {
        public static Market On(Date today, DayCounter dc, Real s, Rate q, Rate r, Volatility v, Rate fxr, Volatility fxv) =>
            new(
                new BlackScholesMertonProcess(
                    s, new FlatForward(today, q, dc), new FlatForward(today, r, dc), new BlackConstantVol(today, new NullCalendar(), v, dc)),
                new FlatForward(today, fxr, dc),
                new BlackConstantVol(today, new NullCalendar(), fxv, dc));

        public static Market Floating(
            EvaluationContext context, DayCounter dc, Real s, Rate q, Rate r, Volatility v, Rate fxr, Volatility fxv) =>
            new(
                new BlackScholesMertonProcess(
                    s,
                    FlatForward.Create(0, new NullCalendar(), q, dc, context),
                    FlatForward.Create(0, new NullCalendar(), r, dc, context),
                    BlackConstantVol.Create(0, new NullCalendar(), v, dc, context)),
                FlatForward.Create(0, new NullCalendar(), fxr, dc, context),
                BlackConstantVol.Create(0, new NullCalendar(), fxv, dc, context));
    }

    private readonly record struct QuantoOptionData(
        OptionType Type, Real Strike, Real S, Rate Q, Rate R, double T, Volatility V, Rate Fxr, Volatility Fxv, Real Corr,
        Real Result, Real Tol);

    private readonly record struct QuantoForwardOptionData(
        OptionType Type, Real Moneyness, Real S, Rate Q, Rate R, double Start, double T, Volatility V, Rate Fxr,
        Volatility Fxv, Real Corr, Real Result, Real Tol);
}
