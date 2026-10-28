// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's notional risk vectors, <c>tests/vectors/cxx/experimental/catbonds/riskynotional.json</c>: the
/// <see cref="NotionalPath"/> a <see cref="DigitalNotionalRisk"/> or a <see cref="ProportionalNotionalRisk"/> writes
/// from events, its notional rate on dates and its loss.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NotionalRiskCxxVectorTests
{
    [Fact]
    public void NotionalRiskCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/catbonds/riskynotional");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            NotionalRisk risk = RiskOf(a[0]);
            var path = new NotionalPath();
            risk.UpdatePath(CatRiskCxxVectorTests.EventsOf(a[1]), path);
            check.Output(c, "notionalRate", a[2].EnumerateArray().Select(d => path.NotionalRate(CatRiskCxxVectorTests.DateOf(d))).ToArray());
            check.Output(c, "loss", path.Loss());
        }

        check.Complete();
    }

    // A notional risk given as ["Digital", threshold] or ["Proportional", attachment, exhaustion], without offset.
    internal static NotionalRisk RiskOf(JsonElement spec) =>
        spec[0].GetString() == "Digital"
            ? new DigitalNotionalRisk(new NoOffset(), VectorCase.Real(spec[1]))
            : new ProportionalNotionalRisk(new NoOffset(), VectorCase.Real(spec[1]), VectorCase.Real(spec[2]));
}
