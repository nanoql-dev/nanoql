// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The zero-allocation pins of the basket operators' steps (the playbook's finite-difference row): a rollback of
/// <see cref="Fdm2dBlackScholesOp"/> on Hundsdorfer's scheme, the two-dimensional engine's default, with the Black and
/// the local volatility, and of <see cref="FdmWienerOp"/> on Douglas's, the n-dimensional engine's, in two and three
/// dimensions, allocates nothing after the first, each measured on fresh values after a warm-up.
/// </summary>
public class FdmBasketAllocationTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ATwoDimensionalBlackScholesStepOnHundsdorferAllocatesNothing(bool localVol)
    {
        GeneralizedBlackScholesProcess p1 = Process(100.0, 0.2), p2 = Process(95.0, 0.3);
        var mesher = new FdmMesherComposite(
            new FdmBlackScholesMesher(15, p1, 1.0, 100.0), new FdmBlackScholesMesher(13, p2, 1.0, 95.0));
        RollbackAllocatesNothing(new Fdm2dBlackScholesOp(mesher, p1, p2, 0.5, 1.0, localVol), mesher, FdmSchemeDesc.Hundsdorfer());
    }

    [Fact]
    public void AWienerStepOnDouglasAllocatesNothing()
    {
        var two = new FdmMesherComposite(new Uniform1dMesher(3.9, 5.3, 15), new Uniform1dMesher(4.0, 5.2, 11));
        RollbackAllocatesNothing(new FdmWienerOp(two, new FlatForward(Today, 0.04, A365), [0.05, 0.01]), two, FdmSchemeDesc.Douglas());
        var three = new FdmMesherComposite(
            new Uniform1dMesher(3.9, 5.3, 9), new Uniform1dMesher(4.0, 5.2, 7), new Uniform1dMesher(4.1, 5.1, 5));
        RollbackAllocatesNothing(new FdmWienerOp(three, null, [0.05, 0.02, 0.01]), three, FdmSchemeDesc.Douglas());
    }

    private static GeneralizedBlackScholesProcess Process(double s, double vol) =>
        new BlackScholesMertonProcess(
            s, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.04, A365),
            new BlackConstantVol(Today, new NullCalendar(), vol, A365));

    private static void RollbackAllocatesNothing(FdmLinearOpComposite op, FdmMesher mesher, FdmSchemeDesc scheme)
    {
        double[] initial = new double[mesher.Layout.Size];
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int i = 0; i < initial.Length; i++, iter.Increment())
        {
            double x = Math.Exp(mesher.Location(iter, 0)) - 100.0;
            initial[i] = x > 0.0 ? x : 0.0;
        }

        var solver = new FdmBackwardSolver(op, ImmutableArray<BoundaryCondition<FdmLinearOp>>.Empty, null, scheme);
        double[] warm = [.. initial];
        solver.Rollback(warm, 1.0, 0.0, 20, 0);

        double[] values = AllocationAssert.AllocatesNothing(
            () => (double[])initial.Clone(),
            fresh => solver.Rollback(fresh, 1.0, 0.0, 20, 0));

        Assert.Equal(warm, values);
    }
}
