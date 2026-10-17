// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/timegrid.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class TimeGridTests
{
    [Fact]
    public void TestConstructorAdditionalSteps()
    {
        // Testing TimeGrid construction with additional steps...

        var tg = new TimeGrid([1.0, 2.0, 4.0], 8);

        // Expect 8 evenly sized steps over the interval [0, 4].
        double[] expected_times = [0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0];

        CheckEqualCollections(Times(tg), expected_times);
    }

    [Fact]
    public void TestConstructorMandatorySteps()
    {
        // Testing TimeGrid construction with only mandatory points...

        var tg = new TimeGrid([0.0, 1.0, 2.0, 4.0]);

        // Time grid must include all times from passed iterator.
        // Further no additional times can be added.
        double[] expected_times = [0.0, 1.0, 2.0, 4.0];

        CheckEqualCollections(Times(tg), expected_times);
    }

    [Fact]
    public void TestConstructorAdditionalStepsAutomatically()
    {
        // Testing TimeGrid construction with time step length determined automatically...

        var tg = new TimeGrid([0.0, 1.0, 2.0, 4.0], 0);

        // Time step length is determined by minimal adjacent distance in given times
        double[] expected_times = [0.0, 1.0, 2.0, 3.0, 4.0];

        CheckEqualCollections(Times(tg), expected_times);
    }

    [Fact]
    public void TestConstructorEvenSteps()
    {
        // Testing TimeGrid construction with n evenly spaced points...

        double end_time = 10;
        Size steps = 5;
        var tg = new TimeGrid(end_time, steps);

        double[] expected_times = [0.0, 2.0, 4.0, 6.0, 8.0, 10.0];

        CheckEqualCollections(Times(tg), expected_times);
    }

    [Fact]
    public void TestConstructorEmptyIterator()
    {
        // Testing that the TimeGrid constructor raises an error for empty iterators...

        double[] times = [];
        CheckThrowsError(() => _ = new TimeGrid(times));
    }

    [Fact]
    public void TestConstructorNegativeValuesInIterator()
    {
        // Testing that the TimeGrid constructor raises an error for negative time values...

        double[] times = [-3.0, 1.0, 4.0, 5.0];
        CheckThrowsError(() => _ = new TimeGrid(times));
    }

    [Fact]
    public void TestIndex()
    {
        // Testing that querying an index by floating-point time works for exact time nodes and throws otherwise...

        // will automatically insert additional point at t=0
        var tg = new TimeGrid([1.0, 2.0, 5.0]);

        CheckThrowsError(() => tg.Index(-2.0));

        Assert.Equal(4, tg.Size);

        CheckThrowsError(() => tg.Index(-0.1));
        Assert.Equal(0, tg.Index(0.0));
        CheckThrowsError(() => tg.Index(0.5));
        Assert.Equal(1, tg.Index(1.0));
        CheckThrowsError(() => tg.Index(1.1));
        Assert.Equal(2, tg.Index(2.0));
        CheckThrowsError(() => tg.Index(2.9));
        Assert.Equal(3, tg.Index(5.0));
        CheckThrowsError(() => tg.Index(5.1));
    }

    [Fact]
    public void TestClosestIndex()
    {
        // Testing that the returned index is closest to the requested time...

        var tg = new TimeGrid([1.0, 2.0, 5.0]);
        Size expected_index = 3;

        if (!(tg.ClosestIndex(4) == expected_index))
        {
            Assert.Fail(
                $"Expected index: {expected_index}, which does not match the returned index: {tg.ClosestIndex(4)}");
        }
    }

    [Fact]
    public void TestClosestTime()
    {
        // Testing that the returned time matches the requested index...
        var tg = new TimeGrid([1.0, 2.0, 5.0]);
        Size expected_time = 5;

        if (!(tg.ClosestTime(4) == expected_time))
        {
            Assert.Fail(
                $"Expected time of: {expected_time}, which does not match the returned time: {tg.ClosestTime(4).ToString(CultureInfo.InvariantCulture)}");
        }
    }

    [Fact]
    public void TestMandatoryTimes()
    {
        // Testing that mandatory times are recalled correctly...
        double[] test_times = [1.0, 2.0, 4.0];
        var tg = new TimeGrid(test_times, 8);

        // Mandatory times are those provided by the original iterator.
        CheckEqualCollections([.. tg.MandatoryTimes], test_times);
    }

    // The times from begin() to end(), through the grid's enumerator.
    private static double[] Times(TimeGrid tg)
    {
        var times = new List<double>();
        foreach (double t in tg)
        {
            times.Add(t);
        }

        return [.. times];
    }

    // BOOST_CHECK_EQUAL_COLLECTIONS: the same number of elements, each equal to its counterpart.
    private static void CheckEqualCollections(double[] actual, double[] expected)
    {
        if (actual.Length != expected.Length)
        {
            Assert.Fail($"Collections size mismatch: {actual.Length} != {expected.Length}");
        }

        for (int i = 0; i < actual.Length; i++)
        {
            if (!(actual[i] == expected[i]))
            {
                Assert.Fail(
                    $"Mismatch at position {i}: {actual[i].ToString("R", CultureInfo.InvariantCulture)} != {expected[i].ToString("R", CultureInfo.InvariantCulture)}");
            }
        }
    }

    // BOOST_CHECK_THROW(..., Error): QuantLib's Error is an ArgumentException or a NanoQLException in nanoql.
    private static void CheckThrowsError(Action action)
    {
        Exception? e = Record.Exception(action);
        Assert.True(
            e is ArgumentException or NanoQLException,
            $"expected an ArgumentException or a NanoQLException, got {e?.GetType().Name ?? "no exception"}");
    }
}
