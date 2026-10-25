// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BatesModel vectors, <c>tests/vectors/models/equity/batesmodel.json</c>: three processes' eight parameters through
/// the model's accessors, the parameters set to other values on the new model
/// <see cref="CalibratedModelExtensions.SetParams{TModel}"/> returns, its process a Bates process on them, the
/// constructor's refusals, and two calibrations to nine helpers priced by the Bates engine, compared on the outputs the
/// S7R6 filter keeps.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BatesModelVectorTests
{
    [Fact]
    public void BatesModelVector()
    {
        var file = VectorFile.Load("models/equity/batesmodel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var model = new Lazy<BatesModel>(() => new BatesModel(BatesProcessVectorTests.BatesProcessOf(c.Input("process"))));
            if (c.Inputs.TryGetProperty("helpers", out System.Text.Json.JsonElement helperSpecs))
            {
                CheckCalibration(check, c, model.Value, helperSpecs);
                continue;
            }

            check.Output(c, "params", Lazy(() => model.Value.Params().ToArray()));
            if (!c.Expected.TryGetProperty("theta", out _))
            {
                continue;
            }

            Read(check, c, model.Value, string.Empty);
            BatesModel after = model.Value.SetParams(new Vector(Doubles(c.Input("newParams"))));
            check.Output(c, "afterParams", after.Params().ToArray());
            Read(check, c, after, "after");
            var process = Assert.IsType<BatesProcess>(after.Process);
            Assert.Equal(after.Lambda, process.Lambda);
            Assert.Equal(after.Nu, process.Nu);
            Assert.Equal(after.Delta, process.Delta);
        }

        check.Complete();
    }

    private static void CheckCalibration(VectorAssert check, VectorCase c, BatesModel model, System.Text.Json.JsonElement specs)
    {
        EvaluationContext context = c.Context();
        HestonProcess p = model.Process;
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
                .SetPricingEngine<BatesModel>((o, ctx, m) => BatesEngine.Calculate(o, ctx, m, 64))),
        ];
        CalibrationResults<BatesModel> result =
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

        foreach (System.Text.Json.JsonProperty output in c.Expected.EnumerateObject())
        {
            check.Output(c, output.Name, outputs[output.Name]);
        }
    }

    private static void Read(VectorAssert check, VectorCase c, BatesModel m, string prefix)
    {
        string Name(string n) => prefix.Length == 0 ? n : prefix + char.ToUpperInvariant(n[0]) + n[1..];
        check.Output(c, Name("theta"), m.Theta);
        check.Output(c, Name("kappa"), m.Kappa);
        check.Output(c, Name("sigma"), m.Sigma);
        check.Output(c, Name("rho"), m.Rho);
        check.Output(c, Name("v0"), m.V0);
        check.Output(c, Name("nu"), m.Nu);
        check.Output(c, Name("delta"), m.Delta);
        check.Output(c, Name("lambda"), m.Lambda);
    }
}
