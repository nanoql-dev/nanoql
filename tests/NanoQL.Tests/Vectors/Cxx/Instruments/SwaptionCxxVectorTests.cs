// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's swaption vectors, <c>tests/vectors/cxx/instruments/swaption.json</c>: the additional results the
/// wheel can't read (<c>timeToExpiry</c>, <c>impliedVolatility</c> and <c>spreadCorrection</c>) on the Black and
/// Bachelier engines with both cash annuity models; <see cref="Settlement.CheckTypeAndMethodConsistency"/> over every
/// pair; and the settlement enums' texts.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwaptionCxxVectorTests
{
    [Fact]
    public void SwaptionCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/swaption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.String("kind") == "settlement")
            {
                int[] types = c.Ints("types"), methods = c.Ints("methods");
                check.Output(
                    c,
                    "checkTypeAndMethodConsistency",
                    types.Select(t => methods.Select(m => (Func<object?>)(() =>
                    {
                        Settlement.CheckTypeAndMethodConsistency((SettlementType)t, (SettlementMethod)m);
                        return null;
                    })).ToArray()).ToArray());
                check.Output(
                    c,
                    "typeText",
                    c.Ints("textTypes").Select(t => (Func<object?>)(() => SwaptionFunctions.ToString((SettlementType)t))).ToArray());
                check.Output(c, "methodText", c.Ints("textMethods").Select(m => ((SettlementMethod)m).ToString()).ToArray());
                continue;
            }

            EvaluationContext context = c.Context();
            Swaption swaption = SwaptionInputs.SwaptionOf(c.Input("swaption"), context);
            YieldTermStructure discountCurve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            string[] tags = [.. c.Input("tags").EnumerateArray().Select(t => t.GetString()!)];
            check.Output(
                c,
                "results",
                c.Input("engines").EnumerateArray().Select(e => Results(swaption, context, discountCurve, e, tags)).ToArray());
        }

        check.Complete();
    }

    // The engine's results at the tags, each a throw where the pricing or the tag's result is refused.
    private static Func<object?>[] Results(
        Swaption swaption, EvaluationContext context, YieldTermStructure discountCurve, JsonElement engine, string[] tags)
    {
        Func<InstrumentResults> price = engine.GetProperty("type").GetString() == "BlackSwaptionEngine"
            ? () => SwaptionInputs.Black(swaption, context, discountCurve, engine)
            : () => SwaptionInputs.Bachelier(swaption, context, discountCurve, engine);
        return [.. tags.Select(t => (Func<object?>)(() => price().Result<double>(t)))];
    }
}
