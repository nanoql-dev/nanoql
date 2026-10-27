// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's incomplete-basis vectors,
/// <c>tests/vectors/cxx/math/matrixutilities/basisincompleteordered.json</c>:
/// <see cref="BasisIncompleteOrdered"/> on seeded, dependent and zero vectors (each addition's result, the sizes, the
/// basis) and a vector of the wrong size, refused; <see cref="OrthogonalProjections"/> on seeded rows at three cut-offs
/// and a dependent row (each vector's validity, the count valid, each projection).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BasisIncompleteOrderedCxxVectorTests
{
    [Fact]
    public void BasisIncompleteOrderedCxxVector()
    {
        var file = VectorFile.Load("cxx/math/matrixutilities/basisincompleteordered");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[][] vectors = [.. c.Inputs.GetProperty("vectors").EnumerateArray().Select(Reals)];
            if (c.Inputs.GetProperty("call").GetString() == "basis")
            {
                var basis = new BasisIncompleteOrdered(c.Inputs.GetProperty("dimension").GetInt32());
                if (c.Expected.TryGetProperty("throws", out _))
                {
                    check.Output(c, "throws", () => basis.AddVector(vectors[0]));
                    continue;
                }

                check.Output(c, "added", vectors.Select(x => basis.AddVector(x)).ToArray());
                check.Output(c, "basisSize", basis.BasisSize);
                check.Output(c, "euclideanDimension", basis.EuclideanDimension);
                check.Output(c, "basis", Rows(basis.GetBasisAsRowsInMatrix()));
            }
            else
            {
                var original = new Matrix(vectors.Length, vectors[0].Length);
                for (int i = 0; i < vectors.Length; ++i)
                {
                    for (int j = 0; j < vectors[i].Length; ++j)
                    {
                        original[i, j] = vectors[i][j];
                    }
                }

                var projector = new OrthogonalProjections(original, c.Double("multiplierCutOff"), c.Double("tolerance"));
                check.Output(c, "validVectors", projector.ValidVectors);
                check.Output(c, "numberValidVectors", projector.NumberValidVectors);
                check.Output(c, "vectors", Enumerable.Range(0, vectors.Length).Select(projector.GetVector).ToArray());
            }
        }

        check.Complete();
    }
}
