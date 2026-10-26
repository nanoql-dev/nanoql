// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The bivariate normal vectors, <c>tests/vectors/math/distributions/bivariatenormaldistribution.json</c>:
/// <see cref="BivariateCumulativeNormalDistributionDr78"/>'s and
/// <see cref="BivariateCumulativeNormalDistributionWe04DP"/>'s probabilities on a grid of bounds and correlations, and
/// their refusals of a correlation outside [−1, 1].
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BivariateNormalDistributionVectorTests
{
    [Fact]
    public void BivariateNormalDistributionVector()
    {
        var file = VectorFile.Load("math/distributions/bivariatenormaldistribution");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            string kind = c.Input("kind").GetString()!;
            double rho = c.Input("rho").GetDouble();
            double[] bounds = [.. c.Input("bounds").EnumerateArray().Select(x => x.GetDouble())];
            check.Output(c, "values", () =>
            {
                Func<double, double, double> f = kind == "Dr78"
                    ? new BivariateCumulativeNormalDistributionDr78(rho).Evaluate
                    : new BivariateCumulativeNormalDistributionWe04DP(rho).Evaluate;
                return bounds.SelectMany(a => bounds.Select(b => f(a, b))).ToArray();
            });
        }

        check.Complete();
    }
}
