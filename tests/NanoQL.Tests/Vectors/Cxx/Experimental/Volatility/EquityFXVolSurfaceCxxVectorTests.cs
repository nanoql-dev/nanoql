// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's equity-FX surface vectors, <c>tests/vectors/cxx/experimental/volatility/equityfxvolsurface.json</c>:
/// <see cref="EquityFXVolSurface"/>'s at-the-money forward volatilities and variances by date and by time, each pair
/// QuantLib refuses a throw, on the twin of <c>tools/cxx/src/experimental/volatility/equityfxvolsurface.cpp</c>'s
/// surface (QuantLib derives nothing from the abstract surface): a flat section at each time, its volatility linear in
/// time.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EquityFXVolSurfaceCxxVectorTests
{
    [Fact]
    public void EquityFXVolSurfaceCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/volatility/equityfxvolsurface");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement s = c.Input("surface");
            var surface = new LinearFlatSurface(
                new Date(s.GetProperty("referenceDate").GetInt32()), s.GetProperty("v0").GetDouble(), s.GetProperty("slope").GetDouble());
            Date[] dates = [.. s.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
            double[] times = Doubles(s.GetProperty("times"));
            Func<object?>[] AtDates(Func<Date, Date, double> f) =>
                [.. Enumerable.Range(0, dates.Length - 1).Select(i => (Func<object?>)(() => f(dates[i], dates[i + 1])))];
            Func<object?>[] AtTimes(Func<double, double, double> f) =>
                [.. Enumerable.Range(0, times.Length - 1).Select(i => (Func<object?>)(() => f(times[i], times[i + 1])))];
            check.Output(c, "atmForwardVolAtDates", AtDates((d1, d2) => surface.AtmForwardVol(d1, d2, true)));
            check.Output(c, "atmForwardVarianceAtDates", AtDates((d1, d2) => surface.AtmForwardVariance(d1, d2, true)));
            check.Output(c, "atmForwardVolAtTimes", AtTimes((t1, t2) => surface.AtmForwardVol(t1, t2, true)));
            check.Output(c, "atmForwardVarianceAtTimes", AtTimes((t1, t2) => surface.AtmForwardVariance(t1, t2, true)));
        }

        check.Complete();
    }

    // The harness's surface: a flat section at each time, its volatility linear in time.
    private sealed class LinearFlatSurface(Date referenceDate, double v0, double slope)
        : EquityFXVolSurface(referenceDate, new NullCalendar(), BusinessDayConvention.Following, new Actual365Fixed())
    {
        public override Date MaxDate => Date.MaxDate;

        public override double MinStrike() => -double.MaxValue;

        public override double MaxStrike() => double.MaxValue;

        protected override SmileSection SmileSectionImpl(double t) =>
            new FlatSmileSection(t, v0 + (slope * t), new Actual365Fixed(), 100.0);
    }
}
