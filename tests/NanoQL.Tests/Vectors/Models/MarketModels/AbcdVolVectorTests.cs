// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AbcdVol vectors, <c>tests/vectors/models/marketmodels/models/abcdvol.json</c>: <see cref="AbcdVol"/> on eight
/// semi-annual and six irregular rates for three abcd functions, on exponential correlations, several evolution times,
/// full and reduced factors and one, undisplaced and displaced: the model's counts, initial rates and displacements,
/// each step's pseudo-root, covariance and total covariance, and each rate's volatility over the steps; and the refused
/// models.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AbcdVolVectorTests
{
    [Fact]
    public void AbcdVolVector()
    {
        var file = VectorFile.Load("models/marketmodels/models/abcdvol");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.String("call") == "refused")
            {
                foreach (JsonProperty m in c.Input("models").EnumerateObject())
                {
                    JsonElement spec = m.Value;
                    check.Output(c, m.Name, Lazy(() => ModelOf(spec)));
                }

                continue;
            }

            CheckModel(check, c, () => ModelOf(c.Input("model")));
        }

        check.Complete();
    }
}
