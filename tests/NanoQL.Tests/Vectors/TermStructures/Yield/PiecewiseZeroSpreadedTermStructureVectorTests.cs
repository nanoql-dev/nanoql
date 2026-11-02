// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The piecewise zero-spreaded curve vectors, <c>tests/vectors/termstructures/yield/piecewisezerospreadedtermstructure.json</c>:
/// the zero-spreaded file's six originals under three spread schedules, linear, backward-flat and cubic interpolation and
/// three compoundings, and the named linear class; the curve's inspectors, and its discounts, zero rates and forward
/// rates at 30 dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PiecewiseZeroSpreadedTermStructureVectorTests
{
    [Fact]
    public void PiecewiseZeroSpreadedTermStructureVector()
    {
        var file = VectorFile.Load("termstructures/yield/piecewisezerospreadedtermstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            YieldTermStructure original = YieldCurveInputs.OriginalOf(c.Input("original"), c.Context());
            double[] spreads = [.. c.Input("spreads").EnumerateArray().Select(s => s.GetDouble())];
            Date[] spreadDates = c.DateInputs("spreadDates");
            var comp = (Compounding)c.Int("compounding");
            var freq = (Frequency)c.Int("frequency");
            JsonElement interpolator = c.Input("interpolator");
            YieldTermStructure curve = c.Input("named").GetBoolean()
                ? new PiecewiseZeroSpreadedTermStructure(original, spreads, spreadDates, comp, freq)
                : interpolator.GetProperty("type").GetString() switch
                {
                    "Linear" => new InterpolatedPiecewiseZeroSpreadedTermStructure<Linear>(
                        original, spreads, spreadDates, comp, freq),
                    "BackwardFlat" => new SpreadedBackwardFlatZeroInterpolatedTermStructure(
                        original, spreads, spreadDates, comp, freq),
                    "Cubic" => new SpreadedCubicZeroInterpolatedTermStructure(
                        original, spreads, spreadDates, comp, freq, BootstrapInputs.CubicOf(interpolator)),
                    var t => throw new InvalidOperationException($"unknown interpolator '{t}'"),
                };
            YieldCurveInputs.CheckCurve(check, c, curve, c.DateInputs("dates"));
        }

        check.Complete();
    }
}
