// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Householder reflection of a vector on the side opposite its unit vector e. QuantLib's reflection vector takes
/// its near-parallel branches by ε = |a − (a·e)e|²/(a·e)² alone (<c>householder.cpp:47-72</c>): for a = −|a|e, ε is 0
/// and the zero vector maps a to itself, −|a|e, and near −e the series branch, derived for a ≈ |a|e, maps a to neither,
/// where the reflection maps a to |a|e, as QuantLib's test holds for vectors on e's side
/// (<c>matrices.cpp:951-1002</c>). nanoql takes QuantLib's general branch, (a − |a|e)/|a − |a|e|, wherever a·e is
/// negative, which has no cancellation there (the S-exo plan's ER5 (iv)): QuantLib's general branch is the check.
/// </summary>
public class HouseholderOppositeTests
{
    [Fact]
    public void AVectorOppositeEReflectsOntoE()
    {
        var e = new Vector([0.0, 1.0, 0.0]);

        // QuantLib maps -2.5e to itself and a vector near -e by its series branch away from |a|e
        foreach (Vector a in new[] { new Vector([0.0, -2.5, 0.0]), new Vector([1e-3, -3.0, 2e-3]) })
        {
            Vector reflected = new HouseholderReflection(e).Evaluate(a);
            double norm = ArrayFunctions.Norm2(a);
            Assert.Equal(0.0, reflected[0], 1e-15);
            Assert.Equal(norm, reflected[1], 1e-14);
            Assert.Equal(0.0, reflected[2], 1e-15);

            // QuantLib's general branch, (a - |a|e)/|a - |a|e|
            Vector c = a - (norm * e);
            Assert.Equal((c / ArrayFunctions.Norm2(c)).ToArray(), new HouseholderReflection(e).ReflectionVector(a).ToArray());
        }
    }
}
