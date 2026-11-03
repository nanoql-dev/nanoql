// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The bootstrap traits and the curves' helpers on data QuantLib reads past its end: a guess's index outside the data
/// is refused before the write, and an empty set of dates, times or data before its last element is read, each naming
/// its parameter; one element still computes, and a helper that returns at t = 0 before reading still returns.
/// </summary>
public class BootstrapTraitsRefusalTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void AGuessOutsideTheDataIsRefused()
    {
        foreach ((string traits, Action<double[], int> update) in new (string, Action<double[], int>)[]
                 {
                     (nameof(Discount), (d, i) => Discount.UpdateGuess(d, 0.9, i)),
                     (nameof(ZeroYield), (d, i) => ZeroYield.UpdateGuess(d, 0.02, i)),
                     (nameof(ForwardRate), (d, i) => ForwardRate.UpdateGuess(d, 0.02, i)),
                     (nameof(SimpleZeroYield), (d, i) => SimpleZeroYield.UpdateGuess(d, 0.02, i)),
                     (nameof(SurvivalProbability), (d, i) => SurvivalProbability.UpdateGuess(d, 0.9, i)),
                     (nameof(HazardRate), (d, i) => HazardRate.UpdateGuess(d, 0.01, i)),
                     (nameof(DefaultDensity), (d, i) => DefaultDensity.UpdateGuess(d, 0.01, i)),
                     (nameof(ZeroInflationTraits), (d, i) => ZeroInflationTraits.UpdateGuess(d, 0.02, i)),
                     (nameof(YoYInflationTraits), (d, i) => YoYInflationTraits.UpdateGuess(d, 0.02, i)),
                     (nameof(AffineHazardRate), (d, i) => AffineHazardRate.UpdateGuess(d, 0.01, i)),
                     (nameof(YoYInflationVolatilityTraits), (d, i) => YoYInflationVolatilityTraits.UpdateGuess(d, 0.1, i)),
                 })
        {
            double[] data = [0.5, 0.5, 0.5];
            AssertIndex(traits, "i ('3') must be less than '3'.", () => update(data, 3));
            AssertIndex(traits, "i ('-1') must be a non-negative value.", () => update(data, -1));
            AssertIndex(traits, "i ('1') must be less than '1'.", () => update([0.5], 1));
            Assert.Equal([0.5, 0.5, 0.5], data);
            update(data, 2);
            Assert.NotEqual(0.5, data[2]);
        }
    }

    [Fact]
    public void ACurveOfNoPointIsRefusedBeforeItsLastIsRead()
    {
        double[] times = [1.0];
        double[] data = [0.9];
        Interpolation interpolation = new LinearInterpolation([0.0, 1.0], [1.0, 0.9]);
        CoxIngersollRoss model = new(0.02, 0.03, 0.5, 0.05);
        var flat = new FlatHazardRate(Today, 0.01, new Actual365Fixed());
        foreach ((string helper, Func<double[], double[], double, double> f) in new (string, Func<double[], double[], double, double>)[]
                 {
                     ("Discount.DiscountImpl", (x, y, t) => Discount.DiscountImpl(x, y, interpolation, t)),
                     ("ZeroYield.ZeroYieldImpl", (x, y, t) => ZeroYield.ZeroYieldImpl(x, y, interpolation, t)),
                     ("ZeroYield.DiscountImpl", (x, y, t) => ZeroYield.DiscountImpl(x, y, interpolation, t)),
                     ("ForwardRate.ZeroYieldImpl", (x, y, t) => ForwardRate.ZeroYieldImpl(x, y, interpolation, t)),
                     ("ForwardRate.DiscountImpl", (x, y, t) => ForwardRate.DiscountImpl(x, y, interpolation, t)),
                     ("SimpleZeroYield.DiscountImpl", (x, y, t) => SimpleZeroYield.DiscountImpl(x, y, interpolation, t)),
                     ("SurvivalProbability.SurvivalProbabilityImpl", (x, y, t) => SurvivalProbability.SurvivalProbabilityImpl(x, y, interpolation, t)),
                     ("SurvivalProbability.DefaultDensityImpl", (x, y, t) => SurvivalProbability.DefaultDensityImpl(x, y, interpolation, t)),
                     ("HazardRate.SurvivalProbabilityImpl", (x, y, t) => HazardRate.SurvivalProbabilityImpl(x, y, interpolation, t)),
                     ("HazardRate.DefaultDensityImpl", (x, y, t) => HazardRate.DefaultDensityImpl(x, y, interpolation, t)),
                     ("HazardRate.HazardRateImpl", (x, y, t) => HazardRate.HazardRateImpl(x, y, interpolation, t, flat)),
                     ("DefaultDensity.SurvivalProbabilityImpl", (x, y, t) => DefaultDensity.SurvivalProbabilityImpl(x, y, interpolation, t)),
                     ("DefaultDensity.DefaultDensityImpl", (x, y, t) => DefaultDensity.DefaultDensityImpl(x, y, interpolation, t)),
                     ("AffineHazardRate.SurvivalProbabilityImpl", (x, y, t) => AffineHazardRate.SurvivalProbabilityImpl(x, y, interpolation, t, model)),
                     ("AffineHazardRate.DefaultDensityImpl", (x, y, t) => AffineHazardRate.DefaultDensityImpl(x, y, interpolation, t, model)),
                     ("AffineHazardRate.HazardRateImpl", (x, y, t) => AffineHazardRate.HazardRateImpl(x, y, interpolation, t, flat, model)),
                 })
        {
            // No time to compare with, then no datum to extrapolate from past the last time.
            AssertEmpty(helper, "times", "the curve has no points", () => f([], [], 0.5));
            AssertEmpty(helper, "data", "the curve has no points", () => f(times, [], 2.0));
            Assert.True(double.IsFinite(f(times, data, 0.5)), helper);
            Assert.True(double.IsFinite(f(times, data, 2.0)), helper);
        }

        // QuantLib returns at t = 0 before its read in these six, so they still do on no points.
        Assert.Equal(interpolation.Evaluate(0.0, true), ForwardRate.ZeroYieldImpl([], [], interpolation, 0.0));
        Assert.Equal(1.0, ForwardRate.DiscountImpl([], [], interpolation, 0.0));
        Assert.Equal(1.0, ZeroYield.DiscountImpl([], [], interpolation, 0.0));
        Assert.Equal(1.0, HazardRate.SurvivalProbabilityImpl([], [], interpolation, 0.0));
        Assert.Equal(1.0, DefaultDensity.SurvivalProbabilityImpl([], [], interpolation, 0.0));
        Assert.Equal(1.0, AffineHazardRate.SurvivalProbabilityImpl([], [], interpolation, 0.0, model), 14);
    }

    [Fact]
    public void ACurveOfNoDateHasNoMaximumDate()
    {
        var last = new Date(15, Month.October, 2027);
        var given = new Date(15, Month.October, 2030);
        foreach ((string traits, Func<Date[], Date?, Date> maxDate, bool takesGiven) in new (string, Func<Date[], Date?, Date>, bool)[]
                 {
                     (nameof(Discount), (d, m) => Discount.MaxDate(d, m), true),
                     (nameof(ZeroYield), (d, m) => ZeroYield.MaxDate(d, m), true),
                     (nameof(ForwardRate), (d, m) => ForwardRate.MaxDate(d, m), true),
                     (nameof(ZeroInflationTraits), (d, m) => ZeroInflationTraits.MaxDate(d, m), true),
                     (nameof(YoYInflationTraits), (d, m) => YoYInflationTraits.MaxDate(d, m), true),

                     // the simple zero curve's maximum date is its last, whatever the bootstrap's helpers need
                     (nameof(SimpleZeroYield), (d, m) => SimpleZeroYield.MaxDate(d, m), false),
                     (nameof(SurvivalProbability), (d, _) => SurvivalProbability.MaxDate(d), false),
                     (nameof(HazardRate), (d, _) => HazardRate.MaxDate(d), false),
                     (nameof(DefaultDensity), (d, _) => DefaultDensity.MaxDate(d), false),
                     (nameof(AffineHazardRate), (d, _) => AffineHazardRate.MaxDate(d), false),
                 })
        {
            AssertEmpty(traits, "dates", "the curve has no dates", () => maxDate([], null));
            Assert.Equal(last, maxDate([Today, last], null));
            if (takesGiven)
            {
                Assert.Equal(given, maxDate([], given));
            }
            else
            {
                AssertEmpty(traits, "dates", "the curve has no dates", () => maxDate([], given));
            }
        }
    }

    private static void AssertIndex(string traits, string message, Action call)
    {
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(call);
        Assert.Equal("i", e.ParamName);
        Assert.True(e.Message.StartsWith(message, StringComparison.Ordinal), $"{traits}: {e.Message}");
    }

    private static void AssertEmpty(string helper, string param, string message, Action call)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(call);
        Assert.True(e.ParamName == param, $"{helper}: {e.ParamName}");
        Assert.True(e.Message.StartsWith(message, StringComparison.Ordinal), $"{helper}: {e.Message}");
    }
}
