// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Bates engine vectors, <c>tests/vectors/cxx/pricingengines/vanilla/batesengine.json</c>: the Bates,
/// det-jump and double-exponential engines on two processes each, their models at default and other arguments, under
/// the Gauss-Laguerre and Gauss-Lobatto constructors: the NPVs and numbers of evaluations of calls and puts at three
/// strikes and maturities, each priced through the engine object and through its type's static <c>Calculate</c>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BatesEngineCxxVectorTests
{
    [Fact]
    public void BatesEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/vanilla/batesengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            AnalyticHestonEngine engine = EngineOf(c);
            double[] strikes = Doubles(c.Input("strikes"));
            var results = new List<Lazy<OneAssetOptionResults>>();
            foreach (int days in c.Ints("maturities"))
            {
                var exercise = new EuropeanExercise(context.EvaluationDate + days);
                foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
                {
                    foreach (double k in strikes)
                    {
                        var option = new VanillaOption(new PlainVanillaPayoff(type, k), exercise);
                        results.Add(new Lazy<OneAssetOptionResults>(() =>
                        {
                            OneAssetOptionResults r = engine.Calculate(option, context);
                            Assert.Equal(r.NPV, Calculated(c, option, context).NPV);
                            return r;
                        }));
                    }
                }
            }

            check.Output(c, "NPV", results.Select(r => Lazy(() => r.Value.NPV)).ToArray());
            check.Output(c, "numberOfEvaluations", results.Select(r => Lazy(() => r.Value.Result<int>("numberOfEvaluations"))).ToArray());
        }

        check.Complete();
    }

    // The case's option priced through its engine type's static Calculate, on the same model and integration.
    private static OneAssetOptionResults Calculated(VectorCase c, VanillaOption option, EvaluationContext context)
    {
        double[] a = Doubles(c.Input("arguments"));
        double Arg(int i, double fallback) => i < a.Length ? a[i] : fallback;
        JsonElement process = c.Input("process");
        JsonElement integration = c.Input("integration");
        bool laguerre = integration.TryGetProperty("order", out JsonElement order);
        double relTolerance = laguerre ? 0.0 : integration.GetProperty("relTolerance").GetDouble();
        int maxEvaluations = laguerre ? 0 : integration.GetProperty("maxEvaluations").GetInt32();
        switch (c.Input("type").GetString())
        {
            case "BatesEngine":
                var bates = new BatesModel(HestonCxxInputs.BatesProcessOf(process));
                return laguerre
                    ? BatesEngine.Calculate(option, context, bates, order.GetInt32())
                    : BatesEngine.Calculate(option, context, bates, relTolerance, maxEvaluations);
            case "BatesDetJumpEngine":
                var detJump = new BatesDetJumpModel(HestonCxxInputs.BatesProcessOf(process), Arg(0, 1.0), Arg(1, 0.1));
                return laguerre
                    ? BatesDetJumpEngine.Calculate(option, context, detJump, order.GetInt32())
                    : BatesDetJumpEngine.Calculate(option, context, detJump, relTolerance, maxEvaluations);
            case "BatesDoubleExpEngine":
                var doubleExp = new BatesDoubleExpModel(HestonCxxInputs.ProcessOf(process), Arg(0, 0.1), Arg(1, 0.1), Arg(2, 0.1), Arg(3, 0.5));
                return laguerre
                    ? BatesDoubleExpEngine.Calculate(option, context, doubleExp, order.GetInt32())
                    : BatesDoubleExpEngine.Calculate(option, context, doubleExp, relTolerance, maxEvaluations);
            default:
                var both = new BatesDoubleExpDetJumpModel(
                    HestonCxxInputs.ProcessOf(process), Arg(0, 0.1), Arg(1, 0.1), Arg(2, 0.1), Arg(3, 0.5), Arg(4, 1.0), Arg(5, 0.1));
                return laguerre
                    ? BatesDoubleExpDetJumpEngine.Calculate(option, context, both, order.GetInt32())
                    : BatesDoubleExpDetJumpEngine.Calculate(option, context, both, relTolerance, maxEvaluations);
        }
    }

    // The engine a case describes: its type on the model the process and the arguments make, by the constructor its
    // integration names.
    private static AnalyticHestonEngine EngineOf(VectorCase c)
    {
        double[] a = Doubles(c.Input("arguments"));
        double Arg(int i, double fallback) => i < a.Length ? a[i] : fallback;
        JsonElement process = c.Input("process");
        JsonElement integration = c.Input("integration");
        bool laguerre = integration.TryGetProperty("order", out JsonElement order);
        double relTolerance = laguerre ? 0.0 : integration.GetProperty("relTolerance").GetDouble();
        int maxEvaluations = laguerre ? 0 : integration.GetProperty("maxEvaluations").GetInt32();
        switch (c.Input("type").GetString())
        {
            case "BatesEngine":
                var bates = new BatesModel(HestonCxxInputs.BatesProcessOf(process));
                return laguerre ? new BatesEngine(bates, order.GetInt32()) : new BatesEngine(bates, relTolerance, maxEvaluations);
            case "BatesDetJumpEngine":
                var detJump = new BatesDetJumpModel(HestonCxxInputs.BatesProcessOf(process), Arg(0, 1.0), Arg(1, 0.1));
                return laguerre
                    ? new BatesDetJumpEngine(detJump, order.GetInt32())
                    : new BatesDetJumpEngine(detJump, relTolerance, maxEvaluations);
            case "BatesDoubleExpEngine":
                var doubleExp = new BatesDoubleExpModel(HestonCxxInputs.ProcessOf(process), Arg(0, 0.1), Arg(1, 0.1), Arg(2, 0.1), Arg(3, 0.5));
                return laguerre
                    ? new BatesDoubleExpEngine(doubleExp, order.GetInt32())
                    : new BatesDoubleExpEngine(doubleExp, relTolerance, maxEvaluations);
            default:
                var both = new BatesDoubleExpDetJumpModel(
                    HestonCxxInputs.ProcessOf(process), Arg(0, 0.1), Arg(1, 0.1), Arg(2, 0.1), Arg(3, 0.5), Arg(4, 1.0), Arg(5, 0.1));
                return laguerre
                    ? new BatesDoubleExpDetJumpEngine(both, order.GetInt32())
                    : new BatesDoubleExpDetJumpEngine(both, relTolerance, maxEvaluations);
        }
    }
}
