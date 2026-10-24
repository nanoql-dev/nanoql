// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The SABR cube's recalibration (the S-cms plan's CMR3 (ii)): QuantLib's <c>recalibration</c> overloads set a swap
/// tenor's β guesses on the cube and refit its sections in place, and <c>updateAfterRecalibration</c> refits the dense
/// grid; nanoql's each return a new cube with both applied, and the receiver's parameters and volatilities stand.
/// </summary>
public class XabrCubeRecalibrationTests
{
    [Fact]
    public void EachOverloadReturnsANewCube()
    {
        (XabrSwaptionVolatilityCube<SwaptionVolCubeSabrModel> cube, Period swapTenor, EvaluationContext context) = Cube();
        double[] parameters = CubeInputs.Flat(cube.SparseSabrParameters());
        double volatility = cube.Volatility(new Period(5, TimeUnit.Years), swapTenor, 0.04, true);
        int n = cube.OptionTenors.Length;
        foreach (XabrSwaptionVolatilityCube<SwaptionVolCubeSabrModel> recalibrated in new[]
                 {
                     cube.Recalibration(0.3, swapTenor, context),
                     cube.Recalibration(Enumerable.Repeat(0.3, n), swapTenor, context),
                     cube.Recalibration(
                         [new Period(1, TimeUnit.Years), new Period(10, TimeUnit.Years)], [0.3, 0.3], swapTenor, context),
                 })
        {
            Assert.NotSame(cube, recalibrated);
            Assert.NotEqual(parameters, CubeInputs.Flat(recalibrated.SparseSabrParameters()));
            Assert.NotEqual(volatility, recalibrated.Volatility(new Period(5, TimeUnit.Years), swapTenor, 0.04, true));
        }

        Assert.Equal(parameters, CubeInputs.Flat(cube.SparseSabrParameters()));
        Assert.Equal(volatility, cube.Volatility(new Period(5, TimeUnit.Years), swapTenor, 0.04, true));
    }

    [Fact]
    public void TheOverloadsAgreeOnAFlatBeta()
    {
        (XabrSwaptionVolatilityCube<SwaptionVolCubeSabrModel> cube, Period swapTenor, EvaluationContext context) = Cube();
        double[] one = CubeInputs.Flat(cube.Recalibration(0.3, swapTenor, context).SparseSabrParameters());
        Assert.Equal(one, CubeInputs.Flat(cube.Recalibration(Enumerable.Repeat(0.3, cube.OptionTenors.Length), swapTenor, context).SparseSabrParameters()));
    }

    // The calibration vectors' SABR cube, with beta fixed, and its market's swap tenor.
    private static (XabrSwaptionVolatilityCube<SwaptionVolCubeSabrModel> Cube, Period SwapTenor, EvaluationContext Context) Cube()
    {
        VectorCase c = VectorFile.Load("termstructures/volatility/swaption/cmsmarketcalibration").Cases[0];
        EvaluationContext context = c.Context();
        var cube = (XabrSwaptionVolatilityCube<SwaptionVolCubeSabrModel>)CubeInputs.CubeOf(c.Input("cube"), context);
        return (cube, SwaptionInputs.SwapIndexOf(c.Input("index")).Tenor, context);
    }
}
