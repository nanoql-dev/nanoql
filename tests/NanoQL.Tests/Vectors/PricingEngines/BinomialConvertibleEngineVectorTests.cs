// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BinomialConvertibleEngine vectors, <c>tests/vectors/pricingengines/bond/binomialconvertibleengine.json</c>:
/// convertibles under European, American and Bermudan exercises on each of the seven trees, over five processes, with
/// and without dividends and credit spreads, their NPVs and settlement values, an expired bond and the refusals; each
/// tree read through its named engine.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BinomialConvertibleEngineVectorTests
{
    [Fact]
    public void BinomialConvertibleEngineVector()
    {
        var file = VectorFile.Load("pricingengines/bond/binomialconvertibleengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            ConvertibleBond bond = ConvertibleInputs.BondOf(c.Input("bond"), context);
            GeneralizedBlackScholesProcess process = EquityInputs.ProcessOf(c.Input("process"), context);
            ImmutableArray<Dividend> dividends = EquityInputs.DividendsOf(c.Input("dividends"));
            var engine = ConvertibleInputs.Engine(c.String("tree"));
            var priced = new Lazy<BondResults>(() => engine(
                bond, context, process, c.Int("timeSteps"), c.Input("creditSpread").GetDouble(), dividends));
            check.Output(c, "NPV", () => priced.Value.NPV);
            check.Output(c, "settlementValue", () => priced.Value.SettlementValue);
        }

        check.Complete();
    }
}
