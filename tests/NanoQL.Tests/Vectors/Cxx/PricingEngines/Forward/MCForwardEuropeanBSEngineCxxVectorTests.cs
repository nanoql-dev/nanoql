// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ForwardEuropeanBSPathPricer and MakeMCForwardEuropeanBSEngine vectors,
/// <c>tests/vectors/cxx/pricingengines/forward/mcforwardeuropeanbsengine.json</c>:
/// <see cref="ForwardEuropeanBSPathPricer"/>'s value on fixed paths and its refusal of a negative moneyness, and
/// <see cref="MakeMCForwardEuropeanBSEngine{TRng, TStats}"/>'s checks, against QuantLib's through the C++ harness.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCForwardEuropeanBSEngineCxxVectorTests
{
    [Fact]
    public void MCForwardEuropeanBSEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/forward/mcforwardeuropeanbsengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("setting", out _))
            {
                string setting = c.String("setting");
                check.Output(c, "error", () => Message(() => Build(setting)));
                continue;
            }

            var type = (OptionType)c.Int("optionType");
            var grid = new NanoQL.TimeGrid(1.0, c.Doubles("values").Length - 1);
            double Price() =>
                new ForwardEuropeanBSPathPricer(type, c.Double("moneyness"), c.Int("resetIndex"), c.Double("discount"))
                    .Evaluate(new MonteCarloPath(grid, new Vector(c.Doubles("values"))));
            if (c.Expected.TryGetProperty("error", out _))
            {
                check.Output(c, "error", () => Message(() => Price()));
                continue;
            }

            check.Output(c, "value", () => Price());
        }

        check.Complete();
    }

    // The builder's setting, converted to an engine as the harness's check does.
    private static void Build(string setting)
    {
        DayCounter dc = new Actual365Fixed();
        Date today = new(15, Month.October, 2026);
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.02, dc), new FlatForward(today, 0.05, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.25, dc));
        var make = new MakeMCForwardEuropeanBSEngine<PseudoRandom, GeneralStatistics>(process, 42);
        _ = setting switch
        {
            "noSteps" => make.WithSamples(10).ToEngine(),
            "overspecified" => make.WithSteps(2).WithStepsPerYear(4).WithSamples(10).ToEngine(),
            "toleranceThenSamples" => make.WithSteps(2).WithAbsoluteTolerance(0.1).WithSamples(10).ToEngine(),
            "samplesThenTolerance" => make.WithSteps(2).WithSamples(10).WithAbsoluteTolerance(0.1).ToEngine(),
            _ => new MakeMCForwardEuropeanBSEngine<LowDiscrepancy, GeneralStatistics>(process, 42)
                .WithSteps(2).WithAbsoluteTolerance(0.1).ToEngine(),
        };
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
