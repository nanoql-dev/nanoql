// SPDX-FileCopyrightText: Copyright (C) 2026 Richard Amaya
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/trinomialtree.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class TrinomialTreeTests
{
    [Fact]
    public void TestSmallMandatoryGapDoesNotExplode()
    {
        // Testing trinomial tree node count stays bounded with a small mandatory time gap...

        var today = new Date(15, January, 2026);

        // Flat yield curve at 5%
        var termStructure = new FlatForward(today, 0.05, new Actual365Fixed());

        // Hull-White process: a=0.1, sigma=0.01
        var model = new HullWhite(termStructure, 0.1, 0.01);
        StochasticProcess1D process = model.Dynamics().Process;

        // TimeGrid with one very short mandatory-time gap (1ms after t=1.0).
        // Without the global dx floor this causes node count to explode to
        // several hundred at the tiny-dt step; with the floor it stays bounded.
        double[] mandatoryTimes = [1.0, 1.0 + 1.0e-3, 2.0, 3.0];
        var grid = new TimeGrid(mandatoryTimes);

        var tree = new TrinomialTree(process, grid);

        Size maxNodes = 0;
        for (Size i = 0; i < grid.Size; ++i)
        {
            maxNodes = Math.Max(maxNodes, tree.Size(i));
        }

        // The trinomial tree node count grows by at most one j-index per
        // side per step, so after nSteps steps maxNodes <= 2*nSteps + 1.
        Size nSteps = grid.Size - 1;
        Size expectedBound = (2 * nSteps) + 1;
        Assert.True(
            maxNodes <= expectedBound,
            $"trinomial tree exceeded derived bound: max node count {maxNodes} > 2*nSteps+1 = {expectedBound} "
            + "(dx-floor fix may be broken)");
    }

    [Fact]
    public void TestFloorThresholdBoundary()
    {
        // Testing trinomial tree behaviour at the floor activation threshold (dt_i / dt_max = 0.01)...

        var today = new Date(15, January, 2026);

        var termStructure = new FlatForward(today, 0.05, new Actual365Fixed());
        var model = new HullWhite(termStructure, 0.1, 0.01);
        StochasticProcess1D process = model.Dynamics().Process;

        // Helper: build a tree on a grid with one custom-ratio short step and report the peak node count, the steps,
        // and the short step's dx, possibly floored, beside its natural v*sqrt(3).
        (Size MaxNodes, Size NSteps, Real ShortStepDx, Real ShortStepNaturalDx) RunProbe(double gapRatio)
        {
            double dtMax = 1.0;
            double[] times = [1.0, 1.0 + (gapRatio * dtMax), 2.0, 3.0];
            var grid = new TimeGrid(times);

            var tree = new TrinomialTree(process, grid);

            // Locate the short step by scanning for the smallest dt; step i's dx is tree.Dx(i + 1). The reference
            // natural dx is computed in the same operation order as production (sqrt(v2) * sqrt(3.0)).
            Size shortIdx = 0;
            for (Size i = 1; i < grid.Size - 1; ++i)
            {
                if (grid.Dt(i) < grid.Dt(shortIdx))
                {
                    shortIdx = i;
                }
            }

            double shortDt = grid.Dt(shortIdx);
            Real shortV2 = process.Variance(grid[shortIdx], 0.0, shortDt);
            Real shortNatural = Math.Sqrt(shortV2) * Math.Sqrt(3.0);

            Size maxNodes = 0;
            for (Size i = 0; i < grid.Size; ++i)
            {
                maxNodes = Math.Max(maxNodes, tree.Size(i));
            }

            return (maxNodes, grid.Size - 1, tree.Dx(shortIdx + 1), shortNatural);
        }

        // Probes are placed immediately on either side of the activation threshold (0.01).
        const double kBelow = 0.0099;
        const double kAbove = 0.0101;

        // Just below threshold: floor active. Node count must stay bounded AND the short step's dx must be strictly
        // larger than its natural value.
        var belowResult = RunProbe(kBelow);
        Size belowBound = (2 * belowResult.NSteps) + 1;
        Assert.True(
            belowResult.MaxNodes <= belowBound,
            $"below-threshold node count {belowResult.MaxNodes} exceeded derived bound 2*nSteps+1 = {belowBound}");
        Assert.True(
            belowResult.ShortStepDx > belowResult.ShortStepNaturalDx,
            $"below-threshold floor did not activate: dx={belowResult.ShortStepDx} natural={belowResult.ShortStepNaturalDx} "
            + "(kFloorThreshold may have drifted down)");

        // Just above threshold: floor inactive. The constructor assigns dx exactly equal to v*sqrt(3) in this regime.
        var aboveResult = RunProbe(kAbove);
        Assert.Equal(aboveResult.ShortStepNaturalDx, aboveResult.ShortStepDx);
    }
}
