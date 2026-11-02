// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The VariancePathPricer vectors, <c>tests/vectors/cxx/pricingengines/forward/mcvarianceswapengine.json</c>:
/// <see cref="VariancePathPricer"/>'s value on fixed paths over constant, curve and surface volatilities, and
/// <see cref="MakeMCVarianceSwapEngine{TRng, TStats}"/>'s and the engine's refusals, against QuantLib's through the C++
/// harness.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCVarianceSwapEngineCxxVectorTests
{
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void MCVarianceSwapEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/forward/mcvarianceswapengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Inputs.TryGetProperty("setting", out _))
            {
                string setting = c.String("setting");
                check.Output(c, "error", () => Message(() => Check(setting, context)));
                continue;
            }

            double[] values = c.Doubles("values");
            var path = new MonteCarloPath(new NanoQL.TimeGrid(c.Double("maturity"), values.Length - 1), new Vector(values));
            var pricer = new VariancePathPricer(Process(c.String("volatility"), context.EvaluationDate));
            check.Output(c, "value", () => pricer.Evaluate(path));
        }

        check.Complete();
    }

    // The harness's processes: spot 100, dividends 2%, rates 5%, and the named volatility.
    private static GeneralizedBlackScholesProcess Process(string volatility, Date today)
    {
        Date[] dates = [today + 91, today + 365, today + 730];
        BlackVolTermStructure vol = volatility switch
        {
            "constant" => new BlackConstantVol(today, new NullCalendar(), 0.25, A365),
            "curve" => new BlackVarianceCurve(today, dates, [0.20, 0.25, 0.22], A365),
            _ => new BlackVarianceSurface(
                today,
                new NullCalendar(),
                dates,
                [80.0, 100.0, 120.0],
                new Matrix(new[,] { { 0.30, 0.28, 0.26 }, { 0.24, 0.23, 0.22 }, { 0.26, 0.24, 0.23 } }),
                A365),
        };
        return new BlackScholesMertonProcess(100.0, new FlatForward(today, 0.02, A365), new FlatForward(today, 0.05, A365), vol);
    }

    // Each setting as the harness builds it: the builder's, converted to an engine, or the engine's own steps.
    private static void Check(string setting, EvaluationContext context)
    {
        GeneralizedBlackScholesProcess p = Process("constant", context.EvaluationDate);
        var builder = new MakeMCVarianceSwapEngine<PseudoRandom, GeneralStatistics>(p, 42);
        var swap = new VarianceSwap(PositionType.Long, 0.04, 1.0, context.EvaluationDate, context.EvaluationDate + 365);
        switch (setting)
        {
            case "noSteps":
                builder.WithSamples(10).ToEngine();
                break;
            case "overspecified":
                builder.WithSteps(2).WithStepsPerYear(4).WithSamples(10).ToEngine();
                break;
            case "toleranceThenSamples":
                builder.WithSteps(2).WithAbsoluteTolerance(0.1).WithSamples(10).ToEngine();
                break;
            case "samplesThenTolerance":
                builder.WithSteps(2).WithSamples(10).WithAbsoluteTolerance(0.1).ToEngine();
                break;
            case "lowDiscrepancyTolerance":
                new MakeMCVarianceSwapEngine<LowDiscrepancy, GeneralStatistics>(p, 42).WithSteps(2).WithAbsoluteTolerance(0.1).ToEngine();
                break;
            default:
                int? steps = setting switch { "zeroTimeSteps" => 0, "bothTimeSteps" => 4, _ => null };
                int? perYear = setting switch { "zeroTimeStepsPerYear" => 0, "bothTimeSteps" => 12, _ => null };
                MCPRVarianceSwapEngine.Calculate(
                    swap, context, p, steps, perYear, false, false, 10, null, null, 42);
                break;
        }
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

        return "built";
    }
}
