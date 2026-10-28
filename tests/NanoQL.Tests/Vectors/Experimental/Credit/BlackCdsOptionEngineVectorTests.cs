// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Black CDS-option engine's vectors, <c>tests/vectors/experimental/credit/blackcdsoptionengine.json</c>: payer
/// and receiver options, knocking out or not, at and away from the money, on four volatilities and two markets, and an
/// expired option: <see cref="BlackCdsOptionEngine"/>'s value and risky annuity, the swap priced by the mid-point
/// engine on the same curves.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackCdsOptionEngineVectorTests
{
    [Fact]
    public void BlackCdsOptionEngineVector()
    {
        var file = VectorFile.Load("experimental/credit/blackcdsoptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CreditDefaultSwap swap = CreditInputs.SwapOf(c.Input("swap"), context);
            DefaultProbabilityTermStructure credit = CreditInputs.CurveOf(c.Input("credit"), context);
            YieldTermStructure discount = CreditInputs.DiscountOf(c.Input("discount"))!;
            double recovery = c.Double("recoveryRate");
            var option = new CdsOption(swap, new EuropeanExercise(c.DateInput("exercise")), c.Bool("knocksOut"));

            CreditDefaultSwapResults SwapEngine(CreditDefaultSwap s, EvaluationContext ctx) =>
                MidPointCdsEngine.Calculate(s, ctx, credit, recovery, discount);

            CdsOptionResults Results() =>
                BlackCdsOptionEngine.Calculate(option, context, credit, recovery, discount, c.Double("volatility"), SwapEngine);

            check.Output(c, "NPV", () => Results().NPV);
            check.Output(c, "riskyAnnuity", () => Results().RiskyAnnuity);
        }

        check.Complete();
    }
}
