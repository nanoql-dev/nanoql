// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's coterminal swap curve-state vectors,
/// <c>tests/vectors/cxx/models/marketmodels/curvestates/coterminalswapcurvestate.json</c>: <see cref="CoterminalSwapCurveState"/>'s
/// reads from several first valid indexes, and its refused sets.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CoterminalSwapCurveStateCxxVectorTests
{
    [Fact]
    public void CoterminalSwapCurveStateCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/curvestates/coterminalswapcurvestate");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] times = c.Doubles("rateTimes"), rates = c.Doubles("values");
            if (c.String("call") == "reads")
            {
                var cs = new CoterminalSwapCurveState(times);
                cs.SetOnCoterminalSwapRates(rates, c.Int("first"));
                CheckReads(check, c, cs, c.Int("first"), c.Ints("spans"));
                continue;
            }

            check.Output(c, "short", Lazy(() => new CoterminalSwapCurveState(times).Also(s => s.SetOnCoterminalSwapRates(rates[..^1]))));
            check.Output(c, "pastIndex", Lazy(() => new CoterminalSwapCurveState(times).Also(s => s.SetOnCoterminalSwapRates(rates, rates.Length))));
        }

        check.Complete();
    }
}
