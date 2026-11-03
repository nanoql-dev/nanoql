// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BjerksundStenslandApproximationEngine vectors,
/// <c>tests/vectors/pricingengines/vanilla/bjerksundstenslandengine.json</c>: American calls and puts priced by the
/// Bjerksund-Stensland approximation over the vanilla engines' grid, their NPVs and Greeks, and the refusals; the
/// outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BjerksundStenslandApproximationEngineVectorTests
{
    [Fact]
    public void BjerksundStenslandApproximationEngineVector() =>
        OptionResultsVectors.Check(
            "pricingengines/vanilla/bjerksundstenslandengine",
            (_, option, context, process) => BjerksundStenslandApproximationEngine.Calculate(option, context, process));
}
