// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Methods;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A step of the ZABR operator allocates nothing (the two-dimensional pin): its parts are built once, and its apply and
/// splitting solves write into the caller's spans through buffers it holds. A rollback of
/// <see cref="FdmBackwardSolver"/> on <see cref="FdmZabrOp"/> under Hundsdorfer's scheme, <c>fullFdPrice</c>'s
/// (<c>zabr.cpp:300</c>), on a glued forward mesher by a volatility mesher as <c>fullFdPrice</c> builds them, is
/// measured on fresh values after a warm-up (<see cref="AllocationAssert"/>), and gives the warm-up's values.
/// </summary>
public class FdmZabrAllocationTests
{
    [Fact]
    public void HundsdorferAllocatesNothing()
    {
        const double forward = 0.03, strike = 0.024, alpha = 0.08;
        var mf = new Glued1dMesher(
            new Concentrating1dMesher(0.012, 0.027, 8, (strike, 0.1), true),
            new Concentrating1dMesher(0.027, 0.067, 13, (forward, 0.1), true));
        var mv = new Concentrating1dMesher(0.055, 0.116, 12, (alpha, 0.1), true);
        var mesher = new FdmMesherComposite(mf, mv);
        var op = new FdmZabrOp(mesher, 0.7, 0.106, -0.3, 0.75);
        double[] initial = new double[mesher.Layout.Size];
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int i = 0; i < initial.Length; i++, iter.Increment())
        {
            initial[i] = Math.Max(mesher.Location(iter, 0) - strike, 0.0);
        }

        var solver = new FdmBackwardSolver(op, [], null, FdmSchemeDesc.Hundsdorfer());
        double[] warm = [.. initial];
        solver.Rollback(warm, 1.0, 0.0, 20, 0);

        double[] values = AllocationAssert.AllocatesNothing(
            () => (double[])initial.Clone(),
            fresh => solver.Rollback(fresh, 1.0, 0.0, 20, 0));

        Assert.Equal(warm, values);
    }
}
