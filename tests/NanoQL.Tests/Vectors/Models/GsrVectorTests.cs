// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The GSR model vectors, <c>tests/vectors/models/shortrate/onefactormodels/gsr.json</c>: <see cref="Gsr"/>'s
/// constructions (its reversions, volatilities, parameters and zero bonds), its refusals, and its calibrations to
/// swaption helpers priced by <see cref="Gaussian1dSwaptionEngine"/> (with the reversions fixed, everything free, and
/// the two iterative ones), each holding the outputs the stability filter keeps.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GsrVectorTests
{
    [Fact]
    public void GsrVector()
    {
        var file = VectorFile.Load("models/shortrate/onefactormodels/gsr");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Name.StartsWith("refused / ", StringComparison.Ordinal))
            {
                check.Output(c, "model", () => Gaussian1dInputs.GsrOf(c.Input("model")).Params());
                continue;
            }

            Gsr model = Gaussian1dInputs.GsrOf(c.Input("model"));
            if (!c.Inputs.TryGetProperty("kind", out System.Text.Json.JsonElement kind))
            {
                check.Output(c, "reversion", model.Reversion.Select(x => (object?)x).ToArray());
                check.Output(c, "volatility", model.Volatility.Select(x => (object?)x).ToArray());
                check.Output(c, "params", ModelInputs.Elements(model.Params()));
                check.Output(c, "zerobond", ModelInputs.Points(c, "zerobondPoints").Select(p => (object?)model.Zerobond(p[0], p[1], p[2])).ToArray());
                continue;
            }

            List<SwaptionHelper> helpers = [.. c.Input("helpers").EnumerateArray().Select(spec =>
                CalibrationInputs.SwaptionHelperOf(spec, context).SetPricingEngine<Gsr>((s, ctx, m) => Gaussian1dSwaptionEngine.Calculate(s, ctx, m)))];
            OptimizationMethod method = CalibrationInputs.MethodOf(c.Input("method"));
            EndCriteria endCriteria = OptimizationSpecs.EndCriteria(c.Input("endCriteria"));
            CalibrationResults<Gsr> result = kind.GetString() switch
            {
                "iterative" => model.CalibrateVolatilitiesIterative(helpers, method, endCriteria, context),
                "reversions iterative" => model.CalibrateReversionsIterative(helpers, method, endCriteria, context),
                "fixed reversions" => model.Calibrate(helpers, method, endCriteria, context, new NoConstraint(), default, model.FixedReversions().AsSpan()),
                _ => model.Calibrate(helpers, method, endCriteria, context),
            };
            Gsr calibrated = result.Model;
            var outputs = new Dictionary<string, object?>
            {
                ["endCriteria"] = result.EndCriteria,
                ["functionEvaluation"] = result.FunctionEvaluation,
            };
            Elements(outputs, "params", calibrated.Params().AsSpan().ToArray());
            Elements(outputs, "problemValues", [.. result.ProblemValues]);
            Elements(outputs, "calibrationErrors", [.. helpers.Select(h => h.CalibrationError(calibrated, context))]);
            foreach (System.Text.Json.JsonProperty output in c.Expected.EnumerateObject())
            {
                check.Output(c, output.Name, outputs[output.Name]);
            }

            // The receiver doesn't change.
            Assert.Equal(Gaussian1dInputs.GsrOf(c.Input("model")).Params(), model.Params());
        }

        check.Complete();
    }

    // A list output's elements, each its own output named with its index, as the filter writes them.
    private static void Elements(Dictionary<string, object?> outputs, string name, double[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            outputs[$"{name}[{i}]"] = values[i];
        }
    }
}
