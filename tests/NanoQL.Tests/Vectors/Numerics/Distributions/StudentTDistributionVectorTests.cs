// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Student t distributions' vectors, <c>tests/vectors/math/distributions/studenttdistribution.json</c>:
/// <see cref="StudentDistribution"/>, <see cref="CumulativeStudentDistribution"/> where QuantLib's formula holds, and
/// <see cref="InverseCumulativeStudent"/>, Boost's quantile, with QuantLib's answers at 0 and 1.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StudentTDistributionVectorTests
{
    [Fact]
    public void StudentTDistributionVector()
    {
        var file = VectorFile.Load("math/distributions/studenttdistribution");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int n = c.Int("n");
            if (c.Name.StartsWith("density", StringComparison.Ordinal))
            {
                var f = new StudentDistribution(n);
                check.Output(c, "density", Reals(c, "x").Select(f.Evaluate).ToArray());
            }
            else if (c.Name.StartsWith("cumulative", StringComparison.Ordinal))
            {
                var f = new CumulativeStudentDistribution(n);
                check.Output(c, "cumulative", Reals(c, "x").Select(f.Evaluate).ToArray());
            }
            else
            {
                var f = new InverseCumulativeStudent(n);
                check.Output(c, "inverse", Reals(c, "p").Select(f.Evaluate).ToArray());
            }
        }

        check.Complete();
    }

    private static double[] Reals(VectorCase c, string name) => [.. c.Input(name).EnumerateArray().Select(VectorCase.Real)];
}
