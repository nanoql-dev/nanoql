// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The nine-point operator alone: QuantLib's constructor allocates its coefficients without initializing them
/// (<c>new Real[size]</c>, <c>ninepointlinearop.cpp</c>), so a bare operator applies garbage until a derived class fills
/// them; nanoql's coefficients start at 0, so the bare operator is the zero operator.
/// </summary>
public class NinePointLinearOpTests
{
    [Fact]
    public void TheBareOperatorIsZero()
    {
        var mesher = new FdmMesherComposite(new Uniform1dMesher(-1.0, 1.0, 5), new Uniform1dMesher(0.0, 3.0, 4));
        var op = new NinePointLinearOp(0, 1, mesher);
        var r = new Vector(mesher.Layout.Size);
        for (int i = 0; i < r.Size; i++)
        {
            r[i] = (i % 7) - 3.0;
        }

        Assert.All(op.Apply(r).ToArray(), x => Assert.Equal(0.0, x));
    }
}
