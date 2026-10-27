// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/spherecylinder.hpp ql/math/optimization/spherecylinder.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>QuantLib's free functions of <c>spherecylinder.hpp</c>.</summary>
public static class SphereCylinderFunctions
{
    /// <summary>
    /// The point of the intersection of the sphere and the cylinder closest to Z: Z's projection where no iteration is
    /// asked, a golden-section search otherwise.
    /// </summary>
    /// <param name="r">The sphere's radius, positive.</param>
    /// <param name="s">The cylinder's radius; a negative one is 0.</param>
    /// <param name="alpha">The cylinder centre's first coordinate, positive.</param>
    /// <param name="z1">Z's first coordinate.</param>
    /// <param name="z2">Z's second coordinate.</param>
    /// <param name="z3">Z's third coordinate.</param>
    /// <param name="maxIterations">The search's iterations at most; 0 for the projection.</param>
    /// <param name="tolerance">The search's interval at which it stops.</param>
    /// <param name="finalWeight">The third coordinate's weight in the distance.</param>
    /// <returns>A new array of the point's three coordinates.</returns>
    /// <exception cref="ArgumentException">The radius or the centre's coordinate isn't positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIterations"/> is negative.</exception>
    /// <exception cref="NanoQLException">The intersection is empty.</exception>
    public static double[] SphereCylinderOptimizerClosest(
        double r, double s, double alpha, double z1, double z2, double z3, int maxIterations, double tolerance, double finalWeight = 1.0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxIterations);
        var optimizer = new SphereCylinderOptimizer(r, s, alpha, z1, z2, z3, finalWeight);
        double[] y = new double[3];

        if (!optimizer.IsIntersectionNonEmpty())
        {
            throw new NanoQLException("intersection empty so no solution");
        }

        if (maxIterations == 0)
        {
            optimizer.FindByProjection(out y[0], out y[1], out y[2]);
        }
        else
        {
            optimizer.FindClosest(maxIterations, tolerance, out y[0], out y[1], out y[2]);
        }

        return y;
    }
}
