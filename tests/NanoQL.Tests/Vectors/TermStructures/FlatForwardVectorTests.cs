// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The flat-forward vectors, <c>tests/vectors/termstructures/yield/flatforward.json</c>: discounts, zero rates and
/// forward rates on curves with a fixed reference date; curves built from settlement days and the evaluation context;
/// and the range checks at and past the maximum time, with extrapolation switched on and off. QuantLib switches a
/// curve in place, where nanoql's switches return a copy: each switch here applies to the curve the previous one
/// returned, and the discount is read on that copy.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FlatForwardVectorTests
{
    [Fact]
    public void FlatForwardVector()
    {
        var file = VectorFile.Load("termstructures/yield/flatforward");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement curve = c.Input("curve");
            switch (c.String("kind"))
            {
                case "fixed":
                    CheckFixed(check, c, CashFlowInputs.CurveOf(curve));
                    break;
                case "settlementDays":
                    FlatForward moving = FlatForward.Create(
                        curve.GetProperty("settlementDays").GetInt32(),
                        CashFlowInputs.CalendarOf(curve.GetProperty("calendar")),
                        curve.GetProperty("forward").GetDouble(),
                        CashFlowInputs.DayCounterOf(curve.GetProperty("dayCounter")),
                        c.Context(),
                        (Compounding)curve.GetProperty("compounding").GetInt32(),
                        (Frequency)curve.GetProperty("frequency").GetInt32());
                    check.Output(c, "referenceDate", moving.ReferenceDate);
                    check.Output(c, "discount", Each(c.DateInputs("dates"), d => moving.Discount(d)));
                    break;
                case "range":
                    CheckRange(check, c, CashFlowInputs.CurveOf(curve));
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static void CheckFixed(VectorAssert check, VectorCase c, FlatForward curve)
    {
        Date[] dates = c.DateInputs("dates");
        check.Output(c, "referenceDate", curve.ReferenceDate);
        check.Output(c, "maxDate", curve.MaxDate);
        check.Output(c, "discount", Each(dates, d => curve.Discount(d)));
        foreach (string output in new[] { "zeroRateContinuous", "zeroRateSemiannual" })
        {
            (DayCounter dc, Compounding comp, Frequency freq) = RateConventions(c.Input(output));
            check.Output(c, output, Each(dates, d => curve.ZeroRate(d, dc, comp, freq).Rate));
        }

        JsonElement forward = c.Input("forwardRate");
        (DayCounter fdc, Compounding fcomp, Frequency ffreq) = RateConventions(forward);
        (Date Start, Date End)[] periods = forward.GetProperty("dates").EnumerateArray()
            .Select(p => (new Date(p[0].GetInt32()), new Date(p[1].GetInt32())))
            .ToArray();
        check.Output(c, "forwardRate", Each(periods, p => curve.ForwardRate(p.Start, p.End, fdc, fcomp, ffreq).Rate));

        JsonElement zeroTimes = c.Input("zeroRateTimes");
        var zcomp = (Compounding)zeroTimes.GetProperty("compounding").GetInt32();
        var zfreq = (Frequency)zeroTimes.GetProperty("frequency").GetInt32();
        double[] times = zeroTimes.GetProperty("times").EnumerateArray().Select(t => t.GetDouble()).ToArray();
        check.Output(c, "zeroRateTimes", Each(times, t => curve.ZeroRate(t, zcomp, zfreq).Rate));

        JsonElement forwardTimes = c.Input("forwardRateTimes");
        var tcomp = (Compounding)forwardTimes.GetProperty("compounding").GetInt32();
        var tfreq = (Frequency)forwardTimes.GetProperty("frequency").GetInt32();
        (double T1, double T2)[] timePairs = forwardTimes.GetProperty("times").EnumerateArray()
            .Select(p => (p[0].GetDouble(), p[1].GetDouble()))
            .ToArray();
        check.Output(c, "forwardRateTimes", Each(timePairs, p => curve.ForwardRate(p.T1, p.T2, tcomp, tfreq).Rate));

        // The period overload, which the wheel doesn't expose, delegates to the end date d + p.
        foreach ((Date start, Date end) in periods.Where(p => p.Start < p.End))
        {
            var tenor = new Period(end - start, TimeUnit.Days);
            Assert.Equal(
                curve.ForwardRate(start, end, fdc, fcomp, ffreq),
                curve.ForwardRate(start, tenor, fdc, fcomp, ffreq));
        }
    }

    private static void CheckRange(VectorAssert check, VectorCase c, FlatForward curve)
    {
        double[] times = c.Doubles("times");
        double pastMaxTime = times[^1];
        check.Output(c, "maxTime", curve.MaxTime);
        check.Output(c, "discountAtTimes", Each(times, t => curve.Discount(t)));
        check.Output(c, "discountAtTimesExtrapolating", Each(times, t => curve.Discount(t, true)));
        check.Output(c, "allowsExtrapolation", curve.AllowsExtrapolation);

        FlatForward enabled = curve.EnableExtrapolation();
        check.Output(c, "allowsAfterEnable", enabled.AllowsExtrapolation);
        check.Output(c, "discountAfterEnable", () => enabled.Discount(pastMaxTime));

        FlatForward disabled = enabled.DisableExtrapolation();
        check.Output(c, "allowsAfterDisable", disabled.AllowsExtrapolation);
        check.Output(c, "discountAfterDisable", () => disabled.Discount(pastMaxTime));
    }

    private static (DayCounter DayCounter, Compounding Compounding, Frequency Frequency) RateConventions(
        JsonElement spec) =>
        (CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            (Compounding)spec.GetProperty("compounding").GetInt32(),
            (Frequency)spec.GetProperty("frequency").GetInt32());

    private static Func<object?>[] Each<TIn, TOut>(TIn[] inputs, Func<TIn, TOut> f) =>
        inputs.Select(x => (Func<object?>)(() => f(x))).ToArray();
}
