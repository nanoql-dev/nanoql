// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The zero-spreaded curve vectors, <c>tests/vectors/termstructures/yield/zerospreadedtermstructure.json</c>: six
/// originals (flat curves with a fixed reference date and from settlement days, discount curves with and without jumps,
/// a zero curve and a forward curve, some extrapolating) under spreads in several compoundings and frequencies, two of
/// them refused at every query; the spreaded curve's reference date, day counter, calendar, maximum date and time and
/// extrapolation flag, and its discounts, zero rates and forward rates at 30 dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ZeroSpreadedTermStructureVectorTests
{
    [Fact]
    public void ZeroSpreadedTermStructureVector()
    {
        var file = VectorFile.Load("termstructures/yield/zerospreadedtermstructure");
        var check = new VectorAssert(file);
        var a365 = new Actual365Fixed();
        foreach (VectorCase c in file.Cases)
        {
            YieldTermStructure original = YieldCurveInputs.OriginalOf(c.Input("original"), c.Context());
            var spreaded = new ZeroSpreadedTermStructure(
                original, c.Double("spread"), (Compounding)c.Int("compounding"), (Frequency)c.Int("frequency"));
            Date[] dates = c.DateInputs("dates");

            check.Output(c, "referenceDate", spreaded.ReferenceDate);
            check.Output(c, "dayCounter", spreaded.DayCounter.Name);
            check.Output(c, "calendar", spreaded.Calendar?.Name);
            check.Output(c, "maxDate", () => spreaded.MaxDate);
            check.Output(c, "maxTime", () => spreaded.MaxTime);
            check.Output(c, "allowsExtrapolation", spreaded.AllowsExtrapolation);
            check.Output(c, "discount", Each(dates, d => spreaded.Discount(d)));
            check.Output(
                c, "zeroRate", Each(dates, d => spreaded.ZeroRate(d, a365, Compounding.Continuous, Frequency.Annual).Rate));
            check.Output(
                c,
                "forwardRate",
                Each(dates, d => spreaded.ForwardRate(
                    d, d + new Period(6, TimeUnit.Months), a365, Compounding.Compounded, Frequency.Semiannual).Rate));
        }

        check.Complete();
    }

    private static Func<object?>[] Each<T>(Date[] dates, Func<Date, T> f) => YieldCurveInputs.Each(dates, f);
}
