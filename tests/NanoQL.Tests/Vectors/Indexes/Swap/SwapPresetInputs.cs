// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Reads a swap preset family's vectors, as <c>tools/vectors/gen/indexes/swap/euriborswap.py</c>'s
/// <c>preset_cases</c> writes them: each case's <c>"preset"</c> (the class's name), <c>"tenor"</c>, <c>"form"</c>
/// (<c>"forwarding"</c> or <c>"forwarding, discounting"</c>, the constructor taking one curve or both) and curves
/// (<c>"forwarding"</c>, <c>"discounting"</c>, flat forward specs, null for none); its outputs whether the index builds,
/// then its name, conventions, Ibor index, whether the discounting curve is empty, and its fixings at the case's dates.
/// </summary>
internal static class SwapPresetInputs
{
    /// <summary>Checks every case of a swap preset family's vector file.</summary>
    /// <param name="path">The vector file's path under <c>tests/vectors</c>, without its extension.</param>
    public static void Check(string path)
    {
        var file = VectorFile.Load(path);
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
        YieldTermStructure? f = IndexInputs.CurveOf(c.Input("forwarding"));
        bool exogenous = c.String("form") == "forwarding, discounting";
        YieldTermStructure? d = IndexInputs.CurveOf(c.Input("discounting"));
        return c.String("preset") switch
        {
            "EuriborSwapIsdaFixA" => exogenous ? new EuriborSwapIsdaFixA(tenor, f, d) : new EuriborSwapIsdaFixA(tenor, f),
            "EuriborSwapIsdaFixB" => exogenous ? new EuriborSwapIsdaFixB(tenor, f, d) : new EuriborSwapIsdaFixB(tenor, f),
            "EuriborSwapIfrFix" => exogenous ? new EuriborSwapIfrFix(tenor, f, d) : new EuriborSwapIfrFix(tenor, f),
            "ChfLiborSwapIsdaFix" => exogenous ? new ChfLiborSwapIsdaFix(tenor, f, d) : new ChfLiborSwapIsdaFix(tenor, f),
            "EurLiborSwapIsdaFixA" => exogenous
                ? new EurLiborSwapIsdaFixA(tenor, f, d)
                : new EurLiborSwapIsdaFixA(tenor, f),
            "EurLiborSwapIsdaFixB" => exogenous
                ? new EurLiborSwapIsdaFixB(tenor, f, d)
                : new EurLiborSwapIsdaFixB(tenor, f),
            "EurLiborSwapIfrFix" => exogenous ? new EurLiborSwapIfrFix(tenor, f, d) : new EurLiborSwapIfrFix(tenor, f),
            "GbpLiborSwapIsdaFix" => exogenous ? new GbpLiborSwapIsdaFix(tenor, f, d) : new GbpLiborSwapIsdaFix(tenor, f),
            "JpyLiborSwapIsdaFixAm" => exogenous
                ? new JpyLiborSwapIsdaFixAm(tenor, f, d)
                : new JpyLiborSwapIsdaFixAm(tenor, f),
            "JpyLiborSwapIsdaFixPm" => exogenous
                ? new JpyLiborSwapIsdaFixPm(tenor, f, d)
                : new JpyLiborSwapIsdaFixPm(tenor, f),
            "UsdLiborSwapIsdaFixAm" => exogenous
                ? new UsdLiborSwapIsdaFixAm(tenor, f, d)
                : new UsdLiborSwapIsdaFixAm(tenor, f),
            "UsdLiborSwapIsdaFixPm" => exogenous
                ? new UsdLiborSwapIsdaFixPm(tenor, f, d)
                : new UsdLiborSwapIsdaFixPm(tenor, f),
            string other => throw new InvalidOperationException($"unknown preset '{other}'"),
        };
    }
}
