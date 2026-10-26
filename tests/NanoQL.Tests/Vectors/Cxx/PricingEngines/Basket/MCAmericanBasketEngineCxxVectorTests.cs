// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's American basket vectors,
/// <c>tests/vectors/cxx/pricingengines/basket/mcamericanbasketengine.json</c>: <see cref="AmericanBasketPathPricer"/>'s
/// states, exercise values and basis values on fixed paths, and its refusals; and the refusals of
/// <see cref="MakeMCAmericanBasketEngine{TRng, TStats}"/> and the engine it makes, with QuantLib's messages.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCAmericanBasketEngineCxxVectorTests
{
    [Fact]
    public void MCAmericanBasketEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/basket/mcamericanbasketengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Input("kind").GetString() == "path")
            {
                var grid = new TimeGrid(Doubles(c.Input("times")));
                MonteCarloPath[] paths = [.. c.Input("paths").EnumerateArray().Select(p => new MonteCarloPath(grid, new Vector(Doubles(p))))];
                var multiPath = new MultiPath([.. paths]);
                int assetNumber = c.Int("assetNumber");
                Payoff payoff = PayoffOf(c.Input("payoff"));
                int order = c.Int("polynomialOrder");
                var family = (LsmBasisSystemPolynomialType)c.Int("polynomialType");
                if (c.Expected.TryGetProperty("error", out _))
                {
                    check.Output(c, "error", () => Message(() =>
                    {
                        var pricer = new AmericanBasketPathPricer(assetNumber, payoff, order, family);
                        for (int t = 0; t < grid.Size; ++t)
                        {
                            pricer.Evaluate(multiPath, t);
                        }
                    }));
                    continue;
                }

                var p = new AmericanBasketPathPricer(assetNumber, payoff, order, family);
                Vector[] states = [.. Enumerable.Range(0, grid.Size).Select(t => p.State(multiPath, t))];
                check.Output(c, "states", states.Select(s => s.ToArray()).ToList());
                check.Output(c, "values", Enumerable.Range(0, grid.Size).Select(t => p.Evaluate(multiPath, t)).ToArray());
                check.Output(c, "basis", states.Select(s => p.BasisSystem().Select(f => f(s)).ToArray()).ToList());
                continue;
            }

            EvaluationContext context = c.Context();
            StochasticProcessArray processes = ProcessesOf(c.Input("processes"), c.Double("correlation"), context.EvaluationDate);
            JsonElement exercise = c.Input("exercise");
            var option = new BasketOption(
                (BasketPayoff)PayoffOf(c.Input("payoff")),
                ExerciseOf(exercise[0].GetString()!, new Date(exercise[1].GetInt32()), context.EvaluationDate));
            JsonElement setters = c.Input("setters");
            Action price = c.Input("traits").GetString() == "PseudoRandom"
                ? () => Built<PseudoRandom>(processes, setters)(option, context)
                : () => Built<LowDiscrepancy>(processes, setters)(option, context);
            check.Output(c, "error", () => Message(price));
        }

        check.Complete();
    }

    // The harness's payoff: [kind, optionType, strike, weights or null], a basket of a plain vanilla payoff, or
    // "Plain", the plain vanilla payoff alone.
    private static Payoff PayoffOf(JsonElement spec)
    {
        var basePayoff = new PlainVanillaPayoff((OptionType)spec[1].GetInt32(), spec[2].GetDouble());
        return spec[0].GetString() switch
        {
            "Min" => new MinBasketPayoff(basePayoff),
            "Max" => new MaxBasketPayoff(basePayoff),
            "Spread" => new SpreadBasketPayoff(basePayoff),
            "Average" => new AverageBasketPayoff(basePayoff, Doubles(spec[3])),
            "Plain" => basePayoff,
            string kind => throw new InvalidOperationException($"unknown payoff {kind}"),
            null => throw new InvalidOperationException("no payoff"),
        };
    }

    // The harness's processes: Black-Scholes-Merton processes on flat Act/365 (Fixed) forwards and constant
    // volatilities from the evaluation date, [spot, r, q, volatility] each, at one correlation.
    private static StochasticProcessArray ProcessesOf(JsonElement specs, double correlation, Date today)
    {
        DayCounter dc = new Actual365Fixed();
        StochasticProcess1D[] processes = [.. specs.EnumerateArray().Select(s => (StochasticProcess1D)new BlackScholesMertonProcess(
            s[0].GetDouble(),
            new FlatForward(today, s[2].GetDouble(), dc),
            new FlatForward(today, s[1].GetDouble(), dc),
            new BlackConstantVol(today, new NullCalendar(), s[3].GetDouble(), dc)))];
        var rho = new Matrix(processes.Length, processes.Length, correlation);
        for (int i = 0; i < processes.Length; ++i)
        {
            rho[i, i] = 1.0;
        }

        return new StochasticProcessArray([.. processes], rho);
    }

    private static Exercise ExerciseOf(string kind, Date date, Date today) => kind switch
    {
        "European" => new EuropeanExercise(date),
        "American" => new AmericanExercise(today, date),
        "AmericanPayoffAtExpiry" => new AmericanExercise(today, date, true),
        _ => throw new InvalidOperationException($"unknown exercise {kind}"),
    };

    // The engine the builder makes after a case's setters, in order, on the seed the first of them sets.
    private static Func<BasketOption, EvaluationContext, MultiAssetOptionResults> Built<TRng>(
        StochasticProcessArray processes, JsonElement setters)
        where TRng : IRngTraits
    {
        JsonElement[] s = [.. setters.EnumerateArray()];
        Assert.Equal("withSeed", s[0][0].GetString());
        var make = new MakeMCAmericanBasketEngine<TRng, GeneralStatistics>(processes, s[0][1].GetInt64());
        foreach (JsonElement setter in s)
        {
            JsonElement value = setter[1];
            make = setter[0].GetString() switch
            {
                "withSeed" => make.WithSeed(value.GetInt64()),
                "withSteps" => make.WithSteps(value.GetInt32()),
                "withStepsPerYear" => make.WithStepsPerYear(value.GetInt32()),
                "withBrownianBridge" => make.WithBrownianBridge(value.GetBoolean()),
                "withAntitheticVariate" => make.WithAntitheticVariate(value.GetBoolean()),
                "withSamples" => make.WithSamples(value.GetInt32()),
                "withAbsoluteTolerance" => make.WithAbsoluteTolerance(value.GetDouble()),
                "withMaxSamples" => make.WithMaxSamples(value.GetInt32()),
                "withCalibrationSamples" => make.WithCalibrationSamples(value.GetInt32()),
                "withPolynomialOrder" => make.WithPolynomialOrder(value.GetInt32()),
                "withBasisSystem" => make.WithBasisSystem((LsmBasisSystemPolynomialType)value.GetInt32()),
                string name => throw new InvalidOperationException($"unknown setter {name}"),
                null => throw new InvalidOperationException("no setter"),
            };
        }

        return make.ToEngine();
    }

    // The refusal's message, QuantLib's text: an argument exception's without the parameter's name .NET appends.
    private static string Message(Action act)
    {
        try
        {
            act();
        }
        catch (ArgumentException e) when (e.ParamName is not null)
        {
            return e.Message.Replace($" (Parameter '{e.ParamName}')", string.Empty, StringComparison.Ordinal);
        }
        catch (Exception e) when (e is ArgumentException or NanoQLException)
        {
            return e.Message;
        }

        return "no refusal";
    }
}
