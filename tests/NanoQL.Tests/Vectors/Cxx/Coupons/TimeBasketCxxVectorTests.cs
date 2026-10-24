// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The time basket's harness vectors, <c>tests/vectors/cxx/cashflows/timebasket.json</c>: the sum and difference of
/// two <see cref="TimeBasket"/>s, QuantLib's <c>+=</c> and <c>-=</c> (<see cref="TimeBasket.Add"/> and
/// <see cref="TimeBasket.Subtract"/>, the operators' named forms), and <see cref="TimeBasket.HasDate"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TimeBasketCxxVectorTests
{
    [Fact]
    public void TimeBasketCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/timebasket");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            TimeBasket first = BasketOf(c.DateInputs("dates"), c.Doubles("values"));
            TimeBasket second = BasketOf(c.DateInputs("otherDates"), c.Doubles("otherValues"));
            TimeBasket plus = TimeBasket.Add(first, second), minus = TimeBasket.Subtract(first, second);
            check.Output(c, "plusDates", plus.Select(x => x.Key));
            check.Output(c, "plusValues", plus.Select(x => x.Value));
            check.Output(c, "minusDates", minus.Select(x => x.Key));
            check.Output(c, "minusValues", minus.Select(x => x.Value));
            check.Output(c, "hasDate", c.DateInputs("probes").Select(first.HasDate));
        }

        check.Complete();
    }

    // An empty basket through the constructor taking nothing, QuantLib's default constructor.
    private static TimeBasket BasketOf(Date[] dates, double[] values) =>
        dates.Length == 0 && values.Length == 0 ? new TimeBasket() : new TimeBasket(dates, values);
}
