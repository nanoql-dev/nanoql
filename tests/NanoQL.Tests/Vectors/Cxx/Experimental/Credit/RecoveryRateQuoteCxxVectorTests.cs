// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's recovery-rate vectors, <c>tests/vectors/cxx/experimental/credit/recoveryratequote.json</c>: ISDA's
/// conventional recoveries, quotes and the constant recovery model.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RecoveryRateQuoteCxxVectorTests
{
    [Fact]
    public void RecoveryRateQuoteCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/recoveryratequote");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Seniority[] seniorities = [.. c.Input("seniorities").EnumerateArray().Select(s => (Seniority)s.GetInt32())];
            Seniority[] asked = [.. c.Input("asked").EnumerateArray().Select(s => (Seniority)s.GetInt32())];
            JsonElement[] keys = [.. c.Input("keys").EnumerateArray()];
            Date[] dates = [.. c.Input("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
            check.Output(c, "conventionalRecovery", Each(seniorities, s => RecoveryRateQuote.ConventionalRecovery(s)));
            check.Output(
                c,
                "isdaConvMap",
                RecoveryRateQuote.MakeIsdaConvMap().Select(kv => new object[] { (int)kv.Key, kv.Value }).ToArray());
            check.Output(c, "quote", Each(c.Input("quotes").EnumerateArray(), q =>
            {
                var x = new RecoveryRateQuote(q[0].GetDouble(), (Seniority)q[1].GetInt32());
                return new object[] { x.Value, (int)x.Seniority };
            }));
            JsonElement[] models = [.. c.Input("models").EnumerateArray()];
            check.Output(c, "recoveryValue", Each(
                models.SelectMany(m => dates.SelectMany(d => keys.Select(k => (m, d, k)))),
                p => new ConstantRecoveryModel(p.m[0].GetDouble(), (Seniority)p.m[1].GetInt32()).RecoveryValue(p.d, PortfolioInputs.KeyOf(p.k))));
            check.Output(c, "appliesToSeniority", Each(
                models.SelectMany(m => asked.Select(a => (m, a))),
                p => new ConstantRecoveryModel(p.m[0].GetDouble(), (Seniority)p.m[1].GetInt32()).AppliesToSeniority(p.a)));
        }

        check.Complete();
    }

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
