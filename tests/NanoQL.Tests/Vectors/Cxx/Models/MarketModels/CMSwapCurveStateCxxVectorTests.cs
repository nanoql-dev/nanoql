// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's constant-maturity swap curve-state vectors,
/// <c>tests/vectors/cxx/models/marketmodels/curvestates/cmswapcurvestate.json</c>: <see cref="CMSwapCurveState"/>'s reads at
/// several spannings and first valid indexes, and its refused sets.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CMSwapCurveStateCxxVectorTests
{
    [Fact]
    public void CMSwapCurveStateCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/curvestates/cmswapcurvestate");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] times = c.Doubles("rateTimes"), rates = c.Doubles("values");
            if (c.String("call") == "reads")
            {
                var cs = new CMSwapCurveState(times, c.Int("spanning"));
                cs.SetOnCMSwapRates(rates, c.Int("first"));
                CheckReads(check, c, cs, c.Int("first"), c.Ints("spans"));
                continue;
            }

            check.Output(c, "short", Lazy(() => new CMSwapCurveState(times, 1).Also(s => s.SetOnCMSwapRates(rates[..^1]))));
            check.Output(c, "pastIndex", Lazy(() => new CMSwapCurveState(times, 1).Also(s => s.SetOnCMSwapRates(rates, rates.Length))));
        }

        check.Complete();
    }
}
