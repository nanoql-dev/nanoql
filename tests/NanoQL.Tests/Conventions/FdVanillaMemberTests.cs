// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The finite-difference CEV, SABR and CIR units' public members that no vector or QuantLib case reaches, each against
/// a form the vectors or the facts check: the operators' matrices, which apply as the operators; the CIR builder's
/// setters, each the engine's argument; and the forward payoff's name, QuantLib's.
/// </summary>
public class FdVanillaMemberTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void TheOperatorsMatricesApplyAsTheOperators()
    {
        var curve = new FlatForward(Today, 0.05, Dc);
        var cevMesher = new FdmMesherComposite(new FdmCEV1dMesher(15, 2.1, 0.75, 0.45, 1.0, 1e-4, 1.0, (2.3, 0.1)));
        var cev = new FdmCEVOp(cevMesher, curve, 2.1, 0.75, 0.45, 0);
        var sabrMesher = new FdmMesherComposite(
            new FdmCEV1dMesher(9, 1.2, 0.4, 0.5, 1.0, 1e-4, 1.0, (1.1, 0.025)),
            new Concentrating1dMesher(Math.Log(0.35) - 1.0, Math.Log(0.35) + 1.0, 7, (Math.Log(0.35), 0.1)));
        var sabr = new FdmSabrOp(sabrMesher, curve, 1.2, 0.35, 0.5, 0.4, -0.3);
        var process = new BlackScholesMertonProcess(
            36.0, new FlatForward(Today, 0.01, Dc), new FlatForward(Today, 0.06, Dc), new BlackConstantVol(Today, new NullCalendar(), 0.2, Dc));
        var cirMesher = new FdmMesherComposite(new FdmBlackScholesMesher(9, process, 1.0, 40.0), new Uniform1dMesher(0.0, 0.2, 7));
        var cir = new FdmCIROp(cirMesher, new CoxIngersollRossProcess(2.0, 0.15, 0.05, 0.05), process, 0.3, 40.0);

        foreach ((FdmLinearOpComposite op, int size) in new (FdmLinearOpComposite, int)[] { (cev, 15), (sabr, 63), (cir, 63) })
        {
            op.SetTime(0.5, 0.75);
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
    public void TheCirBuildersSettersAreTheEnginesArguments()
    {
        var process = new BlackScholesMertonProcess(
            36.0, new FlatForward(Today, 0.01, Dc), new FlatForward(Today, 0.06, Dc), new BlackConstantVol(Today, new NullCalendar(), 0.2, Dc));
        var cir = new CoxIngersollRossProcess(2.0, 0.1, 0.05, 0.05);
        var helper = new FdmQuantoHelper(
            new FlatForward(Today, 0.06, Dc), new FlatForward(Today, 0.02, Dc), new BlackConstantVol(Today, new NullCalendar(), 0.15, Dc), -0.4, 1.0);
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 40.0), new AmericanExercise(Today, Today + 365));
        Date dividendDate = Today + 120;
        OneAssetOptionResults built = new MakeFdCIRVanillaEngine(cir, process, 0.3)
            .WithTGrid(20).WithXGrid(40).WithRGrid(15).WithDampingSteps(1).WithQuantoHelper(helper)
            .WithCashDividends([dividendDate], [1.0]).WithFdmSchemeDesc(FdmSchemeDesc.Douglas())
            .ToEngine()(put, Context);
        OneAssetOptionResults direct = FdCIRVanillaEngine.Calculate(
            put, Context, cir, process, DividendFunctions.DividendVector([dividendDate], [1.0]), 20, 40, 15, 1, 0.3,
            FdmSchemeDesc.Douglas(), helper);
        Assert.Equal(direct.NPV, built.NPV);
        Assert.Equal(direct.Delta, built.Delta);
        Assert.Equal(direct.Gamma, built.Gamma);
        Assert.Equal(direct.Theta, built.Theta);
    }

    [Fact]
    public void TheForwardPayoffsNameIsQuantLibs() =>
        Assert.Equal("ForwardTypePayoff", new VanillaForwardPayoff(OptionType.Put, 30.0).Name);
}
