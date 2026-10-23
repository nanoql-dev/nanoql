// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The piecewise forward-spreaded curve vectors,
/// <c>tests/vectors/termstructures/yield/piecewiseforwardspreadedtermstructure.json</c>: the zero-spreaded file's six
/// originals under linear and backward-flat interpolation and three spread schedules, one from a later date against
/// QuantLib's curve with its first spread prepended at the reference date (the S-sr plan's SSR6 (vii)); the curve's
/// inspectors, and its discounts, zero rates and forward rates from the first spread's date.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PiecewiseForwardSpreadedTermStructureVectorTests
{
    [Fact]
    public void PiecewiseForwardSpreadedTermStructureVector()
    {
        var file = VectorFile.Load("termstructures/yield/piecewiseforwardspreadedtermstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            YieldTermStructure original = YieldCurveInputs.OriginalOf(c.Input("original"), c.Context());
            double[] spreads = [.. c.Input("spreads").EnumerateArray().Select(s => s.GetDouble())];
            Date[] spreadDates = c.DateInputs("spreadDates");
            YieldTermStructure curve = c.String("interpolator") switch
            {
                "Linear" => new InterpolatedPiecewiseForwardSpreadedTermStructure<Linear>(original, spreads, spreadDates),
                "BackwardFlat" => new InterpolatedPiecewiseForwardSpreadedTermStructure<BackwardFlat>(
                    original, spreads, spreadDates),
                var t => throw new InvalidOperationException($"unknown interpolator '{t}'"),
            };
            YieldCurveInputs.CheckCurve(check, c, curve, c.DateInputs("dates"));
        }

        check.Complete();
    }
}
