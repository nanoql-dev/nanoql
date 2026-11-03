// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The nine-point operator alone: QuantLib's constructor allocates its coefficients without initializing them
/// (<c>new Real[size]</c>, <c>ninepointlinearop.cpp</c>), so a bare operator applies garbage until a derived class fills
/// them; nanoql's coefficients start at 0, so the bare operator is the zero operator. On a direction of one point it
/// builds and refuses to apply or to give its matrix, which would read and write past the grid.
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

    [Fact]
    public void ADirectionOfOnePointBuildsAndRefusesToApplyOrGiveItsMatrix()
    {
        // The neighbours of a direction of one point lie past the grid: QuantLib's apply reads them and its matrix
        // writes them. The operator builds, as QuantLib's does.
        foreach (FdmMesherComposite mesher in new[]
                 {
                     new FdmMesherComposite(new Fdm1dMesher(1), new Uniform1dMesher(0.0, 3.0, 4)),
                     new FdmMesherComposite(new Uniform1dMesher(-1.0, 1.0, 5), new Fdm1dMesher(1)),
                 })
        {
            var op = new NinePointLinearOp(0, 1, mesher);
            ArgumentException e = Assert.Throws<ArgumentException>(() => op.Apply(new Vector(mesher.Layout.Size)));
            Assert.Equal("r", e.ParamName);
            Assert.StartsWith("a nine-point apply needs at least two points in each of its directions, not 1", e.Message, StringComparison.Ordinal);
            Assert.Equal(
                "a nine-point matrix needs at least two points in each of its directions, not 1",
                Assert.Throws<InvalidOperationException>(() => op.ToMatrix()).Message);
        }

        var two = new FdmMesherComposite(new Uniform1dMesher(-1.0, 1.0, 2), new Uniform1dMesher(0.0, 3.0, 2));
        Assert.Equal(4, new NinePointLinearOp(0, 1, two).ToMatrix().Size1);
    }
}
