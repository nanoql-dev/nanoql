// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008 Paul Farrington
// SPDX-FileCopyrightText: Copyright (C) 2014 Thema Consulting SA
// SPDX-FileCopyrightText: Copyright (C) 2019 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/quantooption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testValues, testGreeks, testForwardValues, testForwardGreeks, testForwardPerformanceValues, testPDEOptionValues (S-exo's quanto engine); testBarrierValues (S-bar's quanto barrier option); testDoubleBarrierValues (S-xeq's quanto double-barrier option)

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class QuantoOptionTests
{
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
}
