// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The finite-difference core's inner loop allocates nothing (the S-eq plan's SEQR1): the operator's
/// <see cref="FdmBlackScholesOp.SetTime"/> rewrites its bands in held buffers, each scheme steps in place through
/// buffers it holds, and the step conditions apply in place, where QuantLib builds arrays and operators at every step.
/// A rollback of <see cref="FdmBackwardSolver"/>, which keeps its schemes after the first, is measured under each of
/// the eight schemes the solver runs step by step (the method of lines, whose adaptive Runge-Kutta allocates per step
/// as QuantLib's does, is left out), with the local volatility, whose walk over the layout reads the local volatility
/// at each point, and with a Bermudan condition. Each rollback is measured on fresh values after a warm-up on others
/// (<see cref="AllocationAssert"/>), and gives the warm-up's values.
/// </summary>
public class FdmRollbackAllocationTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();

    [Fact]
    public void DouglasAllocatesNothing() => RollbackAllocatesNothing(FdmSchemeDesc.Douglas());

    [Fact]
    public void CrankNicolsonAllocatesNothing() => RollbackAllocatesNothing(FdmSchemeDesc.CrankNicolson());

    [Fact]
    public void ImplicitEulerAllocatesNothing() => RollbackAllocatesNothing(FdmSchemeDesc.ImplicitEuler());

    [Fact]
    public void ExplicitEulerAllocatesNothing() => RollbackAllocatesNothing(FdmSchemeDesc.ExplicitEuler(), steps: 400);

    [Fact]
    public void HundsdorferAllocatesNothing() => RollbackAllocatesNothing(FdmSchemeDesc.Hundsdorfer());

    [Fact]
    public void CraigSneydAllocatesNothing() => RollbackAllocatesNothing(FdmSchemeDesc.CraigSneyd());

    [Fact]
    public void ModifiedCraigSneydAllocatesNothing() => RollbackAllocatesNothing(FdmSchemeDesc.ModifiedCraigSneyd());

    [Fact]
    public void TrBDF2AllocatesNothing() => RollbackAllocatesNothing(FdmSchemeDesc.TrBDF2());

    [Fact]
    public void ALocalVolatilityRollbackAllocatesNothing() =>
        RollbackAllocatesNothing(FdmSchemeDesc.Douglas(), localVol: true);

    [Fact]
    public void ABermudanRollbackAllocatesNothing() => RollbackAllocatesNothing(FdmSchemeDesc.Douglas(), bermudan: true);

    private static void RollbackAllocatesNothing(FdmSchemeDesc desc, int steps = 50, bool localVol = false, bool bermudan = false)
    {
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, Dc), new FlatForward(Today, 0.04, Dc), localVol ? Surface() : new BlackConstantVol(Today, null, 0.2, Dc));
        var mesher = new FdmMesherComposite(new FdmBlackScholesMesher(101, process, 1.0, 100.0));
        var op = new FdmBlackScholesOp(mesher, process, 100.0, localVol);
        var calculator = new FdmLogInnerValue(new PlainVanillaPayoff(OptionType.Put, 100.0), mesher, 0);
        FdmStepConditionComposite? conditions = null;
        if (bermudan)
        {
            var condition = new FdmBermudanStepCondition([Today + 91, Today + 182, Today + 273, Today + 365], Today, Dc, mesher, calculator);
            conditions = new FdmStepConditionComposite([condition.ExerciseTimes], [condition]);
        }

        double[] initial = new double[mesher.Layout.Size];
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int i = 0; i < initial.Length; i++, iter.Increment())
        {
            initial[i] = calculator.AvgInnerValue(iter, 1.0);
        }

        var solver = new FdmBackwardSolver(op, [], conditions, desc);
        double[] warm = [.. initial];
        solver.Rollback(warm, 1.0, 0.0, steps, 0);

        double[] values = AllocationAssert.AllocatesNothing(
            () => (double[])initial.Clone(),
            fresh => solver.Rollback(fresh, 1.0, 0.0, steps, 0));

        Assert.Equal(warm, values);
    }

    private static BlackVarianceSurface Surface()
    {
        var vols = new Matrix(3, 3);
        double[,] rows = { { 0.25, 0.24, 0.23 }, { 0.21, 0.205, 0.2 }, { 0.22, 0.215, 0.21 } };
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                vols[i, j] = rows[i, j];
            }
        }

        return new BlackVarianceSurface(Today, null, [Today + 182, Today + 365, Today + 730], [80.0, 100.0, 120.0], vols, Dc);
    }
}
