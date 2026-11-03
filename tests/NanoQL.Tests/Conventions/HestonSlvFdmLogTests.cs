// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="HestonSLVFDMModel.LogEntries"/> is filled while the constructor calibrates, with logging on, and empty
/// with it off: QuantLib's <c>logEntries()</c> recalibrates on each read; here every read returns the one list built at
/// construction, an entry per step, its densities and meshers the calibration's.
/// </summary>
public class HestonSlvFdmLogTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void WithLoggingTheLogIsBuiltOnceAtConstruction()
    {
        HestonSLVFDMModel model = Model(true);
        ImmutableArray<HestonSLVFDMModelLogEntry> log = model.LogEntries;
        Assert.NotEmpty(log);
        Assert.True(log == model.LogEntries);
        Assert.Equal([.. log.Select(e => e.T).Order()], log.Select(e => e.T));
        Assert.All(log, e => Assert.Equal(e.Mesher.Layout.Size, e.Prob.Length));
        Assert.Equal(73 / 365.0, log[^1].T, 15);
    }

    [Fact]
    public void WithoutLoggingTheLogIsEmpty()
    {
        HestonSLVFDMModel model = Model(false);
        Assert.Empty(model.LogEntries);
        Assert.Equal(Model(true).LeverageFunction.LocalVol(0.1, 100.0, true), model.LeverageFunction.LocalVol(0.1, 100.0, true));
    }

    private static HestonSLVFDMModel Model(bool logging)
    {
        var process = new HestonProcess(
            new FlatForward(Today, 0.04, A365), new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.3, -0.7);
        var parameters = new HestonSLVFokkerPlanckFdmParams(
            51, 51, 200, 50, 100.0, 5, 2, 0.1, 1e-4, 10000, 1e-5, 1e-5, 0.0000025, 1.0, 0.1, 0.9, 1e-5,
            FdmHestonGreensFctAlgorithm.ZeroCorrelation, FdmSquareRootFwdOpTransformationType.Log, FdmSchemeDesc.ModifiedCraigSneyd());
        return new HestonSLVFDMModel(new LocalConstantVol(Today, 0.25, A365), new HestonModel(process), Today + 73, parameters, logging);
    }
}
