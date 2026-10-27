// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's pseudo-root Jacobians vectors, <c>tests/vectors/cxx/models/marketmodels/pathwisegreeks/ratepseudorootjacobian.json</c>:
/// the numerical and analytic derivatives of an Euler step's rates along pseudo-root bumps.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RatePseudoRootJacobianCxxVectorTests
{
    [Fact]
    public void RatePseudoRootJacobianCxxVector() =>
        MarketModelGreeksCxxInputs.CheckJacobians("cxx/models/marketmodels/pathwisegreeks/ratepseudorootjacobian");
}
