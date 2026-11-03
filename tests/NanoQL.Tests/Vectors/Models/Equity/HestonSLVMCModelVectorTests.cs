// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The HestonSLVMCModel vectors, <c>tests/vectors/models/equity/hestonslvmcmodel.json</c>: the Monte Carlo calibrated
/// leverage function on a constant local volatility with the Mersenne-Twister and Sobol factories, on a smile constant
/// in time, at mixing factor 0.5, with a mandatory date and on zero curves, read between the time grid's nodes and at
/// two nodes, the points stable under one-ulp moves of the inputs.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HestonSLVMCModelVectorTests
{
    [Fact]
    public void HestonSLVMCModelVector()
    {
        var file = VectorFile.Load("models/equity/hestonslvmcmodel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("model");
            var model = new HestonSLVMCModel(
                FdHestonVanillaEngineVectorTests.LeverageOf(spec.GetProperty("localVol"))!,
                new HestonModel(HestonProcessVectorTests.HestonProcessOf(spec.GetProperty("heston"))),
                FactoryOf(spec.GetProperty("generator")),
                new Date(spec.GetProperty("endDate").GetInt32()),
                spec.GetProperty("timeStepsPerYear").GetInt32(),
                spec.GetProperty("nBins").GetInt32(),
                spec.GetProperty("calibrationPaths").GetInt32(),
                [.. spec.GetProperty("mandatoryDates").EnumerateArray().Select(d => new Date(d.GetInt32()))],
                spec.GetProperty("mixingFactor").GetDouble());
            check.Output(
                c,
                "leverage",
                c.Input("points").EnumerateArray()
                    .Select(p => (object?)model.LeverageFunction.LocalVol(p[0].GetDouble(), p[1].GetDouble(), true)).ToArray());
        }

        check.Complete();
    }

    private static BrownianGeneratorFactory FactoryOf(JsonElement spec) =>
        spec.GetProperty("type").GetString() == "MT"
            ? new MTBrownianGeneratorFactory(spec.GetProperty("seed").GetInt64())
            : new SobolBrownianGeneratorFactory(
                (SobolBrownianGeneratorBaseOrdering)spec.GetProperty("ordering").GetInt32(),
                spec.GetProperty("seed").GetInt64(),
                (SobolRsgDirectionIntegers)spec.GetProperty("directionIntegers").GetInt32());
}
