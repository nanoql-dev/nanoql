// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's discounter vectors, <c>tests/vectors/cxx/models/marketmodels/discounter.json</c>:
/// <see cref="MarketModelDiscounter"/>'s numeraire bonds on set curve states, payments on the rate times, outside them
/// and in the last period.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MarketModelDiscounterCxxVectorTests
{
    [Fact]
    public void MarketModelDiscounterCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/discounter");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] times = c.Doubles("rateTimes");
            var cs = new LMMCurveState(times);
            cs.SetOnForwardRates(c.Doubles("forwards"));
            check.Output(
                c,
                "numeraireBonds",
                new MarketModelDiscounter(c.Double("paymentTime"), times).NumeraireBonds(cs, c.Input("numeraire").GetInt32()));
        }

        check.Complete();
    }
}
