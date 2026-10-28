// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's issuer vectors, <c>tests/vectors/cxx/experimental/credit/issuer.json</c>: each key's default
/// probabilities and the events matching each key between pairs of dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IssuerCxxVectorTests
{
    [Fact]
    public void IssuerCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/issuer");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Issuer issuer = PortfolioInputs.IssuerOf(c.Input("issuer"), context);
            JsonElement[] keys = [.. c.Input("keys").EnumerateArray()];
            Date[] dates = [.. c.Input("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
            (Date Start, Date End)[] windows =
                [.. c.Input("windows").EnumerateArray().Select(w => (new Date(w[0].GetInt32()), new Date(w[1].GetInt32())))];
            check.Output(c, "defaultProbability", Each(keys, k =>
            {
                DefaultProbabilityTermStructure curve = issuer.DefaultProbability(PortfolioInputs.KeyOf(k));
                return dates.Select(d => (object)curve.DefaultProbability(d)).ToArray();
            }));
            var calls = windows.SelectMany(w => keys.SelectMany(k => new[] { false, true }.Select(inc => (w, k, inc)))).ToArray();
            check.Output(c, "defaultedBetween", Each(calls, p =>
                issuer.DefaultedBetween(p.w.Start, p.w.End, PortfolioInputs.KeyOf(p.k), context, p.inc)?.Date));
            check.Output(c, "defaultsBetween", Each(calls, p =>
                issuer.DefaultsBetween(p.w.Start, p.w.End, PortfolioInputs.KeyOf(p.k), p.inc, context).Select(e => (object)e.Date).ToArray()));
        }

        check.Complete();
    }

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
