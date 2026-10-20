// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticEuropeanEngine vectors, <c>tests/vectors/pricingengines/vanilla/analyticeuropeanengine.json</c>:
/// European options on four payoffs over eight processes, with and without a discount curve, their NPVs and Greeks,
/// expired options and the refusals; the outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticEuropeanEngineVectorTests
{
    private static readonly (string Name, Func<OneAssetOptionResults, double?> Get)[] Results =
    [
        ("delta", r => r.Delta), ("deltaForward", r => r.DeltaForward), ("elasticity", r => r.Elasticity),
        ("gamma", r => r.Gamma), ("theta", r => r.Theta), ("thetaPerDay", r => r.ThetaPerDay), ("vega", r => r.Vega),
        ("rho", r => r.Rho), ("dividendRho", r => r.DividendRho), ("strikeSensitivity", r => r.StrikeSensitivity),
        ("itmCashProbability", r => r.ItmCashProbability),
    ];

    [Fact]
    public void AnalyticEuropeanEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/analyticeuropeanengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            Exercise exercise = ExerciseOf(c.Input("exercise"), context);
            Func<OneAssetOptionResults>[] priced = c.Input("payoffs").EnumerateArray()
                .Select(p => Price(new VanillaOption(PayoffOf(p), exercise), context, process, c))
                .ToArray();
            check.Output(c, "NPV", priced.Select(f => Lazy(() => f().NPV)).ToArray());
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

    private static Func<OneAssetOptionResults> Price(
        VanillaOption option, EvaluationContext context, GeneralizedBlackScholesProcess process, VectorCase c)
    {
        var results = new Lazy<OneAssetOptionResults>(() => c.Inputs.TryGetProperty("discountCurve", out JsonElement d)
            ? AnalyticEuropeanEngine.Calculate(option, context, process, CurveOf(d))
            : AnalyticEuropeanEngine.Calculate(option, context, process));
        return () => results.Value;
    }
}
