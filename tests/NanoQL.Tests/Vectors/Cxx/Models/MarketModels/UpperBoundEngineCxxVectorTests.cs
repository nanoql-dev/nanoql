// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's upper-bound engine vectors,
/// <c>tests/vectors/cxx/models/marketmodels/callability/upperboundengine.json</c>:
/// <see cref="UpperBoundEngine"/>'s values over a few outer and inner paths.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class UpperBoundEngineCxxVectorTests
{
    [Fact]
    public void UpperBoundEngineCxxVector() =>
        MarketModelCallabilityCxxInputs.CheckUpperBound("cxx/models/marketmodels/callability/upperboundengine");
}
