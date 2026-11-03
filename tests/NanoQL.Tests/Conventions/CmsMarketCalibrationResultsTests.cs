// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="CmsMarketCalibration"/>'s results: QuantLib's <c>compute</c> recalibrates the cube it was given and
/// reprices its market in place, leaving its outputs in public members; nanoql's returns a
/// <see cref="CmsMarketCalibrationResults{TResult}"/> holding the recalibrated cube, its dense parameters where it has
/// them, and the market repriced on it, and the calibration's cube and market stand.
/// </summary>
public class CmsMarketCalibrationResultsTests
{
    [Fact]
    public void TheResultsHoldTheRecalibratedCubeAndItsMarket()
    {
        (CmsMarketCalibration calibration, var cube, CmsMarket market, EvaluationContext context) = Calibration();
        CmsMarketCalibrationResults<Vector> r = calibration.Compute(Criteria, Method, new Vector([0.5, 0.01]), false, context);
        Assert.NotSame(cube, r.VolCube);
        Assert.NotSame(market, r.CmsMarket);
        Assert.Equal(CubeInputs.Flat(r.VolCube.SparseSabrParameters()), CubeInputs.Flat(r.SparseSabrParameters));
        Assert.Null(r.DenseSabrParameters);
        Assert.Equal(
            CubeInputs.Flat(market.Reprice(r.VolCube, r.Result[1], context).Browse()), CubeInputs.Flat(r.CmsMarket.Browse()));
    }

    [Fact]
    public void OnAnAtmCalibratedCubeTheResultsHoldItsDenseParameters()
    {
        // the update QuantLib's compute ends with refits the dense grid, which the results hold
        VectorCase c = VectorFile.Load("termstructures/volatility/swaption/cmsmarketcalibration").Cases
            .Single(x => x.Name == "Standard, OnSpread, free reversion, ATM-calibrated cube");
        CmsMarketCalibration calibration = CmsMarketCalibrationVectorTests.CalibrationOf(c, out EvaluationContext context);
        CmsMarketCalibrationResults<Vector> r = calibration.Compute(Criteria, Method, new Vector([0.5, 0.01]), false, context);
        Assert.NotNull(r.DenseSabrParameters);
        Assert.Equal(CubeInputs.Flat(r.VolCube.DenseSabrParameters()), CubeInputs.Flat(r.DenseSabrParameters));
    }

    [Fact]
    public void TheCalibrationsCubeAndMarketStand()
    {
        (CmsMarketCalibration calibration, var cube, CmsMarket market, EvaluationContext context) = Calibration();
        double[] parameters = CubeInputs.Flat(cube.SparseSabrParameters());
        double[] browse = CubeInputs.Flat(market.Browse());
        CmsMarketCalibrationResults<Vector> first = calibration.Compute(Criteria, Method, new Vector([0.5, 0.01]), false, context);
        Assert.Equal(parameters, CubeInputs.Flat(cube.SparseSabrParameters()));
        Assert.Equal(browse, CubeInputs.Flat(market.Browse()));

        // a second compute starts from the same cube and market, so it gives the same results
        CmsMarketCalibrationResults<Vector> second = calibration.Compute(Criteria, Method, new Vector([0.5, 0.01]), false, context);
        Assert.Equal(first.Result.ToArray(), second.Result.ToArray());
        Assert.Equal(first.Error, second.Error);
    }

    private static EndCriteria Criteria => new(20, 10, 1e-6, 1e-6, 1e-6);

    private static LevenbergMarquardt Method => new(1e-8, 1e-8, 1e-8);

    // The calibration vectors' first case, its cube and market held.
    private static (CmsMarketCalibration Calibration, XabrSwaptionVolatilityCube<SwaptionVolCubeSabrModel> Cube, CmsMarket Market,
        EvaluationContext Context) Calibration()
    {
        VectorCase c = VectorFile.Load("termstructures/volatility/swaption/cmsmarketcalibration").Cases[0];
        EvaluationContext context = c.Context();
        var cube = (XabrSwaptionVolatilityCube<SwaptionVolCubeSabrModel>)CubeInputs.CubeOf(c.Input("cube"), context);
        SwapIndex index = SwaptionInputs.SwapIndexOf(c.Input("index"));
        SwaptionVolatilityStructure atm = CubeInputs.AtmOf(c.Input("cube").GetProperty("atm"), context);
        var market = new CmsMarket(
            CubeInputs.Tenors(c.Input("swapLengths")),
            [index],
            index.IborIndex,
            CubeInputs.Rows(c.Input("bidAskSpreads")),
            [CmsMarketVectorTests.PricerOf(c.Input("pricer"), atm)],
            index.ForwardingTermStructure!,
            context);
        var calibration = new CmsMarketCalibration(
            cube, market, CmsMarketVectorTests.MatrixOf(c.Input("weights")), CmsMarketCalibrationCalibrationType.OnSpread);
        return (calibration, cube, market, context);
    }
}
