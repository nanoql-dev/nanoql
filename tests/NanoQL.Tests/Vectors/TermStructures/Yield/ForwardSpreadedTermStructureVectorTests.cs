// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The forward-spreaded curve vectors, <c>tests/vectors/termstructures/yield/forwardspreadedtermstructure.json</c>: the
/// zero-spreaded file's six originals, each spread by 1%, −0.5% and 0; the spreaded curve's inspectors, and its
/// discounts, zero rates and forward rates at 30 dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ForwardSpreadedTermStructureVectorTests
{
    [Fact]
    public void ForwardSpreadedTermStructureVector()
    {
        var file = VectorFile.Load("termstructures/yield/forwardspreadedtermstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            YieldTermStructure original = YieldCurveInputs.OriginalOf(c.Input("original"), c.Context());
            var spreaded = new ForwardSpreadedTermStructure(original, c.Double("spread"));
            Date[] dates = c.DateInputs("dates");
            YieldCurveInputs.CheckCurve(check, c, spreaded, dates);
        }

        check.Complete();
    }
}
