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
/// The C++ harness's Monte Carlo GJR-GARCH vectors,
/// <c>tests/vectors/cxx/pricingengines/vanilla/mceuropeangjrgarchengine.json</c>:
/// <see cref="EuropeanGJRGARCHPathPricer"/> on fixed two-asset paths and its refusal of a negative strike; and the engine
/// <see cref="MakeMCEuropeanGJRGARCHEngine{TRng, TStats}"/> makes after QuantLib's builder's setters, its NPV and error
/// estimate on fixed seeds, and the builder's refusals with QuantLib's messages.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCEuropeanGJRGARCHEngineCxxVectorTests
{
    [Fact]
    public void MCEuropeanGJRGARCHEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/vanilla/mceuropeangjrgarchengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var type = (OptionType)c.Int("optionType");
            if (c.Input("kind").GetString() == "path")
            {
                var grid = new TimeGrid(Doubles(c.Input("times")));
                MonteCarloPath[] paths = [.. c.Input("paths").EnumerateArray().Select(p => new MonteCarloPath(grid, new Vector(Doubles(p))))];
                var multiPath = new MultiPath([.. paths]);
                double discount = c.Double("discount");
                check.Output(
                    c,
                    "value",
                    Doubles(c.Input("strikes")).Select(k => Lazy(() => new EuropeanGJRGARCHPathPricer(type, k, discount).Evaluate(multiPath))).ToArray());
                continue;
            }

            EvaluationContext context = c.Context();
            GJRGARCHProcess process = ProcessOf(c.Input("process"));
            var option = new VanillaOption(new PlainVanillaPayoff(type, c.Double("strike")), new EuropeanExercise(c.DateInput("exerciseDate")));
            JsonElement setters = c.Input("setters");
            bool pseudoRandom = c.Input("traits").GetString() == "PseudoRandom";
            Func<OneAssetOptionResults> price = pseudoRandom
                ? () => Built<PseudoRandom>(process, setters)(option, context)
                : () => Built<LowDiscrepancy>(process, setters)(option, context);
            if (c.Expected.TryGetProperty("error", out _))
            {
                check.Output(c, "error", () => Message(price));
                continue;
            }

            var results = new Lazy<OneAssetOptionResults>(price);
            check.Output(c, "NPV", () => results.Value.NPV);
            check.Output(c, "errorEstimate", () => results.Value.ErrorEstimate);
        }

        check.Complete();
    }

    // The harness's process spec: QuantLib's default discretization on flat forwards from the reference date.
    private static GJRGARCHProcess ProcessOf(JsonElement spec)
    {
        var reference = new Date(spec.GetProperty("referenceDate").GetInt32());
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        double Real(string name) => spec.GetProperty(name).GetDouble();
        return new GJRGARCHProcess(
            new FlatForward(reference, Real("riskFree"), dc),
            new FlatForward(reference, Real("dividend"), dc),
            Real("s0"),
            Real("v0"),
            Real("omega"),
            Real("alpha"),
            Real("beta"),
            Real("gamma"),
            Real("lambda"),
            Real("daysPerYear"));
    }

    // The engine the builder makes after a case's setters, in order, on the seed the first of them sets.
    private static Func<VanillaOption, EvaluationContext, OneAssetOptionResults> Built<TRng>(GJRGARCHProcess process, JsonElement setters)
        where TRng : IRngTraits
    {
        JsonElement[] s = [.. setters.EnumerateArray()];
        Assert.Equal("withSeed", s[0][0].GetString());
        var make = new MakeMCEuropeanGJRGARCHEngine<TRng, GeneralStatistics>(process, s[0][1].GetInt64());
        foreach (JsonElement setter in s)
        {
            JsonElement value = setter[1];
            make = setter[0].GetString() switch
            {
                "withSeed" => make.WithSeed(value.GetInt64()),
                "withSteps" => make.WithSteps(value.GetInt32()),
                "withStepsPerYear" => make.WithStepsPerYear(value.GetInt32()),
                "withSamples" => make.WithSamples(value.GetInt32()),
                "withAbsoluteTolerance" => make.WithAbsoluteTolerance(value.GetDouble()),
                "withMaxSamples" => make.WithMaxSamples(value.GetInt32()),
                "withAntitheticVariate" => make.WithAntitheticVariate(value.GetBoolean()),
                string name => throw new InvalidOperationException($"unknown setter {name}"),
                null => throw new InvalidOperationException("no setter"),
            };
        }

        return make.ToEngine();
    }

    // The refusal's message, QuantLib's text: an argument exception's without the parameter's name .NET appends.
    private static string Message(Func<OneAssetOptionResults> price)
    {
        try
        {
            price();
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
