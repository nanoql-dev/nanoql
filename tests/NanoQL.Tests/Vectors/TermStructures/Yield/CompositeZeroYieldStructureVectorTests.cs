// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The composite curve vectors, <c>tests/vectors/termstructures/yield/compositezeroyieldstructure.json</c>: five pairs
/// of curves sharing their reference date and day counter, under four functions of the two zero rates in two
/// compoundings each; the composite's inspectors, and its discounts, zero rates and forward rates at 30 dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CompositeZeroYieldStructureVectorTests
{
    [Fact]
    public void CompositeZeroYieldStructureVector()
    {
        var file = VectorFile.Load("termstructures/yield/compositezeroyieldstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            YieldTermStructure curve1 = YieldCurveInputs.OriginalOf(c.Input("curve1"), c.Context());
            YieldTermStructure curve2 = YieldCurveInputs.OriginalOf(c.Input("curve2"), c.Context());
            Func<double, double, double> f = c.String("function") switch
            {
                "add" => (a, b) => a + b,
                "sub" => (a, b) => a - b,
                "mean" => (a, b) => (a + b) / 2.0,
                "max" => Math.Max,
                var name => throw new InvalidOperationException($"unknown function '{name}'"),
            };
            var composite = new CompositeZeroYieldStructure(
                curve1, curve2, f, (Compounding)c.Int("compounding"), (Frequency)c.Int("frequency"));
            YieldCurveInputs.CheckCurve(check, c, composite, c.DateInputs("dates"));
        }

        check.Complete();
    }
}
