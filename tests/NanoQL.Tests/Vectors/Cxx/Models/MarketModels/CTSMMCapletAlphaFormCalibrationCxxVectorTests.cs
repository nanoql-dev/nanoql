// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.CalibrationCxxInputs;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's alpha-form caplet calibration vectors,
/// <c>tests/vectors/cxx/models/marketmodels/models/capletcoterminalalphacalibration.json</c>:
/// <see cref="CTSMMCapletAlphaFormCalibration"/> on QuantLib's case and varied ones (the inverse-linear form, the
/// bounds, the initial α, the factors, another abcd, annual rates, and three with no α fitting a caplet, which throw),
/// its results and α; the pass function
/// <see cref="CTSMMCapletAlphaFormCalibration.CapletAlphaFormCalibration"/>'s α, a, b (where each step's final part
/// succeeds) and pseudo-roots; and bounds short of the rates, refused.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CTSMMCapletAlphaFormCalibrationCxxVectorTests
{
    [Fact]
    public void CTSMMCapletAlphaFormCalibrationCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/models/capletcoterminalalphacalibration");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var s = SpecOf(c.Inputs.GetProperty("spec"));
            double[] alphaInitial = Reals(c.Inputs.GetProperty("alphaInitial"));
            double[] alphaMax = Reals(c.Inputs.GetProperty("alphaMax"));
            double[] alphaMin = Reals(c.Inputs.GetProperty("alphaMin"));
            bool maximizeHomogeneity = c.Inputs.GetProperty("maximizeHomogeneity").GetBoolean();
            AlphaForm form = c.Inputs.GetProperty("form").GetString() switch
            {
                "LinearHyperbolic" => new AlphaFormLinearHyperbolic(s.Evolution.RateTimes.AsSpan()),
                "InverseLinear" => new AlphaFormInverseLinear(s.Evolution.RateTimes.AsSpan()),
                var other => throw new InvalidOperationException($"unknown alpha form {other}"),
            };
            int factors = c.Inputs.GetProperty("factors").GetInt32();
            if (c.Inputs.GetProperty("call").GetString() == "calibrate")
            {
                CheckCalibration(
                    check,
                    c,
                    () => new CTSMMCapletAlphaFormCalibration(
                        s.Evolution, s.Corr, s.Variances, s.CapletVols, s.Cs, s.Displacement, alphaInitial, alphaMax, alphaMin,
                        maximizeHomogeneity, form),
                    calibration => calibration.Calibrate(
                        factors,
                        c.Inputs.GetProperty("maxIterations").GetInt32(),
                        c.Inputs.GetProperty("tolerance").GetDouble(),
                        c.Inputs.GetProperty("innerMaxIterations").GetInt32(),
                        c.Inputs.GetProperty("innerTolerance").GetDouble()));
            }
            else
            {
                CTSMMCapletCalibrationStep step = CTSMMCapletAlphaFormCalibration.CapletAlphaFormCalibration(
                    s.Evolution,
                    s.Corr,
                    s.Variances,
                    s.CapletVols,
                    s.Cs,
                    s.Displacement,
                    alphaInitial,
                    alphaMax,
                    alphaMin,
                    maximizeHomogeneity,
                    form,
                    factors,
                    c.Inputs.GetProperty("steps").GetInt32(),
                    c.Inputs.GetProperty("tolerance").GetDouble());
                if (c.Expected.TryGetProperty("failures", out _))
                {
                    check.Output(c, "failures", step.Failures);
                }

                check.Output(c, "alpha", step.Alpha);
                check.Output(c, "a", step.A);
                if (c.Expected.TryGetProperty("b", out _))
                {
                    check.Output(c, "b", step.B);
                }
                check.Output(c, "swapCovariancePseudoRoots", step.SwapCovariancePseudoRoots().Select(Rows).ToArray());
            }
        }

        check.Complete();
    }
}
