// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="ChebyshevInterpolation.WithY"/>, QuantLib's in-place <c>updateY</c> as a copy (the interpolations are
/// immutable): the new values on the same nodes, the receiver unchanged, the barycentric weights shared since they
/// depend on the nodes alone. QuantLib refuses values of another length ("interpolation override has the wrong length",
/// <c>chebyshevinterpolation.cpp:77-78</c>).
/// </summary>
public class ChebyshevInterpolationWithYTests
{
    [Fact]
    public void WithYGivesTheNewValuesAndLeavesTheReceiver()
    {
        double[] y = [1.0, 4.0, 7.0, 4.0, 2.0];
        double[] other = [6.0, 4.0, 5.0, 6.0, -1.0];
        var interpolation = new ChebyshevInterpolation(y);
        ChebyshevInterpolation updated = interpolation.WithY(other);
        var fresh = new ChebyshevInterpolation(other);
        for (double x = -1.0; x <= 1.0; x += 0.125)
        {
            // the copy is a fresh interpolation of the new values, bit for bit; the receiver still reads its own
            Assert.Equal(fresh.Evaluate(x), updated.Evaluate(x));
            Assert.Equal(new ChebyshevInterpolation(y).Evaluate(x), interpolation.Evaluate(x));
        }

        Assert.Equal(interpolation.Nodes().ToArray(), updated.Nodes().ToArray());
    }

    [Fact]
    public void ValuesOfAnotherLengthAreRefused()
    {
        var interpolation = new ChebyshevInterpolation([1.0, 4.0, 7.0, 4.0]);
        Exception e = Assert.ThrowsAny<Exception>(() => interpolation.WithY([1.0, 2.0, 3.0]));
        Assert.True(e is ArgumentException or NanoQLException, e.GetType().Name);
        Assert.Contains("interpolation override has the wrong length", e.Message);
    }
}
