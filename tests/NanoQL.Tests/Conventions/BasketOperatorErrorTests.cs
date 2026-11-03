// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.Fd2dMismatchedTimesTests;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception type, message and parameter name of the refusals of the basket core's operators, its instrument and
/// the process array's in-place step, each QuantLib's text: the 2-D Black-Scholes operator in a third direction, the
/// Wiener operator on a mesher and eigenvalues of different dimensions, a multi-asset option given results without its
/// Greeks, and an in-place step on increments of the wrong size (QuantLib's <c>Matrix * Array</c> check, which its
/// allocating form reads).
/// </summary>
public class BasketOperatorErrorTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void TheTwoDimensionalOperatorHasTwoDirections()
    {
        GeneralizedBlackScholesProcess p1 = Process(100.0, Flat(0.02), Flat(0.05), Vol(0.25));
        GeneralizedBlackScholesProcess p2 = Process(95.0, Flat(0.03), Flat(0.05), Vol(0.3));
        var mesher = new FdmMesherComposite(
            new FdmBlackScholesMesher(15, p1, 1.0, 100.0), new FdmBlackScholesMesher(13, p2, 1.0, 95.0));
        var op = new Fdm2dBlackScholesOp(mesher, p1, p2, 0.5, 1.0);
        double[] r = new double[15 * 13], result = new double[15 * 13];
        Assert.Equal("direction is too large", Assert.Throws<NanoQLException>(() => op.ApplyDirection(2, r, result)).Message);
        Assert.Equal(
            "direction is too large", Assert.Throws<NanoQLException>(() => op.SolveSplitting(2, r, 0.5, result)).Message);
    }

    [Fact]
    public void TheWienerOperatorTakesOneEigenvaluePerDimension()
    {
        var mesher = new FdmMesherComposite(new Uniform1dMesher(-1.0, 1.0, 5), new Uniform1dMesher(-1.0, 1.0, 5));
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new FdmWienerOp(mesher, new FlatForward(Today, 0.04, new Actual365Fixed()), [0.04]));
        Assert.Equal("lambdas", e.ParamName);
        Assert.StartsWith("mesher and lambdas need to be of the same dimension", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMultiAssetOptionTakesResultsWithGreeks()
    {
        var option = new BasketOption(AveragePut(), European());
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => option.FetchResults(new InstrumentResults { NPV = 1.0 }));
        Assert.Equal("results", e.ParamName);
        Assert.StartsWith("no greeks returned from pricing engine", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheInPlaceStepTakesOneIncrementPerFactor()
    {
        GeneralizedBlackScholesProcess p1 = Process(100.0, Flat(0.02), Flat(0.05), Vol(0.25));
        GeneralizedBlackScholesProcess p2 = Process(95.0, Flat(0.03), Flat(0.05), Vol(0.3));
        var rho = new Matrix(new[,] { { 1.0, 0.5 }, { 0.5, 1.0 } });
        var array = new StochasticProcessArray([p1, p2], rho);
        double[] x0 = [100.0, 95.0], result = new double[2];
        ArgumentException e = Assert.Throws<ArgumentException>(() => array.Evolve(0.0, x0, 0.1, [0.3], result));
        Assert.Equal("dw", e.ParamName);
        Assert.StartsWith(
            "vectors and matrices with different sizes (1, 2x2) cannot be multiplied", e.Message, StringComparison.Ordinal);
    }
}
