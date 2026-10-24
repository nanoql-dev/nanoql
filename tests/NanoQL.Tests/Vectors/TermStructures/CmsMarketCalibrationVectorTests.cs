// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CMS market calibration's vectors,
/// <c>tests/vectors/termstructures/volatility/swaption/cmsmarketcalibration.json</c>: cms.cpp's SABR cube and CMS
/// market calibrated on spreads and on prices with the reversion free, and on a matrix and a parametric guess with the
/// reversion fixed, on the analytic Hagan pricer's standard model; the result, the error, the end criteria, and the
/// results' cube's sparse parameters and market's browse table, QuantLib's members after <c>compute</c>, under the
/// stability filter.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CmsMarketCalibrationVectorTests
{
    [Fact]
    public void CmsMarketCalibrationVector()
    {
        var file = VectorFile.Load("termstructures/volatility/swaption/cmsmarketcalibration");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (double[] result, double error, EndCriteriaType endCriteria, double[] sparse, double[] browse, double[]? dense) =
                Calibrate(c);
            check.Output(c, "result", CubeInputs.Kept(c, "result", result));
            check.Output(c, "error", CubeInputs.Kept(c, "error", new[] { error }));
            check.Output(c, "endCriteria", CubeInputs.Kept(c, "endCriteria", new[] { (int)endCriteria }));
            check.Output(c, "sparseSabrParameters", CubeInputs.Kept(c, "sparseSabrParameters", sparse));
            check.Output(c, "browse", CubeInputs.Kept(c, "browse", browse));
            if (c.Expected.TryGetProperty("denseSabrParameters", out _))
            {
                check.Output(c, "denseSabrParameters", CubeInputs.Kept(c, "denseSabrParameters", dense!));
            }
        }

        check.Complete();
    }

    /// <summary>The calibration a case describes, on its own cube and market.</summary>
    /// <param name="c">The case.</param>
    /// <returns>The result, flattened row by row, the error, the end criteria, and the results' cube's sparse
    /// parameters, market's browse table and dense parameters, null for none, each flattened row by row.</returns>
    internal static (double[] Result, double Error, EndCriteriaType EndCriteria, double[] Sparse, double[] Browse, double[]? Dense)
        Calibrate(VectorCase c)
    {
        CmsMarketCalibration calibration = CalibrationOf(c, out EvaluationContext context);
        var endCriteria = new EndCriteria(20, 10, 1e-6, 1e-6, 1e-6);
        var method = new LevenbergMarquardt(1e-8, 1e-8, 1e-8);
        bool isFixed = c.Input("isMeanReversionFixed").GetBoolean();
        JsonElement m = c.Input("meanReversionGuess");
        double? meanReversionGuess = m.ValueKind == JsonValueKind.Null ? null : m.GetDouble();
        switch (c.Input("guessKind").GetString())
        {
            case "vector":
            {
                CmsMarketCalibrationResults<Vector> r = calibration.Compute(
                    endCriteria, method, new Vector(VolatilityInputs.Reals(c.Input("guess"))), isFixed, context);
                return (r.Result.ToArray(), r.Error, r.EndCriteria, CubeInputs.Flat(r.SparseSabrParameters), CubeInputs.Flat(r.CmsMarket.Browse()), Dense(r));
            }

            case "matrix":
            {
                CmsMarketCalibrationResults<Matrix> r = calibration.Compute(
                    endCriteria, method, CmsMarketVectorTests.MatrixOf(c.Input("guess")), isFixed, context, meanReversionGuess);
                return (CubeInputs.Flat(r.Result), r.Error, r.EndCriteria, CubeInputs.Flat(r.SparseSabrParameters), CubeInputs.Flat(r.CmsMarket.Browse()), Dense(r));
            }

            default:
            {
                CmsMarketCalibrationResults<Matrix> r = calibration.ComputeParametric(
                    endCriteria, method, CmsMarketVectorTests.MatrixOf(c.Input("guess")), isFixed, context, meanReversionGuess);
                return (CubeInputs.Flat(r.Result), r.Error, r.EndCriteria, CubeInputs.Flat(r.SparseSabrParameters), CubeInputs.Flat(r.CmsMarket.Browse()), Dense(r));
            }
        }
    }

    // The results' dense parameters, flattened row by row, or null for a cube that has none.
    private static double[]? Dense<TResult>(CmsMarketCalibrationResults<TResult> r)
        where TResult : class =>
        r.DenseSabrParameters is Matrix m ? CubeInputs.Flat(m) : null;

    /// <summary>The calibration a case describes: its cube, its market on its pricer on the cube's at-the-money matrix,
    /// its weights and type; or, where given, other bid and ask spreads, another pricer or another type.</summary>
    /// <param name="c">The case.</param>
    /// <param name="context">The case's context.</param>
    /// <param name="bidAskSpreads">The bid and ask spreads, or <see langword="null"/> for the case's.</param>
    /// <param name="pricer">The pricer on the at-the-money matrix, or <see langword="null"/> for the case's.</param>
    /// <param name="calibrationType">The calibration type, or <see langword="null"/> for the case's.</param>
    /// <returns>The calibration.</returns>
    internal static CmsMarketCalibration CalibrationOf(
        VectorCase c,
        out EvaluationContext context,
        IEnumerable<IEnumerable<double>>? bidAskSpreads = null,
        Func<SwaptionVolatilityStructure, CmsCouponPricer>? pricer = null,
        CmsMarketCalibrationCalibrationType? calibrationType = null)
    {
        context = c.Context();
        JsonElement cubeSpec = c.Input("cube");
        var cube = (XabrSwaptionVolatilityCube<SwaptionVolCubeSabrModel>)CubeInputs.CubeOf(cubeSpec, context);
        SwaptionVolatilityStructure atm = CubeInputs.AtmOf(cubeSpec.GetProperty("atm"), context);
        SwapIndex index = SwaptionInputs.SwapIndexOf(c.Input("index"));
        CmsCouponPricer cmsPricer = pricer is null ? CmsMarketVectorTests.PricerOf(c.Input("pricer"), atm) : pricer(atm);
        var market = new CmsMarket(
            CubeInputs.Tenors(c.Input("swapLengths")),
            [index],
            index.IborIndex,
            bidAskSpreads ?? CubeInputs.Rows(c.Input("bidAskSpreads")),
            [cmsPricer],
            index.ForwardingTermStructure!,
            context);
        return new CmsMarketCalibration(
            cube,
            market,
            CmsMarketVectorTests.MatrixOf(c.Input("weights")),
            calibrationType ?? Enum.Parse<CmsMarketCalibrationCalibrationType>(c.Input("calibrationType").GetString()!));
    }
}
