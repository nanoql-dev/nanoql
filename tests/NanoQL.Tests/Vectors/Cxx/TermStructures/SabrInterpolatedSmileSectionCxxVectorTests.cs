// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's SABR-interpolated smile section vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/sabrinterpolatedsmilesection.json</c>: seven fits of
/// <see cref="SabrInterpolatedSmileSection"/> by both of QuantLib's constructors, free and fixed parameters,
/// unweighted, floating strikes, a missing quote and a shift, their fitted parameters, errors and end criteria where
/// stable under one-ulp moves of the inputs, the strike range, level and exercise time, and the volatility and variance
/// at stable strikes.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SabrInterpolatedSmileSectionCxxVectorTests
{
    [Fact]
    public void SabrInterpolatedSmileSectionCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/sabrinterpolatedsmilesection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("section");
            SabrInterpolatedSmileSection s = SectionOf(spec, c.Context());
            double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "atmLevel", s.AtmLevel());
            check.Output(c, "exerciseTime", s.ExerciseTime);
            // The fit's outputs the stability filter kept.
            foreach ((string name, object value) in new (string, object)[]
            {
                ("alpha", s.Alpha), ("beta", s.Beta), ("nu", s.Nu), ("rho", s.Rho), ("rmsError", s.RmsError), ("maxError", s.MaxError),
                ("endCriteria", s.EndCriteria),
            })
            {
                if (c.Expected.TryGetProperty(name, out _))
                {
                    check.Output(c, name, value);
                }
            }

            check.Output(c, "volatility", strikes.Select(k => (object?)s.Volatility(k)).ToArray());
            check.Output(c, "variance", strikes.Select(k => (object?)s.Variance(k)).ToArray());
        }

        check.Complete();
    }

    // The section by QuantLib's constructor on values, or on quotes, a missing one a null volatility.
    private static SabrInterpolatedSmileSection SectionOf(JsonElement spec, EvaluationContext context)
    {
        double? Param(string name) => SmileSectionInputs.NullableReal(spec.GetProperty(name));
        bool Flag(string name) => spec.GetProperty(name).GetBoolean();

        var optionDate = new Time.Date(spec.GetProperty("optionDate").GetInt32());
        double forward = IndexInputs.Real(spec.GetProperty("forward"));
        double[] strikes = VolatilityInputs.Reals(spec.GetProperty("strikes"));
        double atmVolatility = IndexInputs.Real(spec.GetProperty("atmVolatility"));
        double?[] vols = [.. spec.GetProperty("vols").EnumerateArray().Select(SmileSectionInputs.NullableReal)];
        Time.DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        double shift = IndexInputs.Real(spec.GetProperty("shift"));
        return Flag("quotes")
            ? SabrInterpolatedSmileSection.Create(
                optionDate, forward, strikes, Flag("hasFloatingStrikes"), atmVolatility, vols, Param("alpha"), Param("beta"),
                Param("nu"), Param("rho"), context, Flag("isAlphaFixed"), Flag("isBetaFixed"), Flag("isNuFixed"),
                Flag("isRhoFixed"), Flag("vegaWeighted"), null, null, dc, shift)
            : SabrInterpolatedSmileSection.Create(
                optionDate, forward, strikes, Flag("hasFloatingStrikes"), atmVolatility, [.. vols.Select(x => x!.Value)],
                Param("alpha"), Param("beta"), Param("nu"), Param("rho"), context, Flag("isAlphaFixed"), Flag("isBetaFixed"),
                Flag("isNuFixed"), Flag("isRhoFixed"), Flag("vegaWeighted"), null, null, dc, shift);
    }
}
