// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Longstaff-Schwartz regression vectors,
/// <c>tests/vectors/cxx/methods/montecarlo/genericlsregression.json</c>:
/// <see cref="GenericLSRegressionFunctions.GenericLongstaffSchwartzRegression"/> on collected
/// node data, its estimate, coefficients and first row's cash flows.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GenericLSRegressionCxxVectorTests
{
    [Fact]
    public void GenericLSRegressionCxxVector() =>
        MarketModelCallabilityCxxInputs.CheckRegressionOrOptimization("cxx/methods/montecarlo/genericlsregression");
}
