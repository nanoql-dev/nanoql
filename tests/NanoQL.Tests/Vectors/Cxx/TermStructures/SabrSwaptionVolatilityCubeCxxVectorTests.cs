// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's swaption volatility cube vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/swaption/sabrswaptionvolatilitycube.json</c>: what the wheel doesn't
/// reach, a cube's maximum swap tenor, its at-the-money strikes by option and swap tenors and a SABR cube's market
/// volatilities by strike spread, kept where the stability filter found them stable; and the constructions and
/// calibrations QuantLib refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SabrSwaptionVolatilityCubeCxxVectorTests
{
    [Fact]
    public void SabrSwaptionVolatilityCubeCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/swaption/sabrswaptionvolatilitycube");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", () => CubeInputs.CubeOf(c.Input("cube"), context) is null ? 0 : null);
                continue;
            }

            SwaptionVolatilityCube s = CubeInputs.CubeOf(c.Input("cube"), context);
            (Period Option, Period Swap)[] points =
            [
                .. c.Input("points").EnumerateArray().Select(p => (VolatilityInputs.TenorOf(p[0]), VolatilityInputs.TenorOf(p[1]))),
            ];
            check.Output(c, "maxSwapTenor", CubeInputs.Kept(c, "maxSwapTenor", new[] { s.MaxSwapTenor.Length, (int)s.MaxSwapTenor.Units }));
            check.Output(
                c,
                "atmStrike",
                CubeInputs.Kept(c, "atmStrike", points.Select(p => (Func<object?>)(() => s.AtmStrike(p.Option, p.Swap, context)))));
            if (s is SabrSwaptionVolatilityCube sabr)
            {
                check.Output(
                    c,
                    "marketVolCube",
                    CubeInputs.Kept(
                        c, "marketVolCube", Enumerable.Range(0, sabr.StrikeSpreads.Length).SelectMany(i => CubeInputs.Flat(sabr.MarketVolCube(i)))));
            }
        }

        check.Complete();
    }
}
