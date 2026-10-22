// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's rankReducedSqrt vectors, <c>tests/vectors/cxx/math/matrixutilities/pseudosqrt.json</c>:
/// <see cref="PseudoSqrtFunctions.RankReducedSqrt"/>, which the wheel doesn't expose, of symmetricschurdecomposition.py's
/// matrices under the None, Spectral and Higham salvaging algorithms at three ranks and three retained percentages;
/// and its refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PseudoSqrtCxxVectorTests
{
    [Fact]
    public void PseudoSqrtCxxVector()
    {
        var file = VectorFile.Load("cxx/math/matrixutilities/pseudosqrt");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix m = MatrixVectorTests.FromRows(c.Input("matrix"));
            int maxRank = c.Int("maxRank");
            double percentage = c.Double("percentage");
            var sa = (SalvagingAlgorithmType)c.Int("algorithm");
            check.Output(
                c, "sqrt", () => MatrixVectorTests.Rows(PseudoSqrtFunctions.RankReducedSqrt(m, maxRank, percentage, sa)));
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"cxx/math/matrixutilities/pseudosqrt: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
    }
}
