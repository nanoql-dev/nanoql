// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CDS option's vectors, <c>tests/vectors/experimental/credit/cdsoption.json</c>: <see cref="CdsOption"/>'s expiry,
/// at-the-money rate and implied volatility (null where the stability filter dropped it), and its constructor's two
/// refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CdsOptionVectorTests
{
    [Fact]
    public void CdsOptionVector()
    {
        var file = VectorFile.Load("experimental/credit/cdsoption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CreditDefaultSwap swap = CreditInputs.SwapOf(c.Input("swap"), context);
            var exercise = new EuropeanExercise(c.DateInput("exercise"));
            if (!c.Inputs.TryGetProperty("credit", out _))
            {
                check.Output(c, "construction", () => new CdsOption(swap, exercise, c.Bool("knocksOut")).IsExpired(context));
                continue;
            }

            DefaultProbabilityTermStructure credit = CreditInputs.CurveOf(c.Input("credit"), context);
            YieldTermStructure discount = CreditInputs.DiscountOf(c.Input("discount"))!;
            double recovery = c.Double("recoveryRate");
            var option = new CdsOption(swap, exercise, c.Bool("knocksOut"));

            CreditDefaultSwapResults SwapEngine(CreditDefaultSwap s, EvaluationContext ctx) =>
                MidPointCdsEngine.Calculate(s, ctx, credit, recovery, discount);

            check.Output(c, "isExpired", option.IsExpired(context));
            check.Output(c, "atmRate", () => option.AtmRate(context, SwapEngine));
            double[] targets = [.. c.Input("targets").EnumerateArray().Select(e => e.GetDouble())];
            int[] dropped = c.Ints("dropped");
            check.Output(
                c,
                "impliedVolatility",
                targets.Select((t, i) => dropped.Contains(i)
                    ? (double?)null
                    : option.ImpliedVolatility(t, context, discount, credit, recovery, SwapEngine)).ToArray());
        }

        check.Complete();
    }
}
