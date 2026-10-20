// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/nthorderderivativeop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testFirstOrder2PointsApply, testFirstOrder3PointsOnUniformGrid, testFirstOrder5PointsOnUniformGrid, testFirstOrder2PointsOnUniformGrid, testFirstOrder4PointsOnUniformGrid, testFirstOrder2PointsOn2DimUniformGrid, testSecondOrder3PointsNonUniformGrid, testSecondOrder4PointsNonUniformGrid, testThirdOrder4PointsUniformGrid, testHigherOrderHestonOptionPricing, testHigherOrderAndRichardsonExtrapolation, testCompareFirstDerivativeOpNonUniformGrid, testCompareFirstDerivativeOp2dUniformGrid, testMixedSecondOrder9PointsOnUniformGrid (B6.2)

using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class NthOrderDerivativeOpTests
{
    [Fact]
    public void TestSparseMatrixApply()
    {
        // Testing sparse matrix apply...

        var sm = new SparseMatrix(5, 7);

        Assert.True(sm.Size1 == 5, "check sm.size1() == SparseMatrix::size_type(5) has failed");
        Assert.True(sm.Size2 == 7, "check sm.size2() == SparseMatrix::size_type(7) has failed");

        sm[1, 3] = 3.0;

        var x = new Vector(7, 0.0, 1.0);
        Vector y = SparseMatrixFunctions.Prod(sm, x);

        Assert.True(ComparisonFunctions.CloseEnough(y[0], 0.0), "check close_enough(y[0], 0.0) has failed");
        Assert.True(ComparisonFunctions.CloseEnough(y[1], 3.0 * 3.0), "check close_enough(y[1], 3.0*3.0) has failed");
        Assert.True(ComparisonFunctions.CloseEnough(y[2], 0.0), "check close_enough(y[2], 0.0) has failed");
        Assert.True(ComparisonFunctions.CloseEnough(y[3], 0.0), "check close_enough(y[3], 0.0) has failed");
        Assert.True(ComparisonFunctions.CloseEnough(y[4], 0.0), "check close_enough(y[4], 0.0) has failed");
    }
}
