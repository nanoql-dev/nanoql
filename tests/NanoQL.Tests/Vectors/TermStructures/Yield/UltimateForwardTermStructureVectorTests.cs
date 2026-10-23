// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ultimate-forward curve vectors, <c>tests/vectors/termstructures/yield/ultimateforwardtermstructure.json</c>: the
/// zero-spreaded file's six originals under four settings of last liquid and ultimate forward rates, first smoothing
/// point, growth factor and rounding; the curve's inspectors, and its discounts, zero rates and forward rates to a hundred
/// years.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class UltimateForwardTermStructureVectorTests
{
    [Fact]
    public void UltimateForwardTermStructureVector()
    {
        var file = VectorFile.Load("termstructures/yield/ultimateforwardtermstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            YieldTermStructure original = YieldCurveInputs.OriginalOf(c.Input("original"), c.Context());
            JsonElement fsp = c.Input("firstSmoothingPoint");
            JsonElement digits = c.Input("roundingDigits");
            var curve = new UltimateForwardTermStructure(
                original,
                c.Double("llfr"),
                c.Double("ufr"),
                new Period(fsp[0].GetInt32(), (TimeUnit)fsp[1].GetInt32()),
                c.Double("alpha"),
                digits.ValueKind == JsonValueKind.Null ? null : digits.GetInt32(),
                (Compounding)c.Int("compounding"),
                (Frequency)c.Int("frequency"));
            YieldCurveInputs.CheckCurve(check, c, curve, c.DateInputs("dates"));
        }

        check.Complete();
    }
}
