// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The forward-flat interpolation's primitive past its last node. QuantLib's <c>primitive</c> beyond <c>x[n-1]</c> adds
/// <c>(x - x[n-2])·y[n-2]</c> (<c>forwardflatinterpolation.hpp:93-97</c>), while its value there is <c>y[n-1]</c>
/// (<c>:87-88</c>), so the primitive's slope isn't the value (QuantLib's defect, R8). nanoql's primitive beyond
/// <c>x[n-1]</c> is <c>primitive[n-1] + (x - x[n-1])·y[n-1]</c>, and QuantLib's expression at and before it. On
/// x = [0, 1, 2, 3] and y = [0.01, 0.02, 0.03, 0.05], at 4 and 5 it is 0.11 and 0.16, where QuantLib's is 0.09 and
/// 0.12: QuantLib's own forward-flat forward curve integrates there as nanoql does, its <c>-ln D(t)</c> on the wheel
/// 0.11000000000000003 and 0.16 (<c>forwardcurve.hpp:155-168</c>). At 2.5 and 3 both give 0.045 and 0.06.
/// </summary>
public class ForwardFlatPrimitiveTests
{
    [Fact]
    public void ThePrimitivePastTheLastNodeGrowsAtTheValueThere()
    {
        double[] x = [0.0, 1.0, 2.0, 3.0];
        double[] y = [0.01, 0.02, 0.03, 0.05];
        Interpolation f = new ForwardFlatInterpolation(x, y).EnableExtrapolation();

        Assert.Equal(0.045, f.Primitive(2.5), 15);
        Assert.Equal(0.06, f.Primitive(3.0), 15);

        // QuantLib's: 0.09 and 0.12
        Assert.True(Tolerance.Within(0.11, f.Primitive(4.0)), $"{f.Primitive(4.0)}");
        Assert.True(Tolerance.Within(0.16, f.Primitive(5.0)), $"{f.Primitive(5.0)}");
        Assert.Equal(0.05, f.Evaluate(5.0));
    }
}
