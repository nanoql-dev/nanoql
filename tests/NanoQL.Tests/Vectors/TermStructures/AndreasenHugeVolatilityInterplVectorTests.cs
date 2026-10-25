// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AndreasenHugeVolatilityInterpl vectors,
/// <c>tests/vectors/termstructures/volatility/equityfx/andreasenhugevolatilityinterpl.json</c>: six calibrations across
/// the calibration and interpolation types, their ranges, forwards and errors, and their option prices and local
/// volatilities at the points the stability filter kept.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AndreasenHugeVolatilityInterplVectorTests
{
    [Fact]
    public void AndreasenHugeVolatilityInterplVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/andreasenhugevolatilityinterpl");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            AndreasenHugeVolatilityInterpl a = InterplOf(c.Input("calibration"), c.Context().EvaluationDate);
            double[] times = c.Doubles("times");
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            check.Output(c, "maxDate", a.MaxDate);
            check.Output(c, "minStrike", a.MinStrike());
            check.Output(c, "maxStrike", a.MaxStrike());
            check.Output(c, "fwd", (object?[])[.. times.Select(t => (object?)a.Fwd(t))]);
            if (c.Expected.TryGetProperty("calibrationError", out _))
            {
                AndreasenHugeCalibrationError e = a.CalibrationError();
                check.Output(c, "calibrationError", (object?[])[e.Min, e.Max, e.Avg]);
            }

            check.Output(c, "callPrice", (object?[])[.. points.Select(p => Lazy(() => a.OptionPrice(p[0], p[1], OptionType.Call)))]);
            check.Output(c, "putPrice", (object?[])[.. points.Select(p => Lazy(() => a.OptionPrice(p[0], p[1], OptionType.Put)))]);
            check.Output(c, "localVol", (object?[])[.. points.Select(p => Lazy(() => a.LocalVol(p[0], p[1])))]);
        }

        check.Complete();
    }

    // The interpolation a calibration spec describes, its options' expiries offsets from the evaluation date.
    internal static AndreasenHugeVolatilityInterpl InterplOf(JsonElement spec, Date today)
    {
        (VanillaOption, double)[] options =
        [
            .. spec.GetProperty("options").EnumerateArray().Select(o => (
                new VanillaOption(
                    new PlainVanillaPayoff((OptionType)o[0].GetInt32(), o[1].GetDouble()), new EuropeanExercise(today + o[2].GetInt32())),
                o[3].GetDouble())),
        ];
        JsonElement minStrike = spec.GetProperty("minStrike"), maxStrike = spec.GetProperty("maxStrike");
        return new AndreasenHugeVolatilityInterpl(
            options,
            spec.GetProperty("spot").GetDouble(),
            CurveOf(spec.GetProperty("riskFree")),
            CurveOf(spec.GetProperty("dividend")),
            (AndreasenHugeVolatilityInterplInterpolationType)spec.GetProperty("interpolationType").GetInt32(),
            (AndreasenHugeVolatilityInterplCalibrationType)spec.GetProperty("calibrationType").GetInt32(),
            spec.GetProperty("nGridPoints").GetInt32(),
            minStrike.ValueKind == JsonValueKind.Null ? null : minStrike.GetDouble(),
            maxStrike.ValueKind == JsonValueKind.Null ? null : maxStrike.GetDouble());
    }
}
