// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Lagrange interpolation at its nodes (the S-van plan's VR4 (xxviii), and the run's own find on descending nodes).
/// QuantLib finds the node by a binary search for the first x not below x − 10ε|x| and returns its y when that x is
/// less than 10ε|x| above (<c>lagrangeinterpolation.hpp:114-120</c>): at x = 0 the band is empty, so a node at zero falls
/// through to the barycentric sum, which divides by zero (NaN); and on descending x values the search never lands on a
/// node, so every node's value is NaN [executed on the wheel: <c>scratchpad\c2\oracle_c2.py</c>, section lagrange].
/// nanoql scans the nodes in order with the band closed. QuantLib's check: the node's y, and its own values beside the
/// nodes.
/// </summary>
public class LagrangeZeroNodeTests
{
    [Fact]
    public void ANodeAtZeroGivesItsValue()
    {
        // QuantLib: NaN at 0, and 2.0 at ±1e-300
        var interpolation = new LagrangeInterpolation([-2.0, -1.0, 0.0, 0.5, 2.0], [1.0, -1.0, 2.0, 0.5, 3.0]);
        Assert.Equal(2.0, interpolation.Evaluate(0.0));
        Assert.Equal(2.0, interpolation.Evaluate(1e-300));
        Assert.Equal(2.0, interpolation.Evaluate(-1e-300));
    }

    [Fact]
    public void DescendingNodesGiveTheirValues()
    {
        // QuantLib: NaN at each node, and 1.765432098765432 at 0, between them
        double[] x = [2.0, 0.5, -0.25, -1.0];
        double[] y = [3.0, 0.5, 2.0, -1.0];
        var interpolation = new LagrangeInterpolation(x, y);
        for (int i = 0; i < x.Length; i++)
        {
            Assert.Equal(y[i], interpolation.Evaluate(x[i], true));
        }

        Assert.Equal(1.765432098765432, interpolation.Evaluate(0.0, true));
    }
}
