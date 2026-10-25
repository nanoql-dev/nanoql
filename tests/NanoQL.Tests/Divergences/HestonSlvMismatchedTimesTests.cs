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
/// The stochastic-local volatility models and the Heston Green's function on structures counted differently (the S-hes
/// plan's HR7 (xxi), the mismatched-dates class of master Revision 58). QuantLib's models run their time grids from the
/// risk-free curve's reference date and read the dividend curve and the local volatility at the risk-free curve's times
/// (<c>hestonslvmcmodel.cpp:47-55</c>, <c>hestonslvfdmmodel.cpp:300-330</c>), its Green's function the rates likewise.
/// nanoql's run on <see cref="FdmHestonOp.Clock"/>, each structure read at the instants its times denote, a local
/// volatility on another clock read through an adapter that extrapolates where the local volatility does. QuantLib's
/// check: its models on the market restated on one clock (r 4% on Act/360, q 2%·360/365 on Act/360), which nanoql's
/// on the original market (q 2% on Act/365) reproduce [executed on the wheel and the harness:
/// <c>scratchpad\c4\oracle_c4.py</c>]; the finite-difference calibration within its own spread under one-ulp moves of
/// its inputs (3.2e-9 measured, the S-hes run record's C4 entry).
/// </summary>
public class HestonSlvMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A360 = new Actual360();
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly LocalVolTermStructure LocalVol = new LocalConstantVol(Today, 0.25, A365);

    [Fact]
    public void TheMonteCarloModelReadsTheDividendCurveAtItsOwnTimes()
    {
        LocalVolTermStructure leverage = new HestonSLVMCModel(
            LocalVol, Model(), new MTBrownianGeneratorFactory(1234), Today + 365, 12, 21, 8192).LeverageFunction;

        // as given, QuantLib: 1.3902255462786601, 1.224465992916725, 1.5762006464817684
        AssertClose(1.389653883603824, leverage.LocalVol(0.5, 100.0, true), 1e-10);
        AssertClose(1.223836386556807, leverage.LocalVol(1.0, 90.0, true), 1e-10);
        AssertClose(1.575556048090163, leverage.LocalVol(13.0 / 24.0, 110.0, true), 1e-10);
    }

    [Fact]
    public void TheFiniteDifferenceModelReadsTheDividendCurveAtItsOwnTimes()
    {
        LocalVolTermStructure leverage = new HestonSLVFDMModel(LocalVol, Model(), Today + 365, Parameters()).LeverageFunction;

        // as given, QuantLib: 1.3423194539973495, 1.2087257806827008, 1.547723333088376
        AssertClose(1.3420152342629652, leverage.LocalVol(0.5, 100.0, true), 1e-8);
        AssertClose(1.2083436634228566, leverage.LocalVol(1.0, 90.0, true), 1e-8);
        AssertClose(1.547396112335777, leverage.LocalVol(0.75, 110.0, true), 1e-8);
    }

    [Fact]
    public void TheGreensFunctionReadsTheRatesOnTheClock()
    {
        double[] x = [Math.Log(90.0), Math.Log(100.0), Math.Log(110.0)];
        var mesher = new FdmMesherComposite(new Predefined1dMesher(x), new Predefined1dMesher([0.03, 0.04, 0.05]));
        var greens = new FdmHestonGreensFct(mesher, Model().Process, FdmSquareRootFwdOpTransformationType.Plain);

        double[] zeroCorrelation =
        [
            0.3546289271968496, 365.7910424075976, 1.2525682875294608, 0.6498694080661609, 670.3243587158821,
            2.2953734147225173, 0.2903242311088198, 299.46232523363904, 1.0254406707956574,
        ];
        double[] gaussian =
        [
            7.091452471745549e-07, 237.86366354188323, 0.8116655549757407, 0.0011457261598176493, 928.5895370253854,
            0.013603293614244356, 0.12153665651000564, 238.0127876823394, 1.496896768016231e-05,
        ];
        Vector z = greens.Get(0.02, FdmHestonGreensFctAlgorithm.ZeroCorrelation);
        Vector g = greens.Get(0.02, FdmHestonGreensFctAlgorithm.Gaussian);
        for (int i = 0; i < 9; ++i)
        {
            AssertClose(zeroCorrelation[i], z[i], 1e-12);
            AssertClose(gaussian[i], g[i], 1e-12);
        }
    }

    [Fact]
    public void ALocalVolatilityOnAnotherClockExtrapolatesWhereItDoes()
    {
        // strikes 60 to 160 only: the calibration's meshers reach past them, read without the extrapolate flag, so the
        // surface's own extrapolation decides, on its clock as on the model's
        var surface = new FixedLocalVolSurface(
            Today, [Today + 90, Today + 400], [60.0, 100.0, 160.0], Flat(0.25, 3, 2), A365);
        Assert.Throws<NanoQLException>(() => new HestonSLVFDMModel(surface, Model(), Today + 365, Parameters()));

        LocalVolTermStructure leverage =
            new HestonSLVFDMModel(surface.EnableExtrapolation(), Model(), Today + 365, Parameters()).LeverageFunction;
        LocalVolTermStructure constant = new HestonSLVFDMModel(LocalVol, Model(), Today + 365, Parameters()).LeverageFunction;
        AssertClose(constant.LocalVol(0.5, 100.0, true), leverage.LocalVol(0.5, 100.0, true), 1e-8);
    }

    private static HestonModel Model() =>
        new(new HestonProcess(new FlatForward(Today, 0.04, A360), new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.3, -0.7));

    private static HestonSLVFokkerPlanckFdmParams Parameters() =>
        new(51, 51, 200, 50, 100.0, 5, 2, 0.1, 1e-4, 10000, 1e-5, 1e-5, 0.0000025, 1.0, 0.1, 0.9, 1e-5,
            FdmHestonGreensFctAlgorithm.ZeroCorrelation, FdmSquareRootFwdOpTransformationType.Log, FdmSchemeDesc.ModifiedCraigSneyd());

    private static Matrix Flat(double value, int rows, int columns)
    {
        var m = new Matrix(rows, columns);
        for (int i = 0; i < rows; ++i)
        {
            for (int j = 0; j < columns; ++j)
            {
                m[i, j] = value;
            }
        }

        return m;
    }

    private static void AssertClose(double expected, double actual, double tolerance) =>
        Assert.True(Math.Abs(actual - expected) <= tolerance * Math.Max(1.0, Math.Abs(expected)), $"{actual:R} for {expected:R}");
}
