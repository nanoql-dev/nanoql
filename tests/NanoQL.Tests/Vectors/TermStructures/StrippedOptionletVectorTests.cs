// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="StrippedOptionlet"/>'s vectors, <c>tests/vectors/termstructures/volatility/optionlet/strippedoptionlet.json</c>:
/// stripped optionlets on one strike vector for every date, their dates, times, strikes, volatilities, at-the-money
/// rates and inspectors; and the ones QuantLib refuses to build.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StrippedOptionletVectorTests
{
    [Fact]
    public void StrippedOptionletVector()
    {
        var file = VectorFile.Load("termstructures/volatility/optionlet/strippedoptionlet");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            StrippedOptionlet? optionlet = null;
            check.Output(c, "built", () =>
            {
                optionlet = StripperInputs.StrippedOptionletOf(c.Input("optionlet"), context);
                return true;
            });
            if (optionlet is null || !c.Expected.TryGetProperty("optionletMaturities", out _))
            {
                continue;
            }

            StrippedOptionlet s = optionlet;
            int n = s.OptionletMaturities;
            check.Output(c, "optionletMaturities", n);
            check.Output(c, "optionletFixingDates", s.OptionletFixingDates);
            check.Output(c, "optionletFixingTimes", s.OptionletFixingTimes);
            check.Output(c, "optionletStrikes", Enumerable.Range(0, n).Select(i => s.OptionletStrikes(i)).ToArray());
            check.Output(c, "optionletVolatilities", Enumerable.Range(0, n).Select(i => s.OptionletVolatilities(i)).ToArray());
            check.Output(c, "atmOptionletRates", () => s.AtmOptionletRates(context));
            check.Output(c, "dayCounter", s.DayCounter.Name);
            check.Output(c, "calendar", s.Calendar!.Name);
            check.Output(c, "settlementDays", s.SettlementDays);
            check.Output(c, "businessDayConvention", s.BusinessDayConvention);
            check.Output(c, "volatilityType", s.VolatilityType);
            check.Output(c, "displacement", s.Displacement);
            check.Output(c, "volatilitiesPastLast", () => s.OptionletVolatilities(n));
            check.Output(c, "strikesPastLast", () => s.OptionletStrikes(n));
        }

        check.Complete();
    }
}
