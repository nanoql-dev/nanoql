// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The immutable matrix utilities copy the vectors and matrices they're given and return copies (master §2.2's working
/// types row): changing a <see cref="HouseholderTransformation"/>'s, a <see cref="HouseholderReflection"/>'s or a
/// <see cref="CovarianceDecomposition"/>'s argument afterwards, or a vector or matrix it returned, changes nothing.
/// </summary>
public class MatrixUtilitiesInputCopyTests
{
    [Fact]
    public void ChangingAnArgumentOrAResultAfterwardsChangesNothing()
    {
        var v = new Vector([0.3, -0.4, 0.5]);
        var transformation = new HouseholderTransformation(v);
        Matrix before = transformation.GetMatrix();
        v[0] = 9.0;
        before[0, 0] = 7.0;
        Assert.Equal(1.0 - (2 * 0.3 * 0.3 / 0.5), transformation.GetMatrix()[0, 0], 1e-15);

        var e = new Vector([1.0, 0.0, 0.0]);
        var reflection = new HouseholderReflection(e);
        Vector reflected = reflection.Evaluate(new Vector([1.0, 1.0, 0.0]));
        e[0] = 0.0;
        e[1] = 1.0;
        Assert.Equal(reflected.ToArray(), reflection.Evaluate(new Vector([1.0, 1.0, 0.0])).ToArray());

        var cov = new Matrix(2, 2, 0.01);
        cov[0, 0] = 0.04;
        cov[1, 1] = 0.09;
        var decomposition = new CovarianceDecomposition(cov);
        cov[0, 1] = cov[1, 0] = 0.0;
        decomposition.CorrelationMatrix()[0, 1] = 5.0;
        decomposition.Variances()[0] = 5.0;
        Assert.Equal(0.01 / (0.2 * 0.3), decomposition.CorrelationMatrix()[0, 1], 1e-15);
        Assert.Equal(0.04, decomposition.Variances()[0]);
        Assert.Equal(0.3, decomposition.StandardDeviations()[1]);
    }
}
