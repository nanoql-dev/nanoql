// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The implied curve vectors, <c>tests/vectors/termstructures/yield/impliedtermstructure.json</c>: the zero-spreaded
/// file's six originals, each implied at its reference date and 1 and 3 years later; the implied curve's inspectors,
/// its discounts at 17 times, and its discounts, zero rates and forward rates at 30 dates, but on the 30/360 original,
/// whose date reads nanoql takes by date (the S-sr plan's SSR6 (viii)).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ImpliedTermStructureVectorTests
{
    [Fact]
    public void ImpliedTermStructureVector()
    {
        var file = VectorFile.Load("termstructures/yield/impliedtermstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            YieldTermStructure original = YieldCurveInputs.OriginalOf(c.Input("original"), c.Context());
            var implied = new ImpliedTermStructure(original, new Date(c.Int("referenceDate")));
            Date[] dates = c.DateInputs("dates");
            YieldCurveInputs.CheckCurve(check, c, implied, dates);
            double[] times = [.. c.Input("times").EnumerateArray().Select(t => t.GetDouble())];
            check.Output(c, "discountAtTime", times.Select(t => (Func<object?>)(() => implied.Discount(t))).ToArray());
        }

        check.Complete();
    }
}
