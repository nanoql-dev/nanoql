// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A fixed reversion given to the CMS market calibration: QuantLib passes it through the transform's round trip, √(m²)
/// = |m| (<c>cmsmarketcalibration.cpp:362-365</c> with <c>:188-191</c>; the matrix and parametric overloads
/// <c>:430-433</c>, <c>:520-523</c>), and reports m (<c>:373-374</c>), so a negative reversion prices as its absolute
/// value: on the wheel, on the vectors' market with the linear TSR pricer, a fixed −0.03 fits to an error of
/// 0.010816623721669666, +0.03's, where QuantLib's own fixed path with no reversion given, on pricers built at −0.03,
/// fits to 0.010817563120824606. nanoql passes the reversion as given. QuantLib's check: that path, the pricers' own
/// reversion repriced as it stands (<c>:188-189</c>); here on nanoql's pricers, whose TSR prices on the SABR cube
/// differ from QuantLib's.
/// </summary>
public class CmsCalibrationFixedReversionTests
{
    [Fact]
    public void AGivenNegativeReversionPricesAsGiven()
    {
        CmsMarketCalibrationResults<Vector> given = Calibration(0.0, out EvaluationContext context)
            .Compute(Criteria, Method, new Vector([0.5, -0.03]), true, context);
        CmsMarketCalibrationResults<Vector> none = Calibration(-0.03, out context).Compute(Criteria, Method, new Vector([0.5]), true, context);
        Assert.Equal(-0.03, given.Result[1]);
        Assert.Equal((none.Result[0], none.Error), (given.Result[0], given.Error));
        Assert.Equal(CubeInputs.Flat(none.CmsMarket.ImpliedCmsSpreads()), CubeInputs.Flat(given.CmsMarket.ImpliedCmsSpreads()));

        CmsMarketCalibrationResults<Vector> positive = Calibration(0.0, out context).Compute(Criteria, Method, new Vector([0.5, 0.03]), true, context);
        Assert.NotEqual(positive.Error, given.Error);
    }

    [Fact]
    public void TheMatrixAndParametricOverloadsPassItAsGiven()
    {
        var matrixGuess = new Matrix(4, 1, 0.5);
        CmsMarketCalibrationResults<Matrix> given = Calibration(0.0, out EvaluationContext context)
            .Compute(Criteria, Method, matrixGuess, true, context, -0.03);
        CmsMarketCalibrationResults<Matrix> none = Calibration(-0.03, out context).Compute(Criteria, Method, matrixGuess, true, context);
        Assert.Equal(Column(none.Result, 0), Column(given.Result, 0));
        Assert.Equal(none.Error, given.Error);
        Assert.All(Column(given.Result, 1), m => Assert.Equal(-0.03, m));

        var parametricGuess = new Matrix(new[,] { { 0.5 }, { 0.5 }, { 0.1 } });
        given = Calibration(0.0, out context).ComputeParametric(Criteria, Method, parametricGuess, true, context, -0.03);
        none = Calibration(-0.03, out context).ComputeParametric(Criteria, Method, parametricGuess, true, context);
        Assert.Equal(Column(none.Result, 0), Column(given.Result, 0));
        Assert.Equal(none.Error, given.Error);
        Assert.All(Column(given.Result, 1), m => Assert.Equal(-0.03, m));
    }

    private static EndCriteria Criteria => new(20, 10, 1e-6, 1e-6, 1e-6);

    private static LevenbergMarquardt Method => new(1e-8, 1e-8, 1e-8);

    // The vectors' calibration on spreads, its pricer the linear TSR pricer at a reversion.
    private static CmsMarketCalibration Calibration(double reversion, out EvaluationContext context) =>
        CmsMarketCalibrationVectorTests.CalibrationOf(
            VectorFile.Load("termstructures/volatility/swaption/cmsmarketcalibration").Cases[0],
            out context,
            pricer: atm => new LinearTsrPricer(atm, reversion));

    private static double[] Column(Matrix m, int j) => [.. Enumerable.Range(0, m.Rows).Select(i => m[i, j])];
}
