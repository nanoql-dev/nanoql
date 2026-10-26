// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BasketInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The getCovariance vectors, <c>tests/vectors/math/matrixutilities/getcovariance.json</c>:
/// <see cref="GetCovarianceFunctions.GetCovariance"/> in one to five dimensions, within the tolerance off symmetry, and
/// its four refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GetCovarianceVectorTests
{
    [Fact]
    public void GetCovarianceVector()
    {
        var file = VectorFile.Load("math/matrixutilities/getcovariance");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] stdDev = c.Doubles("stdDev");
            Matrix corr = MatrixOf(c.Input("corr"));
            check.Output(c, "covariance", () => Flat(GetCovarianceFunctions.GetCovariance(stdDev, corr)));
        }

        check.Complete();
    }
}
