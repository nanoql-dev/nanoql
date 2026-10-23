// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's smile section utilities vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/smilesectionutils.json</c>: <see cref="SmileSectionUtils"/> over
/// harness and SABR sections, with the default and a given moneyness grid, the section's level and a given one, and
/// without and with the deletion of arbitrage points: its grids, call prices, level and arbitrage-free region; and its
/// constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SmileSectionUtilsCxxVectorTests
{
    [Fact]
    public void SmileSectionUtilsCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/smilesectionutils");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("utilities");
            SmileSectionUtils Build() => new(
                SmileSectionInputs.SectionOf(spec.GetProperty("section"), c.Context()),
                VolatilityInputs.Reals(spec.GetProperty("moneynessGrid")),
                SmileSectionInputs.NullableReal(spec.GetProperty("atm")),
                spec.GetProperty("deleteArbitragePoints").GetBoolean());

            check.Output(c, "moneyGrid", () => Build().MoneyGrid());
            check.Output(c, "strikeGrid", () => Build().StrikeGrid());
            check.Output(c, "callPrices", () => Build().CallPrices());
            check.Output(c, "atmLevel", () => Build().AtmLevel());
            check.Output(c, "arbitragefreeRegion", () =>
            {
                (double left, double right) = Build().ArbitragefreeRegion();
                return new[] { left, right };
            });
            check.Output(c, "arbitragefreeIndices", () =>
            {
                (int left, int right) = Build().ArbitragefreeIndices();
                return new[] { left, right };
            });
        }

        check.Complete();
    }
}
