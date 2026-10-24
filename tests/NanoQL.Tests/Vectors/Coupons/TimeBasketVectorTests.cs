// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The time basket vectors, <c>tests/vectors/cashflows/timebasket.json</c>: <see cref="TimeBasket"/>'s items, dates
/// and values, and its rebinning on sorted, unsorted and repeated buckets, with QuantLib's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TimeBasketVectorTests
{
    [Fact]
    public void TimeBasketVector()
    {
        var file = VectorFile.Load("cashflows/timebasket");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Date[] dates = c.DateInputs("dates");
            double[] values = CouponInputs.Reals(c.Input("values"));
            if (c.Expected.TryGetProperty("construction", out _))
            {
                check.Output(c, "construction", () => new TimeBasket(dates, values).Count);
                continue;
            }

            var basket = new TimeBasket(dates, values);
            Date[] probes = c.DateInputs("probes");
            Date[][] buckets = [.. c.Input("buckets").EnumerateArray().Select(b => b.EnumerateArray().Select(d => new Date(d.GetInt32())).ToArray())];
            check.Output(c, "size", basket.Count);
            check.Output(c, "itemDates", basket.Select(x => x.Key));
            check.Output(c, "itemValues", basket.Select(x => x.Value));
            check.Output(c, "hasDate", probes.Select(basket.HasDate));
            check.Output(c, "rebinDates", buckets.Select(b => (Func<object?>)(() => basket.Rebin(b).Select(x => x.Key).ToArray())).ToArray());
            check.Output(c, "rebinValues", buckets.Select(b => (Func<object?>)(() => basket.Rebin(b).Select(x => x.Value).ToArray())).ToArray());
            check.Output(c, "values", probes.Select(d => basket[d]));
        }

        check.Complete();
    }
}
