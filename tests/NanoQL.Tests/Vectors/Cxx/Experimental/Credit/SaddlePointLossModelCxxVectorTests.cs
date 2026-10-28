// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's saddle-point loss model vectors,
/// <c>tests/vectors/cxx/experimental/credit/saddlepointlossmodel.json</c>: the model's statistics through the baskets
/// it is bound to, those it refuses, and the whole portfolio's tail and density.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SaddlePointLossModelCxxVectorTests
{
    [Fact]
    public void SaddlePointLossModelCxxVector() => LossModelStatistics.Check("cxx/experimental/credit/saddlepointlossmodel", (c, check, basket) =>
    {
        var model = (SaddlePointLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>)basket.LossModel!;
        Date[] dates = [.. c.Input("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
        double[] losses = [.. c.Input("losses").EnumerateArray().Select(VectorCase.Real)];
        (Date D, double X)[] grid = [.. dates.SelectMany(d => losses.Select(x => (d, x)))];
        check.Output(c, "members", new object[]
        {
            grid.Select(g => model.ProbOverPortfLoss(g.D, g.X)).ToArray(),
            grid.Select(g => model.ProbDensity(g.D, g.X)).ToArray(),
        });
    });
}
