// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The interval price's vectors, <c>tests/vectors/prices.json</c>: twenty prices through their inspectors, each type's
/// value and modifier and the four-value modifier, and eight series through <c>MakeSeries</c>, <c>ExtractValues</c>
/// and <c>ExtractComponent</c>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PricesVectorTests
{
    private static readonly (string Name, IntervalPriceType Type)[] Types =
        [("Open", IntervalPriceType.Open), ("Close", IntervalPriceType.Close), ("High", IntervalPriceType.High), ("Low", IntervalPriceType.Low)];

    [Fact]
    public void PricesVector()
    {
        var file = VectorFile.Load("prices");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name.StartsWith("price ", StringComparison.Ordinal))
            {
                double[] q = c.Doubles("price");
                var p = new IntervalPrice(q[0], q[1], q[2], q[3]);
                check.Output(c, "open", p.Open);
                check.Output(c, "close", p.Close);
                check.Output(c, "high", p.High);
                check.Output(c, "low", p.Low);
                double replacement = c.Double("replacement");
                foreach ((string name, IntervalPriceType type) in Types)
                {
                    check.Output(c, "value" + name, p.Value(type));
                    IntervalPrice set = p.SetValue(replacement, type);
                    check.Output(c, "set" + name, (double[])[set.Open, set.Close, set.High, set.Low]);
                }

                double[] news = c.Doubles("values");
                IntervalPrice all = p.SetValues(news[0], news[1], news[2], news[3]);
                check.Output(c, "setValues", (double[])[all.Open, all.Close, all.High, all.Low]);
            }
            else
            {
                TimeSeries<IntervalPrice> series = IntervalPrice.MakeSeries(c.DateInputs("dates"), c.Doubles("open"), c.Doubles("close"), c.Doubles("high"), c.Doubles("low"));
                check.Output(c, "dates", series.Dates().Select(d => (object?)d));
                foreach ((string name, IntervalPriceType type) in Types)
                {
                    check.Output(c, "values" + name, IntervalPrice.ExtractValues(series, type));
                    TimeSeries<double> component = IntervalPrice.ExtractComponent(series, type);
                    check.Output(c, "component" + name + "Dates", component.Dates().Select(d => (object?)d));
                    check.Output(c, "component" + name + "Values", component.Values());
                }
            }
        }

        check.Complete();
    }
}
