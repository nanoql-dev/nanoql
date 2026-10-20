// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the seasonalities' refusals, which the vectors record only as throws: the construction checks
/// of QuantLib's <c>validate</c> are an <see cref="ArgumentException"/> naming <c>seasonalityFactors</c> and an
/// <see cref="ArgumentOutOfRangeException"/> naming <c>frequency</c>; what a built seasonality can't do is a
/// <see cref="NanoQLException"/> with QuantLib's text: a multi-year seasonality inconsistent with its curve, refused by
/// the curve's constructor and by <see cref="InflationTermStructureExtensions.SetSeasonality"/>, Kerkhof's factor without
/// twelve factors, and its year-on-year correction.
/// </summary>
public class SeasonalityConstructionTests
{
    private static readonly double[] Twelve = [.. Enumerable.Range(0, 12).Select(i => 1.0 + (i * 0.001))];
    private static readonly Date Base = new(1, Month.January, 2024);

    [Fact]
    public void TheConstructionChecksNameTheirArguments()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new MultiplicativePriceSeasonality(Base, Frequency.Monthly, []));
        Assert.Equal("seasonalityFactors", e.ParamName);
        Assert.StartsWith("no seasonality factors given", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentException>(() => new MultiplicativePriceSeasonality(Base, Frequency.Monthly, [.. Twelve, 1.0]));
        Assert.Equal("seasonalityFactors", e.ParamName);
        Assert.StartsWith("For frequency Monthly require multiple of 12 factors 13 were given.", e.Message, StringComparison.Ordinal);
        foreach (Frequency f in new[] { Frequency.Annual, Frequency.Once, Frequency.NoFrequency, Frequency.OtherFrequency })
        {
            ArgumentOutOfRangeException o = Assert.Throws<ArgumentOutOfRangeException>(
                () => new MultiplicativePriceSeasonality(Base, f, [1.0]));
            Assert.Equal("frequency", o.ParamName);
            Assert.StartsWith("bad frequency specified: ", o.Message, StringComparison.Ordinal);
        }

        ArgumentOutOfRangeException unknown = Assert.Throws<ArgumentOutOfRangeException>(
            () => new MultiplicativePriceSeasonality(Base, (Frequency)5, [1.0]));
        Assert.Equal("frequency", unknown.ParamName);
        Assert.StartsWith("unknown frequency (5)", unknown.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => new KerkhofSeasonality(Base, [1.0, 1.0]));
        Assert.Equal("seasonalityFactors", e.ParamName);
    }

    [Fact]
    public void ABuiltSeasonalitysRefusalsAreExceptions()
    {
        var reference = new Date(15, Month.June, 2024);
        Date[] dates = [new(1, Month.April, 2024), new(1, Month.April, 2030)];
        double[] reversed = [.. Twelve.Reverse()];
        var inconsistent = new MultiplicativePriceSeasonality(Base, Frequency.Monthly, [.. Twelve, .. reversed]);
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => new ZeroInflationCurve(reference, dates, [0.03, 0.03], Frequency.Monthly, new Actual365Fixed(), inconsistent));
        Assert.StartsWith("seasonality is inconsistent with inflation term structure, factors ", e.Message, StringComparison.Ordinal);
        var curve = new ZeroInflationCurve(reference, dates, [0.03, 0.03], Frequency.Monthly, new Actual365Fixed());
        Assert.Throws<NanoQLException>(
            () => curve.SetSeasonality(inconsistent, new EvaluationContext { EvaluationDate = reference }));

        var kerkhof = new KerkhofSeasonality(Base, [.. Twelve, .. Twelve]);
        e = Assert.Throws<NanoQLException>(() => kerkhof.SeasonalityFactor(new Date(1, Month.March, 2024)));
        Assert.Equal("12 monthly seasonal factors needed for Kerkhof Seasonality: got 24", e.Message);

        var yoy = new YoYInflationCurve(
            reference, dates, [0.03, 0.03], Frequency.Monthly, new Actual365Fixed(), new KerkhofSeasonality(Base, Twelve));
        e = Assert.Throws<NanoQLException>(() => yoy.YoyRate(new Date(1, Month.May, 2025)));
        Assert.Equal("Seasonal Kerkhof model is not defined on YoY rates", e.Message);
    }
}
