// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The dense parameters of a SABR cube that is neither at-the-money-calibrated nor calibrated in a single pass, which
/// has none. QuantLib's <c>denseSabrParameters()</c> browses a default-constructed <c>Cube</c>, whose layer count is
/// uninitialized (<c>sabrswaptionvolatilitycube.hpp:134, 416-424, 1045-1048, 1408</c>); the wheel returned a 0 × 2
/// matrix on QuantLib's 3 × 3 cube, a shape read from an uninitialized member. nanoql refuses it with
/// <see cref="NanoQLException"/>.
/// </summary>
public class SabrCubeDenseParametersTests
{
    [Fact]
    public void ACubeWithoutDenseParametersRefusesThem()
    {
        var cube = CubeFixture.Sabr(isAtmCalibrated: false);
        NanoQLException e = Assert.Throws<NanoQLException>(() => cube.DenseSabrParameters());
        Assert.Equal("no dense parameters: the cube is neither ATM-calibrated nor calibrated in a single pass", e.Message);
        Assert.Equal(9, cube.SparseSabrParameters().Rows);
    }
}
