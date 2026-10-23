// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The flat extrapolator's harness vectors, <c>tests/vectors/cxx/math/interpolations/flatextrapolation.json</c>:
/// <see cref="FlatExtrapolator"/> on nine interpolations of the linear, backward-flat, log-linear, cubic and log-cubic
/// families, with the value, derivatives and primitive inside and outside the range, the range accessors,
/// <see cref="Interpolation.IsInRange"/> and the value with extrapolation enabled on the decorator.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FlatExtrapolatorCxxVectorTests
{
    [Fact]
    public void FlatExtrapolatorCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolations/flatextrapolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FlatExtrapolator Build() => new(InterpolationInputs.Build(c.Input("interpolation")));

            InterpolationInputs.CheckMembers(check, c, Build);
            FlatExtrapolator f = Build();
            double[] points = InterpolationInputs.Numbers(c.Input("points"));
            check.Output(c, "xMin", f.XMin);
            check.Output(c, "xMax", f.XMax);
            check.Output(c, "xValues", f.XValues());
            check.Output(c, "yValues", f.YValues());
            check.Output(c, "isInRange", points.Select(f.IsInRange).ToArray());
            check.Output(c, "enableExtrapolation", InterpolationInputs.Deferred(points, p => f.EnableExtrapolation().Evaluate(p)));
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"cxx/math/interpolations/flatextrapolation: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
    }
}
