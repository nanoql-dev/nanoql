// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's flat-volatility model vectors, <c>tests/vectors/cxx/models/marketmodels/models/flatvol.json</c>:
/// <see cref="FlatVol"/>'s reads through <see cref="MarketModel"/>'s members (pseudo-roots, covariances, total
/// covariances, time-dependent volatilities), <see cref="FlatVolFactory"/>'s models, <see cref="FlatVolFunctions.FlatVolCovariance"/>,
/// and the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FlatVolCxxVectorTests
{
    [Fact]
    public void FlatVolCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/models/flatvol");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("call"))
            {
                case "model":
                    CheckModel(check, c, () => ModelOf(c.Input("model")));
                    break;
                case "refused":
                    foreach (JsonProperty m in c.Input("models").EnumerateObject())
                    {
                        JsonElement spec = m.Value;
                        check.Output(c, m.Name, Lazy(() => ModelOf(spec)));
                    }

                    break;
                case "factory":
                {
                    var curve = new FlatForward(c.DateInput("referenceDate"), c.Double("rate"), new Actual365Fixed());
                    var factory = new FlatVolFactory(
                        c.Double("longTermCorrelation"), c.Double("beta"), c.Doubles("times"), c.Doubles("vols"), curve, c.Double("displacement"));
                    var evolution = new EvolutionDescription(c.Doubles("rateTimes"), c.Doubles("evolutionTimes"));
                    CheckModel(check, c, () => factory.Create(evolution, c.Int("factors")));
                    break;
                }

                default:
                {
                    double[][] args = [.. c.Input("args").EnumerateArray().Select(Reals)];
                    check.Output(c, "covariances", args.Select(a => FlatVolFunctions.FlatVolCovariance(a[0], a[1], a[2], a[3], a[4], a[5])).ToArray());
                    check.Output(c, "reversed", Lazy(() => FlatVolFunctions.FlatVolCovariance(1.0, 0.5, 2.0, 2.0, 0.2, 0.2)));
                    break;
                }
            }
        }

        check.Complete();
    }
}
