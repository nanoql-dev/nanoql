// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticDigitalAmericanEngine vectors, <c>tests/vectors/pricingengines/vanilla/analyticdigitalamericanengine.json</c>:
/// American cash-or-nothing and asset-or-nothing options at the hit (knock-in: the NPV, delta, gamma and rho) and at
/// expiry (knock-in and knock-out: the NPV), and the refusals; the outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticDigitalAmericanEngineVectorTests
{
    private static readonly (string Name, Func<OneAssetOptionResults, double?> Get)[] Results =
    [
        ("NPV", r => r.NPV), ("delta", r => r.Delta), ("gamma", r => r.Gamma), ("rho", r => r.Rho),
    ];

    [Fact]
    public void AnalyticDigitalAmericanEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/analyticdigitalamericanengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            Exercise exercise = ExerciseOf(c.Input("exercise"), context);
            bool knockIn = c.Bool("knockIn");
            Func<OneAssetOptionResults>[] priced = c.Input("payoffs").EnumerateArray()
                .Select(p =>
                {
                    var option = new VanillaOption(PayoffOf(p), exercise);
                    var results = new Lazy<OneAssetOptionResults>(() => knockIn
                        ? AnalyticDigitalAmericanEngine.Calculate(option, context, process)
                        : AnalyticDigitalAmericanKOEngine.Calculate(option, context, process));
                    return (Func<OneAssetOptionResults>)(() => results.Value);
                })
                .ToArray();
            foreach ((string name, Func<OneAssetOptionResults, double?> get) in Results)
            {
                if (c.Expected.TryGetProperty(name, out _))
                {
                    check.Output(c, name, priced.Select(f => Lazy(() => get(f()))).ToArray());
                }
            }
        }

        check.Complete();
    }
}
