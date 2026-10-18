// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's rate-helper vectors, <c>tests/vectors/cxx/termstructures/yield/ratehelpers.json</c>: the deposit
/// and swap helpers (<see cref="RateHelperInputs"/>) bound by
/// <see cref="BootstrapHelperExtensions.SetTermStructure{T}(T, YieldTermStructure)"/> to a flat forward curve and to an
/// interpolated discount curve, with their implied quotes and quote errors; <see cref="SwapRateHelper.ForwardStart"/>;
/// and the swap helper's coupon pricer.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RateHelpersCxxVectorTests
{
    [Fact]
    public void RateHelpersCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/yield/ratehelpers");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("helper");
            BootstrapHelper<YieldTermStructure>? helper = null;
            check.Output(c, "built", () =>
            {
                helper = RateHelperInputs.HelperOf(spec, context);
                return true;
            });
            if (helper is null)
            {
                continue;
            }

            check.Output(c, "pillarDate", helper.PillarDate);
            YieldTermStructure[] curves = [.. c.Input("curves").EnumerateArray().Select(CurveOf)];
            foreach ((string name, YieldTermStructure curve) in new[] { "Flat", "Curve" }.Zip(curves))
            {
                BootstrapHelper<YieldTermStructure> bound = helper.SetTermStructure(curve);
                check.Output(c, "impliedQuote" + name, () => bound.ImpliedQuote(context));
                check.Output(c, "quoteError" + name, () => bound.QuoteError(context));
            }

            if (helper is SwapRateHelper swap)
            {
                Period forwardStart = swap.ForwardStart;
                check.Output(c, "forwardStart", new object[] { forwardStart.Length, (int)forwardStart.Units });
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
