// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception type, message and parameter name of the Heston model's, helper's and engine's refusals: those the
/// vector files record as <c>{"throws": true}</c>, accepting either type, and those no vector reaches. An argument a
/// member checks is an <see cref="ArgumentException"/> naming it, with QuantLib's text where QuantLib has one; a value
/// the member computes or holds is a <see cref="NanoQLException"/>.
/// </summary>
public class HestonErrorTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();
    private static readonly YieldTermStructure RiskFree = new FlatForward(Today, 0.04, Dc);
    private static readonly YieldTermStructure Dividends = new FlatForward(Today, 0.02, Dc);

    [Fact]
    public void ModelRefusals()
    {
        Refused<ArgumentNullException>(() => new HestonModel(null!), "process", null);
        Refused<ArgumentException>(() => new HestonModel(Process(theta: 0.0)), "process", "0: invalid value");
        Refused<ArgumentException>(() => new HestonModel(Process(kappa: -1.0)), "process", "-1: invalid value");
        Refused<ArgumentException>(() => new HestonModel(Process(sigma: 0.0)), "process", "0: invalid value");
        Refused<ArgumentException>(() => new HestonModel(Process(rho: 1.5)), "process", "1.5: invalid value");
        Refused<ArgumentException>(() => new HestonModel(Process(v0: 0.0)), "process", "0: invalid value");

        var feller = new HestonModelFellerConstraint();
        Assert.True(feller.Test(new Vector([0.04, 1.5, 0.3])));
        Assert.False(feller.Test(new Vector([0.04, 1.5, 0.4])));
        Assert.False(feller.Test(new Vector([0.04, 1.5, -0.1])));
        Refused<ArgumentException>(() => feller.Test(new Vector([0.04, 1.5])), "p", "the Feller constraint reads three parameters");
        Refused<ArgumentNullException>(() => feller.Test(null!), "p", null);
    }

    [Fact]
    public void HelperRefusals()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        var period = new Period(1, TimeUnit.Years);
        Refused<ArgumentNullException>(
            () => new HestonModelHelper(period, null!, 100.0, 100.0, 0.2, RiskFree, Dividends, context), "calendar", null);
        Refused<ArgumentNullException>(
            () => new HestonModelHelper(period, new NullCalendar(), 100.0, 100.0, 0.2, null!, Dividends, context), "riskFreeRate", null);
        Refused<ArgumentOutOfRangeException>(
            () => new HestonModelHelper(
                period, new NullCalendar(), 100.0, 100.0, 0.2, RiskFree, Dividends, context, (BlackCalibrationHelperCalibrationErrorType)7),
            "calibrationErrorType",
            "unknown Calibration Error Type");

        var helper = new HestonModelHelper(period, new NullCalendar(), 100.0, 100.0, 0.2, RiskFree, Dividends, context)
            .SetPricingEngine<HestonModel>(null);
        var model = new HestonModel(Process());
        Refused<NanoQLException>(() => helper.ModelValue(model, context), null, "null pricing engine");
        Assert.Empty(helper.Times(context));

        // an expired option is worth nothing without an engine, as QuantLib's instrument
        var later = new EvaluationContext { EvaluationDate = Today + 400 };
        Assert.Equal(0.0, helper.ModelValue(model, later));
    }

    [Fact]
    public void EngineRefusals()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        var model = new HestonModel(Process());
        var engine = new AnalyticHestonEngine(model);
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);

        Refused<ArgumentNullException>(() => new AnalyticHestonEngine(null!), "model", null);
        Refused<ArgumentException>(() => new AnalyticHestonEngine(model, 193), "integrationOrder", "maximum integraton order (192) exceeded");
        Refused<ArgumentOutOfRangeException>(
            () => new AnalyticHestonEngine(model, (AnalyticHestonEngineComplexLogFormula)8, FourierIntegration.GaussLaguerre()),
            "cpxLog",
            "unknown complex log formula");
        Refused<ArgumentException>(
            () => new AnalyticHestonEngine(
                model, AnalyticHestonEngineComplexLogFormula.BranchCorrection, FourierIntegration.GaussLobatto(1e-8, null)),
            "cpxLog",
            "Branch correction does not work in conjunction with adaptive integration methods");

        var american = new VanillaOption(payoff, new AmericanExercise(Today, Today + 365));
        Refused<NanoQLException>(() => engine.Calculate(american, context), null, "not an European option");
        var digital = new VanillaOption(new CashOrNothingPayoff(OptionType.Call, 100.0, 1.0), new EuropeanExercise(Today + 365));
        Refused<NanoQLException>(() => engine.Calculate(digital, context), null, "non plain vanilla payoff given");
        var zeroSpot = new AnalyticHestonEngine(new HestonModel(Process(s0: 0.0)));
        Refused<NanoQLException>(() => zeroSpot.PriceVanillaPayoff(payoff, 1.0), null, "negative or null underlying given");
        Refused<ArgumentNullException>(() => engine.PriceVanillaPayoff(null!, 1.0), "payoff", null);

        // the asymptotic control variate's damping exponent
        var asymptotic = new AnalyticHestonEngine(
            model, AnalyticHestonEngineComplexLogFormula.AsymptoticChF, FourierIntegration.GaussLaguerre(), 1e-25, -0.4);
        Refused<NanoQLException>(() => asymptotic.PriceVanillaPayoff(payoff, 1.0), null, "alpha must be equal to -0.5");

        // the control variate's helper
        Refused<ArgumentNullException>(
            () => new AnalyticHestonEngineAPHelper(1.0, 100.0, 100.0, AnalyticHestonEngineComplexLogFormula.AngledContour, null!),
            "engine",
            "pricing engine required");
        Refused<ArgumentException>(
            () => new AnalyticHestonEngineAPHelper(1.0, 100.0, 100.0, AnalyticHestonEngineComplexLogFormula.Gatheral, engine),
            "cpxLog",
            "unknown control variate");
        Refused<NanoQLException>(
            () => new AnalyticHestonEngineAPHelper(1.0, 100.0, 100.0, AnalyticHestonEngineComplexLogFormula.AngledContour, new AddOnEngine(model))
                .Evaluate(1.0),
            null,
            "only Heston model is supported");

        // the optimal alpha's searches; QuantLib's operator() takes -0.5 where either fails
        var alpha = new AnalyticHestonEngineOptimalAlpha(1.0, engine);
        Refused<ArgumentNullException>(() => new AnalyticHestonEngineOptimalAlpha(1.0, null!), "engine", null);
        Refused<ArgumentNullException>(() => new AnalyticHestonEngineOptimalAlpha(Today + 365, null!), "engine", null);
        Assert.True(alpha.AlphaMax(100.0) >= 0.0);
        Assert.True(alpha.AlphaMin(100.0) <= -1.0);
    }

    private static HestonProcess Process(
        double s0 = 100.0, double v0 = 0.04, double kappa = 1.5, double theta = 0.04, double sigma = 0.3, double rho = -0.5) =>
        new(RiskFree, Dividends, s0, v0, kappa, theta, sigma, rho);

    private static void Refused<TException>(Action call, string? paramName, string? message)
        where TException : Exception
    {
        TException e = Assert.Throws<TException>(call);
        if (e is ArgumentException a)
        {
            Assert.Equal(paramName, a.ParamName);
        }

        if (message is not null)
        {
            Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
        }
    }

    // An engine that adds a term to the characteristic function, as Bates's does.
    private sealed class AddOnEngine(HestonModel model) : AnalyticHestonEngine(model)
    {
        protected override System.Numerics.Complex AddOnTerm(double phi, double t, int j) => new(0.0, 1e-3);
    }
}
