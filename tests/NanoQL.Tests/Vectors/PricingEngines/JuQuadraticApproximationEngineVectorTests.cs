// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The JuQuadraticApproximationEngine vectors, <c>tests/vectors/pricingengines/vanilla/juquadraticengine.json</c>:
/// American calls and puts priced by Ju's quadratic approximation over S-van's grid, their NPVs and Greeks, and the
/// refusals; the outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class JuQuadraticApproximationEngineVectorTests
{
    [Fact]
    public void JuQuadraticApproximationEngineVector() =>
        OptionResultsVectors.Check(
            "pricingengines/vanilla/juquadraticengine",
            (_, option, context, process) => JuQuadraticApproximationEngine.Calculate(option, context, process));
}
