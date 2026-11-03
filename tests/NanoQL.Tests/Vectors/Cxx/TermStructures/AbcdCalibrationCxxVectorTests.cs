// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's abcd calibration vectors, <c>tests/vectors/cxx/termstructures/volatility/abcdcalibration.json</c>:
/// <see cref="AbcdCalibration"/>'s fits of falling, rising and dipping term structures from two guesses, with d fixed,
/// vega weighted and capped, their parameters, errors, end criteria, adjustment factors and fitted volatilities where
/// stable under one-ulp moves of the inputs; its members at the guesses, before a fit; and the fits QuantLib refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AbcdCalibrationCxxVectorTests
{
    [Fact]
    public void AbcdCalibrationCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/abcdcalibration");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("fit");
            double[] points = VolatilityInputs.Reals(c.Input("points"));
            double[] t = VolatilityInputs.Reals(spec.GetProperty("t"));
            double[] vols = VolatilityInputs.Reals(spec.GetProperty("blackVols"));
            switch (c.String("kind"))
            {
                case "compute":
                    AbcdCalibration fit = CalibrationOf(spec).Compute();
                    Check(check, c, "a", () => fit.A);
                    Check(check, c, "b", () => fit.B);
                    Check(check, c, "c", () => fit.C);
                    Check(check, c, "d", () => fit.D);
                    CheckMembers(check, c, fit, t, vols, points);
                    Check(check, c, "endCriteria", () => fit.EndCriteria);
                    break;
                case "guess":
                    CheckMembers(check, c, CalibrationOf(spec), t, vols, points);
                    break;
                case "refusal":
                    check.Output(c, "compute", () => CalibrationOf(spec).Compute().A);
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    /// <summary>The calibration a harness fit spec describes, not yet fitted.</summary>
    /// <param name="spec">The spec of <c>tools/cxx/src/termstructures/volatility/abcdspec.hpp</c>.</param>
    /// <returns>The calibration.</returns>
    internal static AbcdCalibration CalibrationOf(JsonElement spec)
    {
        double[] guess = VolatilityInputs.Reals(spec.GetProperty("guess"));
        bool[] fixedFlags = [.. spec.GetProperty("fixed").EnumerateArray().Select(x => x.GetBoolean())];
        return new AbcdCalibration(
            VolatilityInputs.Reals(spec.GetProperty("t")),
            VolatilityInputs.Reals(spec.GetProperty("blackVols")),
            guess[0],
            guess[1],
            guess[2],
            guess[3],
            fixedFlags[0],
            fixedFlags[1],
            fixedFlags[2],
            fixedFlags[3],
            spec.GetProperty("vegaWeighted").GetBoolean(),
            EndCriteriaOf(spec.GetProperty("endCriteria")),
            null);
    }

    /// <summary>The end criteria a harness spec describes, or <see langword="null"/> for the fit's default.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The end criteria.</returns>
    internal static EndCriteria? EndCriteriaOf(JsonElement spec) => spec.ValueKind == JsonValueKind.Null
        ? null
        : new EndCriteria(
            spec.GetProperty("maxIterations").GetInt32(),
            spec.GetProperty("maxStationaryStateIterations").GetInt32(),
            spec.GetProperty("rootEpsilon").GetDouble(),
            spec.GetProperty("functionEpsilon").GetDouble(),
            spec.GetProperty("gradientNormEpsilon").GetDouble());

    // The members a fit and its guesses share: the errors, the factors on the fit's times and the volatility at the
    // points.
    private static void CheckMembers(VectorAssert check, VectorCase c, AbcdCalibration cal, double[] t, double[] vols, double[] points)
    {
        Check(check, c, "error", () => cal.Error());
        Check(check, c, "maxError", () => cal.MaxError());
        Check(check, c, "errors", () => cal.Errors().ToArray());
        Check(check, c, "k", () => cal.K(t, vols));
        Check(check, c, "value", () => points.Select(cal.Value).ToArray());
    }

    private static void Check(VectorAssert check, VectorCase c, string name, Func<object?> value)
    {
        if (c.Expected.TryGetProperty(name, out _))
        {
            check.Output(c, name, value);
        }
    }
}
