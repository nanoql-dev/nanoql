// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's interpolating CPI cap and floor engine vectors,
/// <c>tests/vectors/cxx/experimental/inflation/cpicapfloorengines.json</c>: caps and floors to two to twelve years,
/// observed flat and linearly, at the surface's lag and a month longer, priced off each surface; a shorter lag
/// refused.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterpolatingCPICapFloorEngineCxxVectorTests
{
    [Fact]
    public void InterpolatingCPICapFloorEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/inflation/cpicapfloorengines");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CPICapFloorTermPriceSurface surface = InflationSurfaceInputs.CPISurfaceOf(c.Input("surface"), context);
            var index = (ZeroInflationIndex)InflationInputs.IndexOf(c.Input("surface").GetProperty("index"));
            check.Output(c, "npv", c.Input("capFloors").EnumerateArray().Select(spec => (Func<object?>)(() =>
                InterpolatingCPICapFloorEngine.Calculate(
                    InflationSurfaceInputs.CPICapFloorOf(spec, index), context, surface).NPV)).ToArray());
        }

        check.Complete();
    }
}
