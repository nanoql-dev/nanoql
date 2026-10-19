// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A cube's settlement days are its at-the-money structure's, as QuantLib's cube forwards them
/// (<c>swaptionvolcube.hpp:56</c>), which the cube passes to its base with the other conventions: 0 on the floating
/// matrix, and null, nanoql's "settlement days not provided", on one with a fixed reference date. The reference date
/// follows the structure too. The wheel binds <c>settlementDays</c> on no swaption structure, so no vector holds them.
/// </summary>
public class CubeSettlementDaysTests
{
    [Fact]
    public void ACubesSettlementDaysAreItsStructures()
    {
        var floating = CubeFixture.AtmMatrix();
        Assert.Equal(0, floating.SettlementDays);
        foreach (SwaptionVolatilityCube cube in new SwaptionVolatilityCube[]
                 {
                     CubeFixture.Sabr(floating, isAtmCalibrated: false), CubeFixture.Interpolated(floating),
                 })
        {
            Assert.Equal(0, cube.SettlementDays);
            Assert.Equal(floating.ReferenceDate, cube.ReferenceDate);
        }

        var fixedDate = new Date(18, Month.June, 2025);
        var fixedMatrix = CubeFixture.AtmMatrix(fixedDate);
        Assert.Null(fixedMatrix.SettlementDays);
        foreach (SwaptionVolatilityCube cube in new SwaptionVolatilityCube[]
                 {
                     CubeFixture.Sabr(fixedMatrix, isAtmCalibrated: false), CubeFixture.Interpolated(fixedMatrix),
                 })
        {
            Assert.Null(cube.SettlementDays);
            Assert.Equal(fixedDate, cube.ReferenceDate);
        }
    }
}
