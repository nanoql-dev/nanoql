// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Andreasen-Huge interpolation's slices, memoised by time inside the immutable object: a query at a time computes
/// that time's slice once, from the calibration alone, so threads querying new times together get the bits one thread
/// gets, and a query's bits don't depend on what was queried before, nearby times included.
/// </summary>
public class AndreasenHugeMemoTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void ThreadsQueryingNewTimesGetTheBitsOfOneThread()
    {
        double[] times = [.. Enumerable.Range(1, 64).Select(i => 0.0137 * i)];
        double[] strikes = [70.0, 95.0, 100.0, 120.0];

        AndreasenHugeVolatilityInterpl reference = Interpolation();
        double[][] expected = [.. times.Select(t => Values(reference, t, strikes))];

        AndreasenHugeVolatilityInterpl shared = Interpolation();
        var actual = new double[times.Length][];
        Parallel.For(
            0,
            8,
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            thread =>
            {
                // each thread walks the times from its own offset, so threads meet at times none has computed
                for (int k = 0; k < times.Length; ++k)
                {
                    int i = (k + (thread * 8)) % times.Length;
                    actual[i] = Values(shared, times[i], strikes);
                }
            });

        for (int i = 0; i < times.Length; ++i)
        {
            Assert.Equal(expected[i], actual[i]);
        }
    }

    [Fact]
    public void AQuerysBitsDontDependOnEarlierQueries()
    {
        double[] strikes = [70.0, 100.0, 130.0];
        double t = 0.6;

        AndreasenHugeVolatilityInterpl fresh = Interpolation();
        double[] first = Values(fresh, t, strikes);

        // the same time after nearby ones, among them times a memo keyed coarsely would merge with it
        AndreasenHugeVolatilityInterpl used = Interpolation();
        foreach (double u in new[] { 0.25, 0.6004, 0.5996, 1.2, 0.601 })
        {
            _ = Values(used, u, strikes);
        }

        Assert.Equal(first, Values(used, t, strikes));
        Assert.NotEqual(Values(used, 0.6004, strikes), first);
    }

    private static double[] Values(AndreasenHugeVolatilityInterpl a, double t, double[] strikes) =>
        [.. strikes.Select(k => a.LocalVol(t, k)), .. strikes.Select(k => a.OptionPrice(t, k, OptionType.Call))];

    private static AndreasenHugeVolatilityInterpl Interpolation()
    {
        (VanillaOption, double)[] options =
        [
            .. new[] { 80.0, 100.0, 120.0 }.SelectMany(k => new[] { 91, 365, 730 }.Select(d => (
                new VanillaOption(new PlainVanillaPayoff(OptionType.Call, k), new EuropeanExercise(Today + d)),
                0.2 + (0.1 * ((k - 100.0) / 100.0) * ((k - 100.0) / 100.0))))),
        ];
        DayCounter dc = new Actual365Fixed();
        return new AndreasenHugeVolatilityInterpl(
            options,
            100.0,
            new FlatForward(Today, 0.02, dc),
            new FlatForward(Today, 0.01, dc),
            AndreasenHugeVolatilityInterplInterpolationType.CubicSpline,
            AndreasenHugeVolatilityInterplCalibrationType.Call,
            120);
    }
}
