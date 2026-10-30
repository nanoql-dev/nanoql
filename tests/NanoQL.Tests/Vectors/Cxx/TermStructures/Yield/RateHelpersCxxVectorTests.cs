// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's rate-helper vectors, <c>tests/vectors/cxx/termstructures/yield/ratehelpers.json</c>: the deposit,
/// swap, FRA, futures and FX swap helpers (<see cref="RateHelperInputs"/>) bound by
/// <see cref="BootstrapHelperExtensions.SetTermStructure{T}(T, YieldTermStructure)"/> to a flat forward curve and to an
/// interpolated discount curve, with their implied quotes and quote errors, an FX swap helper with no collateral curve
/// refusing to be bound; <see cref="SwapRateHelper.ForwardStart"/>; the swap helper's coupon pricer; the FRA helper's
/// constructor from a period and a length; and the futures helper's mixed forms, which collapse here.
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
            BootstrapHelper<YieldTermStructure> unbound = helper;

            // the outputs QuantLib's master moves (#2899), null on both sides
            HashSet<string> skip = c.Inputs.TryGetProperty("skip", out JsonElement skipped)
                ? [.. skipped.EnumerateArray().Select(e => e.GetString()!)]
                : [];
            foreach ((string name, YieldTermStructure curve) in new[] { "Flat", "Curve" }.Zip(curves))
            {
                // binding can refuse too: an FX swap helper with no collateral curve
                Func<object?> quote = () => unbound.SetTermStructure(curve).ImpliedQuote(context);
                Func<object?> error = () => unbound.SetTermStructure(curve).QuoteError(context);
                check.Output(c, "impliedQuote" + name, skip.Contains("impliedQuote" + name) ? () => null : quote);
                check.Output(c, "quoteError" + name, skip.Contains("quoteError" + name) ? () => null : error);
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
