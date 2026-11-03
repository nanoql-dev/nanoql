// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The exponential forward correlation's three departures. One rate is accepted: QuantLib requires <c>numberOfRates_ >
/// 1</c> under the text "Rate times must contain at least two values", so it refuses one rate when given two times
/// (<c>expcorrelations.cpp:82-83</c>), where its own time-homogeneous correlation accepts one. At an exponent other
/// than 1, correlation times that skip a rate time but the last are refused: QuantLib's FIXME asks for the check
/// (<c>:105-106</c>), and without it each period's matrix is taken at the period's midpoint, after a rate that is alive
/// at the period's start has fixed, so the models give that rate no variance there: on nine rates fixing 0.5 … 4.5 and
/// times {2.0, 4.5}, FlatVol's total variance of rate 0 is 0 against σ²T 0.02, rate 4's 0.08 against 0.10. And a last
/// correlation time past the last rate time is refused with a text naming the time it checks, where QuantLib's calls it
/// the next-to-last (<c>:107-110</c>).
/// </summary>
public class ExponentialCorrelationTests
{
    [Fact]
    public void OneRateIsTheTimeHomogeneousCorrelationOfOneRate()
    {
        // QuantLib: "Rate times must contain at least two values".
        double[] times = [1.0, 2.0];
        var x = new ExponentialForwardCorrelation(times, 0.5, 0.2, 1.0);
        var h = new TimeHomogeneousForwardCorrelation(ExpCorrelationsFunctions.ExponentialCorrelations(times, 0.5, 0.2, 1.0, 0.0), times);
        Assert.Equal(1, x.NumberOfRates);
        Assert.Equal(h.Times, x.Times);
        Assert.Equal(h.Correlations().Single(), x.Correlations().Single());
        Assert.Equal(1.0, x.Correlation(0)[0, 0]);
    }

    [Fact]
    public void TimesSkippingARateTimeAreRefusedAtAnExponentOtherThanOne()
    {
        double[] rateTimes = [.. Enumerable.Range(1, 10).Select(i => 0.5 * i)];
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new ExponentialForwardCorrelation(rateTimes, 0.5, 0.2, 0.5, [2.0, 4.5]));
        Assert.StartsWith("corr times ( 2 4.5 ) skip the 1st rate time (0.5)", e.Message, StringComparison.Ordinal);

        // The default times, every rate time but the last, hold QuantLib's requirement, and the rates' variances are
        // their own: rate 0's σ²T 0.02 and rate 4's 0.10, where QuantLib's {2.0, 4.5} gives 0 and 0.08.
        var corr = new ExponentialForwardCorrelation(rateTimes, 0.5, 0.2, 0.5);
        var model = new FlatVol(
            Enumerable.Repeat(0.2, 9).ToArray(), corr, new EvolutionDescription(rateTimes), 9, Enumerable.Repeat(0.04, 9).ToArray(), new double[9]);
        Matrix total = model.TotalCovariance(model.NumberOfSteps - 1);
        Assert.Equal(0.02, total[0, 0], 1e-12);
        Assert.Equal(0.10, total[4, 4], 1e-12);
    }

    [Fact]
    public void ALastTimePastTheLastRateTimeIsNamedAsTheLast()
    {
        // QuantLib: "last corr time 2is after next-to-last rate time 1.5".
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new ExponentialForwardCorrelation([0.5, 1.0, 1.5], 0.5, 0.2, 0.5, [0.5, 1.0, 2.0]));
        Assert.StartsWith("last corr time 2 is after last rate time 1.5", e.Message, StringComparison.Ordinal);
        ImmutableArray<double> accepted = new ExponentialForwardCorrelation([0.5, 1.0, 1.5], 0.5, 0.2, 0.5, [0.5, 1.0, 1.5]).Times;
        Assert.Equal([0.5, 1.0, 1.5], accepted);
    }
}
