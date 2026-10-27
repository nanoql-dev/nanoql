// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's pathwise engines vectors, <c>tests/vectors/cxx/models/marketmodels/pathwiseaccountingengine.json</c>:
/// the prices of <see cref="PathwiseAccountingEngine"/>, <see cref="PathwiseVegasAccountingEngine"/> and
/// <see cref="PathwiseVegasOuterAccountingEngine"/> over a few paths.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PathwiseAccountingEngineCxxVectorTests
{
    [Fact]
    public void PathwiseAccountingEngineCxxVector() =>
        MarketModelGreeksCxxInputs.CheckEngines("cxx/models/marketmodels/pathwiseaccountingengine");
}
