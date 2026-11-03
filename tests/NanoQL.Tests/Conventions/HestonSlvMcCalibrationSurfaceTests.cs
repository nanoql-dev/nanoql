// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Monte Carlo SLV calibration reads its working leverage as QuantLib 1.44 reads its surface, and returns the fixed
/// local-volatility surface: while it calibrates, a read at a node time beyond that time's strikes extrapolates by the
/// time's line, so the calibrated leverage is QuantLib's on the wheel's case
/// (<c>tests/vectors/models/equity/hestonslvmcmodel.json</c>'s first, recorded from QuantLib-SWIG 1.44); the
/// <see cref="HestonSLVMCModel.LeverageFunction"/> it returns is <see cref="FixedLocalVolSurface"/>, which holds such a
/// strike at the time's nearest, where QuantLib's returned surface extrapolates.
/// </summary>
public class HestonSlvMcCalibrationSurfaceTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void TheCalibrationReadsItsWorkingLeverageAsQuantLib144()
    {
        LocalVolTermStructure leverage = Model().LeverageFunction;

        // QuantLib 1.44's leverage at these points (QuantLib-SWIG 1.44)
        AssertClose(1.384708439310267, leverage.LocalVol(0.5, 100.0, true));
        AssertClose(1.0569305423367548, leverage.LocalVol(1.0, 80.0, true));
        AssertClose(1.6043076169276118, leverage.LocalVol(13.0 / 24.0, 110.0, true));
    }

    [Fact]
    public void TheReturnedLeverageHoldsAStrikeBeyondANodesStrikesAtTheNearest()
    {
        LocalVolTermStructure leverage = Model().LeverageFunction;

        // QuantLib's returned surface extrapolates at the node 0.5: 19.407688886136786 at 1e4, 178.7403728460816 at 1e5,
        // 0.33665916973941323 at 1 and 0.3298952488798136 at 0.1
        double high = leverage.LocalVol(0.5, 1e4, true), low = leverage.LocalVol(0.5, 1.0, true);
        Assert.Equal(high, leverage.LocalVol(0.5, 1e5, true));
        Assert.Equal(low, leverage.LocalVol(0.5, 0.1, true));
        Assert.InRange(high, 0.5, 3.0);
        Assert.InRange(low, 0.5, 3.0);
    }

    private static HestonSLVMCModel Model()
    {
        var process = new HestonProcess(
            new FlatForward(Today, 0.04, A365), new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.3, -0.7);
        return new HestonSLVMCModel(
            new LocalConstantVol(Today, 0.25, A365), new HestonModel(process), new MTBrownianGeneratorFactory(1234), Today + 365, 12, 21, 8192);
    }

    private static void AssertClose(double expected, double actual) =>
        Assert.True(Math.Abs(actual - expected) <= 1e-10 * Math.Max(1.0, Math.Abs(expected)), $"{actual} for {expected}");
}
