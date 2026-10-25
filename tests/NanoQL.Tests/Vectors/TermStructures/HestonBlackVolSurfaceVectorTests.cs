// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The HestonBlackVolSurface vectors, <c>tests/vectors/termstructures/volatility/equityfx/hestonblackvolsurface.json</c>:
/// two Heston models on flat and zero curves by three forms of the analytic engine, the Black volatility and variance at
/// three times and five strikes, the at-the-money level and the reference date.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HestonBlackVolSurfaceVectorTests
{
    [Fact]
    public void HestonBlackVolSurfaceVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/hestonblackvolsurface");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var model = new HestonModel(HestonProcessVectorTests.HestonProcessOf(c.Input("process")));
            JsonElement spec = c.Input("surface");
            var surface = new HestonBlackVolSurface(
                model,
                (AnalyticHestonEngineComplexLogFormula)spec.GetProperty("cpxLog").GetInt32(),
                FourierIntegration.GaussLaguerre(spec.GetProperty("order").GetInt32()));
            double[] times = Doubles(c.Input("times"));
            double[] strikes = Doubles(c.Input("strikes"));
            check.Output(c, "blackVol", times.SelectMany(t => strikes.Select(k => Lazy(() => surface.BlackVol(t, k)))).ToArray());
            check.Output(
                c, "blackVariance", times.SelectMany(t => strikes.Select(k => Lazy(() => surface.BlackVariance(t, k)))).ToArray());
            check.Output(c, "atmLevel", times.Select(t => (object?)surface.AtmLevel(t)).ToArray());
            check.Output(c, "referenceDate", surface.ReferenceDate);
        }

        check.Complete();
    }
}
