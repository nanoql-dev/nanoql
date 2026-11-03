// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The ZABR and Dupire operators' matrix decompositions apply as the operators, QuantLib's
/// <c>FdmLinearOpComposite::toMatrix</c> being their sum: the ZABR operator's three parts (its forward and volatility
/// directions and its mixed term, <c>fdmzabrop.cpp</c>'s <c>toMatrixDecomp</c>) and the Dupire operator's one
/// (<c>fdmdupire1dop.cpp</c>'s), each sum times a vector the operator's <c>apply</c> of it, as the short-rate
/// operators' (<c>FdmMemberTests</c>).
/// </summary>
public class ZabrOperatorMatrixTests
{
    [Fact]
    public void TheDecompositionsApplyAsTheOperators()
    {
        var zabrMesher = new FdmMesherComposite(new Uniform1dMesher(0.005, 0.1, 9), new Uniform1dMesher(0.02, 0.3, 7));
        var zabr = new FdmZabrOp(zabrMesher, 0.7, 0.2, -0.3, 0.75);
        var dupireMesher = new FdmMesherComposite(new Concentrating1dMesher(0.00001, 0.10, 21, (0.03, 0.1), true));
        Vector strikes = dupireMesher.Locations(0);
        var dupire = new FdmDupire1dOp(dupireMesher, new Vector([.. Enumerable.Range(0, strikes.Size).Select(i => 0.2 + (0.01 * i))]));

        foreach ((FdmLinearOpComposite op, int size, int parts) in new (FdmLinearOpComposite, int, int)[] { (zabr, 63, 3), (dupire, 21, 1) })
        {
            var r = new Vector([.. Enumerable.Range(0, size).Select(i => Math.Sin(0.7 * i) + 1.5)]);
            ImmutableArray<SparseMatrix> decomposition = op.ToMatrixDecomp();
            SparseMatrix sum = decomposition[0];
            for (int k = 1; k < decomposition.Length; k++)
            {
                sum += decomposition[k];
            }

            Assert.Equal(parts, decomposition.Length);
            Vector applied = op.Apply(r), product = SparseMatrixFunctions.Prod(sum, r);
            for (int i = 0; i < size; i++)
            {
                Assert.True(Math.Abs(product[i] - applied[i]) <= 1e-12 * Math.Max(1.0, Math.Abs(applied[i])), $"{i}");
            }
        }
    }
}
