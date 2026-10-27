// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's bump instrument Jacobians vectors, <c>tests/vectors/cxx/models/marketmodels/pathwisegreeks/bumpinstrumentjacobian.json</c>:
/// <see cref="VolatilityBumpInstrumentJacobian"/>'s derivatives and one-percent bumps, and
/// <see cref="OrthogonalizedBumpFinder"/>'s bumps.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BumpInstrumentJacobianCxxVectorTests
{
    [Fact]
    public void BumpInstrumentJacobianCxxVector() =>
        MarketModelGreeksCxxInputs.CheckBumpInstruments("cxx/models/marketmodels/pathwisegreeks/bumpinstrumentjacobian");
}
