// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BaroneAdesiWhaleyApproximationEngine vectors,
/// <c>tests/vectors/pricingengines/vanilla/baroneadesiwhaleyengine.json</c>: American calls and puts priced by the
/// Barone-Adesi-Whaley approximation over S-van's grid, their NPVs and Greeks, and the refusals; the outputs nanoql
/// fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BaroneAdesiWhaleyApproximationEngineVectorTests
{
    [Fact]
    public void BaroneAdesiWhaleyApproximationEngineVector() =>
        OptionResultsVectors.Check(
            "pricingengines/vanilla/baroneadesiwhaleyengine",
            (_, option, context, process) => BaroneAdesiWhaleyApproximationEngine.Calculate(option, context, process));
}
