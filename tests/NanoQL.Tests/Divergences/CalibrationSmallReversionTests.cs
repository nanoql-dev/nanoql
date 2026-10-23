// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Hull-White calibrations that reach small mean reversion (the S-sr plan's SSR3, and the user's ruling of 2026-10-22
/// on its premise): QuantLib 1.44's Vasicek B and A cut to their a = 0 forms below √ε and cancel above it
/// (<c>vasicek.cpp:36-58</c>), and nanoql's are QuantLib's master's, free of the cancellation (#2882). Where a
/// calibration's optimum lies near a = 0, QuantLib's optimizer reads the cancelling forms: its calibrated parameters and
/// the cost it reports there are 1.44's arithmetic, not the model's, and a start that 1.44's cancellation turns away
/// from a = 0 reaches the boundary there instead. The calibrations are <c>models/model.json</c>'s, on their inputs;
/// that file keeps the outputs S7R6's filter finds stable. QuantLib's check: its cost where its forms hold, at an
/// ordinary reversion, equal to nanoql's within the tolerance, and its own interior minimum, which nanoql's cost has too.
/// </summary>
public class CalibrationSmallReversionTests
{
    [Fact]
    public void ACalibrationAtSmallReversionReportsTheCostOfItsParameters()
    {
        // "2025 normal / Simplex": QuantLib ends at a = 1.635e-6 and reports the cost 0.0017685872905640779 there; the
        // model's cost at those parameters is 0.0031536, continuous down to a = 1e-12, and nanoql's own calibration
        // ends below it.
        (OneFactorAffineModel model, List<CalibrationHelper> helpers, EvaluationContext context, VectorCase c) = Calibration("2025 normal / Simplex");
        double atQuantLibs = model.Value(new Vector([1.635052558138888e-06, 0.004918132922846474]), helpers, context);
        double nearZero = model.Value(new Vector([1e-12, 0.004918132922846474]), helpers, context);
        Assert.True(atQuantLibs > 1.7 * 0.0017685872905640779, $"{atQuantLibs}");
        Assert.True(Math.Abs(nearZero - atQuantLibs) <= 1e-4 * atQuantLibs, $"{nearZero} against {atQuantLibs}");
        Assert.True(Calibrate(model, helpers, context, c).Cost < atQuantLibs);

        // Where QuantLib's forms hold, a = 0.05 ("2025 normal / fixed reversion"), the costs agree.
        Assert.True(Tolerance.Within(0.0036569943645343933, model.Value(new Vector([0.05, 0.005835286244636059]), helpers, context)));
    }

    [Fact]
    public void AFarStartReachesTheBoundaryMinimum()
    {
        // "2002 / start 0.2, 0.002": QuantLib's Levenberg-Marquardt ends at the interior minimum (0.0464063, 0.0057992),
        // cost 0.11579995302631445; nanoql's at a ≈ 8.6e-9, cost 0.8235, the boundary, where 1.44's cancelling forms
        // sat between. The interior minimum is nanoql's too: its cost there is QuantLib's, and the start (0.03, 0.012)
        // reaches it (QuantLib's 0.11579995302652267).
        (OneFactorAffineModel model, List<CalibrationHelper> helpers, EvaluationContext context, VectorCase c) = Calibration("2002 / start 0.2, 0.002");
        (Vector parameters, double cost) = Calibrate(model, helpers, context, c);
        Assert.True(parameters[0] < 1e-6, $"{parameters[0]}");
        Assert.True(cost > 0.8, $"{cost}");
        Assert.True(Tolerance.Within(0.11579995302631445, model.Value(new Vector([0.04640630316220947, 0.005799156983748529]), helpers, context)));

        (OneFactorAffineModel near, List<CalibrationHelper> nearHelpers, EvaluationContext nearContext, VectorCase nearCase) = Calibration("2002 / start 0.03, 0.012");
        Assert.True(Tolerance.Within(0.11579995302652267, Calibrate(near, nearHelpers, nearContext, nearCase).Cost));
    }

    private static (OneFactorAffineModel Model, List<CalibrationHelper> Helpers, EvaluationContext Context, VectorCase Case) Calibration(string name)
    {
        VectorCase c = VectorFile.Load("models/model").Cases.Single(x => x.Name == name);
        EvaluationContext context = c.Context();
        OneFactorAffineModel model = CalibrationInputs.ModelOf(c.Input("model"));
        List<CalibrationHelper> helpers = [.. c.Input("helpers").EnumerateArray().Select(spec =>
            (CalibrationHelper)CalibrationInputs.WithJamshidian(CalibrationInputs.SwaptionHelperOf(spec, context), null))];
        return (model, helpers, context, c);
    }

    private static (Vector Parameters, double Cost) Calibrate(
        OneFactorAffineModel model, List<CalibrationHelper> helpers, EvaluationContext context, VectorCase c)
    {
        Constraint? constraint = c.Inputs.TryGetProperty("constraint", out JsonElement cs) ? OptimizationSpecs.Constraint(cs) : null;
        CalibrationResults<OneFactorAffineModel> result = model.Calibrate(
            helpers, CalibrationInputs.MethodOf(c.Input("method")), OptimizationSpecs.EndCriteria(c.Input("endCriteria")), context, constraint);
        return (result.Model.Params(), result.Model.Value(result.Model.Params(), helpers, context));
    }
}
