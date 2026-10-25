// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The stochastic-local volatility types' accessors, QuantLib's: <see cref="HestonSLVProcess"/> returns its Heston
/// process's parameters, spot and curves, its leverage function and mixing factor, and applies a move as the Heston
/// process does (the spot by e^dx, the variance by dx); the models return the local volatility they were given and
/// their Heston model's process.
/// </summary>
public class HestonSlvAccessorTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void TheProcessReturnsItsHestonProcesssParametersAndAppliesAsIt()
    {
        HestonProcess heston = Heston();
        var leverage = new LocalConstantVol(Today, 1.1, A365);
        var process = new HestonSLVProcess(heston, leverage, 0.7);

        Assert.Equal((0.04, -0.7, 1.5, 0.05, 0.3, 100.0), (process.V0, process.Rho, process.Kappa, process.Theta, process.Sigma, process.S0));
        Assert.Equal(0.7, process.MixingFactor);
        Assert.Same(leverage, process.LeverageFct);
        Assert.Same(heston.DividendYield, process.DividendYield);
        Assert.Same(heston.RiskFreeRate, process.RiskFreeRate);

        var x0 = new Vector([95.0, 0.03]);
        var dx = new Vector([0.02, -0.01]);
        Vector moved = process.Apply(x0, dx);
        Assert.Equal(heston.Apply(x0, dx).ToArray(), moved.ToArray());
        Assert.Equal([95.0 * Math.Exp(0.02), 0.03 + -0.01], moved.ToArray());
    }

    [Fact]
    public void TheModelsReturnWhatTheyWereGiven()
    {
        HestonProcess heston = Heston();
        var model = new HestonModel(heston);
        var localVol = new LocalConstantVol(Today, 0.25, A365);

        var mc = new HestonSLVMCModel(localVol, model, new MTBrownianGeneratorFactory(1234), Today + 91, 12, 11, 1024);
        Assert.Same(localVol, mc.LocalVol);
        Assert.Same(model.Process, mc.HestonProcess);

        var parameters = new HestonSLVFokkerPlanckFdmParams(
            51, 51, 200, 50, 100.0, 5, 2, 0.1, 1e-4, 10000, 1e-5, 1e-5, 0.0000025, 1.0, 0.1, 0.9, 1e-5,
            FdmHestonGreensFctAlgorithm.ZeroCorrelation, FdmSquareRootFwdOpTransformationType.Log, FdmSchemeDesc.ModifiedCraigSneyd());
        var fd = new HestonSLVFDMModel(localVol, model, Today + 91, parameters);
        Assert.Same(localVol, fd.LocalVol);
        Assert.Same(model.Process, fd.HestonProcess);
    }

    private static HestonProcess Heston() =>
        new(new FlatForward(Today, 0.04, A365), new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.05, 0.3, -0.7);
}
