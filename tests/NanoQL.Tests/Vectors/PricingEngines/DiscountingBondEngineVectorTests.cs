// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The discounting bond engine vectors, <c>tests/vectors/pricingengines/bond/discountingbondengine.json</c>: four bonds
/// priced on and around their payment dates, under both reference-date flags and two curves, the engine's flag unset:
/// the NPV, the settlement value, the valuation date, the error estimate, and the clean and dirty prices read from them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscountingBondEngineVectorTests
{
    [Fact]
    public void DiscountingBondEngineVector()
    {
        var file = VectorFile.Load("pricingengines/bond/discountingbondengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Bond bond = BondInputs.BondOf(c.Input("bond"), context);
            FlatForward curve = CashFlowInputs.CurveOf(c.Input("curve"));
            BondResults? results = null;
            Exception? error = null;
            try
            {
                results = DiscountingBondEngine.Calculate(bond, context, curve);
            }
            catch (Exception e) when (e is ArgumentException or NanoQLException)
            {
                error = e;
            }

            BondResults Results() => results ?? throw error!;
            check.Output(c, "NPV", () => Results().NPV);
            check.Output(c, "settlementValue", () => Results().SettlementValue);
            check.Output(c, "valuationDate", () => Results().ValuationDate);
            check.Output(c, "errorEstimate", () => Results().ErrorEstimate);
            check.Output(c, "cleanPrice", () => bond.CleanPrice(Results(), context));
            check.Output(c, "dirtyPrice", () => bond.DirtyPrice(Results(), context));
        }

        check.Complete();
    }
}
