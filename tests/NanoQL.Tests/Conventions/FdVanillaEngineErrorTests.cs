// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Reflection;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception type, message and parameter name of the finite-difference CEV, SABR and CIR engines' and the
/// time-dependent boundary's refusals. QuantLib's "non-striked payoff given" (<c>testFdEnginesWithNonStrikedPayoff</c>,
/// its Black-Scholes, Heston and CIR engines) is the type here: a vanilla option takes a striked payoff, so no engine can
/// be given another, and each engine's check stands for QuantLib's. An exercise on or before the discount curve's date
/// leaves the CEV and SABR engines no time on their curve's clock: QuantLib's throw there, in Boost's non-central
/// chi-squared ("Non centrality parameter is inf") on the curve's date and in its incomplete gamma before it [executed:
/// <c>scratchpad\c4\probe\fdcev_edges.cpp</c>], is nanoql's refusal of the option, as its other finite-difference engines
/// refuse an exercise their clocks have no time for.
/// </summary>
public class FdVanillaEngineErrorTests
{
    private static readonly Date Today = new(22, Month.February, 2018);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void AVanillaOptionTakesAStrikedPayoff()
    {
        ConstructorInfo[] constructors = typeof(VanillaOption).GetConstructors();
        Assert.NotEmpty(constructors);
        Assert.All(constructors, c => Assert.Equal(typeof(StrikedTypePayoff), c.GetParameters()[0].ParameterType));
    }

    [Fact]
    public void AnExerciseOnOrBeforeTheCurvesDateIsRefused()
    {
        // the curve settling two days on, the exercise on its date and before it
        var curve = new FlatForward(Today + 2, 0.05, new Actual365Fixed());
        foreach (Date exercise in new[] { Today + 1, Today + 2 })
        {
            var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 2.3), new EuropeanExercise(exercise));
            const string Message = "the exercise is on or before the discount curve's reference date, where the engine has no time";
            Refused<ArgumentException>(() => FdCEVVanillaEngine.Calculate(option, Context, 2.1, 0.75, 0.45, curve), "option", Message);
            Refused<ArgumentException>(
                () => FdSabrVanillaEngine.Calculate(option, Context, 2.1, 0.75, 0.45, 0.3, 0.1, curve), "option", Message);
            Refused<ArgumentException>(
                () => FdSabrVanillaEngine.Calculate(option, Context, 2.1, 0.75, 0.45, 0.0, 0.1, curve), "option", Message);
        }
    }

    [Fact]
    public void SabrParameterRefusals()
    {
        var curve = new FlatForward(Today, 0.05, new Actual365Fixed());
        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 2.3), new EuropeanExercise(Today + 365));
        Refused<ArgumentException>(
            () => FdSabrVanillaEngine.Calculate(option, Context, 2.1, 0.75, 1.0, 0.3, 0.1, curve), "beta", "beta must be smaller than 1.0: 1 not allowed");
        Refused<ArgumentException>(
            () => FdSabrVanillaEngine.Calculate(option, Context, 2.1, 0.75, 0.5, -0.1, 0.1, curve), "nu", "nu must be non negative: -0.1 not allowed");
        Refused<ArgumentException>(
            () => FdSabrVanillaEngine.Calculate(option, Context, 2.1, 0.0, 0.5, 0.3, 0.1, curve), "alpha", "alpha must be positive: 0 not allowed");
        Refused<ArgumentException>(
            () => FdSabrVanillaEngine.Calculate(option, Context, 2.1, 0.75, 0.5, 0.3, 1.0, curve), "rho", "rho square must be less than one: 1 not allowed");
    }

    [Fact]
    public void TheBoundarysValuesMustMatchItsSide()
    {
        var mesher = new FdmMesherComposite(new Uniform1dMesher(0.0, 1.0, 3), new Uniform1dMesher(0.0, 1.0, 3));
        var bc = new FdmTimeDepDirichletBoundary(mesher, t => ImmutableArray.Create(1.0, 2.0), 0, BoundaryConditionSide.Upper);
        bc.SetTime(0.5);
        Refused<NanoQLException>(
            () => bc.ApplyAfterApplying(new double[9]), null, "values on boundary size (2) does not match hypersurface size (3)");
    }

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
}
