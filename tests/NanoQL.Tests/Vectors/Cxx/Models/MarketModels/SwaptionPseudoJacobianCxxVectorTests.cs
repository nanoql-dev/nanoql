// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's pseudo-derivatives vectors, <c>tests/vectors/cxx/models/marketmodels/pathwisegreeks/swaptionpseudojacobian.json</c>:
/// <see cref="SwaptionPseudoDerivative"/>'s and <see cref="CapPseudoDerivative"/>'s volatilities and
/// derivatives.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwaptionPseudoJacobianCxxVectorTests
{
    [Fact]
    public void SwaptionPseudoJacobianCxxVector() =>
        MarketModelGreeksCxxInputs.CheckPseudoDerivatives("cxx/models/marketmodels/pathwisegreeks/swaptionpseudojacobian");
}
