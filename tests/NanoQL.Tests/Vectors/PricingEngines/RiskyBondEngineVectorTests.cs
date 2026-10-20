// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The risky bond engine vectors, <c>tests/vectors/pricingengines/bond/riskybondengine.json</c>:
/// <see cref="RiskyBondEngine"/>'s NPV, settlement value and valuation date for four bonds around a payment date, on a
/// flat hazard rate and a hazard-rate curve at two recoveries, and the prices the bond reads from them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RiskyBondEngineVectorTests
{
    [Fact]
    public void RiskyBondEngineVector()
    {
        var file = VectorFile.Load("pricingengines/bond/riskybondengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Bond bond = BondInputs.BondOf(c.Input("bond"), context);
            FlatForward curve = CashFlowInputs.CurveOf(c.Input("curve"));
            DefaultProbabilityTermStructure credit = CreditInputs.CurveOf(c.Input("credit"), context);
            BondResults results = RiskyBondEngine.Calculate(bond, context, credit, c.Double("recoveryRate"), curve);
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "settlementValue", results.SettlementValue);
            check.Output(c, "valuationDate", results.ValuationDate);
            check.Output(c, "cleanPrice", () => bond.CleanPrice(results, context));
            check.Output(c, "dirtyPrice", () => bond.DirtyPrice(results, context));
        }

        check.Complete();
    }
}
