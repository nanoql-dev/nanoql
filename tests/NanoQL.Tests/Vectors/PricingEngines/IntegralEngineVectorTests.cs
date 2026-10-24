// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The IntegralEngine vectors, <c>tests/vectors/pricingengines/vanilla/integralengine.json</c>: the NPVs of European
/// options on four payoffs over six processes, an expired option, a percentage-strike payoff and the refusal of an
/// American exercise.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IntegralEngineVectorTests
{
    [Fact]
    public void IntegralEngineVector() =>
        OptionResultsVectors.Check(
            "pricingengines/vanilla/integralengine",
            (_, option, context, process) => IntegralEngine.Calculate(option, context, process));
}
