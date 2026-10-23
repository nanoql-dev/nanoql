// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The finite-difference core's public members that no vector or QuantLib case reaches, each against a form the vectors
/// check: the quanto adjustment of a vector, the mesher's process helper, an operator's copy, the composite mesher's
/// two- and four-mesher constructors, the model's rollback without a condition and its stopping times, and the
/// short-rate operators' and the nine-point operator's matrices and copies, which apply as the operators.
/// </summary>
public class FdmMemberTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();

    [Fact]
    public void TheQuantoAdjustmentOfAVectorIsEachVolatilitysOwn()
    {
        var helper = new FdmQuantoHelper(
            new FlatForward(Today, 0.04, Dc), new FlatForward(Today, 0.06, Dc), new BlackConstantVol(Today, null, 0.12, Dc), -0.75, 1.0);
        double[] vols = [0.1, 0.25, 0.4];
        Vector adjusted = helper.QuantoAdjustment(new Vector(vols), 0.25, 0.5);
        Assert.Equal(vols.Select(v => helper.QuantoAdjustment(v, 0.25, 0.5)).ToArray(), adjusted.ToArray());
    }

    [Fact]
    public void TheProcessHelperDatesItsVolatilityByTheRiskFreeCurve()
    {
        YieldTermStructure rTS = new FlatForward(Today + 2, 0.05, new Actual360());
        YieldTermStructure qTS = new FlatForward(Today, 0.02, Dc);
        GeneralizedBlackScholesProcess process = FdmBlackScholesMesher.ProcessHelper(100.0, rTS, qTS, 0.2);
        Assert.Equal(100.0, process.X0);
        Assert.Same(rTS, process.RiskFreeRate);
        Assert.Same(qTS, process.DividendYield);
        Assert.Equal(rTS.ReferenceDate, process.BlackVolatility.ReferenceDate);
        Assert.Equal(rTS.DayCounter, process.BlackVolatility.DayCounter);
        Assert.Equal(0.2, process.BlackVolatility.BlackVol(1.0, 100.0));
    }

    [Fact]
    public void AnOperatorsCopyAppliesAsTheOriginal()
    {
        var mesher = new FdmMesherComposite(new Uniform1dMesher(-1.0, 2.0, 7));
        TripleBandLinearOp op = new SecondDerivativeOp(0, mesher).Add(new FirstDerivativeOp(0, mesher));
        var r = new Vector([0.3, -0.2, 0.5, 0.9, -0.4, 0.1, 0.7]);
        Assert.Equal(op.Apply(r).ToArray(), op.Clone().Apply(r).ToArray());
        Assert.Equal(op.Apply(r).ToArray(), new TripleBandLinearOp(op).Apply(r).ToArray());
        Assert.Equal(op.SolveSplitting(r, 0.3).ToArray(), op.Clone().SolveSplitting(r, 0.3).ToArray());
    }

    [Fact]
    public void TheTwoAndFourMesherConstructorsAreTheListsOwn()
    {
        Fdm1dMesher a = new Uniform1dMesher(0.0, 1.0, 3), b = new Uniform1dMesher(-1.0, 2.0, 4);
        Fdm1dMesher c = new Concentrating1dMesher(0.0, 5.0, 5, (1.0, 0.2)), d = new Uniform1dMesher(2.0, 3.0, 2);
        AssertSameMesher(new FdmMesherComposite([a, b]), new FdmMesherComposite(a, b));
        AssertSameMesher(new FdmMesherComposite([a, b, c, d]), new FdmMesherComposite(a, b, c, d));
    }

    [Fact]
    public void ARollbackWithoutAConditionIsOneWithAnEmptyComposite()
    {
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, Dc), new FlatForward(Today, 0.05, Dc), new BlackConstantVol(Today, null, 0.25, Dc));
        var mesher = new FdmMesherComposite(new FdmBlackScholesMesher(21, process, 1.0, 100.0));
        var calculator = new FdmLogInnerValue(new PlainVanillaPayoff(OptionType.Put, 100.0), mesher, 0);
        double[] initial = new double[21];
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int i = 0; i < initial.Length; i++, iter.Increment())
        {
            initial[i] = calculator.AvgInnerValue(iter, 1.0);
        }

        double[] without = [.. initial], with = [.. initial];
        var model = new FiniteDifferenceModel<DouglasScheme>(
            new DouglasScheme(0.5, new FdmBlackScholesOp(mesher, process, 100.0)), [0.6, 0.2, 0.6]);
        Assert.Equal([0.2, 0.6], model.StoppingTimes.ToArray());
        model.Rollback(without, 1.0, 0.0, 20);
        var other = new FiniteDifferenceModel<DouglasScheme>(
            new DouglasScheme(0.5, new FdmBlackScholesOp(mesher, process, 100.0)), [0.6, 0.2, 0.6]);
        other.Rollback(with, 1.0, 0.0, 20, new FdmStepConditionComposite([], []));
        Assert.Equal(with, without);
    }

    [Fact]
    public void TheShortRateOperatorsMatricesApplyAsTheOperators()
    {
        YieldTermStructure curve = new FlatForward(Today, 0.03, Dc);
        var g2 = new G2(curve, 0.07, 0.011, 0.3, 0.008, -0.6);
        var mesher2 = new FdmMesherComposite(
            new FdmSimpleProcess1dMesher(9, new OrnsteinUhlenbeckProcess(g2.A(), g2.Sigma), 5.0, 1, 1e-5),
            new FdmSimpleProcess1dMesher(7, new OrnsteinUhlenbeckProcess(g2.B(), g2.Eta), 5.0, 1, 1e-5));
        var g2Op = new FdmG2Op(mesher2, g2, 0, 1);
        g2Op.SetTime(0.5, 0.75);
        var hullWhite = new HullWhite(curve, 0.1, 0.01);
        var mesher1 = new FdmMesherComposite(
            new FdmSimpleProcess1dMesher(11, new OrnsteinUhlenbeckProcess(0.1, 0.01), 5.0, 1, 1e-5));
        var hwOp = new FdmHullWhiteOp(mesher1, hullWhite, 0);
        hwOp.SetTime(0.5, 0.75);

        foreach ((FdmLinearOpComposite op, int size) in new (FdmLinearOpComposite, int)[] { (g2Op, 63), (hwOp, 11) })
        {
            var r = new Vector([.. Enumerable.Range(0, size).Select(i => Math.Sin(0.7 * i) + 1.5)]);
            ImmutableArray<SparseMatrix> parts = op.ToMatrixDecomp();
            SparseMatrix sum = parts[0];
            for (int k = 1; k < parts.Length; k++)
            {
                sum += parts[k];
            }

            Vector applied = op.Apply(r), product = SparseMatrixFunctions.Prod(sum, r);
            for (int i = 0; i < size; i++)
            {
                Assert.True(Math.Abs(product[i] - applied[i]) <= 1e-12 * Math.Max(1.0, Math.Abs(applied[i])), $"{i}");
            }
        }
    }

    [Fact]
    public void TheNinePointOperatorsCopiesAndMatrixApplyAsIt()
    {
        var mesher = new FdmMesherComposite(new Uniform1dMesher(-1.0, 2.0, 5), new Uniform1dMesher(0.0, 3.0, 4));
        NinePointLinearOp op = new SecondOrderMixedDerivativeOp(0, 1, mesher)
            .Mult(new Vector([.. Enumerable.Range(0, 20).Select(i => 1.0 + (0.1 * i))]));
        var r = new Vector([.. Enumerable.Range(0, 20).Select(i => Math.Cos(0.3 * i))]);
        Vector applied = op.Apply(r);
        Assert.Equal(applied.ToArray(), op.Clone().Apply(r).ToArray());
        Assert.Equal(applied.ToArray(), new NinePointLinearOp(op).Apply(r).ToArray());
        Vector product = SparseMatrixFunctions.Prod(op.ToMatrix(), r);
        for (int i = 0; i < 20; i++)
        {
            Assert.True(Math.Abs(product[i] - applied[i]) <= 1e-12 * Math.Max(1.0, Math.Abs(applied[i])), $"{i}");
        }
    }

    private static void AssertSameMesher(FdmMesherComposite expected, FdmMesherComposite actual)
    {
        Assert.Equal(expected.Layout.Dim.ToArray(), actual.Layout.Dim.ToArray());
        for (int dir = 0; dir < expected.Layout.Dim.Length; dir++)
        {
            Assert.Equal(expected.Locations(dir).ToArray(), actual.Locations(dir).ToArray());
        }
    }
}
