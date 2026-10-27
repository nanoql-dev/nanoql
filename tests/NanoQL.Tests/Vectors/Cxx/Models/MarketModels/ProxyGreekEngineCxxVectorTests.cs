// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's proxy Greek engine vectors, <c>tests/vectors/cxx/models/marketmodels/proxygreekengine.json</c>:
/// <see cref="ProxyGreekEngine"/>'s values and Greeks over a few paths, QuantLib's weight.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ProxyGreekEngineCxxVectorTests
{
    [Fact]
    public void ProxyGreekEngineCxxVector() =>
        MarketModelGreeksCxxInputs.CheckProxy("cxx/models/marketmodels/proxygreekengine");
}
