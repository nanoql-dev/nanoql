// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The regression's basis size (the S-mmp plan's MMPR4 (xix), Revision 2's O1-5, fixed under R8). QuantLib takes the
/// number of basis functions from path 0, valid or not (<c>genericlsregression.cpp:39</c>), and the node-data
/// collection leaves an invalid path's values empty, so a path ended before an exercise, first among the paths, makes
/// QuantLib copy every valid path's values past a one-entry buffer: a crash (the round-1 review's probe). nanoql takes
/// the first valid path's, and refuses an exercise no path reaches with the parametric optimisation's text, "no valid
/// paths". Check: QuantLib's regression on the same data with the invalid path moved last, bit for bit at the exercise
/// the path misses, within the reordered sums' rounding at the one before (this run's <c>probes\c3_facts.cpp</c>; the
/// planner's MPP22).
/// </summary>
public class GenericLSRegressionBasisSizeTests
{
    [Fact]
    public void AnInvalidFirstPathIsSkipped()
    {
        // the probe's data, path 5 invalid at the second exercise, moved first
        NodeData[][] data = Data([5, 0, 1, 2, 3, 4]);
        GenericLSRegressionResults r = GenericLSRegressionFunctions.GenericLongstaffSchwartzRegression(data);

        // QuantLib's, the invalid path last: at the exercise it misses, the valid paths in the same order, bit for bit;
        // at the first, where it is valid, its place reorders the sums
        Assert.Equal([0.026500000000029944, -0.84000000000099817, 2.0000000000022879], r.BasisCoefficients[1]);
        double[] quantLibFirst = [0.0059303571428601654, 0.038928571428577641, -1.0892857142856798];
        for (int k = 0; k < 3; ++k)
        {
            Assert.Equal(quantLibFirst[k], r.BasisCoefficients[0][k], 1e-10 * Math.Abs(quantLibFirst[k]));
        }

        Assert.Equal(0.0077166666666666677, r.BiasedEstimate, 1e-17);
    }

    [Fact]
    public void AnExerciseNoPathReachesIsRefused()
    {
        NodeData[][] data = Data([0, 1, 2, 3, 4, 5]);
        foreach (NodeData d in data[2])
        {
            d.IsValid = false;
        }

        Assert.Equal(
            "no valid paths",
            Assert.Throws<NanoQLException>(() => GenericLSRegressionFunctions.GenericLongstaffSchwartzRegression(data)).Message);
    }

    // The probe's node data, its paths in the given order: three basis values on each valid path.
    private static NodeData[][] Data(int[] order)
    {
        var data = new NodeData[3][];
        for (int i = 0; i < 3; ++i)
        {
            data[i] = new NodeData[6];
            for (int p = 0; p < 6; ++p)
            {
                int j = order[p];
                var d = new NodeData();
                if (i == 0)
                {
                    d.CumulatedCashFlows = 0.001 * j;
                }
                else
                {
                    d.IsValid = !(i == 2 && j == 5);
                    d.ExerciseValue = 0.002 + (0.0005 * (((i * 7) + (j * 3)) % 5));
                    d.CumulatedCashFlows = 0.0015 + (0.0004 * (((i * 5) + (j * 2)) % 7));
                    d.ControlValue = 0.0;
                    if (d.IsValid)
                    {
                        d.Values = [1.0, 0.03 + (0.001 * j), 0.0009 + (0.0001 * j * j)];
                    }
                }

                data[i][p] = d;
            }
        }

        return data;
    }
}
