// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The piecewise spread curve vectors, <c>tests/vectors/termstructures/yield/piecewisespreadyieldcurve.json</c>: spread
/// discount factors bootstrapped on a deposit and five swaps over the zero-spreaded file's six originals, on
/// log-linear, log-cubic and log-mixed-linear-cubic interpolation; the curve's nodes, inspectors, discounts, zero rates
/// and forward rates, and the discounts of the spread discount curve rebuilt from its nodes, but past a base that
/// doesn't extrapolate.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PiecewiseSpreadYieldCurveVectorTests
{
    [Fact]
    public void PiecewiseSpreadYieldCurveVector()
    {
        var file = VectorFile.Load("termstructures/yield/piecewisespreadyieldcurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            YieldTermStructure baseCurve = YieldCurveInputs.OriginalOf(c.Input("base"), c.Context());
            BootstrapHelper<YieldTermStructure>[] helpers = RateHelperInputs.HelpersOf(c.Input("helpers"), c.Context());
            JsonElement i = c.Input("interpolator");
            Date[] dates = c.DateInputs("dates");
            (YieldTermStructure curve, ImmutableArray<Date> nodeDates, ImmutableArray<double> nodeData) = Build(
                i, baseCurve, helpers, c.Context());
            check.Output(c, "built", true);
            check.Output(c, "nodeDates", nodeDates.Cast<object?>().ToArray());
            check.Output(c, "nodeData", nodeData.Cast<object?>().ToArray());
            YieldCurveInputs.CheckCurve(check, c, curve, dates);

            YieldTermStructure raw = (i.GetProperty("type").GetString() switch
            {
                "LogLinear" => new SpreadDiscountCurve(baseCurve, nodeDates, nodeData),
                "LogCubic" => new LogCubicSpreadDiscountCurve(
                    baseCurve, nodeDates, nodeData, BootstrapInputs.LogCubicOf(i)),
                _ => (YieldTermStructure)new LogMixedLinearCubicSpreadDiscountCurve(
                    baseCurve, nodeDates, nodeData, BootstrapInputs.LogMixedLinearCubicOf(i)),
            }).EnableExtrapolation();
            check.Output(c, "rawDiscount", YieldCurveInputs.Each(c.DateInputs("rawDates"), d => raw.Discount(d)));
        }

        check.Complete();
    }

    // The spread curve an interpolator spec names, bootstrapped, with its nodes.
    private static (YieldTermStructure Curve, ImmutableArray<Date> Dates, ImmutableArray<double> Data) Build(
        JsonElement i, YieldTermStructure baseCurve, BootstrapHelper<YieldTermStructure>[] helpers, EvaluationContext context)
    {
        switch (i.GetProperty("type").GetString())
        {
            case "LogLinear":
                var logLinear = new PiecewiseLogLinearSpreadDiscount(baseCurve, helpers, context);
                return (logLinear, logLinear.Dates(), logLinear.Data());
            case "LogCubic":
                var logCubic = new PiecewiseLogCubicSpreadDiscount(
                    baseCurve, helpers, context, BootstrapInputs.LogCubicOf(i));
                return (logCubic, logCubic.Dates(), logCubic.Data());
            case "LogMixedLinearCubic":
                var logMixed = new PiecewiseLogMixedLinearCubicSpreadDiscount(
                    baseCurve, helpers, context, BootstrapInputs.LogMixedLinearCubicOf(i));
                return (logMixed, logMixed.Dates(), logMixed.Data());
            case var t:
                throw new InvalidOperationException($"unknown interpolator '{t}'");
        }
    }
}
