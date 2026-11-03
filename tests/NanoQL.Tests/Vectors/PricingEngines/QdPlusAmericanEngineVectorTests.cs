// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The QdPlusAmericanEngine vectors, <c>tests/vectors/pricingengines/vanilla/qdplusamericanengine.json</c>: the NPVs of
/// American calls and puts over the vanilla engines' grid, priced by the engine's defaults and six other settings of
/// its nodes, solver, accuracy and evaluation cap, and the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class QdPlusAmericanEngineVectorTests
{
    [Fact]
    public void QdPlusAmericanEngineVector() =>
        OptionResultsVectors.Check(
            "pricingengines/vanilla/qdplusamericanengine",
            (c, option, context, process) =>
            {
                JsonElement spec = c.Input("engine");
                JsonElement maxIter = spec.GetProperty("maxIter");
                return QdPlusAmericanEngine.Calculate(
                    option,
                    context,
                    process,
                    spec.GetProperty("interpolationPoints").GetInt32(),
                    (QdPlusAmericanEngineSolverType)spec.GetProperty("solverType").GetInt32(),
                    spec.GetProperty("eps").GetDouble(),
                    maxIter.ValueKind == JsonValueKind.Null ? null : maxIter.GetInt32());
            });
}
