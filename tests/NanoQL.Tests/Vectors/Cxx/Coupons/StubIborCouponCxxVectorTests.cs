// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's weighted index vectors, <c>tests/vectors/cxx/cashflows/stubiborcoupon.json</c>:
/// <see cref="StubIborCouponWeightedIndex"/>, which the wheel doesn't bind, with its inspectors (its name among them,
/// whose weights tie at the fifth decimal), maturity, forecast, past and stored fixings, the refusal of a clone, and the
/// constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StubIborCouponCxxVectorTests
{
    [Fact]
    public void StubIborCouponCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/stubiborcoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            (IborIndex Index, double Weight)[] components = c.Input("components").EnumerateArray()
                .Select(x => (IndexInputs.IborIndexOf(x[0]), x[1].GetDouble()))
                .ToArray();
            if (c.Expected.TryGetProperty("index", out _))
            {
                check.Output(c, "index", () => new StubIborCouponWeightedIndex(components));
                continue;
            }

            var index = new StubIborCouponWeightedIndex(components);
            check.Output(c, "name", index.Name);
            check.Output(c, "familyName", index.FamilyName);
            check.Output(c, "tenor", new object[] { index.Tenor.Length, index.Tenor.Units });
            check.Output(c, "fixingDays", index.FixingDays);
            check.Output(c, "dayCounter", index.DayCounter.Name);
            check.Output(c, "businessDayConvention", index.BusinessDayConvention);
            check.Output(c, "endOfMonth", index.EndOfMonth);
            check.Output(c, "allowsNativeFixings", index.AllowsNativeFixings);
            check.Output(
                c,
                "components",
                index.Components.Select(x => new object[] { x.Index.Name, x.Weight }).ToArray());
            check.Output(c, "maturityDate", Each(c.DateInputs("valueDates"), index.MaturityDate));
            check.Output(c, "forecastFixing", Each(c.DateInputs("forecastDates"), d => index.ForecastFixing(d, context)));
            check.Output(c, "pastFixing", Each(c.DateInputs("pastDates"), d => index.PastFixing(d, context)));
            check.Output(c, "fixing", Each(c.DateInputs("fixingDates"), d => index.Fixing(d, context)));
            check.Output(c, "clone", () => index.Clone(null));
        }

        check.Complete();
    }

    private static Func<object?>[] Each<T>(Date[] dates, Func<Date, T> f) => CouponInputs.Each(dates, f);
}
