// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The SVI fit vectors, <c>tests/vectors/experimental/volatility/sviinterpolatedsmilesection.json</c>:
/// <see cref="SviInterpolatedSmileSection"/>'s fitted parameters, errors, end criterion and volatilities, the outputs
/// S7R6's one-ulp refits leave stable.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SviInterpolatedSmileSectionVectorTests
{
    [Fact]
    public void SviInterpolatedSmileSectionVector()
    {
        var file = VectorFile.Load("experimental/volatility/sviinterpolatedsmilesection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement f = c.Input("fit");
            bool[] fixedParams = [.. f.GetProperty("fixed").EnumerateArray().Select(e => e.GetBoolean())];
            SviInterpolatedSmileSection s = SviInterpolatedSmileSection.Create(
                context.EvaluationDate + f.GetProperty("optionDays").GetInt32(),
                f.GetProperty("forward").GetDouble(),
                Doubles(f.GetProperty("strikes")),
                f.GetProperty("floating").GetBoolean(),
                f.GetProperty("atmVolatility").GetDouble(),
                Doubles(f.GetProperty("vols")),
                Guess(f, "a"),
                Guess(f, "b"),
                Guess(f, "sigma"),
                Guess(f, "rho"),
                Guess(f, "m"),
                context,
                fixedParams[0],
                fixedParams[1],
                fixedParams[2],
                fixedParams[3],
                fixedParams[4],
                f.GetProperty("vegaWeighted").GetBoolean());
            (string Key, Func<object?> Value)[] scalars =
            [
                ("a", () => s.A), ("b", () => s.B), ("sigma", () => s.Sigma), ("rho", () => s.Rho), ("m", () => s.M),
                ("rmsError", () => s.RmsError), ("maxError", () => s.MaxError), ("endCriteria", () => (int)s.EndCriteria),
                ("atmLevel", () => s.AtmLevel()), ("minStrike", () => s.MinStrike()), ("maxStrike", () => s.MaxStrike()),
            ];
            foreach ((string key, Func<object?> value) in scalars)
            {
                if (c.Expected.TryGetProperty(key, out _))
                {
                    check.Output(c, key, value);
                }
            }

            check.Output(c, "values", Doubles(c.Input("valueStrikes")).Select(k => Lazy(() => s.Volatility(k))).ToArray());
        }

        check.Complete();
    }

    private static double? Guess(JsonElement f, string key) =>
        f.GetProperty(key).ValueKind == JsonValueKind.Null ? null : f.GetProperty(key).GetDouble();
}
