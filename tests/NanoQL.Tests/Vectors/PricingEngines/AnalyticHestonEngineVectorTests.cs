// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticHestonEngine vectors, <c>tests/vectors/pricingengines/vanilla/analytichestonengine.json</c>: the
/// characteristic function and its logarithm on SP4's contours, the NPVs and evaluation counts of calls and puts under
/// each complex-log formula and integration, OptimalAlpha's searches, choice, bounds and functions, and the branch
/// correction's refusal of an adaptive integration.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticHestonEngineVectorTests
{
    [Fact]
    public void AnalyticHestonEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/analytichestonengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var model = new HestonModel(HestonProcessVectorTests.HestonProcessOf(c.Input("model")));
            if (c.Inputs.TryGetProperty("z", out JsonElement zs))
            {
                var engine = new AnalyticHestonEngine(model);
                double t = c.Double("t");
                Complex[] points = [.. zs.EnumerateArray().Select(z => new Complex(z[0].GetDouble(), z[1].GetDouble()))];
                check.Output(c, "chF", points.Select(z => Lazy(() => Pair(engine.ChF(z, t)))).ToArray());
                check.Output(c, "lnChF", points.Select(z => Lazy(() => Pair(engine.LnChF(z, t)))).ToArray());
            }
            else if (c.Inputs.TryGetProperty("engine", out JsonElement spec))
            {
                var exercise = new EuropeanExercise(c.DateInput("maturity"));
                var type = (OptionType)c.Int("type");
                Func<double, OneAssetOptionResults> price =
                    k => Priced(model, spec, new VanillaOption(new PlainVanillaPayoff(type, k), exercise), context);
                check.Output(c, "NPV", Doubles(c.Input("strikes")).Select(k => Lazy(() => price(k).NPV)).ToArray());
                check.Output(
                    c,
                    "numberOfEvaluations",
                    Doubles(c.Input("countStrikes")).Select(k => Lazy(() => price(k).Result<int>("numberOfEvaluations"))).ToArray());
            }
            else if (c.Inputs.TryGetProperty("strike", out JsonElement strikeInput))
            {
                // one strike's searches, each output present where QuantLib's own value is stable
                double k = strikeInput.GetDouble();
                AnalyticHestonEngineOptimalAlpha Alpha() => new(c.Double("t"), new AnalyticHestonEngine(model));
                OptionalOutput(check, c, "alphaGreaterZero", () => Triple(Alpha().AlphaGreaterZero(k)));
                OptionalOutput(check, c, "alphaSmallerMinusOne", () => Triple(Alpha().AlphaSmallerMinusOne(k)));
                OptionalOutput(check, c, "alpha", () => Alpha().Evaluate(k));
                OptionalOutput(check, c, "alphaEvaluations", () => Alpha().EvaluateWithStatistics(k).Evaluations);
                OptionalOutput(check, c, "alphaMax", () => Alpha().AlphaMax(k));
                OptionalOutput(check, c, "alphaMin", () => Alpha().AlphaMin(k));
            }
            else
            {
                var alpha = new AnalyticHestonEngineOptimalAlpha(c.Double("t"), new AnalyticHestonEngine(model));
                check.Output(c, "M", Doubles(c.Input("k")).Select(k => Lazy(() => alpha.M(k))).ToArray());
                double[] xs = [0.0, 3.0, 6.283185307179586];
                check.Output(c, "kPlus", xs.Select(x => (object?)alpha.K(x, 1)).ToArray());
                check.Output(c, "kMinus", xs.Select(x => (object?)alpha.K(x, -1)).ToArray());
            }
        }

        check.Complete();
    }

    private static void OptionalOutput(VectorAssert check, VectorCase c, string name, Func<object?> actual)
    {
        if (c.Expected.TryGetProperty(name, out _))
        {
            check.Output(c, name, actual);
        }
    }

    private static double[] Pair(Complex c) => [c.Real, c.Imaginary];

    private static object[] Triple((double Alpha, double Value, int Evaluations) r) => [r.Alpha, r.Value, r.Evaluations];

    // An option priced on an engine spec: the tolerance constructor through the static Calculate, which builds it; the
    // others on the engine EngineOf builds.
    private static OneAssetOptionResults Priced(HestonModel model, JsonElement spec, VanillaOption option, EvaluationContext context)
    {
        JsonElement integration = spec.GetProperty("integration");
        return spec.GetProperty("cpxLog").ValueKind == JsonValueKind.Null && integration.GetProperty("type").GetString() == "relTolerance"
            ? AnalyticHestonEngine.Calculate(
                option, context, model, integration.GetProperty("relTolerance").GetDouble(), integration.GetProperty("maxEvaluations").GetInt32())
            : EngineOf(model, spec).Calculate(option, context);
    }

    // An engine spec other than the tolerance constructor's: the order constructor where no formula is given, the full
    // one otherwise.
    private static AnalyticHestonEngine EngineOf(HestonModel model, JsonElement spec)
    {
        JsonElement integration = spec.GetProperty("integration");
        if (spec.GetProperty("cpxLog").ValueKind == JsonValueKind.Null)
        {
            return new AnalyticHestonEngine(model, integration.GetProperty("order").GetInt32());
        }

        return new AnalyticHestonEngine(
            model,
            (AnalyticHestonEngineComplexLogFormula)spec.GetProperty("cpxLog").GetInt32(),
            IntegrationOf(integration),
            spec.GetProperty("andersenPiterbargEpsilon").GetDouble());
    }

    private static FourierIntegration IntegrationOf(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        return type switch
        {
            "gaussLaguerre" => FourierIntegration.GaussLaguerre(spec.GetProperty("order").GetInt32()),
            "gaussLegendre" => FourierIntegration.GaussLegendre(spec.GetProperty("order").GetInt32()),
            "gaussChebyshev" => FourierIntegration.GaussChebyshev(spec.GetProperty("order").GetInt32()),
            "gaussChebyshev2nd" => FourierIntegration.GaussChebyshev2nd(spec.GetProperty("order").GetInt32()),
            "gaussLobatto" => FourierIntegration.GaussLobatto(
                spec.GetProperty("relTolerance").ValueKind == JsonValueKind.Null ? null : spec.GetProperty("relTolerance").GetDouble(),
                spec.GetProperty("absTolerance").GetDouble(),
                spec.GetProperty("maxEvaluations").GetInt32()),
            "simpson" => FourierIntegration.Simpson(spec.GetProperty("absTolerance").GetDouble(), spec.GetProperty("maxEvaluations").GetInt32()),
            "trapezoid" => FourierIntegration.Trapezoid(spec.GetProperty("absTolerance").GetDouble(), spec.GetProperty("maxEvaluations").GetInt32()),
            _ => throw new ArgumentException($"unknown integration {type}", nameof(spec)),
        };
    }
}
