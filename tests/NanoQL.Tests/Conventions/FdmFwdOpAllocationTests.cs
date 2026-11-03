// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A step of the forward operators allocates nothing (the one-dimensional zero-allocation pin): <c>SetTime</c> rewrites
/// the bands in place through buffers the operators hold, by the Black forward variance and by the local volatility. A
/// Douglas rollback (the local-volatility density calculator's scheme) is measured on fresh values after a warm-up
/// (<see cref="AllocationAssert"/>), and gives the warm-up's values.
/// </summary>
public class FdmFwdOpAllocationTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ABlackScholesForwardStepAllocatesNothing(bool localVol)
    {
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.01, Dc), new FlatForward(Today, 0.04, Dc), new BlackConstantVol(Today, new NullCalendar(), 0.25, Dc));
        var mesher = new FdmMesherComposite(new FdmBlackScholesMesher(31, process, 1.0, 100.0));
        RollbackAllocatesNothing(new FdmBlackScholesFwdOp(mesher, process, 100.0, localVol), mesher);
    }

    [Fact]
    public void ALocalVolForwardStepAllocatesNothing()
    {
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.01, Dc), new FlatForward(Today, 0.04, Dc), new BlackConstantVol(Today, new NullCalendar(), 0.25, Dc));
        var mesher = new FdmMesherComposite(new FdmBlackScholesMesher(31, process, 1.0, 100.0));
        var op = new FdmLocalVolFwdOp(
            mesher, 100.0, new FlatForward(Today, 0.04, Dc), new FlatForward(Today, 0.01, Dc), new LocalConstantVol(Today, 0.25, Dc));
        RollbackAllocatesNothing(op, mesher);
    }

    private static void RollbackAllocatesNothing(FdmLinearOpComposite op, FdmMesher mesher)
    {
        // a narrow density about the log spot
        double[] initial = new double[mesher.Layout.Size];
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int i = 0; i < initial.Length; i++, iter.Increment())
        {
            double x = mesher.Location(iter, 0) - Math.Log(100.0);
            initial[i] = Math.Exp(-50.0 * x * x);
        }

        var solver = new FdmBackwardSolver(op, [], null, FdmSchemeDesc.Douglas());
        double[] warm = [.. initial];
        solver.Rollback(warm, 1.0, 0.0, 20, 0);

        double[] values = AllocationAssert.AllocatesNothing(
            () => (double[])initial.Clone(),
            fresh => solver.Rollback(fresh, 1.0, 0.0, 20, 0));

        Assert.Equal(warm, values);
    }
}
