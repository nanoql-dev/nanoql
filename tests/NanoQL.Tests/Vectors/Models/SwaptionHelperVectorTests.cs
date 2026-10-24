// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The swaption helper vectors, <c>tests/vectors/models/shortrate/calibrationhelpers/swaptionhelper.json</c>: helpers
/// of QuantLib's three constructors, strikes, nominals, error types, volatility types, settlement days and indexes (an
/// overnight index among them), each with its market value, its swaption and swap, its times, its Black prices, its
/// model values and calibration errors on Hull-White and Vasicek through the Jamshidian engine, and an implied
/// volatility; and the refusals, where QuantLib throws at the helper's first use and nanoql at its construction.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwaptionHelperVectorTests
{
    [Fact]
    public void SwaptionHelperVector()
    {
        var file = VectorFile.Load("models/shortrate/calibrationhelpers/swaptionhelper");
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

            SwaptionHelper helper = CalibrationInputs.SwaptionHelperOf(spec, context);
            YieldTermStructure termStructure = CashFlowInputs.CurveOf(spec.GetProperty("termStructure"));
            double volatility = helper.Volatility;
            check.Output(c, "marketValue", helper.MarketValue);
            check.Output(c, "expiry", helper.Swaption.Exercise.Date(0));
            check.Output(c, "strike", helper.Underlying.FixedRate);
            check.Output(c, "nominal", () => helper.Underlying.Nominal());
            check.Output(c, "maturity", helper.Underlying.FixedSchedule.Dates[^1]);
            check.Output(c, "underlyingType", helper.Underlying.Type);
            check.Output(c, "fixedDates", helper.Underlying.FixedSchedule.Dates.Select(d => (object?)d));
            check.Output(c, "floatingDates", helper.Underlying.FloatingSchedule.Dates.Select(d => (object?)d));
            // swaptionhelper.py leaves the times out where nanoql's fix moves them (the S-cms plan's CMR4).
            if (c.Expected.TryGetProperty("times", out _))
            {
                check.Output(c, "times", helper.Times(context));
            }
            check.Output(c, "blackPrice", ((double[])[0.5, 1.0, 2.0]).Select(f => (object?)helper.BlackPrice(f * volatility, context)));

            HullWhite hullWhite = CalibrationInputs.HullWhiteOf(c.Input("hullWhite"), termStructure);
            SwaptionHelper onHullWhite = CalibrationInputs.WithJamshidian(helper, null);
            double hullWhiteValue = onHullWhite.ModelValue(hullWhite, context);
            check.Output(c, "modelValueHullWhite", hullWhiteValue);
            check.Output(c, "calibrationErrorHullWhite", () => onHullWhite.CalibrationError(hullWhite, context));
            (double low, double high) = helper.VolatilityType == VolatilityType.ShiftedLognormal ? (0.0010, 10.0) : (0.00005, 0.50);
            check.Output(c, "impliedVolatility", () => helper.ImpliedVolatility(hullWhiteValue, 1e-10, 1000, low, high, context));

            Vasicek vasicek = CalibrationInputs.VasicekOf(c.Input("vasicek"));
            SwaptionHelper onVasicek = CalibrationInputs.WithJamshidian(helper, termStructure);
            check.Output(c, "modelValueVasicek", () => onVasicek.ModelValue(vasicek, context));
            check.Output(c, "calibrationErrorVasicek", () => onVasicek.CalibrationError(vasicek, context));

            if (c.Inputs.TryGetProperty("clamps", out JsonElement clamps))
            {
                // Model values outside the Black prices at the bracket's ends: the implied volatility is clamped.
                check.Output(
                    c,
                    "calibrationErrorClamped",
                    clamps.EnumerateArray().Select(m => (Func<object?>)(() =>
                        onHullWhite.CalibrationError(CalibrationInputs.HullWhiteOf(m, termStructure), context))));
            }

            if (c.Expected.TryGetProperty("modelValueNoEngine", out _))
            {
                // An expired swaption is worth 0 with no engine: QuantLib's instrument reads its expiry first.
                check.Output(c, "modelValueNoEngine", () => helper.ModelValue(hullWhite, context));
            }
        }

        check.Complete();
    }

    // Each refusal builds the helper inside its output, since nanoql refuses at construction what QuantLib refuses at
    // the helper's first use.
    private static void Refusals(VectorAssert check, VectorCase c, JsonElement spec, EvaluationContext context)
    {
        SwaptionHelper Helper() => CalibrationInputs.SwaptionHelperOf(spec, context);
        HullWhite Model() => CalibrationInputs.HullWhiteOf(c.Input("hullWhite"), CashFlowInputs.CurveOf(spec.GetProperty("termStructure")));

        foreach (JsonProperty output in c.Expected.EnumerateObject())
        {
            Func<object?> f = output.Name switch
            {
                "calibrationError" => () => CalibrationInputs.WithJamshidian(Helper(), null).CalibrationError(Model(), context),
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
