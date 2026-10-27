// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.CalibrationCxxInputs;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's maximally homogeneous caplet calibration vectors,
/// <c>tests/vectors/cxx/models/marketmodels/models/capletcoterminalmaxhomogeneity.json</c>:
/// <see cref="CTSMMCapletMaxHomogeneityCalibration"/> on QuantLib's case and varied ones (the priority, the projection,
/// the factors, a displacement, caplets it can't fit, another abcd, annual rates), its results; the pass function
/// <see cref="CTSMMCapletMaxHomogeneityCalibration.CapletMaxHomogeneityCalibration"/>'s failures, swaption error and
/// pseudo-roots; and a priority outside [0, 1], refused.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CTSMMCapletMaxHomogeneityCalibrationCxxVectorTests
{
    [Fact]
    public void CTSMMCapletMaxHomogeneityCalibrationCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/models/capletcoterminalmaxhomogeneity");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var s = SpecOf(c.Inputs.GetProperty("spec"));
            double priority = c.Inputs.GetProperty("priority").GetDouble();
            int factors = c.Inputs.GetProperty("factors").GetInt32();
            if (c.Inputs.GetProperty("call").GetString() == "calibrate")
            {
                CheckCalibration(
                    check,
                    c,
                    () => new CTSMMCapletMaxHomogeneityCalibration(
                        s.Evolution, s.Corr, s.Variances, s.CapletVols, s.Cs, s.Displacement, priority),
                    calibration => calibration.Calibrate(
                        factors,
                        c.Inputs.GetProperty("maxIterations").GetInt32(),
                        c.Inputs.GetProperty("tolerance").GetDouble(),
                        c.Inputs.GetProperty("innerMaxIterations").GetInt32(),
                        c.Inputs.GetProperty("innerTolerance").GetDouble()));
            }
            else
            {
                CTSMMCapletCalibrationStep step = CTSMMCapletMaxHomogeneityCalibration.CapletMaxHomogeneityCalibration(
                    s.Evolution,
                    s.Corr,
                    s.Variances,
                    s.CapletVols,
                    s.Cs,
                    s.Displacement,
                    priority,
                    factors,
                    c.Inputs.GetProperty("maxIterations").GetInt32(),
                    c.Inputs.GetProperty("tolerance").GetDouble());
                check.Output(c, "failures", step.Failures);
                check.Output(c, "totalSwaptionError", step.TotalSwaptionError);
                check.Output(c, "swapCovariancePseudoRoots", step.SwapCovariancePseudoRoots().Select(Rows).ToArray());
            }
        }

        check.Complete();
    }
}
