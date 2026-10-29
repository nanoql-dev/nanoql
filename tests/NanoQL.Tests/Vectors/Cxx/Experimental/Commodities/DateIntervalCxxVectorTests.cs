// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.CommodityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's date interval vectors, <c>tests/vectors/cxx/experimental/commodities/dateinterval.json</c>:
/// <see cref="DateInterval.Intersection"/> and <see cref="DateInterval.IsDateBetween"/> under its four flag pairs, at
/// dates inside and outside the interval.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DateIntervalCxxVectorTests
{
    [Fact]
    public void DateIntervalCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/commodities/dateinterval");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var interval = new DateInterval(new Date(c.Input("startDate").GetInt32()), new Date(c.Input("endDate").GetInt32()));
            check.Output(c, "intersection", c.Input("others").EnumerateArray().Select(o =>
            {
                DateInterval? x = interval.Intersection(new DateInterval(new Date(o[0].GetInt32()), new Date(o[1].GetInt32())));
                return x is null ? null : (object?)new object?[] { x.StartDate, x.EndDate };
            }).ToArray());
            check.Output(c, "equal", c.Input("others").EnumerateArray().Select(o =>
            {
                var other = new DateInterval(new Date(o[0].GetInt32()), new Date(o[1].GetInt32()));
                return (object?)new object?[] { interval == other, interval != other };
            }).ToArray());
            check.Output(c, "text", interval.ToString());
            check.Output(c, "between", Dates(c.Input("dates")).Select(d => (object?)new object?[]
            {
                interval.IsDateBetween(d, true, true), interval.IsDateBetween(d, true, false),
                interval.IsDateBetween(d, false, true), interval.IsDateBetween(d, false, false),
            }).ToArray());
        }

        check.Complete();
    }
}
