// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009, 2010, 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/fdmlinearop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testFdmLinearOpLayout, testCrankNicolsonWithDamping, testHighInterestRateBlackScholesMesher, testLowVolatilityHighDiscreteDividendBlackScholesMesher (S-eq's third commit); testUniformGridMesher, testFirstDerivativesMapApply, testSecondDerivativesMapApply, testDerivativeWeightsOnNonUniformGrids, testSecondOrderMixedDerivativesMapApply, testTripleBandMapSolve, testFdmHestonBarrier, testFdmHestonAmerican, testFdmHestonExpress, testFdmHestonHullWhiteOp, testBiCGstab, testGMRES (B6.2); testFdmMesherIntegral (B4.2)

using NanoQL.Numerics;
using SparseMatrixReference = NanoQL.Numerics.SparseMatrix;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class FdmLinearOpTests
{
    // qldefines.hpp's QL_EPSILON, which nanoql keeps internal.
    private const double QL_EPSILON = 2.220446049250313E-16;

    [Fact]
    public void TestSpareMatrixReference()
    {
        // Testing SparseMatrixReference type...

        const Size rows = 10;
        const Size columns = 10;
        const Size nMatrices = 5;
        const Size nElements = 50;

        var rng = new MersenneTwisterUniformRng(1234);

        var expected = new SparseMatrix(rows, columns);
        SparseMatrix[] v = [.. Enumerable.Range(0, nMatrices).Select(_ => new SparseMatrix(rows, columns))];
        var refs = new List<SparseMatrixReference>();

        foreach (SparseMatrix i in v)
        {
            SparseMatrixReference m = i;
            for (Size j = 0; j < nElements; ++j)
            {
                Size row = (Size)(rng.Next().Value * rows);
                Size column = (Size)(rng.Next().Value * columns);

                Real value = rng.Next().Value;
                m[row, column] += value;
                expected[row, column] += value;
            }

            refs.Add(m);
        }

        // std::accumulate(refs.begin()+1, refs.end(), SparseMatrix(refs.front()))
        SparseMatrix calculated = refs.Skip(1).Aggregate(refs[0].Clone(), (sum, r) => sum + r);

        for (Size i = 0; i < rows; ++i)
        {
            for (Size j = 0; j < columns; ++j)
            {
                if (Math.Abs(calculated[i, j] - expected[i, j]) > 100 * QL_EPSILON)
                {
                    Assert.Fail(
                        "Error using sparse matrix references in "
                        + $"Element ({i}, {j})"
                        + $"\n expected  : {expected[i, j]}"
                        + $"\n calculated: {calculated[i, j]}");
                }
            }
        }
    }

    [Fact]
    public void TestSparseMatrixZeroAssignment()
    {
        // Testing assignment to zero in sparse matrix...

        var m = new SparseMatrix(5, 5);
        if (NrElementsOfSparseMatrix(m) != 0)
        {
            Assert.Fail("non zero return for an emtpy matrix");
        }

        m[0, 0] = 0.0;
        m[1, 2] = 0.0;
        if (NrElementsOfSparseMatrix(m) != 2)
        {
            Assert.Fail("two elements expected");
        }

        m[1, 3] = 1.0;
        if (NrElementsOfSparseMatrix(m) != 3)
        {
            Assert.Fail("three elements expected");
        }

        m[1, 3] = 0.0;
        if (NrElementsOfSparseMatrix(m) != 3)
        {
            Assert.Fail("three elements expected");
        }
    }

    // QuantLib's nrElementsOfSparseMatrix: the stored elements counted row by row, as uBLAS's row iterator visits
    // them (none beyond the last row written to).
    private static Size NrElementsOfSparseMatrix(SparseMatrix m)
    {
        Size retVal = 0;
        for (int i = 0; i < m.Size1; ++i)
        {
            if (i + 1 < m.Filled1)
            {
                retVal += m.Index1Data[i + 1] - m.Index1Data[i];
            }
        }

        return retVal;
    }
}
