// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The credit default swap vectors, <c>tests/vectors/instruments/creditdefaultswap.json</c>:
/// <see cref="CreditDefaultSwap"/>'s inspectors and mid-point results, its implied hazard rate and conventional spread
/// under both models (null where the stability filter dropped them), and
/// <see cref="CreditDefaultSwapFunctions.CdsMaturity"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CreditDefaultSwapVectorTests
{
    private static readonly (string Name, CreditDefaultSwapPricingModel Model)[] Models =
        [("Midpoint", CreditDefaultSwapPricingModel.Midpoint), ("ISDA", CreditDefaultSwapPricingModel.ISDA)];

    [Fact]
    public void CreditDefaultSwapVector()
    {
        var file = VectorFile.Load("instruments/creditdefaultswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (!c.Inputs.TryGetProperty("swap", out _))
            {
                int[] tenor = c.Ints("tenor");
                check.Output(
                    c,
                    "maturity",
                    () => CreditDefaultSwapFunctions.CdsMaturity(
                        c.DateInput("tradeDate"), new Period(tenor[0], (TimeUnit)tenor[1]), (DateGenerationRule)c.Int("rule")));
                continue;
            }

            CreditDefaultSwap cds = CreditInputs.SwapOf(c.Input("swap"), context);
            DefaultProbabilityTermStructure credit = CreditInputs.CurveOf(c.Input("credit"), context);
            YieldTermStructure discount = CreditInputs.DiscountOf(c.Input("discount"))!;
            double recovery = c.Double("recoveryRate");
            CreditInputs.CheckInspectors(check, c, cds, context);
            CreditInputs.CheckResults(check, c, MidPointCdsEngine.Calculate(cds, context, credit, recovery, discount));
            if (!c.Bool("implied"))
            {
                continue;
            }

            string[] dropped = [.. c.Input("dropped").EnumerateArray().Select(e => e.GetString()!)];
            double target = c.Double("target");
            foreach ((string name, CreditDefaultSwapPricingModel model) in Models)
            {
                string hazard = "impliedHazardRate" + name, spread = "conventionalSpread" + name;
                check.Output(
                    c,
                    hazard,
                    dropped.Contains(hazard)
                        ? null
                        : () => cds.ImpliedHazardRate(target, discount, new Actual365Fixed(), context, recovery, 1e-8, model));
                check.Output(
                    c,
                    spread,
                    dropped.Contains(spread)
                        ? null
                        : () => cds.ConventionalSpread(recovery, discount, new Actual365Fixed(), context, model));
            }
        }

        check.Complete();
    }
}
