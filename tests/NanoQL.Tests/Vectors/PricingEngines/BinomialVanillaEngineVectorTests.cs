// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BinomialVanillaEngine vectors, <c>tests/vectors/pricingengines/vanilla/binomialengine.json</c>: European and
/// American calls and puts on each of the seven trees, over four processes, their NPVs, deltas, gammas and thetas, an
/// expired option and the refusals; each tree read through its named engine.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BinomialVanillaEngineVectorTests
{
    private static readonly (string Name, Func<OneAssetOptionResults, double?> Get)[] Results =
    [
        ("NPV", r => r.NPV), ("delta", r => r.Delta), ("gamma", r => r.Gamma), ("theta", r => r.Theta),
    ];

    [Fact]
    public void BinomialVanillaEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/binomialengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            Exercise exercise = ExerciseOf(c.Input("exercise"), context);
            Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, int, OneAssetOptionResults> engine =
                Engine(c.String("tree"));
            int steps = c.Int("timeSteps");
            Lazy<OneAssetOptionResults>[] priced =
            [
                .. c.Input("payoffs").EnumerateArray().Select(p => new Lazy<OneAssetOptionResults>(
                    () => engine(new VanillaOption(PayoffOf(p), exercise), context, process, steps))),
            ];
            foreach ((string name, Func<OneAssetOptionResults, double?> get) in Results)
            {
                check.Output(c, name, priced.Select(r => Lazy(() => get(r.Value))).ToArray());
            }
        }

        check.Complete();
    }

    // QuantLib-Python's names for the engine's template arguments
    private static Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, int, OneAssetOptionResults> Engine(string tree) =>
        tree switch
        {
            "jr" => BinomialJRVanillaEngine.Calculate,
            "crr" => BinomialCRRVanillaEngine.Calculate,
            "eqp" => BinomialEQPVanillaEngine.Calculate,
            "trigeorgis" => BinomialTrigeorgisVanillaEngine.Calculate,
            "tian" => BinomialTianVanillaEngine.Calculate,
            "lr" => BinomialLRVanillaEngine.Calculate,
            "joshi4" => BinomialJ4VanillaEngine.Calculate,
            _ => throw new ArgumentException($"unknown tree {tree}", nameof(tree)),
        };
}
