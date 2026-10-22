// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Quotes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BlackDeltaCalculator vectors, <c>tests/vectors/pricingengines/blackdeltacalculator.json</c>:
/// <see cref="BlackDeltaCalculator"/>'s deltas from strikes, strikes from deltas and at-the-money strikes for calls and
/// puts under each delta convention on two markets at three standard deviations, QuantLib's refusals among them; the
/// premium-adjusted puts beyond the non-adjusted range are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackDeltaCalculatorVectorTests
{
    [Fact]
    public void BlackDeltaCalculatorVector()
    {
        var file = VectorFile.Load("pricingengines/blackdeltacalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var calculator = new BlackDeltaCalculator(
                (OptionType)c.Input("optionType").GetInt32(),
                (DeltaVolQuoteDeltaType)c.Input("deltaType").GetInt32(),
                c.Input("spot").GetDouble(),
                c.Input("dDiscount").GetDouble(),
                c.Input("fDiscount").GetDouble(),
                c.Input("stdDev").GetDouble());
            check.Output(
                c,
                "deltaFromStrike",
                Doubles(c.Input("strikes")).Select(k => Lazy(() => calculator.DeltaFromStrike(k))).ToArray());
            check.Output(
                c,
                "strikeFromDelta",
                Doubles(c.Input("deltas")).Select(d => Lazy(() => calculator.StrikeFromDelta(d))).ToArray());
            DeltaVolQuoteAtmType[] atmTypes = [.. Enumerable.Range(0, 7).Select(a => (DeltaVolQuoteAtmType)a)];
            check.Output(c, "atmStrike", atmTypes.Select(a => Lazy(() => calculator.AtmStrike(a))).ToArray());
        }

        check.Complete();
    }
}
