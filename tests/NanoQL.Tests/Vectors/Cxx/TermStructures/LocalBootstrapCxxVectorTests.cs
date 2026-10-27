// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's local-bootstrap vectors, <c>tests/vectors/cxx/termstructures/localbootstrap.json</c>: piecewise
/// convex-monotone forward curves on <see cref="LocalBootstrap"/>, their nodes, maximum date, helpers' quote errors and
/// discounts.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LocalBootstrapCxxVectorTests
{
    [Fact]
    public void LocalBootstrapCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/localbootstrap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("curve"), b = c.Input("bootstrap");
            BootstrapHelper<YieldTermStructure>[] helpers = RateHelperInputs.HelpersOf(c.Input("helpers"), context);
            JsonElement accuracy = b.GetProperty("accuracy");
            var curve = new PiecewiseYieldCurve<ForwardRate, ConvexMonotone, LocalBootstrap>(
                new Date(spec.GetProperty("referenceDate").GetInt32()),
                helpers,
                CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
                context,
                i: new ConvexMonotone(
                    spec.GetProperty("quadraticity").GetDouble(),
                    spec.GetProperty("monotonicity").GetDouble(),
                    spec.GetProperty("forcePositive").GetBoolean()),
                bootstrap: new LocalBootstrap(
                    b.GetProperty("localisation").GetInt32(),
                    b.GetProperty("forcePositive").GetBoolean(),
                    accuracy.ValueKind == JsonValueKind.Null ? null : accuracy.GetDouble()));
            check.Output(c, "dates", curve.Dates().Cast<object?>().ToArray());
            check.Output(c, "data", curve.Data().Cast<object?>().ToArray());
            check.Output(c, "maxDate", curve.MaxDate);
            check.Output(c, "quoteErrors", helpers.Select(h => h.SetTermStructure(curve).QuoteError(context)).ToArray());
            check.Output(c, "discounts", c.DateInputs("discountDates").Select(d => curve.Discount(d, true)).ToArray());
        }

        check.Complete();
    }
}
