// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's no-arbitrage SABR model vectors, <c>tests/vectors/cxx/experimental/volatility/noarbsabr.json</c>:
/// <see cref="NoArbSabrModel"/> at and near each parameter's bounds, its forwards, parameters, expiry and absorption
/// probability, its call prices, digital prices and density at strikes around the forward, and the interpolated
/// absorption probability (QuantLib's <c>detail::D0Interpolator</c>, internal here, reached by reflection); QuantLib's
/// refusals. The model as <c>tools/cxx/src/experimental/volatility/noarbsabr.cpp</c> builds it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NoArbSabrModelCxxVectorTests
{
    [Fact]
    public void NoArbSabrModelCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/volatility/noarbsabr");
        var check = new VectorAssert(file);
        Type d0Type = typeof(NoArbSabrModel).Assembly.GetType("NanoQL.Experimental.NoArbSabrD0Interpolator", throwOnError: true)!;
        MethodInfo d0Value = d0Type.GetMethod("Value", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (VectorCase c in file.Cases)
        {
            JsonElement s = c.Input("model");
            double t = s.GetProperty("expiryTime").GetDouble(), forward = s.GetProperty("forward").GetDouble();
            double alpha = s.GetProperty("alpha").GetDouble(), beta = s.GetProperty("beta").GetDouble();
            double nu = s.GetProperty("nu").GetDouble(), rho = s.GetProperty("rho").GetDouble();
            double[] strikes = Doubles(s.GetProperty("strikes"));
            var model = new Lazy<NoArbSabrModel>(() => new NoArbSabrModel(t, forward, alpha, beta, nu, rho));
            (string Key, Func<object?> Value)[] outputs =
            [
                ("forward", () => model.Value.Forward),
                ("numericalForward", () => model.Value.NumericalForward),
                ("expiryTime", () => model.Value.ExpiryTime),
                ("alpha", () => model.Value.Alpha),
                ("beta", () => model.Value.Beta),
                ("nu", () => model.Value.Nu),
                ("rho", () => model.Value.Rho),
                ("absorptionProbability", () => model.Value.AbsorptionProbability),
                ("optionPrice", () => strikes.Select(k => model.Value.OptionPrice(k)).ToArray()),
                ("digitalOptionPrice", () => strikes.Select(k => model.Value.DigitalOptionPrice(k)).ToArray()),
                ("density", () => strikes.Select(k => model.Value.Density(k)).ToArray()),
                ("d0", () =>
                {
                    object d = Activator.CreateInstance(
                        d0Type, BindingFlags.NonPublic | BindingFlags.Instance, null, [forward, t, alpha, beta, nu, rho], null)!;
                    return (double)d0Value.Invoke(d, null)!;
                }),
            ];
            foreach ((string key, Func<object?> value) in outputs)
            {
                if (c.Expected.TryGetProperty(key, out _))
                {
                    check.Output(c, key, value);
                }
            }
        }

        check.Complete();
    }
}
