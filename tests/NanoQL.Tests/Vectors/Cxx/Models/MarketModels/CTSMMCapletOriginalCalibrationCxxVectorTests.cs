// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.CalibrationCxxInputs;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's original caplet calibration vectors,
/// <c>tests/vectors/cxx/models/marketmodels/models/capletcoterminalswaptioncalibration.json</c>:
/// <see cref="CTSMMCapletOriginalCalibration"/> on QuantLib's case and varied ones (the roots, the full approximation,
/// α, the factors, a displacement, caplets it can't fit, another abcd, annual rates), its results; the pass function
/// <see cref="CTSMMCapletOriginalCalibration.CalibrationFunction"/>; and α short of the rates, refused.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CTSMMCapletOriginalCalibrationCxxVectorTests
{
    [Fact]
    public void CTSMMCapletOriginalCalibrationCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/models/capletcoterminalswaptioncalibration");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var s = SpecOf(c.Inputs.GetProperty("spec"));
            double[] alpha = Reals(c.Inputs.GetProperty("alpha"));
            bool lowestRoot = c.Inputs.GetProperty("lowestRoot").GetBoolean();
            bool useFullApprox = c.Inputs.GetProperty("useFullApprox").GetBoolean();
            int factors = c.Inputs.GetProperty("factors").GetInt32();
            if (c.Inputs.GetProperty("call").GetString() == "calibrate")
            {
                CheckCalibration(
                    check,
                    c,
                    () => new CTSMMCapletOriginalCalibration(
                        s.Evolution, s.Corr, s.Variances, s.CapletVols, s.Cs, s.Displacement, alpha, lowestRoot, useFullApprox),
                    calibration => calibration.Calibrate(
                        factors,
                        c.Inputs.GetProperty("maxIterations").GetInt32(),
                        c.Inputs.GetProperty("tolerance").GetDouble(),
                        c.Inputs.GetProperty("innerMaxIterations").GetInt32(),
                        c.Inputs.GetProperty("innerTolerance").GetDouble()));
            }
            else
            {
                CTSMMCapletCalibrationStep step = CTSMMCapletOriginalCalibration.CalibrationFunction(
                    s.Evolution, s.Corr, s.Variances, s.CapletVols, s.Cs, s.Displacement, alpha, lowestRoot, useFullApprox, factors);
                check.Output(c, "failures", step.Failures);
                check.Output(c, "swapCovariancePseudoRoots", step.SwapCovariancePseudoRoots().Select(Rows).ToArray());
            }
        }

        check.Complete();
    }
}
