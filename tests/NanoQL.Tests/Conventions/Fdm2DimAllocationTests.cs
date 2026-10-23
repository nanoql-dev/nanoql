// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The two-dimensional finite-difference step allocates nothing (the S-sr plan's SSD10, round 1's O1-11): the G2
/// operator builds its dynamics once and rewrites its discounting in a held buffer at each
/// <see cref="FdmG2Op.SetTime"/>, its apply and splitting solves write into the caller's spans, and each scheme steps in
/// place, where QuantLib's operator builds its short-rate dynamics and arrays at every step. A rollback of
/// <see cref="FdmBackwardSolver"/> on the G2 operator is measured under the six schemes the solver runs step by step
/// without Krylov solvers (Douglas, Hundsdorfer, modified Hundsdorfer, Craig-Sneyd, modified Craig-Sneyd and explicit
/// Euler), on fresh values after a warm-up (<see cref="AllocationAssert"/>), and gives the warm-up's values.
/// </summary>
public class Fdm2DimAllocationTests
{
    private static readonly Date Today = new(15, Month.January, 2025);

    [Fact]
    public void DouglasAllocatesNothing() => RollbackAllocatesNothing(FdmSchemeDesc.Douglas());

    [Fact]
    public void HundsdorferAllocatesNothing() => RollbackAllocatesNothing(FdmSchemeDesc.Hundsdorfer());

    [Fact]
    public void ModifiedHundsdorferAllocatesNothing() => RollbackAllocatesNothing(FdmSchemeDesc.ModifiedHundsdorfer());

    [Fact]
    public void CraigSneydAllocatesNothing() => RollbackAllocatesNothing(FdmSchemeDesc.CraigSneyd());

    [Fact]
    public void ModifiedCraigSneydAllocatesNothing() => RollbackAllocatesNothing(FdmSchemeDesc.ModifiedCraigSneyd());

    [Fact]
    public void ExplicitEulerAllocatesNothing() => RollbackAllocatesNothing(FdmSchemeDesc.ExplicitEuler(), steps: 400);

    private static void RollbackAllocatesNothing(FdmSchemeDesc desc, int steps = 20)
    {
        var curve = new FlatForward(Today, 0.027, new Actual365Fixed());
        var model = new G2(curve, 0.07, 0.011, 0.3, 0.008, -0.6);
        var mesher = new FdmMesherComposite(
            new FdmSimpleProcess1dMesher(15, new OrnsteinUhlenbeckProcess(model.A(), model.Sigma), 5.0, 1, 1e-5),
            new FdmSimpleProcess1dMesher(12, new OrnsteinUhlenbeckProcess(model.B(), model.Eta), 5.0, 1, 1e-5));
        var op = new FdmG2Op(mesher, model, 0, 1);
        double[] initial = new double[mesher.Layout.Size];
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int i = 0; i < initial.Length; i++, iter.Increment())
        {
            // the factors' sum, floored at 0, a payer's exercise value in shape
            double x = mesher.Location(iter, 0) + mesher.Location(iter, 1);
            initial[i] = x > 0.0 ? x : 0.0;
        }

        var solver = new FdmBackwardSolver(op, [], null, desc);
        double[] warm = [.. initial];
        solver.Rollback(warm, 5.0, 0.0, steps, 0);

        double[] values = AllocationAssert.AllocatesNothing(
            () => (double[])initial.Clone(),
            fresh => solver.Rollback(fresh, 5.0, 0.0, steps, 0));

        Assert.Equal(warm, values);
    }
}
