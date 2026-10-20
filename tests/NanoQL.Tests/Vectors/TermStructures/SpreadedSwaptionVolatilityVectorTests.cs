// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The spreaded swaption volatility's vectors, <c>tests/vectors/termstructures/volatility/swaption/spreadedswaptionvol.json</c>:
/// a spread over the at-the-money matrix, with and without extrapolation, the normal and the shifted matrix, the SABR
/// cube, the ZABR cube and the interpolated cube; the members it reads from its base, its volatilities, variances and
/// shifts, and its spreaded smile sections, with their converted volatilities, the outputs over a calibrated cube kept
/// only where the stability filter found them stable.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SpreadedSwaptionVolatilityVectorTests
{
    private static readonly (Period Option, Period Swap)[] TenorPoints =
    [
        (new(1, TimeUnit.Years), new(2, TimeUnit.Years)), (new(1, TimeUnit.Years), new(10, TimeUnit.Years)),
        (new(5, TimeUnit.Years), new(2, TimeUnit.Years)), (new(5, TimeUnit.Years), new(10, TimeUnit.Years)),
        (new(10, TimeUnit.Years), new(2, TimeUnit.Years)), (new(10, TimeUnit.Years), new(10, TimeUnit.Years)),
    ];

    private static readonly (double Time, double Length)[] TimePoints = [(0.5, 2.0), (5.0, 10.0), (20.0, 15.0)];

    private static readonly double[] Strikes = [0.02, 0.04, 0.06];

    private static readonly (Period Option, Period Swap) Past = (new(40, TimeUnit.Years), new(40, TimeUnit.Years));

    private static readonly (Period Option, Period Swap)[] SectionTenors =
    [
        (new(5, TimeUnit.Years), new(5, TimeUnit.Years)), (new(1, TimeUnit.Years), new(10, TimeUnit.Years)),
    ];

    private static readonly (double Time, double Length)[] SectionTimes = [(2.0, 5.0)];

    // A shift no base section has: QuantLib's spreaded section defines only a conversion to another shift than its own
    // (Divergences/SpreadedSmileSectionTypeTests.cs).
    private const double ConversionShift = 0.0137;

    [Fact]
    public void SpreadedSwaptionVolatilityVector()
    {
        var file = VectorFile.Load("termstructures/volatility/swaption/spreadedswaptionvol");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("spreaded");
            JsonElement baseSpec = spec.GetProperty("base");
            SwaptionVolatilityStructure baseVol = baseSpec.TryGetProperty("atm", out _)
                ? CubeInputs.CubeOf(baseSpec, context)
                : CubeInputs.AtmOf(baseSpec, context);
            if (spec.GetProperty("baseExtrapolates").GetBoolean())
            {
                baseVol = baseVol.EnableExtrapolation();
            }

            var s = new SpreadedSwaptionVolatility(baseVol, IndexInputs.Real(spec.GetProperty("spread")));
            Date D(Period o) => s.OptionDateFromTenor(o);
            void Lazy(string name, IEnumerable<Func<object?>> values) => check.Output(c, name, CubeInputs.Kept(c, name, values));

            check.Output(c, "referenceDate", s.ReferenceDate);
            check.Output(c, "dayCounter", s.DayCounter.Name);
            check.Output(c, "calendar", s.Calendar!.Name);
            check.Output(c, "businessDayConvention", (int)s.BusinessDayConvention);
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "maxTime", s.MaxTime);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "volatilityType", (int)s.VolatilityType);
            check.Output(c, "allowsExtrapolation", s.AllowsExtrapolation);
            Lazy(
                "volatilityDatePeriod",
                from p in TenorPoints
                from k in Strikes
                select (Func<object?>)(() => s.Volatility(D(p.Option), p.Swap, k)));
            Lazy(
                "volatilityTimeTime",
                from p in TimePoints
                from k in Strikes
                select (Func<object?>)(() => s.Volatility(p.Time, p.Length, k)));
            Lazy(
                "volatilityPast",
                from e in new[] { false, true }
                from k in Strikes
                select (Func<object?>)(() => s.Volatility(D(Past.Option), Past.Swap, k, e)));
            Lazy(
                "blackVariance",
                from p in TenorPoints
                from k in Strikes
                select (Func<object?>)(() => s.BlackVariance(D(p.Option), p.Swap, k)));
            Lazy("shift", TimePoints.Select(p => (Func<object?>)(() => s.Shift(p.Time, p.Length))));
            Lazy("sectionsDatePeriod", SectionTenors.SelectMany(p => Section(() => s.SmileSection(D(p.Option), p.Swap))));
            Lazy("sectionsTimeTime", SectionTimes.SelectMany(p => Section(() => s.SmileSection(p.Time, p.Length))));
        }

        check.Complete();
    }

    // A section's outputs: its ATM level, exercise time, shift and type, then at each strike its volatility, converted
    // to a normal one and to a shifted lognormal one at ConversionShift; each a throw where the section or the value is
    // refused.
    private static IEnumerable<Func<object?>> Section(Func<SmileSection> section)
    {
        var values = new List<Func<object?>>
        {
            () => section().AtmLevel(),
            () => section().ExerciseTime,
            () => section().Shift,
            () => (int)section().VolatilityType,
        };
        foreach (double k in Strikes)
        {
            values.Add(() => section().Volatility(k));
            values.Add(() => section().Volatility(k, VolatilityType.Normal, ConversionShift));
            values.Add(() => section().Volatility(k, VolatilityType.ShiftedLognormal, ConversionShift));
        }

        return values;
    }
}
