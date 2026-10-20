// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/tqreigendecomposition.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class TqrEigenDecompositionTests
{
    [Fact]
    public void TestEigenValueDecomposition()
    {
        // Testing TQR eigenvalue decomposition...

        var diag = new Vector(5);
        var sub = new Vector(4, 1);
        diag[0] = 11;
        diag[1] = 7;
        diag[2] = 6;
        diag[3] = 2;
        diag[4] = 0;
        Real[] ev =
        [
            11.2467832217139119,
            7.4854967362908535,
            5.5251516080277518,
            2.1811760273123308,
            -0.4386075933448487,
        ];

        var tqre = new TqrEigenDecomposition(diag, sub, TqrEigenDecompositionEigenVectorCalculation.WithoutEigenVector);
        for (Size i = 0; i < diag.Size; ++i)
        {
            Real expected = ev[i];
            Real calculated = tqre.Eigenvalues()[i];
            Real tolerance = 1.0e-10;
            if (Math.Abs(expected - calculated) > tolerance)
            {
                Assert.Fail("wrong eigenvalue \n" + $"calculated: {calculated} expected  : {expected}");
            }
        }
    }

    [Fact]
    public void TestZeroOffDiagEigenValues()
    {
        // Testing TQR zero-off-diagonal eigenvalues...

        var diag = new Vector(5);
        var sub = new Vector(4, 1);
        sub[0] = sub[2] = 0;
        diag[0] = 12;
        diag[1] = 9;
        diag[2] = 6;
        diag[3] = 3;
        diag[4] = 0;

        var tqre1 = new TqrEigenDecomposition(diag, sub);

        sub[0] = sub[2] = 1e-14;
        var tqre2 = new TqrEigenDecomposition(diag, sub);

        for (Size i = 0; i < diag.Size; ++i)
        {
            Real expected = tqre2.Eigenvalues()[i];
            Real calculated = tqre1.Eigenvalues()[i];
            Real tolerance = 1.0e-10;
            if (Math.Abs(expected - calculated) > tolerance)
            {
                Assert.Fail("wrong eigenvalue \n" + $"calculated: {calculated} expected  : {expected}");
            }
        }
    }

    [Fact]
    public void TestEigenVectorDecomposition()
    {
        // Testing TQR eigenvector decomposition...

        var diag = new Vector(2, 1);
        var sub = new Vector(1, 1);

        var tqre = new TqrEigenDecomposition(diag, sub);
        Real tolerance = 1.0e-10;

        Matrix ev = tqre.Eigenvectors();
        if (Math.Abs(0.25 + (ev[0, 0] * ev[0, 1] * ev[1, 0] * ev[1, 1])) > tolerance)
        {
            Assert.Fail("wrong eigenvector");
        }
    }
}
