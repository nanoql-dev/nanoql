// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// What the Black-Vasicek engine reads (<c>analyticeuropeanvasicekengine.cpp:72-91</c>): the rates come from the short
/// rate model, its r0 and its bond, and the process's risk-free curve gives the time alone, so two curves on one date and
/// day counter give one price; the model is any Vasicek, a Hull-White model among them, whose bond is its own, fitted to
/// its curve (QuantLib's virtual call).
/// </summary>
public class BlackVasicekEngineInputsTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void TheRiskFreeCurveGivesTheTimeAlone()
    {
        var model = new Vasicek(0.05, 0.1, 0.05, 0.01, 0.0);
        Assert.Equal(Price(Process(0.01), model), Price(Process(0.08), model));
    }

    [Fact]
    public void AHullWhiteModelPricesOnItsOwnBond()
    {
        // Hull-White on a flat 4% curve: its bond is the curve's discount, so with a vanishing short-rate volatility and
        // no correlation the price is Black's on that discount
        YieldTermStructure curve = new FlatForward(Today, 0.04, A365);
        GeneralizedBlackScholesProcess process = Process(0.04);
        double price = Price(process, new HullWhite(curve, 0.1, 1e-12));
        double black = AnalyticEuropeanEngine.Calculate(Call(), Context, process).NPV!.Value;
        Assert.Equal(0.0, (price / black) - 1.0, 1e-10);
    }

    private static GeneralizedBlackScholesProcess Process(double riskFreeRate) =>
        new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, riskFreeRate, A365), new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));

    private static VanillaOption Call() =>
        new(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 730));

    private static double Price(GeneralizedBlackScholesProcess process, Vasicek model) =>
        AnalyticBlackVasicekEngine.Calculate(Call(), Context, process, model, 0.0).NPV!.Value;
}
