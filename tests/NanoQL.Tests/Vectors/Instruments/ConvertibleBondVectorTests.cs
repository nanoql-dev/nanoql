// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The convertible bond vectors, <c>tests/vectors/instruments/bonds/convertiblebonds.json</c>: zero-coupon,
/// fixed-coupon and floating-rate convertibles with calls, soft calls and puts, their inspectors without a date, their
/// settlement dates and their prices on the Cox-Ross-Rubinstein convertible engine, which read the arguments the bond
/// sets up, each priced at a settlement lag of 0 (the case's <c>pricedBond</c>); and the refusal of a last callability
/// after maturity.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ConvertibleBondVectorTests
{
    [Fact]
    public void ConvertibleBondVector()
    {
        var file = VectorFile.Load("instruments/bonds/convertiblebonds");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("construct", out _))
            {
                check.Output(c, "construct", () => ConvertibleInputs.BondOf(c.Input("bond"), context));
                continue;
            }

            ConvertibleBond bond = ConvertibleInputs.BondOf(c.Input("bond"), context);
            BondInputs.CheckStatic(check, c, bond, context);
            check.Output(c, "settlementDate", bond.SettlementDate(context));
            if (c.Expected.TryGetProperty("NPV", out _))
            {
                GeneralizedBlackScholesProcess process = EquityInputs.ProcessOf(c.Input("process"), context);
                ConvertibleBond pricedBond = ConvertibleInputs.BondOf(c.Input("pricedBond"), context);
                var priced = new Lazy<BondResults>(() => BinomialCRRConvertibleEngine.Calculate(
                    pricedBond, context, process, c.Int("timeSteps"), c.Input("creditSpread").GetDouble()));
                check.Output(c, "NPV", () => priced.Value.NPV);
                check.Output(c, "settlementValue", () => priced.Value.SettlementValue);
            }
        }

        check.Complete();
    }
}
