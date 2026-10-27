// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's vega bump clusters vectors, <c>tests/vectors/cxx/models/marketmodels/pathwisegreeks/vegabumpcluster.json</c>:
/// <see cref="VegaBumpCluster"/>'s checks and <see cref="VegaBumpCollection"/>'s default clusters.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class VegaBumpClusterCxxVectorTests
{
    [Fact]
    public void VegaBumpClusterCxxVector() =>
        MarketModelGreeksCxxInputs.CheckBumpClusters("cxx/models/marketmodels/pathwisegreeks/vegabumpcluster");
}
