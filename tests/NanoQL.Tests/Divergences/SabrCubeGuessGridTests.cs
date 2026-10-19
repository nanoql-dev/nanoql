// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The grid a SABR cube's parameter guesses sit on. QuantLib's cube builds its guesses in its constructor
/// (<c>sabrswaptionvolatilitycube.hpp:357-359, 370-385</c>) on the grid its base computed from the evaluation date
/// adjusted on the at-the-money structure's calendar (<c>swaptionvolcube.cpp:37-42</c>); its first calculation
/// recomputes the grid from the at-the-money structure's reference date (<c>swaptionvoldiscrete.cpp:180-190</c>) and
/// never rebuilds the guesses, so where the two dates differ the fits start from guesses interpolated off their nodes.
/// With β fixed at guesses of 0.3, 0.5 and 0.7 by option tenor, on an at-the-money matrix fixed on Wednesday 18 June
/// 2025 and an evaluation date of Monday 16 June, the wheel's β at the 10-year option node is 0.4998783824870782.
/// nanoql builds the guesses on the at-the-money structure's grid, the cube's only one, so the node's β is its guess,
/// 0.5000000000000001 after the fit's transforms, as QuantLib's is when the structure is dated on the evaluation date.
/// </summary>
public class SabrCubeGuessGridTests
{
    [Fact]
    public void TheGuessesSitOnTheAtTheMoneyStructuresGrid()
    {
        double[][] rows = [.. new[] { 0.3, 0.5, 0.7 }.SelectMany(b => Enumerable.Repeat(new[] { 0.2, b, 0.4, 0.0 }, 3))];
        var cube = CubeFixture.Sabr(
            atm: CubeFixture.AtmMatrix(new Date(18, Month.June, 2025)), guesses: rows, fixedFlags: CubeFixture.BetaFixed,
            isAtmCalibrated: false);
        Numerics.Matrix sparse = cube.SparseSabrParameters();

        // A row per node, swap length by swap length, option time by option time: (2Y, 10Y) is the second.
        Assert.Equal(10.005479452054795, sparse[1, 1]);
        Assert.Equal(0.5000000000000001, sparse[1, 3]);
        Assert.Equal(0.2999999999999999, sparse[0, 3]);
        Assert.Equal(0.7, sparse[2, 3]);
    }
}
