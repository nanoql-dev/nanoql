// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The cap helper vectors, <c>tests/vectors/models/shortrate/calibrationhelpers/caphelper.json</c>: helpers of lengths,
/// indexes, fixed legs, error types and volatility types, with and without the first caplet, each with its market
/// value, its times, its Black prices, its model value and calibration error on Hull-White through the analytic
/// cap/floor engine, and an implied volatility; and the refusals, where QuantLib throws at the helper's first use and
/// nanoql at its construction.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CapHelperVectorTests
{
    [Fact]
    public void CapHelperVector()
    {
        var file = VectorFile.Load("models/shortrate/calibrationhelpers/caphelper");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("helper");
            if (c.Name.StartsWith("refused / ", StringComparison.Ordinal))
            {
                Refusals(check, c, spec, context);
                continue;
            }

            CapHelper helper = CalibrationInputs.CapHelperOf(spec, context);
            YieldTermStructure termStructure = CashFlowInputs.CurveOf(spec.GetProperty("termStructure"));
            double volatility = helper.Volatility;
            check.Output(c, "marketValue", helper.MarketValue);
            check.Output(c, "times", helper.Times(context));
            check.Output(c, "blackPrice", ((double[])[0.5, 1.0, 2.0]).Select(f => (object?)helper.BlackPrice(f * volatility, context)));

            HullWhite hullWhite = CalibrationInputs.HullWhiteOf(c.Input("hullWhite"), termStructure);
            CapHelper onHullWhite = CalibrationInputs.WithAnalytic(helper);
            double hullWhiteValue = onHullWhite.ModelValue(hullWhite, context);
            check.Output(c, "modelValueHullWhite", hullWhiteValue);
            check.Output(c, "calibrationErrorHullWhite", () => onHullWhite.CalibrationError(hullWhite, context));
            (double low, double high) = helper.VolatilityType == VolatilityType.ShiftedLognormal ? (0.0010, 10.0) : (0.00005, 0.50);
            check.Output(c, "impliedVolatility", () => helper.ImpliedVolatility(hullWhiteValue, 1e-10, 1000, low, high, context));

            if (c.Inputs.TryGetProperty("clamps", out JsonElement clamps))
            {
                // Model values outside the Black prices at the bracket's ends: the implied volatility is clamped.
                check.Output(
                    c,
                    "calibrationErrorClamped",
                    clamps.EnumerateArray().Select(m => (Func<object?>)(() =>
                        onHullWhite.CalibrationError(CalibrationInputs.HullWhiteOf(m, termStructure), context))));
            }
        }

        check.Complete();
    }

    // Each refusal builds the helper inside its output, since nanoql refuses at construction what QuantLib refuses at
    // the helper's first use.
    private static void Refusals(VectorAssert check, VectorCase c, JsonElement spec, EvaluationContext context)
    {
        CapHelper Helper() => CalibrationInputs.CapHelperOf(spec, context);
        HullWhite Model() => CalibrationInputs.HullWhiteOf(c.Input("hullWhite"), CashFlowInputs.CurveOf(spec.GetProperty("termStructure")));

        foreach (JsonProperty output in c.Expected.EnumerateObject())
        {
            Func<object?> f = output.Name switch
            {
                "calibrationError" => () => CalibrationInputs.WithAnalytic(Helper()).CalibrationError(Model(), context),
                "calibrationErrorNoEngine" => () => Helper().CalibrationError(Model(), context),
                "modelValueNoEngine" => () => Helper().ModelValue(Model(), context),
                "marketValue" => () => Helper().MarketValue,
                "impliedGuessOutside" => () => Helper().ImpliedVolatility(0.01, 1e-10, 100, 0.25, 1.0, context),
                "impliedNotBracketed" => () => Helper().ImpliedVolatility(10.0, 1e-10, 100, 0.01, 1.0, context),
                string name => throw new InvalidOperationException($"unknown output '{name}'"),
            };
            check.Output(c, output.Name, f);
        }
    }
}
