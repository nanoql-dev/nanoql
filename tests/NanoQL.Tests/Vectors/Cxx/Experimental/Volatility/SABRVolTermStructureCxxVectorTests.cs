// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's SABR term structure vectors,
/// <c>tests/vectors/cxx/experimental/volatility/sabrvoltermstructure.json</c>: <see cref="SABRVolTermStructure"/> on rates
/// and equity parameters, its maximum date and strike range, and its volatilities and variances by time and by date at
/// strikes around the spot, extrapolating; QuantLib's refusals. The structure as
/// <c>tools/cxx/src/experimental/volatility/sabrvoltermstructure.cpp</c> builds it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SABRVolTermStructureCxxVectorTests
{
    [Fact]
    public void SABRVolTermStructureCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/volatility/sabrvoltermstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement s = c.Input("structure");
            double[] times = Doubles(s.GetProperty("queryTimes"));
            Date[] dates = [.. s.GetProperty("queryDates").EnumerateArray().Select(x => new Date(x.GetInt32()))];
            double[] ks = Doubles(s.GetProperty("queryStrikes"));
            var ts = new SABRVolTermStructure(
                s.GetProperty("alpha").GetDouble(),
                s.GetProperty("beta").GetDouble(),
                s.GetProperty("gamma").GetDouble(),
                s.GetProperty("rho").GetDouble(),
                s.GetProperty("s0").GetDouble(),
                s.GetProperty("r").GetDouble(),
                new Date(s.GetProperty("referenceDate").GetInt32()),
                CashFlowInputs.DayCounterOf(s.GetProperty("dayCounter")));
            (string Key, Func<object?> Value)[] outputs =
            [
                ("maxDate", () => ts.MaxDate),
                ("minStrike", () => ts.MinStrike()),
                ("maxStrike", () => ts.MaxStrike()),
                ("volAtTime", () => times.Select(t => ks.Select(k => ts.BlackVol(t, k, true)).ToArray()).ToArray()),
                ("varianceAtTime", () => times.Select(t => ks.Select(k => ts.BlackVariance(t, k, true)).ToArray()).ToArray()),
                ("volAtDate", () => dates.Select(d => ks.Select(k => ts.BlackVol(d, k, true)).ToArray()).ToArray()),
                ("varianceAtDate", () => dates.Select(d => ks.Select(k => ts.BlackVariance(d, k, true)).ToArray()).ToArray()),
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
}
