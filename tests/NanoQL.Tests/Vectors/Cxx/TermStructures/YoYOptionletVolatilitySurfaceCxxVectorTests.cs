// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's year-on-year optionlet volatility vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure.json</c>: the
/// members the wheel doesn't bind, <see cref="YoYOptionletVolatilitySurface.VolatilityType"/>,
/// <see cref="YoYOptionletVolatilitySurface.Displacement"/> and the time form of
/// <see cref="YoYOptionletVolatilitySurface.Volatility(double, double)"/>, with the constructor's refusal of a
/// displacement other than 0 or 1.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class YoYOptionletVolatilitySurfaceCxxVectorTests
{
    [Fact]
    public void YoYOptionletVolatilitySurfaceCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            YoYOptionletVolatilitySurface Surface() => InflationInputs.YoYVolatilityOf(c.Input("surface"), context);
            check.Output(c, "volatilityType", () => Surface().VolatilityType);
            check.Output(c, "displacement", () => Surface().Displacement);
            check.Output(
                c,
                "volatilityAtTimes",
                VolatilityInputs.Grid(c.Doubles("times"), c.Doubles("strikes"), (t, x) => Surface().Volatility(t, x)));
        }

        check.Complete();
    }
}
