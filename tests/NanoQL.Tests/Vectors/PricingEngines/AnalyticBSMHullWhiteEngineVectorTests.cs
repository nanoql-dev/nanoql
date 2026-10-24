// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticBSMHullWhiteEngine vectors, <c>tests/vectors/pricingengines/vanilla/analyticbsmhullwhiteengine.json</c>:
/// European calls and puts over four processes, three Hull-White models on the process's risk-free curve and three
/// correlations, their NPVs and Greeks, an expired option and the refusal of an American exercise; the outputs nanoql
/// fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticBSMHullWhiteEngineVectorTests
{
    [Fact]
    public void AnalyticBSMHullWhiteEngineVector() =>
        OptionResultsVectors.Check(
            "pricingengines/vanilla/analyticbsmhullwhiteengine",
            (c, option, context, process) =>
            {
                JsonElement model = c.Input("model");
                return AnalyticBSMHullWhiteEngine.Calculate(
                    option,
                    context,
                    c.Double("correlation"),
                    process,
                    new HullWhite(
                        process.RiskFreeRate, model.GetProperty("a").GetDouble(), model.GetProperty("sigma").GetDouble()));
            });
}
