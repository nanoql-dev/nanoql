// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// The bivariate Student t distribution's harness vectors,
/// <c>tests/vectors/cxx/math/distributions/bivariatestudenttdistribution.json</c>:
/// <see cref="BivariateCumulativeStudentDistribution"/> for odd and even degrees of freedom and correlations from −1 to
/// 1, on a grid of bounds.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BivariateStudentTDistributionCxxVectorTests
{
    [Fact]
    public void BivariateStudentTDistributionCxxVector()
    {
        var file = VectorFile.Load("cxx/math/distributions/bivariatestudenttdistribution");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var f = new BivariateCumulativeStudentDistribution(c.Int("n"), VectorCase.Real(c.Input("rho")));
            double[] x = Reals(c, "x"), y = Reals(c, "y");
            check.Output(c, "value", x.Zip(y, f.Evaluate).ToArray());
        }

        check.Complete();
    }

    private static double[] Reals(VectorCase c, string name) => [.. c.Input(name).EnumerateArray().Select(VectorCase.Real)];
}
