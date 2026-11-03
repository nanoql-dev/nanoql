// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A step of the Dupire operator allocates nothing (the one-dimensional pin): its map is built once from the local
/// volatilities, and its apply and splitting solve write into the caller's spans. A rollback of
/// <see cref="FdmBackwardSolver"/> on <see cref="FdmDupire1dOp"/> under Douglas's scheme, <c>fdPrice</c>'s
/// (<c>zabr.cpp:187</c>), on a strike mesher concentrating at the forward as <c>fdPrice</c> builds it, is measured on
/// fresh values after a warm-up (<see cref="AllocationAssert"/>), and gives the warm-up's values.
/// </summary>
public class FdmDupireAllocationTests
{
    [Fact]
    public void DouglasAllocatesNothing()
    {
        const double forward = 0.03;
        var mesher = new FdmMesherComposite(new Concentrating1dMesher(0.00001, 0.10, 60, (forward, 0.1), true));
        Vector strikes = mesher.Locations(0);
        var localVol = new Vector(strikes.Size);
        for (int i = 0; i < strikes.Size; i++)
        {
            // a skew in shape, the level 0.08 / f^0.3 of a ZABR local volatility
            localVol[i] = 0.08 / Math.Pow(Math.Max(strikes[i], 1e-4), 0.3);
        }

        var op = new FdmDupire1dOp(mesher, localVol);
        double[] initial = new double[mesher.Layout.Size];
        for (int i = 0; i < initial.Length; i++)
        {
            initial[i] = Math.Max(forward - strikes[i], 0.0);
        }

        var solver = new FdmBackwardSolver(op, [], null, FdmSchemeDesc.Douglas());
        double[] warm = [.. initial];
        solver.Rollback(warm, 1.0, 0.0, 24, 0);

        double[] values = AllocationAssert.AllocatesNothing(
            () => (double[])initial.Clone(),
            fresh => solver.Rollback(fresh, 1.0, 0.0, 24, 0));

        Assert.Equal(warm, values);
    }
}
