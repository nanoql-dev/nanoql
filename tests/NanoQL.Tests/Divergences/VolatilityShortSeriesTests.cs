// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The volatility estimators on series too short for them (§2.2's undefined row, refused). QuantLib's
/// <c>Garch11::calculate</c> on fewer than two points reads before the series' start (<c>garch.cpp:385-388</c>),
/// <c>SimpleLocalEstimator</c> and the open-close Garman-Klass estimators increment an empty series' begin, and
/// <c>ConstantEstimator</c> advances past the end when its window is longer than the series. nanoql refuses each before
/// the read, with an <c>ArgumentException</c> naming the series. Check: each refusal's type, parameter and text.
/// </summary>
public class VolatilityShortSeriesTests
{
    private static readonly TimeSeries<double> OnePoint = new([new Date(7, July, 1962)], [0.1]);
    private static readonly TimeSeries<double> Empty = new([], []);

    [Fact]
    public void GarchOnOnePointIsRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => new Garch11(0.2, 0.3, 0.4).Calculate(OnePoint));
        Assert.Equal("quoteSeries", e.ParamName);
        Assert.StartsWith("a GARCH series needs at least two points", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AConstantWindowLongerThanTheSeriesIsRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => new ConstantEstimator(2).Calculate(OnePoint));
        Assert.Equal("volatilitySeries", e.ParamName);
        Assert.StartsWith("the window of 2 values is longer than the series of 1", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSimpleLocalEstimatorOnAnEmptySeriesIsRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => new SimpleLocalEstimator(1 / 360.0).Calculate(Empty));
        Assert.Equal("quoteSeries", e.ParamName);
        Assert.StartsWith("the quote series is empty", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOpenCloseGarmanKlassEstimatorOnAnEmptySeriesIsRefused()
    {
        var empty = new TimeSeries<IntervalPrice>([], []);
        foreach (GarmanKlassAbstract estimator in (GarmanKlassAbstract[])[new GarmanKlassSigma1(1 / 360.0, 0.4), new GarmanKlassSigma3(1 / 360.0, 0.4), new GarmanKlassSigma6(1 / 360.0, 0.4)])
        {
            ArgumentException e = Assert.Throws<ArgumentException>(() => estimator.Calculate(empty));
            Assert.Equal("quoteSeries", e.ParamName);
            Assert.StartsWith("the quote series is empty", e.Message, StringComparison.Ordinal);
        }
    }
}
