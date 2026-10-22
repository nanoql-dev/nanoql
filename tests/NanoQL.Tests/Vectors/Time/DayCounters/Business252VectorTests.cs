// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Business/252 vectors, <c>tests/vectors/time/daycounters/business252.json</c>: the name, the day count and the
/// year fraction on Brazil's settlement calendar (QuantLib's default), BOVESPA, TARGET and the New York Stock Exchange.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Business252VectorTests
{
    [Fact]
    public void Business252()
    {
        var file = VectorFile.Load("time/daycounters/business252");
        var check = new VectorAssert(file);
        var dayCounters = new Dictionary<string, Business252>();
        foreach (VectorCase c in file.Cases)
        {
            string key = $"{c.String("calendar")} {c.NullableInt("market")} {c.Bool("default")}";
            if (!dayCounters.TryGetValue(key, out Business252? dc))
            {
                dc = c.Bool("default")
                    ? new Business252()
                    : new Business252(c.String("calendar") switch
                    {
                        "Brazil" => new Brazil((BrazilMarket)c.Int("market")),
                        "TARGET" => new TARGET(),
                        "UnitedStates" => new UnitedStates((UnitedStatesMarket)c.Int("market")),
                        var name => throw new ArgumentException($"unknown calendar {name}", nameof(c)),
                    });
                dayCounters[key] = dc;
            }

            Date d1 = c.DateInput("d1");
            Date[] d2 = c.DateInputs("d2");
            check.Output(c, "name", dc.Name);
            check.Output(c, "dayCount", Each(d2, x => dc.DayCount(d1, x)));
            check.Output(c, "yearFraction", Each(d2, x => dc.YearFraction(d1, x)));
        }

        check.Complete();
    }

    private static Func<object?>[] Each<T>(Date[] dates, Func<Date, T> f) =>
        dates.Select(x => (Func<object?>)(() => f(x))).ToArray();
}
