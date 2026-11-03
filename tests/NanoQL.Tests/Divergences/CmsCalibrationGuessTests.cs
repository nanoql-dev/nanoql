// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The CMS market calibration's guess with the reversion fixed: QuantLib's vector overload passes the betas to the
/// optimizer untransformed on that path alone (<c>cmsmarketcalibration.cpp:359-366</c>), where its free path and its
/// matrix and parametric overloads transform them (<c>:379-381</c>, <c>:426-427</c>), and reads the result back through
/// the transform (<c>:372</c>), so a guess of β starts the fit at e^(−β²): on the wheel, on the vectors' market quoting
/// the spreads the cube's own fit implies (0.0017046615749698334, 0.0018719617461963108, 0.0020272382060141435,
/// 0.0022422177072925265), the fit from a guess of 0.5 returns a beta of 0.5000000003809772 and an error of
/// 4.659445968967119e-16, and on those spreads 2bp lower a beta of 0.9999985865285671, another fit. nanoql transforms
/// the guess on every path. QuantLib's check: its vector overload given the guess's transform, √(−ln 0.5), which starts
/// the fit at a beta of 0.5: there a beta of 0.5000000000000001 and an error of 0, and 2bp lower 1e-06 and
/// 0.00019999259008147676. The matrix overload, which transforms, calibrates a beta per swap length, another problem on
/// this market, and refuses a market of one length ("not enough points to interpolate").
/// </summary>
public class CmsCalibrationGuessTests
{
    private static readonly double[] Implied =
        [0.0017046615749698334, 0.0018719617461963108, 0.0020272382060141435, 0.0022422177072925265];

    [Fact]
    public void TheFixedPathStartsAtTheGuess()
    {
        CmsMarketCalibrationResults<Vector> r = Fit(0.0);
        Assert.True(Math.Abs(r.Result[0] - 0.5) <= 1e-12, $"{r.Result[0]}");
        Assert.Equal(0.01, r.Result[1]);
        Assert.True(r.Error <= 1e-20, $"{r.Error}");
    }

    [Fact]
    public void FromTheGuessItFindsQuantLibsFit()
    {
        CmsMarketCalibrationResults<Vector> r = Fit(-0.0002);
        Assert.Equal(1e-06, r.Result[0]);
        Assert.True(Math.Abs(r.Error - 0.00019999259008147676) <= 1e-12 * r.Error, $"{r.Error}");
    }

    // The fixed-reversion fit from a beta of 0.5 at a reversion of 1%, on the implied spreads moved by an offset.
    private static CmsMarketCalibrationResults<Vector> Fit(double offset)
    {
        VectorCase c = VectorFile.Load("termstructures/volatility/swaption/cmsmarketcalibration").Cases[0];
        double[][] spreads = [.. Implied.Select(s => new[] { s + offset, s + offset })];
        CmsMarketCalibration calibration = CmsMarketCalibrationVectorTests.CalibrationOf(c, out EvaluationContext context, spreads);
        return calibration.Compute(
            new EndCriteria(20, 10, 1e-6, 1e-6, 1e-6), new LevenbergMarquardt(1e-8, 1e-8, 1e-8), new Vector([0.5, 0.01]), true, context);
    }
}
