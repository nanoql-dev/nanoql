// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The AnalyticDividendEuropeanEngine vectors,
/// <c>tests/vectors/pricingengines/vanilla/analyticdividendeuropeanengine.json</c>: European options on four payoffs
/// over four processes and six dividend schedules, their NPVs, deltas, gammas, vegas, thetas and rhos, and the
/// refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AnalyticDividendEuropeanEngineVectorTests
{
    private static readonly (string Name, Func<OneAssetOptionResults, double?> Get)[] Results =
    [
        ("delta", r => r.Delta), ("gamma", r => r.Gamma), ("vega", r => r.Vega), ("theta", r => r.Theta),
        ("rho", r => r.Rho),
    ];

    [Fact]
    public void AnalyticDividendEuropeanEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/analyticdividendeuropeanengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            Exercise exercise = ExerciseOf(c.Input("exercise"), context);
            ImmutableArray<Dividend> dividends = DividendsOf(c.Input("dividends"));
            Lazy<OneAssetOptionResults>[] priced = c.Input("payoffs").EnumerateArray()
                .Select(p => new Lazy<OneAssetOptionResults>(() =>
                    AnalyticDividendEuropeanEngine.Calculate(new VanillaOption(PayoffOf(p), exercise), context, process, dividends)))
                .ToArray();
            check.Output(c, "NPV", priced.Select(r => Lazy(() => r.Value.NPV)).ToArray());
            foreach ((string name, Func<OneAssetOptionResults, double?> get) in Results)
            {
                if (c.Expected.TryGetProperty(name, out _))
                {
                    check.Output(c, name, priced.Select(r => Lazy(() => get(r.Value))).ToArray());
                }
            }
        }

        check.Complete();
    }
}
