// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The finite-difference SLV model's first density is the Green's function of the process its forward operator evolves,
/// the variance's volatility the mixing factor times σ. QuantLib builds its operator and variance mesher on the mixed σ
/// and its Green's function on the unmixed σ (<c>hestonslvfdmmodel.cpp:297</c>, <c>:346</c>, <c>:415</c>, <c>:418</c>),
/// a density the operator's process doesn't have. The independent check is the mixed process's own density (<see
/// cref="HestonProcess.Pdf"/>): against it the Gaussian algorithm on the mixed σ is within 5% in L1 at the first step,
/// on the unmixed σ 76% off. At a mixing factor of 1 the density is QuantLib's; no QuantLib case runs the model with
/// another.
/// </summary>
public class HestonSlvFdmMixingGreensTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void TheFirstDensityIsTheMixedProcesssGreensFunction()
    {
        var parameters = new HestonSLVFokkerPlanckFdmParams(
            51, 51, 200, 50, 100.0, 5, 2, 0.1, 1e-4, 10000, 1e-5, 1e-5, 0.0000025, 1.0, 0.1, 0.9, 1e-5,
            FdmHestonGreensFctAlgorithm.Gaussian, FdmSquareRootFwdOpTransformationType.Log, FdmSchemeDesc.ModifiedCraigSneyd());
        var model = new HestonSLVFDMModel(
            new LocalConstantVol(Today, 0.25, A365), new HestonModel(Process(0.6)), Today + 73, parameters, true, null, 0.5);
        HestonSLVFDMModelLogEntry first = model.LogEntries[0];

        // the leverage at the start, the local volatility over √v₀
        Vector expected = new FdmHestonGreensFct(first.Mesher, Process(0.3), FdmSquareRootFwdOpTransformationType.Log, 0.25 / 0.2)
            .Get(first.T, FdmHestonGreensFctAlgorithm.Gaussian);
        Assert.Equal(expected.ToArray(), first.Prob.ToArray());
    }

    [Fact]
    public void TheMixedSigmasDensityIsTheMixedProcesssAndTheUnmixedIsnt()
    {
        const double t = 1.0 / 200;
        HestonProcess mixed = Process(0.3);
        double sdx = Math.Sqrt(0.04 * t), sdv = 0.3 * Math.Sqrt(0.04 * t);
        double[] xs = [.. Enumerable.Range(0, 41).Select(i => Math.Log(100.0) + ((i - 20) * 0.25 * sdx))];
        double[] vs = [.. Enumerable.Range(0, 41).Select(i => 0.04 + ((i - 20) * 0.25 * sdv))];
        var mesher = new FdmMesherComposite(new Predefined1dMesher(xs), new Predefined1dMesher(vs));
        Vector onMixed = new FdmHestonGreensFct(mesher, mixed, FdmSquareRootFwdOpTransformationType.Plain).Get(t, FdmHestonGreensFctAlgorithm.Gaussian);
        Vector onUnmixed = new FdmHestonGreensFct(mesher, Process(0.6), FdmSquareRootFwdOpTransformationType.Plain).Get(t, FdmHestonGreensFctAlgorithm.Gaussian);

        double mixedError = 0.0, unmixedError = 0.0, norm = 0.0;
        for (int j = 0; j < vs.Length; ++j)
        {
            for (int i = 0; i < xs.Length; ++i)
            {
                double exact = mixed.Pdf(xs[i], vs[j], t, 1e-6);
                int k = (j * xs.Length) + i;
                mixedError += Math.Abs(onMixed[k] - exact);
                unmixedError += Math.Abs(onUnmixed[k] - exact);
                norm += exact;
            }
        }

        Assert.True(mixedError / norm < 0.05, $"{mixedError / norm}");
        Assert.True(unmixedError / norm > 0.7, $"{unmixedError / norm}");
    }

    // v0 = theta 0.04, kappa 1.5, rho -0.7, spot 100: sigma 0.6 at a mixing factor of 0.5 is sigma 0.3's dynamics
    private static HestonProcess Process(double sigma) =>
        new(new FlatForward(Today, 0.04, A365), new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, sigma, -0.7);
}
