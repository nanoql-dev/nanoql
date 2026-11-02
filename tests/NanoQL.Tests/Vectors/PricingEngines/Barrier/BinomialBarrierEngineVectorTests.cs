// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BarrierInputs;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BinomialBarrierEngine vectors, <c>tests/vectors/pricingengines/barrier/binomialbarrierengine.json</c>: the four
/// barrier types with and without a rebate on the seven trees with Derman and Kani's adjustment, on four processes,
/// calls and puts at three strikes: the NPV, delta, gamma and theta; an expired option and the refusals. The outputs
/// nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BinomialBarrierEngineVectorTests
{
    private static readonly (string Name, Func<OneAssetOptionResults, double?> Get)[] Greeks =
    [
        ("delta", r => r.Delta), ("gamma", r => r.Gamma), ("theta", r => r.Theta),
    ];

    [Fact]
    public void BinomialBarrierEngineVector()
    {
        var file = VectorFile.Load("pricingengines/barrier/binomialbarrierengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            Func<BarrierOption, EvaluationContext, GeneralizedBlackScholesProcess, int, int, OneAssetOptionResults> engine =
                Engine(c.String("tree"));
            int steps = c.Int("steps");
            Lazy<OneAssetOptionResults>[] priced =
            [
                .. c.Input("payoffs").EnumerateArray().Select(p => new Lazy<OneAssetOptionResults>(
                    () => engine(OptionOf(c.Input("barrier"), p, c.Input("exercise"), context), context, process, steps, 0))),
            ];
            check.Output(c, "NPV", priced.Select(r => Lazy(() => r.Value.NPV)).ToArray());
            foreach ((string name, Func<OneAssetOptionResults, double?> get) in Greeks)
            {
                if (c.Expected.TryGetProperty(name, out _))
                {
                    check.Output(c, name, priced.Select(r => Lazy(() => get(r.Value))).ToArray());
                }
            }
        }

        check.Complete();
    }

    // QuantLib-Python's names for the engine's tree, each on QuantLib-SWIG's asset, Derman and Kani's
    private static Func<BarrierOption, EvaluationContext, GeneralizedBlackScholesProcess, int, int, OneAssetOptionResults> Engine(
        string tree) =>
        tree switch
        {
            "crr" => BinomialCRRBarrierEngine.Calculate,
            "jr" => BinomialJRBarrierEngine.Calculate,
            "eqp" => BinomialEQPBarrierEngine.Calculate,
            "trigeorgis" => BinomialTrigeorgisBarrierEngine.Calculate,
            "tian" => BinomialTianBarrierEngine.Calculate,
            "lr" => BinomialLRBarrierEngine.Calculate,
            "j4" => BinomialJ4BarrierEngine.Calculate,
            _ => throw new ArgumentException($"unknown tree {tree}", nameof(tree)),
        };
}
