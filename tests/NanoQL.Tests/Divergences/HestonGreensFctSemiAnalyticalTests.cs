// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="FdmHestonGreensFct"/>'s semi-analytical algorithm reads <see cref="HestonProcess.Pdf"/>, the fixed
/// density (<c>Divergences/HestonPdfTests.cs</c>), where QuantLib 1.44's returns NaN at every point: on the equity
/// Heston process at 0.02 years, the plain and log transformations, QuantLib's Green's function is NaN at each of the
/// nine points below (the harness, <c>FdmHestonGreensFct::get</c>); nanoql's is the density at each node, times v under
/// the log transformation, positive and finite.
/// </summary>
public class HestonGreensFctSemiAnalyticalTests
{
    private static readonly Date Today = new(15, October, 2026);

    [Theory]
    [InlineData(FdmSquareRootFwdOpTransformationType.Plain)]
    [InlineData(FdmSquareRootFwdOpTransformationType.Log)]
    public void TheDensityIsTheProcesssAtEachNode(FdmSquareRootFwdOpTransformationType type)
    {
        DayCounter a365 = new Actual365Fixed();
        var process = new HestonProcess(new FlatForward(Today, 0.04, a365), new FlatForward(Today, 0.02, a365), 100.0, 0.04, 1.5, 0.04, 0.3, -0.7);
        double[] x = [Math.Log(95.0), Math.Log(100.0), Math.Log(105.0)];
        double[] v = [0.03, 0.04, 0.05];
        bool log = type == FdmSquareRootFwdOpTransformationType.Log;
        var mesher = new FdmMesherComposite(new Predefined1dMesher(x), new Predefined1dMesher(log ? v.Select(u => Math.Log(u)) : v));

        Vector density = new FdmHestonGreensFct(mesher, process, type).Get(0.02, FdmHestonGreensFctAlgorithm.SemiAnalytical);

        for (int j = 0; j < v.Length; ++j)
        {
            for (int i = 0; i < x.Length; ++i)
            {
                // the node's variance as the Green's function reads it, exp(ln v) under the log transformation
                double node = log ? Math.Exp(Math.Log(v[j])) : v[j];
                double expected = process.Pdf(x[i], node, 0.02, 1e-4) * (log ? node : 1.0);
                Assert.Equal(expected, density[(j * x.Length) + i]);
                Assert.True(double.IsFinite(expected) && expected > 0.0);
            }
        }
    }
}
