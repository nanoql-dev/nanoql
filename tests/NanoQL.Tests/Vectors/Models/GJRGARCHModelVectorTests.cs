// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The GJRGARCHModel vectors, <c>tests/vectors/models/equity/gjrgarchmodel.json</c>: two processes' six parameters
/// through the model's accessors, the parameters set to other values on the new model
/// <see cref="CalibratedModelExtensions.SetParams{TModel}"/> returns, its process a GJR-GARCH process on them, and two
/// calibrations to nine helpers priced by the analytic GJR-GARCH engine, compared on the outputs the stability filter
/// keeps.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GJRGARCHModelVectorTests
{
    [Fact]
    public void GJRGARCHModelVector()
    {
        var file = VectorFile.Load("models/equity/gjrgarchmodel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var model = new GJRGARCHModel(GJRGARCHProcessVectorTests.GJRGARCHProcessOf(c.Input("process")));
            if (c.Inputs.TryGetProperty("helpers", out JsonElement helperSpecs))
            {
                CheckCalibration(check, c, model, helperSpecs);
                continue;
            }

            check.Output(c, "params", model.Params().ToArray());
            Read(check, c, model, string.Empty);
            GJRGARCHModel after = model.SetParams(new Vector(Doubles(c.Input("newParams"))));
            check.Output(c, "afterParams", after.Params().ToArray());
            Read(check, c, after, "after");
            GJRGARCHProcess process = after.Process;
            Assert.Equal(after.Omega, process.Omega);
            Assert.Equal(after.Alpha, process.Alpha);
            Assert.Equal(after.Beta, process.Beta);
            Assert.Equal(after.Gamma, process.Gamma);
            Assert.Equal(after.Lambda, process.Lambda);
            Assert.Equal(after.V0, process.V0);
        }

        check.Complete();
    }

    private static void CheckCalibration(VectorAssert check, VectorCase c, GJRGARCHModel model, JsonElement specs)
    {
        EvaluationContext context = c.Context();
        GJRGARCHProcess p = model.Process;
        List<HestonModelHelper> helpers =
        [
            .. specs.EnumerateArray().Select(h => new HestonModelHelper(
                    new Period(h.GetProperty("months").GetInt32(), TimeUnit.Months),
                    new NullCalendar(),
                    p.S0,
                    h.GetProperty("strike").GetDouble(),
                    h.GetProperty("vol").GetDouble(),
                    p.RiskFreeRate,
                    p.DividendYield,
                    context)
                .SetPricingEngine<GJRGARCHModel>(AnalyticGJRGARCHEngine.Calculate)),
        ];
        CalibrationResults<GJRGARCHModel> result =
            model.Calibrate(helpers, new LevenbergMarquardt(), new EndCriteria(400, 40, 1.0e-8, 1.0e-8, 1.0e-8), context);
        var outputs = new Dictionary<string, object?>
        {
            ["endCriteria"] = (int)result.EndCriteria,
            ["functionEvaluation"] = result.FunctionEvaluation,
        };
        double[] parameters = result.Model.Params().ToArray();
        for (int i = 0; i < parameters.Length; i++)
        {
            outputs[$"params[{i}]"] = parameters[i];
        }

        for (int i = 0; i < helpers.Count; i++)
        {
            outputs[$"calibrationErrors[{i}]"] = helpers[i].CalibrationError(result.Model, context);
        }

        foreach (JsonProperty output in c.Expected.EnumerateObject())
        {
            check.Output(c, output.Name, outputs[output.Name]);
        }
    }

    private static void Read(VectorAssert check, VectorCase c, GJRGARCHModel m, string prefix)
    {
        string Name(string n) => prefix.Length == 0 ? n : prefix + char.ToUpperInvariant(n[0]) + n[1..];
        check.Output(c, Name("omega"), m.Omega);
        check.Output(c, Name("alpha"), m.Alpha);
        check.Output(c, Name("beta"), m.Beta);
        check.Output(c, Name("gamma"), m.Gamma);
        check.Output(c, Name("lambda"), m.Lambda);
        check.Output(c, Name("v0"), m.V0);
    }
}
