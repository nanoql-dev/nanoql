// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's sphere-cylinder vectors, <c>tests/vectors/cxx/math/optimization/spherecylinder.json</c>:
/// <see cref="SphereCylinderOptimizer"/> on QuantLib's two cases, seeded pairs at three weights and three iteration
/// counts, a cylinder of radius 0 and disjoint pairs (whether they meet, the closest point, the projection), and
/// <see cref="SphereCylinderFunctions.SphereCylinderOptimizerClosest"/> (its throw where they don't meet).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SphereCylinderCxxVectorTests
{
    [Fact]
    public void SphereCylinderCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/spherecylinder");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double r = c.Double("r"), s = c.Double("s"), alpha = c.Double("alpha");
            double z1 = c.Double("z1"), z2 = c.Double("z2"), z3 = c.Double("z3"), w = c.Double("zweight");
            int maxIterations = c.Inputs.GetProperty("maxIterations").GetInt32();
            double tolerance = c.Double("tolerance");
            var optimizer = new SphereCylinderOptimizer(r, s, alpha, z1, z2, z3, w);
            check.Output(c, "isIntersectionNonEmpty", optimizer.IsIntersectionNonEmpty());
            if (optimizer.IsIntersectionNonEmpty())
            {
                optimizer.FindClosest(maxIterations, tolerance, out double y1, out double y2, out double y3);
                check.Output(c, "closest", new[] { y1, y2, y3 });
                bool found = optimizer.FindByProjection(out y1, out y2, out y3);
                check.Output(c, "projectionFound", found);
                check.Output(c, "projection", new[] { y1, y2, y3 });
            }

            check.Output(
                c,
                "function",
                () => SphereCylinderFunctions.SphereCylinderOptimizerClosest(r, s, alpha, z1, z2, z3, maxIterations, tolerance, w));
        }

        check.Complete();
    }
}
