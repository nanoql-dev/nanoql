// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="OptionletStripper1"/>'s vectors,
/// <c>tests/vectors/termstructures/volatility/optionlet/optionletstripper1.json</c>: strips of QuantLib's test surfaces
/// and the cap survey's, both volatility types, a displacement, dontThrow, a given switch strike, the at-par switch,
/// Euribor and SOFR; their dates, rates, strikes, volatilities (the ones the stability filter drops null), matrices and
/// inspectors; and the strippers QuantLib refuses, which nanoql refuses when built.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OptionletStripper1VectorTests
{
    [Fact]
    public void OptionletStripper1Vector()
    {
        var file = VectorFile.Load("termstructures/volatility/optionlet/optionletstripper1");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            OptionletStripper1? stripper = null;
            check.Output(c, "built", () =>
            {
                stripper = StripperInputs.StripperOf(c.Inputs, context);
                return true;
            });
            if (stripper is null || !c.Expected.TryGetProperty("optionletMaturities", out _))
            {
                continue;
            }

            OptionletStripper1 s = stripper;
            int n = s.OptionletMaturities;
            HashSet<(int, int)> dropped =
                [.. c.Input("dropped").EnumerateArray().Select(p => (p[0].GetInt32(), p[1].GetInt32()))];
            check.Output(c, "optionletMaturities", n);
            check.Output(c, "optionletFixingDates", s.OptionletFixingDates);
            check.Output(c, "optionletFixingTimes", s.OptionletFixingTimes);
            check.Output(c, "atmOptionletRates", s.AtmOptionletRates(context));
            check.Output(c, "optionletStrikes", Enumerable.Range(0, n).Select(i => s.OptionletStrikes(i)).ToArray());
            check.Output(
                c,
                "optionletVolatilities",
                Enumerable.Range(0, n)
                    .Select(i => s.OptionletVolatilities(i).Select((x, j) => dropped.Contains((i, j)) ? null : (object?)x).ToArray())
                    .ToArray());
            check.Output(c, "capFloorPrices", StripperInputs.Rows(s.CapFloorPrices()));
            check.Output(c, "capFloorVolatilities", StripperInputs.Rows(s.CapFloorVolatilities()));
            check.Output(c, "optionletPrices", StripperInputs.Rows(s.OptionletPrices()));
            check.Output(c, "switchStrike", s.SwitchStrike);

            // QuantLib's refusal for a surface on a fixed date is nanoql's null.
            check.Mapped(c, "settlementDays", () => s.SettlementDays, null, "a fixed reference date's settlement days are null");
            check.Output(c, "calendar", s.Calendar!.Name);
            check.Output(c, "dayCounter", s.DayCounter.Name);
            check.Output(c, "businessDayConvention", s.BusinessDayConvention);
            check.Output(c, "volatilityType", s.VolatilityType);
            check.Output(c, "displacement", s.Displacement);
            check.Output(c, "pastLast", () => s.OptionletVolatilities(n));
        }

        check.Complete();
    }
}
