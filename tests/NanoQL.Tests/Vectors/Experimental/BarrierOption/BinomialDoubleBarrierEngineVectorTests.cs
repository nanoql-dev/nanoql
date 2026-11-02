// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BarrierInputs;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BinomialDoubleBarrierEngine vectors on the Derman-Kani asset,
/// <c>tests/vectors/experimental/barrieroption/binomialdoublebarrierengine.json</c>: knock-ins and knock-outs on two
/// corridors, European and American, on QuantLib-SWIG's seven trees at 51 and 101 steps, their NPV, delta, gamma and
/// theta; an expired option and a refused KIKO. The outputs nanoql fixes are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BinomialDoubleBarrierEngineVectorTests
{
    private static readonly string[] Outputs = ["NPV", "delta", "gamma", "theta"];

    [Fact]
    public void BinomialDoubleBarrierEngineVector()
    {
        var file = VectorFile.Load("experimental/barrieroption/binomialdoublebarrierengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess process = ProcessOf(c.Input("process"), context);
            int steps = c.Int("steps");
            string tree = c.String("tree");
            Lazy<OneAssetOptionResults>[] priced = [.. c.Input("payoffs").EnumerateArray().Select(p =>
                new Lazy<OneAssetOptionResults>(() => Price(
                    tree, DoubleOptionOf(c.Input("barrier"), p, c.Input("exercise"), context), context, process, steps)))];
            foreach (string output in Outputs)
            {
                check.Output(c, output, priced.Select(r => Lazy(() => output switch
                {
                    "NPV" => r.Value.NPV,
                    "delta" => r.Value.Delta,
                    "gamma" => r.Value.Gamma,
                    _ => r.Value.Theta,
                })).ToArray());
            }
        }

        check.Complete();
    }

    // QuantLib-SWIG's Binomial<tree>DoubleBarrierEngine, each on the Derman-Kani asset
    private static OneAssetOptionResults Price(
        string tree, DoubleBarrierOption option, EvaluationContext context, GeneralizedBlackScholesProcess process, int steps) =>
        tree switch
        {
            "crr" => BinomialCRRDoubleBarrierEngine.Calculate(
                option, context, process, steps),
            "jr" => BinomialJRDoubleBarrierEngine.Calculate(
                option, context, process, steps),
            "eqp" => BinomialEQPDoubleBarrierEngine.Calculate(
                option, context, process, steps),
            "trigeorgis" => BinomialTrigeorgisDoubleBarrierEngine.Calculate(
                option, context, process, steps),
            "tian" => BinomialTianDoubleBarrierEngine.Calculate(
                option, context, process, steps),
            "lr" => BinomialLRDoubleBarrierEngine.Calculate(
                option, context, process, steps),
            "j4" => BinomialJ4DoubleBarrierEngine.Calculate(
                option, context, process, steps),
            _ => throw new ArgumentException($"unknown tree {tree}", nameof(tree)),
        };
}
