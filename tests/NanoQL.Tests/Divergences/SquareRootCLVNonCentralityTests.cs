// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The square-root CLV model's non-centrality 4κe^(−κt)x₀/(σ²(1 − e^(−κt))) with 1 − e^(−κt) through expm1 below κt
/// 1e-2 (fixed). QuantLib's 1 − e^(−κt) cancels as κt falls: at κ 1e-4 and a day, κt 2.7e-7, its non-centrality is
/// 7.0e-11 relative off the 40-digit value (mpmath), 3284.99955000002, and the collocation points move with it.
/// nanoql's points, on bounds that don't clamp, are the Gauss nodes at the reference non-centrality.
/// </summary>
public class SquareRootCLVNonCentralityTests
{
    [Fact]
    public void ThePointsAreTheNodesAtTheReferenceNonCentrality()
    {
        var today = new Date(5, Month.October, 2016);
        DayCounter dc = new Actual365Fixed();
        Date maturity = today + 1;
        var model = new SquareRootCLVModel(
            SquareRootCLVRescaleTests.Bs(today, dc), new SquareRootProcess(0.09, 1e-4, 0.2, 0.09), [maturity], 14);
        double df = 4 * 0.09 * 1e-4 / (0.2 * 0.2);
        const double reference = 3284.99955000002;

        Vector x = model.CollocationPointsX(maturity);
        double[] nodes = SquareRootCLVRescaleTests.SortedNodes(df, reference);
        for (int i = 0; i < nodes.Length; i++)
        {
            Assert.True(Math.Abs(x[i] - nodes[i]) <= 1e-13 * nodes[i], $"point {i}: {x[i]} for {nodes[i]}");
        }

        // QuantLib's non-centrality and its nodes
        double t = dc.YearFraction(today, maturity);
        double quantLib = SquareRootCLVRescaleTests.Params(0.09, 1e-4, 0.2, 0.09, t).Ncp;
        Assert.True(Math.Abs((quantLib / reference) - 1) > 6e-11);
        double[] quantLibNodes = SquareRootCLVRescaleTests.SortedNodes(df, quantLib);
        Assert.True(Math.Abs((quantLibNodes[^1] / nodes[^1]) - 1) > 2e-11);
    }
}
