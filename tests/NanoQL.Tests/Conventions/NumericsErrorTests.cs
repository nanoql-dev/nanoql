// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using Complex = System.Numerics.Complex;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception type, message and parameter name of the numerics' refusals that no vector reaches: QuantLib's checks
/// of an argument, each an <see cref="ArgumentException"/> naming it, with QuantLib's text, and the in-place sparse
/// sum's refusal of an element outside the matrix. QuantLib's guards on paths
/// no ordinary input takes (the series' and continued fractions' iteration limits, the Gauss-Lobatto rule's "no more
/// machine number") have no fact.
/// </summary>
public class NumericsErrorTests
{
    [Fact]
    public void ModifiedBesselFunctionRefusals()
    {
        // modifiedbessel.cpp:206-207 and :234-235
        const string Message = "negative argument requires complex version of modifiedBesselFunction";
        Refused(() => ModifiedBesselFunctions.ModifiedBesselFunction_i(0.5, -1.0), "x", Message);
        Refused(() => ModifiedBesselFunctions.ModifiedBesselFunction_i_exponentiallyWeighted(0.5, -1.0), "x", Message);
    }

    [Fact]
    public void ExponentialIntegralRefusals()
    {
        // exponentialintegrals.cpp:101 and :136
        Refused(() => ExponentialIntegralsFunctions.Ci(-1.0), "x", "x < 0 => Ci(x) = Ci(-x) + i*pi");
        Refused(() => ExponentialIntegralsFunctions.Ei(new Complex(800.0, 0.5)), "z", "argument error (800,0.5)");
    }

    [Fact]
    public void TabulatedGaussLegendreRefusals()
    {
        // gaussianquadratures.cpp:159-176, the orders 6, 7, 12 and 20 alone, QuantLib's QL_FAIL in the switch's default
        Refused(() => new TabulatedGaussLegendre(5), "n", "order 5 not supported");
        Refused(() => new TabulatedGaussLegendre().Order(8), "order", "order 8 not supported");
    }

    [Fact]
    public void SparseMatrixProductRefusal()
    {
        // sparsematrix.hpp:52-55
        var a = new SparseMatrix(3, 2);
        Refused(
            () => SparseMatrixFunctions.Prod(a, new Vector(3)),
            "x",
            "vectors and sparse matrices with different sizes (3, 3x2) cannot be multiplied");
    }

    [Fact]
    public void SparseMatrixPlusAssignRefusal()
    {
        // an undefined write in uBLAS (Divergences/SparseMatrixPlusAssignTests.cs)
        var a = new SparseMatrix(2, 2);
        var b = new SparseMatrix(3, 2);
        b[2, 1] = 1.0;
        Refused(() => a += b, "other", "element (2, 1) outside the 2 x 2 matrix");
    }

    private static void Refused(Action call, string paramName, string message)
    {
        var e = Assert.Throws<ArgumentException>(call);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}
