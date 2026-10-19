// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's discounting bond engine vectors, <c>tests/vectors/cxx/pricingengines/bond/discountingbondengine.json</c>:
/// the bond engine vectors' bonds priced on and around their payment dates with the engine's
/// <c>includeSettlementDateFlows</c> null, false and true: the NPV, the settlement value, the valuation date, the error
/// estimate, and the clean and dirty prices read from them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscountingBondEngineCxxVectorTests
{
    [Fact]
    public void DiscountingBondEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/bond/discountingbondengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Bond bond = BondInputs.BondOf(c.Input("bond"), context);
            FlatForward curve = CashFlowInputs.CurveOf(c.Input("curve"));
            bool?[] flags =
            [
                .. c.Input("flags").EnumerateArray()
                    .Select(f => f.ValueKind == JsonValueKind.Null ? (bool?)null : f.GetBoolean()),
            ];
            Func<BondResults>[] results = [.. flags.Select(f => Results(bond, context, curve, f))];
            check.Output(c, "NPV", BondInputs.Each(results, r => r().NPV));
            check.Output(c, "settlementValue", BondInputs.Each(results, r => r().SettlementValue));
            check.Output(c, "valuationDate", BondInputs.Each(results, r => r().ValuationDate));
            check.Output(c, "errorEstimate", BondInputs.Each(results, r => r().ErrorEstimate));
            check.Output(c, "cleanPrice", BondInputs.Each(results, r => bond.CleanPrice(r(), context)));
            check.Output(c, "dirtyPrice", BondInputs.Each(results, r => bond.DirtyPrice(r(), context)));
        }

        check.Complete();
    }

    // The engine's results under a flag, or its refusal, thrown again wherever they are read.
    private static Func<BondResults> Results(Bond bond, EvaluationContext context, FlatForward curve, bool? flag)
    {
        BondResults? results = null;
        Exception? error = null;
        try
        {
            results = DiscountingBondEngine.Calculate(bond, context, curve, flag);
        }
        catch (Exception e) when (e is ArgumentException or NanoQLException)
        {
            error = e;
        }

        return () => results ?? throw error!;
    }
}
