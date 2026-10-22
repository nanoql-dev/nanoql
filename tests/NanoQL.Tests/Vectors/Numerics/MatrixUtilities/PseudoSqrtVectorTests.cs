// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The pseudoSqrt vectors, <c>tests/vectors/math/matrixutilities/pseudosqrt.json</c>:
/// <see cref="PseudoSqrtFunctions.PseudoSqrt"/> of symmetricschurdecomposition.py's matrices under each salvaging
/// algorithm, the hypersphere and lower-diagonal ones where they optimize left out; and the refusals of no rows and
/// of a matrix that isn't square.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PseudoSqrtVectorTests
{
    [Fact]
    public void PseudoSqrtVector()
    {
        var file = VectorFile.Load("math/matrixutilities/pseudosqrt");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix m = MatrixVectorTests.FromRows(c.Input("matrix"));
            var sa = (SalvagingAlgorithmType)c.Int("algorithm");
            check.Output(c, "sqrt", () => MatrixVectorTests.Rows(PseudoSqrtFunctions.PseudoSqrt(m, sa)));
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"math/matrixutilities/pseudosqrt: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
    }
}
