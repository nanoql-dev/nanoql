// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's node-data collection vectors,
/// <c>tests/vectors/cxx/models/marketmodels/callability/collectnodedata.json</c>:
/// <see cref="CollectNodeDataFunctions.CollectNodeData"/>'s node data over a few paths.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CollectNodeDataCxxVectorTests
{
    [Fact]
    public void CollectNodeDataCxxVector() =>
        MarketModelCallabilityCxxInputs.CheckCollect("cxx/models/marketmodels/callability/collectnodedata");
}
