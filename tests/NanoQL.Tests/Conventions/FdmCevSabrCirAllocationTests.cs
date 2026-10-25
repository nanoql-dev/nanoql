// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A step of the CEV, SABR and CIR operators, and an application of the swing condition, allocate nothing (the S-van
/// plan's VD12, S3's zero-allocation pin in S-eq's one-dimensional form and S-sr's two-dimensional one): their parts are
/// built once, <c>SetTime</c> rewrites them in place, and the apply, splitting solves and preconditioner write into the
/// caller's spans through buffers they hold; the swing condition writes through a scratch buffer it holds. A rollback of
/// <see cref="FdmBackwardSolver"/> on each operator under its engine's default scheme, Douglas for the CEV engine and
/// Hundsdorfer for the SABR and CIR engines (VR8), is measured on fresh values after a warm-up
/// (<see cref="AllocationAssert"/>), and gives the warm-up's values.
/// </summary>
public class FdmCevSabrCirAllocationTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();

    [Fact]
    public void ACevStepOnDouglasAllocatesNothing()
    {
        var mesher = new FdmMesherComposite(new FdmCEV1dMesher(40, 2.1, 0.75, 0.45, 1.0, 1e-4, 1.0, (2.3, 0.1)));
        var op = new FdmCEVOp(mesher, new FlatForward(Today, 0.15, Dc), 2.1, 0.75, 0.45, 0);
        RollbackAllocatesNothing(op, mesher, FdmSchemeDesc.Douglas(), 2.3);
    }

    [Fact]
    public void ASabrStepOnHundsdorferAllocatesNothing()
    {
        var mesher = new FdmMesherComposite(
            new FdmCEV1dMesher(20, 1.2, 0.4, 0.5, 1.0, 1e-4, 1.0, (1.1, 0.025)),
            new Concentrating1dMesher(Math.Log(0.35) - 1.0, Math.Log(0.35) + 1.0, 9, (Math.Log(0.35), 0.1)));
        var op = new FdmSabrOp(mesher, new FlatForward(Today, 0.05, Dc), 1.2, 0.35, 0.5, 0.4, -0.3);
        RollbackAllocatesNothing(op, mesher, FdmSchemeDesc.Hundsdorfer(), 1.1);
    }

    [Fact]
    public void ACirStepOnHundsdorferAllocatesNothing()
    {
        var process = new BlackScholesMertonProcess(
            36.0, new FlatForward(Today, 0.01, Dc), new FlatForward(Today, 0.06, Dc), new BlackConstantVol(Today, new NullCalendar(), 0.2, Dc));
        var mesher = new FdmMesherComposite(
            new FdmBlackScholesMesher(15, process, 1.0, 40.0), new Uniform1dMesher(0.0, 0.2, 11));
        var helper = new FdmQuantoHelper(
            new FlatForward(Today, 0.06, Dc), new FlatForward(Today, 0.02, Dc), new BlackConstantVol(Today, new NullCalendar(), 0.15, Dc), -0.4, 1.0);
        var op = new FdmCIROp(mesher, new CoxIngersollRossProcess(2.0, 0.15, 0.05, 0.05), process, 0.3, 40.0, helper);
        RollbackAllocatesNothing(op, mesher, FdmSchemeDesc.Hundsdorfer(), Math.Log(40.0));
    }

    [Fact]
    public void TheSwingConditionAllocatesNothing()
    {
        var process = new BlackScholesMertonProcess(
            30.0, new FlatForward(Today, 0.02, Dc), new FlatForward(Today, 0.14, Dc), new BlackConstantVol(Today, new NullCalendar(), 0.4, Dc));
        var mesher = new FdmMesherComposite(new FdmBlackScholesMesher(20, process, 1.0, 30.0), new Uniform1dMesher(0, 3, 4));
        double[] times = [0.25, 0.5, 0.75, 1.0];
        var condition = new FdmSimpleSwingCondition(
            times, mesher, new FdmLogInnerValue(new VanillaForwardPayoff(OptionType.Put, 30.0), mesher, 0), 1, 1);
        double[] initial = new double[mesher.Layout.Size];
        double[] warm = [.. initial];
        condition.ApplyTo(warm, 0.5);

        double[] values = AllocationAssert.AllocatesNothing(() => (double[])initial.Clone(), fresh => condition.ApplyTo(fresh, 0.5));

        Assert.Equal(warm, values);
    }

    private static void RollbackAllocatesNothing(FdmLinearOpComposite op, FdmMesher mesher, FdmSchemeDesc desc, double strike)
    {
        double[] initial = new double[mesher.Layout.Size];
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int i = 0; i < initial.Length; i++, iter.Increment())
        {
            double x = mesher.Location(iter, 0) - strike;
            initial[i] = x > 0.0 ? x : 0.0;
        }

        var solver = new FdmBackwardSolver(op, [], null, desc);
        double[] warm = [.. initial];
        solver.Rollback(warm, 1.0, 0.0, 20, 0);

        double[] values = AllocationAssert.AllocatesNothing(
            () => (double[])initial.Clone(),
            fresh => solver.Rollback(fresh, 1.0, 0.0, 20, 0));

        Assert.Equal(warm, values);
    }
}
