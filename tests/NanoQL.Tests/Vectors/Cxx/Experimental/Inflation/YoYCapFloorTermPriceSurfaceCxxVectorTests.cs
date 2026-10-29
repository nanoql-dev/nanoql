// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's year-on-year price surface vectors,
/// <c>tests/vectors/cxx/experimental/inflation/yoycapfloortermpricesurface.json</c>: the at-the-money swap rates by
/// date, which the wheel's binding can't return, on QuantLib's euro price surfaces.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class YoYCapFloorTermPriceSurfaceCxxVectorTests
{
    [Fact]
    public void YoYCapFloorTermPriceSurfaceCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/inflation/yoycapfloortermpricesurface");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            (var nominal, var index) = InflationSurfaceInputs.EuroMarketOf(c.Input("market"));
            var surf = InflationSurfaceInputs.YoYSurfaceOf(c.Input("surface"), nominal, index, context);
            (var dates, var rates) = surf.AtmYoYSwapDateRates();
            check.Output(c, "atmYoYSwapDateRates", new object?[] { dates.Select(d => (object?)d).ToArray(), rates });
        }

        check.Complete();
    }
}
