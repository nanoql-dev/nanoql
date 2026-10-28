// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's pool vectors, <c>tests/vectors/cxx/experimental/credit/pool.json</c>: pools added in name order,
/// their names, keys, issuers and times.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PoolCxxVectorTests
{
    [Fact]
    public void PoolCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/pool");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Pool pool = PortfolioInputs.PoolOf(c.Input("pool"), context);
            string[] names = [.. c.Input("names").EnumerateArray().Select(n => n.GetString()!)];
            JsonElement[] keys = [.. c.Input("keys").EnumerateArray()];
            Date[] dates = [.. c.Input("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
            check.Output(c, "inspect", new object[]
            {
                pool.Size, pool.Names.ToArray(), pool.DefaultKeys.Select(PortfolioInputs.KeySummary).ToArray(),
            });
            check.Output(c, "has", Each(names, n => pool.Has(n)));
            check.Output(c, "get", Each(
                names.SelectMany(n => keys.SelectMany(k => dates.Select(d => (n, k, d)))),
                p => pool.Get(p.n).DefaultProbability(PortfolioInputs.KeyOf(p.k)).DefaultProbability(p.d)));
            check.Output(c, "defaultKey", Each(names, n => PortfolioInputs.KeySummary(pool.DefaultKey(n))));
            check.Output(c, "getTime", Each(
                c.Input("setTimes").EnumerateArray().SelectMany(s => names.Select(n => (s, n))),
                p =>
                {
                    Pool timed = pool;
                    foreach (JsonElement x in p.s.EnumerateArray())
                    {
                        timed = timed.SetTime(x[0].GetString()!, x[1].GetDouble());
                    }

                    return timed.GetTime(p.n);
                }));
        }

        check.Complete();
    }

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
