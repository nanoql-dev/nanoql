// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The calibration vectors, <c>tests/vectors/models/model.json</c>: Hull-White and Vasicek calibrated to swaption
/// helpers with Levenberg-Marquardt and Simplex, with weights, fixed parameters and added constraints; each with its
/// parameters, end criterion, problem values and number of function evaluations, the helpers' errors under the
/// calibrated model, and the cost at its parameters and others; and the refusals of <see cref="CalibratedModelExtensions.Calibrate"/>
/// and <see cref="CalibratedModelExtensions.Value"/>. A calibration's outputs are those the master plan's S7R6 filter
/// keeps, each list element its own output ("params[0]"), so each case is compared on the outputs it holds.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ModelVectorTests
{
    [Fact]
    public void ModelVector()
    {
        var file = VectorFile.Load("models/model");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CheckCalibration(check, c);
        }

        check.Complete();
    }

    /// <summary>
    /// Compares a calibration case's outputs, those it holds, or a refusal or value case's (the model files'
    /// calibrations of <c>tools/vectors/gen/models/shortrate/onefactormodels/</c>, which take this file's machinery).
    /// </summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    internal static void CheckCalibration(VectorAssert check, VectorCase c)
    {
        EvaluationContext context = c.Context();
        OneFactorAffineModel model = CalibrationInputs.ModelOf(c.Input("model"));
        bool engine = !c.Inputs.TryGetProperty("engine", out JsonElement e) || e.GetBoolean();
        List<CalibrationHelper> helpers = [.. c.Input("helpers").EnumerateArray().Select(spec => Helper(spec, model, engine, context))];
        Constraint? constraint = c.Inputs.TryGetProperty("constraint", out JsonElement cs) ? OptimizationSpecs.Constraint(cs) : null;
        double[] weights = c.Inputs.TryGetProperty("weights", out JsonElement ws) ? CouponInputs.Reals(ws) : [];
        bool[] fixParameters = c.Inputs.TryGetProperty("fixParameters", out JsonElement fs)
            ? [.. fs.EnumerateArray().Select(f => f.GetBoolean())]
            : [];
        OptimizationMethod method = CalibrationInputs.MethodOf(c.Input("method"));
        EndCriteria endCriteria = OptimizationSpecs.EndCriteria(c.Input("endCriteria"));

        if (c.Expected.TryGetProperty("calibrate", out _))
        {
            check.Output(c, "calibrate", () => model.Calibrate(helpers, method, endCriteria, context, constraint, weights, fixParameters).Model.Params());
            return;
        }

        // A calibration's valueParams are the two parameter sets value() reads after it; a value case's, one set.
        JsonElement valueParams = c.Input("valueParams");
        if (valueParams.GetArrayLength() == 0 || valueParams[0].ValueKind != JsonValueKind.Array)
        {
            Vector parameters = new(CouponInputs.Reals(c.Input("valueParams")));
            check.Output(c, "value", () => model.Value(parameters, helpers, context));
            return;
        }

        CalibrationResults<OneFactorAffineModel> result =
            model.Calibrate(helpers, method, endCriteria, context, constraint, weights, fixParameters);
        OneFactorAffineModel calibrated = result.Model;
        var outputs = new Dictionary<string, object?>
        {
            ["endCriteria"] = result.EndCriteria,
            ["functionEvaluation"] = result.FunctionEvaluation,
            ["value"] = calibrated.Value(calibrated.Params(), helpers, context),
        };
        Elements(outputs, "params", calibrated.Params().AsSpan().ToArray());
        Elements(outputs, "problemValues", [.. result.ProblemValues]);
        Elements(outputs, "calibrationErrors", [.. helpers.Select(h => h.CalibrationError(calibrated, context))]);
        Elements(
            outputs,
            "values",
            [.. c.Input("valueParams").EnumerateArray().Select(p => calibrated.Value(new Vector(CouponInputs.Reals(p)), helpers, context))]);
        foreach (JsonProperty output in c.Expected.EnumerateObject())
        {
            check.Output(c, output.Name, outputs[output.Name]);
        }

        // The receiver doesn't change.
        Assert.Equal(CalibrationInputs.ModelOf(c.Input("model")).Params(), model.Params());
    }

    // A list output's elements, each its own output named with its index, as the filter writes them.
    private static void Elements(Dictionary<string, object?> outputs, string name, double[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            outputs[$"{name}[{i}]"] = values[i];
        }
    }

    // A helper priced by the Jamshidian engine on the model it is given; a model with no curve of its own (a Vasicek, a
    // Cox-Ingersoll-Ross) with the helper's.
    private static CalibrationHelper Helper(JsonElement spec, OneFactorAffineModel model, bool engine, EvaluationContext context)
    {
        SwaptionHelper helper = CalibrationInputs.SwaptionHelperOf(spec, context);
        if (!engine)
        {
            return helper;
        }

        return CalibrationInputs.WithJamshidian(
            helper, model is ITermStructureConsistentModel ? null : CashFlowInputs.CurveOf(spec.GetProperty("termStructure")));
    }
}
