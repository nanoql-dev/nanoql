// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's bond helper vectors, <c>tests/vectors/cxx/termstructures/yield/bondhelpers.json</c>: the bond
/// helper vectors' helpers and five on bonds the wheel can't build (<see cref="BondInputs.HelperOf"/>), with
/// <see cref="BondHelper.PriceType"/>, their dates, and their implied quotes and quote errors bound by
/// <see cref="BootstrapHelperExtensions.SetTermStructure{T}(T, YieldTermStructure)"/> to a flat forward curve and to
/// an interpolated discount curve. A refused construction is the case's one output, "built".
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BondHelpersCxxVectorTests
{
    [Fact]
    public void BondHelpersCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/yield/bondhelpers");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BondHelper? helper = null;
            check.Output(c, "built", () =>
            {
                helper = BondInputs.HelperOf(c.Input("helper"), context);
                return true;
            });
            if (helper is null)
            {
                continue;
            }

            check.Output(c, "priceType", (int)helper.PriceType);
            check.Output(c, "earliestDate", helper.EarliestDate);
            check.Output(c, "maturityDate", helper.MaturityDate);
            check.Output(c, "latestRelevantDate", helper.LatestRelevantDate);
            check.Output(c, "pillarDate", helper.PillarDate);
            check.Output(c, "latestDate", helper.LatestDate);
            YieldTermStructure[] curves = [.. c.Input("curves").EnumerateArray().Select(CurveOf)];
            BondHelper unbound = helper;
            foreach ((string name, YieldTermStructure curve) in new[] { "Flat", "Curve" }.Zip(curves))
            {
                check.Output(c, "impliedQuote" + name, () => unbound.SetTermStructure(curve).ImpliedQuote(context));
                check.Output(c, "quoteError" + name, () => unbound.SetTermStructure(curve).QuoteError(context));
            }
        }

        check.Complete();
    }

    private static YieldTermStructure CurveOf(JsonElement spec) =>
        spec.TryGetProperty("type", out _)
            ? new InterpolatedDiscountCurve<LogLinear>(
                spec.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32())),
                CouponInputs.Reals(spec.GetProperty("discounts")),
                CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")))
            : CashFlowInputs.CurveOf(spec);
}
