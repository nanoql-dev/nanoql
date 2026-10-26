// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BasketInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's covariance decomposition vectors,
/// <c>tests/vectors/cxx/math/matrixutilities/getcovariance.json</c>:
/// <see cref="CovarianceDecomposition"/>'s variances, standard deviations and correlation matrix in one to five
/// dimensions, within and beyond the symmetry tolerance, and of a non-square matrix (refused).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GetCovarianceCxxVectorTests
{
    [Fact]
    public void GetCovarianceCxxVector()
    {
        var file = VectorFile.Load("cxx/math/matrixutilities/getcovariance");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix cov = MatrixOf(c.Input("covariance"));
            double tolerance = c.Double("tolerance");
            check.Output(c, "variances", () => new CovarianceDecomposition(cov, tolerance).Variances().ToArray());
            check.Output(
                c, "standardDeviations", () => new CovarianceDecomposition(cov, tolerance).StandardDeviations().ToArray());
            check.Output(
                c, "correlationMatrix", () => Flat(new CovarianceDecomposition(cov, tolerance).CorrelationMatrix()));
        }

        check.Complete();
    }
}
