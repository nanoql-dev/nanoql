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
/// and <see cref="CalibratedModelExtensions.Value"/>.
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
                continue;
            }

            if (!c.Expected.TryGetProperty("params", out _))
            {
                Vector parameters = new(CouponInputs.Reals(c.Input("valueParams")));
                check.Output(c, "value", () => model.Value(parameters, helpers, context));
                continue;
            }

            CalibrationResults<OneFactorAffineModel> result =
                model.Calibrate(helpers, method, endCriteria, context, constraint, weights, fixParameters);
            OneFactorAffineModel calibrated = result.Model;
            check.Output(c, "params", calibrated.Params());
            check.Output(c, "endCriteria", result.EndCriteria);
            check.Output(c, "problemValues", result.ProblemValues);
            check.Output(c, "functionEvaluation", result.FunctionEvaluation);
            check.Output(c, "calibrationErrors", helpers.Select(h => (object?)h.CalibrationError(calibrated, context)));
            check.Output(c, "value", calibrated.Value(calibrated.Params(), helpers, context));
            check.Output(
                c,
                "values",
                c.Input("valueParams").EnumerateArray().Select(p => (object?)calibrated.Value(new Vector(CouponInputs.Reals(p)), helpers, context)));

            // The receiver doesn't change.
            Assert.Equal(CalibrationInputs.ModelOf(c.Input("model")).Params(), model.Params());
        }

        check.Complete();
    }

    // A helper priced by the Jamshidian engine on the model it is given; a Vasicek with the helper's own curve.
    private static CalibrationHelper Helper(JsonElement spec, OneFactorAffineModel model, bool engine, EvaluationContext context)
    {
        SwaptionHelper helper = CalibrationInputs.SwaptionHelperOf(spec, context);
        if (!engine)
        {
            return helper;
        }

        return CalibrationInputs.WithJamshidian(
            helper, model is HullWhite ? null : CashFlowInputs.CurveOf(spec.GetProperty("termStructure")));
    }
}
