// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.CommodityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's commodity curve vectors, <c>tests/vectors/cxx/experimental/commodities/commoditycurve.json</c>:
/// <see cref="CommodityCurve"/>'s node times, last date, prices, basis prices and nearby prices, on the curve built
/// from its dates; the same curve through <see cref="CommodityCurve.Create"/> and <see cref="CommodityCurve.SetPrices"/>
/// gives the same values (its first date the evaluation date).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CommodityCurveCxxVectorTests
{
    [Fact]
    public void CommodityCurveCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/commodities/commoditycurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CommodityCurve curve = CurveOf(c.Input("curve"), context);
            CommodityCurve moving = MovingCurveOf(c.Input("curve"), context);
            Date[] dates = Dates(c.Input("dates"));
            var contracts = ContractsOf(c.Input("contracts"));
            int nearby = c.Input("nearbyOffset").GetInt32();

            check.Output(c, "nodes", curve.Nodes.Select(n => (object?)new object?[] { n.Date, n.Price }).ToArray());
            check.Output(c, "text", curve.ToString());
            check.Output(c, "times", curve.Times);
            check.Output(c, "maxDate", curve.MaxDate);
            check.Output(c, "price", dates.Select(d => curve.Price(d, null, 0)).ToArray());
            check.Output(c, "basisOfPrice", dates.Select(curve.BasisOfPrice).ToArray());
            check.Output(c, "nearby", dates.Select(d => (Func<object?>)(() => new object?[]
            {
                curve.Price(d, contracts, nearby), curve.UnderlyingPriceDate(d, contracts, nearby),
            })).ToArray());

            Assert.Equal(curve.Nodes.Select(n => n.Date), curve.Dates);
            Assert.Equal(curve.Nodes.Select(n => n.Price), curve.Prices);
            Assert.Equal(curve.Times, moving.Times);
            Assert.Equal(dates.Select(d => curve.Price(d, null, 0)), dates.Select(d => moving.Price(d, null, 0)));
        }

        check.Complete();
    }
}
