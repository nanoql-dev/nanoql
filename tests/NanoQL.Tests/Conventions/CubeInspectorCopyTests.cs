// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A SABR cube's matrix inspectors return copies (playbook §2.5): a <see cref="Matrix"/> is a working type, so changing
/// one an inspector returned leaves the cube, and the next call's matrix, unchanged. So does the interpolated cube's
/// <c>VolSpreads(i)</c>.
/// </summary>
public class CubeInspectorCopyTests
{
    [Fact]
    public void TheMatrixInspectorsReturnCopies()
    {
        var cube = CubeFixture.Sabr();
        foreach (Func<Matrix> inspector in new Func<Matrix>[]
                 {
                     cube.SparseSabrParameters, cube.DenseSabrParameters, cube.MarketVolCube, cube.VolCubeAtmCalibrated,
                     () => cube.MarketVolCube(2),
                 })
        {
            Matrix first = inspector();
            double[] before = Flat(first);
            first[0, 1] = 99.0;
            Assert.Equal(before, Flat(inspector()));
        }

        var interpolated = CubeFixture.Interpolated();
        Matrix spreads = interpolated.VolSpreads(0);
        double[] spreadsBefore = Flat(spreads);
        spreads[0, 0] = 99.0;
        Assert.Equal(spreadsBefore, Flat(interpolated.VolSpreads(0)));
    }

    private static double[] Flat(Matrix m) => [.. Enumerable.Range(0, m.Rows).SelectMany(i => m.Row(i).ToArray())];
}
