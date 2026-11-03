// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The no-arbitrage SABR fit vectors,
/// <c>tests/vectors/experimental/volatility/noarbsabrinterpolatedsmilesection.json</c>:
/// <see cref="NoArbSabrInterpolatedSmileSection"/>'s fitted parameters, errors, end criterion and volatilities, the
/// outputs one-ulp refits leave stable.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NoArbSabrInterpolatedSmileSectionVectorTests
{
    [Fact]
    public void NoArbSabrInterpolatedSmileSectionVector()
    {
        var file = VectorFile.Load("experimental/volatility/noarbsabrinterpolatedsmilesection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement f = c.Input("fit");
            bool[] fixedParams = [.. f.GetProperty("fixed").EnumerateArray().Select(e => e.GetBoolean())];
            NoArbSabrInterpolatedSmileSection s = NoArbSabrInterpolatedSmileSection.Create(
                context.EvaluationDate + f.GetProperty("optionDays").GetInt32(),
                f.GetProperty("forward").GetDouble(),
                Doubles(f.GetProperty("strikes")),
                f.GetProperty("floating").GetBoolean(),
                f.GetProperty("atmVolatility").GetDouble(),
                Doubles(f.GetProperty("vols")),
                f.GetProperty("alpha").GetDouble(),
                f.GetProperty("beta").GetDouble(),
                f.GetProperty("nu").GetDouble(),
                f.GetProperty("rho").GetDouble(),
                context,
                fixedParams[0],
                fixedParams[1],
                fixedParams[2],
                fixedParams[3],
                f.GetProperty("vegaWeighted").GetBoolean());
            (string Key, Func<object?> Value)[] scalars =
            [
                ("alpha", () => s.Alpha), ("beta", () => s.Beta), ("nu", () => s.Nu), ("rho", () => s.Rho),
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
}
