// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's caplet variance curve vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/optionlet/capletvariancecurve.json</c>:
/// <see cref="CapletVarianceCurve"/> lognormal, normal, displaced and with a falling total variance; its dates, strike
/// range, volatility type and displacement, its volatilities and variances by date and by time with and without
/// extrapolation, and its smile sections' exercise times, volatilities and variances; and the curves QuantLib refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CapletVarianceCurveCxxVectorTests
{
    [Fact]
    public void CapletVarianceCurveCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/optionlet/capletvariancecurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("curve");
            switch (c.String("kind"))
            {
                case "curve":
                    CheckCurve(check, c, CurveOf(spec));
                    break;
                case "refusal":
                    check.Output(c, "curve", () => CurveOf(spec).MaxDate);
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static CapletVarianceCurve CurveOf(JsonElement spec) => new(
        new Date(spec.GetProperty("referenceDate").GetInt32()),
        spec.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32())).ToArray(),
        VolatilityInputs.Reals(spec.GetProperty("vols")),
        CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
        (VolatilityType)spec.GetProperty("volatilityType").GetInt32(),
        spec.GetProperty("displacement").GetDouble());

    private static void CheckCurve(VectorAssert check, VectorCase c, CapletVarianceCurve curve)
    {
        Date[] dates = c.DateInputs("dates");
        double[] times = VolatilityInputs.Reals(c.Input("times"));
        double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
        check.Output(c, "referenceDate", curve.ReferenceDate);
        check.Output(c, "maxDate", curve.MaxDate);
        check.Output(c, "maxTime", curve.MaxTime);
        check.Output(c, "minStrike", curve.MinStrike());
        check.Output(c, "maxStrike", curve.MaxStrike());
        check.Output(c, "volatilityType", curve.VolatilityType);
        check.Output(c, "displacement", curve.Displacement);
        foreach (bool extrapolate in new[] { false, true })
        {
            string suffix = extrapolate ? ", extrapolated" : string.Empty;
            check.Output(c, "volatility(Date)" + suffix, Grid(dates, strikes, (d, k) => curve.Volatility(d, k, extrapolate)));
            check.Output(c, "volatility(Time)" + suffix, Grid(times, strikes, (t, k) => curve.Volatility(t, k, extrapolate)));
            check.Output(c, "blackVariance(Date)" + suffix, Grid(dates, strikes, (d, k) => curve.BlackVariance(d, k, extrapolate)));
            check.Output(c, "blackVariance(Time)" + suffix, Grid(times, strikes, (t, k) => curve.BlackVariance(t, k, extrapolate)));
        }

        double strike = strikes[1];
        foreach ((string kind, Func<int, SmileSection> section) in new (string, Func<int, SmileSection>)[]
        {
            ("Date", i => curve.SmileSection(dates[i], true)),
            ("Time", i => curve.SmileSection(times[i], true)),
        })
        {
            int n = kind == "Date" ? dates.Length : times.Length;
            check.Output(c, $"smileSection({kind}).exerciseTime", Points(n, i => section(i).ExerciseTime));
            check.Output(c, $"smileSection({kind}).volatility", Points(n, i => section(i).Volatility(strike)));
            check.Output(c, $"smileSection({kind}).variance", Points(n, i => section(i).Variance(strike)));
        }
    }

    // The values at points × strikes, each computed when compared, so that a throw is checked.
    private static Func<object?>[][] Grid<T>(T[] points, double[] strikes, Func<T, double, double> f) =>
        [.. points.Select(p => strikes.Select(k => (Func<object?>)(() => f(p, k))).ToArray())];

    private static Func<object?>[] Points(int n, Func<int, double> f) =>
        [.. Enumerable.Range(0, n).Select(i => (Func<object?>)(() => f(i)))];
}
