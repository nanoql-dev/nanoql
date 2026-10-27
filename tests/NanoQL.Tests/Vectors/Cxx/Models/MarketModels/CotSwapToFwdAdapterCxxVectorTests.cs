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
/// The C++ harness's coterminal-to-forward adapter vectors,
/// <c>tests/vectors/cxx/models/marketmodels/models/cotswaptofwdadapter.json</c>:
/// <see cref="CotSwapToFwdAdapter"/> on FlatVol and AbcdVol models of coterminal swap rates on eight semi-annual and
/// six irregular rates, on the default evolution times and finer ones, at full and reduced factors, undisplaced and
/// displaced (its reads: counts, initial rates, displacements, each step's pseudo-root, covariance and total
/// covariance, and each rate's volatility over the steps); <see cref="CotSwapToFwdAdapterFactory"/> on FlatVolFactory;
/// and the refused models.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CotSwapToFwdAdapterCxxVectorTests
{
    [Fact]
    public void CotSwapToFwdAdapterCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/models/cotswaptofwdadapter");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("call"))
            {
                case "adapter":
                    CheckModel(check, c, () => new CotSwapToFwdAdapter(ModelOf(c.Input("model"))));
                    break;
                case "refused":
                    foreach (JsonProperty m in c.Input("models").EnumerateObject())
                    {
                        JsonElement spec = m.Value;
                        check.Output(c, m.Name, Lazy(() => new CotSwapToFwdAdapter(ModelOf(spec))));
                    }

                    break;
                default:
                {
                    var curve = new FlatForward(c.DateInput("referenceDate"), c.Double("rate"), new Actual365Fixed());
                    var factory = new CotSwapToFwdAdapterFactory(new FlatVolFactory(
                        c.Double("longTermCorrelation"), c.Double("beta"), c.Doubles("times"), c.Doubles("vols"), curve, c.Double("displacement")));
                    var evolution = new EvolutionDescription(c.Doubles("rateTimes"), c.Doubles("evolutionTimes"));
                    CheckModel(check, c, () => factory.Create(evolution, c.Int("factors")));
                    break;
                }
            }
        }

        check.Complete();
    }
}
