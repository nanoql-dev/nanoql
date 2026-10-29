// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's volatility cube vectors, <c>tests/vectors/cxx/experimental/volatility/volcube.json</c>:
/// <see cref="VolatilityCube"/>'s construction checks on SABR surfaces and abcd curves at given settlement days, the
/// counts of its surfaces and curves where QuantLib builds it, and QuantLib's refusals. The cube as
/// <c>tools/cxx/src/experimental/volatility/volcube.cpp</c> builds it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class VolatilityCubeCxxVectorTests
{
    private static readonly Period[] Tenors =
        [new(1, TimeUnit.Months), new(3, TimeUnit.Months), new(6, TimeUnit.Months), new(1, TimeUnit.Years), new(2, TimeUnit.Years)];

    private static readonly double[] AtmVols = [0.20, 0.24, 0.26, 0.255, 0.24];

    [Fact]
    public void VolatilityCubeCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/volatility/volcube");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement s = c.Input("cube");
            var index = new Euribor(new Period(6, TimeUnit.Months), new FlatForward(context.EvaluationDate, 0.03, new Actual365Fixed()));
            var cube = new Lazy<VolatilityCube>(() => new VolatilityCube(
                [.. s.GetProperty("surfaceSettlementDays").EnumerateArray().Select(d => new SabrVolSurface(
                    index,
                    Curve(d.GetInt32(), context),
                    Tenors,
                    [-0.01, 0.0, 0.01],
                    [.. Tenors.Select(_ => new[] { 0.01, 0.0, -0.005 })],
                    context))],
                [.. s.GetProperty("curveSettlementDays").EnumerateArray().Select(d => Curve(d.GetInt32(), context))]));
            check.Output(c, "surfaces", () => cube.Value.Surfaces.Length);
            if (c.Expected.TryGetProperty("curves", out _))
            {
                check.Output(c, "curves", () => cube.Value.Curves.Length);
            }
        }

        check.Complete();
    }

    private static AbcdAtmVolCurve Curve(int settlementDays, EvaluationContext context) =>
        AbcdAtmVolCurve.Create(settlementDays, new TARGET(), Tenors, AtmVols, context);
}
