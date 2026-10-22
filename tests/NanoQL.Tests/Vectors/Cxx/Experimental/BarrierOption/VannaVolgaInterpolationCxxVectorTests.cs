// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Vanna-Volga interpolation vectors,
/// <c>tests/vectors/cxx/experimental/barrieroption/vannavolgainterpolation.json</c>:
/// <see cref="VannaVolgaInterpolation"/>'s values on three-volatility smiles over two markets and three expiries,
/// through the interpolation and through the
/// <see cref="VannaVolga"/> factory, where the stability filter keeps them; the factory's required points; and the
/// refusals of two and four strikes.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class VannaVolgaInterpolationCxxVectorTests
{
    [Fact]
    public void VannaVolgaInterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/barrieroption/vannavolgainterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Expected.TryGetProperty("requiredPoints", out _))
            {
                check.Output(c, "requiredPoints", VannaVolga.RequiredPoints);
                continue;
            }

            double[] x = Numbers(c.Input("x")), y = Numbers(c.Input("y")), points = Numbers(c.Input("points"));
            double spot = Number(c.Input("spot")), dDiscount = Number(c.Input("dDiscount"));
            double fDiscount = Number(c.Input("fDiscount")), t = Number(c.Input("t"));
            if (c.Expected.TryGetProperty("factoryValues", out _))
            {
                Interpolation f = new VannaVolga(spot, dDiscount, fDiscount, t).Interpolate(x, y);
                check.Output(c, "factoryValues", points.Select(k => (object?)f.Evaluate(k, true)));
                continue;
            }

            check.Output(
                c,
                "values",
                () => points.Select(
                    k => new VannaVolgaInterpolation(x, y, spot, dDiscount, fDiscount, t).Evaluate(k, true))
                    .ToArray());
        }

        check.Complete();
    }
}
