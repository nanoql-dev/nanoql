// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Euribor swap presets' vectors, <c>tests/vectors/indexes/swap/euriborswap.json</c>:
/// <see cref="EuriborSwapIsdaFixA"/>, <see cref="EuriborSwapIsdaFixB"/> and <see cref="EuriborSwapIfrFix"/> at several
/// tenors, in both constructors' forms: their names, conventions and Ibor indexes, their fixings, and the refusal of a
/// tenor that can't be compared with a year.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EuriborSwapVectorTests
{
    [Fact]
    public void EuriborSwapVector()
    {
        var file = VectorFile.Load("indexes/swap/euriborswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            SwapIndex? index = null;
            check.Output(c, "built", () =>
            {
                index = Make(c);
                return true;
            });
            if (index is null)
            {
                continue;
            }

            check.Output(c, "name", index.Name);
            check.Output(c, "familyName", index.FamilyName);
            check.Output(c, "fixingDays", index.FixingDays);
            check.Output(c, "fixedLegTenor", new object[] { index.FixedLegTenor.Length, index.FixedLegTenor.Units });
            check.Output(c, "fixedLegConvention", index.FixedLegConvention);
            check.Output(c, "dayCounter", index.DayCounter.Name);
            check.Output(c, "fixingCalendar", index.FixingCalendar.Name);
            check.Output(c, "currency", index.Currency!.Code);
            check.Output(c, "iborIndex", index.IborIndex.Name);
            check.Output(c, "discountingTermStructureEmpty", index.DiscountingTermStructure is null);
            check.Output(
                c,
                "fixing",
                c.DateInputs("dates").Select(d => (Func<object?>)(() => index.Fixing(d, context))).ToArray());
        }

        check.Complete();
    }

    private static SwapIndex Make(VectorCase c)
    {
        Period tenor = SwapInputs.PeriodOf(c.Input("tenor"));
        YieldTermStructure? forwarding = IndexInputs.CurveOf(c.Input("forwarding"));
        bool exogenous = c.String("form") == "forwarding, discounting";
        YieldTermStructure? discounting = IndexInputs.CurveOf(c.Input("discounting"));
        return c.String("preset") switch
        {
            "EuriborSwapIsdaFixA" => exogenous
                ? new EuriborSwapIsdaFixA(tenor, forwarding, discounting)
                : new EuriborSwapIsdaFixA(tenor, forwarding),
            "EuriborSwapIsdaFixB" => exogenous
                ? new EuriborSwapIsdaFixB(tenor, forwarding, discounting)
                : new EuriborSwapIsdaFixB(tenor, forwarding),
            "EuriborSwapIfrFix" => exogenous
                ? new EuriborSwapIfrFix(tenor, forwarding, discounting)
                : new EuriborSwapIfrFix(tenor, forwarding),
            string other => throw new InvalidOperationException($"unknown preset '{other}'"),
        };
    }
}
