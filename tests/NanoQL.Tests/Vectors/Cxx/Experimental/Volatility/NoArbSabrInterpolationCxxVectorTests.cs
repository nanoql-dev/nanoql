// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's no-arbitrage SABR interpolation vectors,
/// <c>tests/vectors/cxx/experimental/volatility/noarbsabrinterpolation.json</c>: <see cref="NoArbSabrInterpolation"/>'s
/// fits at expiries of 5 and 10 years, their parameters, errors, end criteria, weights and values, and the
/// <see cref="NoArbSabr"/> factory's values, the outputs S7R6's one-ulp refits leave stable; QuantLib's refusal. The
/// fits as <c>tools/cxx/src/experimental/volatility/noarbsabrinterpolation.cpp</c> runs them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NoArbSabrInterpolationCxxVectorTests
{
    [Fact]
    public void NoArbSabrInterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/volatility/noarbsabrinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] points = Doubles(c.Input("points"));
            if (c.Inputs.TryGetProperty("factory", out JsonElement f))
            {
                NoArbSabr factory = Factory(f);
                check.Output(c, "values", () =>
                {
                    Interpolation fitted = factory.Interpolate(Doubles(f.GetProperty("x")), Doubles(f.GetProperty("y")));
                    return CubeInputs.Kept(c, "values", points.Select(p => fitted.Evaluate(p, true)));
                });
                continue;
            }

            JsonElement s = c.Input("interpolation");
            var fit = new Lazy<NoArbSabrInterpolation>(() => new NoArbSabrInterpolation(
                Doubles(s.GetProperty("x")),
                Doubles(s.GetProperty("y")),
                s.GetProperty("t").GetDouble(),
                s.GetProperty("forward").GetDouble(),
                SmileSectionInputs.NullableReal(s.GetProperty("alpha")),
                SmileSectionInputs.NullableReal(s.GetProperty("beta")),
                SmileSectionInputs.NullableReal(s.GetProperty("nu")),
                SmileSectionInputs.NullableReal(s.GetProperty("rho")),
                s.GetProperty("alphaIsFixed").GetBoolean(),
                s.GetProperty("betaIsFixed").GetBoolean(),
                s.GetProperty("nuIsFixed").GetBoolean(),
                s.GetProperty("rhoIsFixed").GetBoolean(),
                s.GetProperty("vegaWeighted").GetBoolean(),
                null,
                null,
                s.GetProperty("errorAccept").GetDouble(),
                s.GetProperty("useMaxError").GetBoolean(),
                s.GetProperty("maxGuesses").GetInt32()));
            (string Key, Func<object?> Value)[] outputs =
            [
                ("expiry", () => fit.Value.Expiry),
                ("forward", () => fit.Value.Forward),
                ("alpha", () => fit.Value.Alpha),
                ("beta", () => fit.Value.Beta),
                ("nu", () => fit.Value.Nu),
                ("rho", () => fit.Value.Rho),
                ("rmsError", () => fit.Value.RmsError),
                ("maxError", () => fit.Value.MaxError),
                ("endCriteria", () => (int)fit.Value.EndCriteria),
                ("interpolationWeights", () => fit.Value.InterpolationWeights().ToArray()),
                ("values", () => CubeInputs.Kept(c, "values", points.Select(p => fit.Value.Evaluate(p, true)))),
            ];
            foreach ((string key, Func<object?> value) in outputs)
            {
                if (c.Expected.TryGetProperty(key, out _))
                {
                    check.Output(c, key, value);
                }
            }
        }

        check.Complete();
    }

    private static NoArbSabr Factory(JsonElement s) => new(
        s.GetProperty("t").GetDouble(),
        s.GetProperty("forward").GetDouble(),
        SmileSectionInputs.NullableReal(s.GetProperty("alpha")),
        SmileSectionInputs.NullableReal(s.GetProperty("beta")),
        SmileSectionInputs.NullableReal(s.GetProperty("nu")),
        SmileSectionInputs.NullableReal(s.GetProperty("rho")),
        s.GetProperty("alphaIsFixed").GetBoolean(),
        s.GetProperty("betaIsFixed").GetBoolean(),
        s.GetProperty("nuIsFixed").GetBoolean(),
        s.GetProperty("rhoIsFixed").GetBoolean(),
        s.GetProperty("vegaWeighted").GetBoolean(),
        null,
        null,
        s.GetProperty("errorAccept").GetDouble(),
        s.GetProperty("useMaxError").GetBoolean(),
        s.GetProperty("maxGuesses").GetInt32());
}
