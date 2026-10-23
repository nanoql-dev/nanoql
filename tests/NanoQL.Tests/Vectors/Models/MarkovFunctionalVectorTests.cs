// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The wheel's Markov-functional model vectors, <c>tests/vectors/models/shortrate/onefactormodels/markovfunctional.json</c>:
/// four calibrations of <see cref="MarkovFunctional"/>, to swaptions and to caplets, with Kahale's smile or none and the
/// adjustments, and at a zero reversion: the volatilities, the numeraire and zero bonds, a curve of the model's clock,
/// a forward rate, a swap rate and annuity and zero-bond options; and a calibration to coterminal swaptions, each output
/// where stable (the S7 plan's S7R6).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MarkovFunctionalVectorTests
{
    [Fact]
    public void MarkovFunctionalVector()
    {
        var file = VectorFile.Load("models/shortrate/onefactormodels/markovfunctional");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            MarkovFunctional model = Gaussian1dInputs.MarkovFunctionalOf(c.Input("model"), context);
            Dictionary<string, object?> outputs = c.Inputs.TryGetProperty("helpers", out _)
                ? Calibrated(c, model, context)
                : Members(c, model, context);
            foreach (System.Text.Json.JsonProperty output in c.Expected.EnumerateObject())
            {
                check.Output(c, output.Name, outputs[output.Name]);
            }
        }

        check.Complete();
    }

    private static Dictionary<string, object?> Members(VectorCase c, MarkovFunctional m, EvaluationContext context)
    {
        YieldTermStructure forwardCurve = CashFlowInputs.CurveOf(c.Input("forwardCurve"));
        YieldTermStructure other = CashFlowInputs.CurveOf(c.Input("otherCurve"));
        var euribor = new Euribor6M(forwardCurve);
        var swapIndex = new EuriborSwapIsdaFixA(new Period(5, TimeUnit.Years), forwardCurve);
        var cal = new TARGET();
        Date today = context.EvaluationDate;
        Date fixing = cal.Advance(today, 2, TimeUnit.Years);
        Date reference = cal.Advance(today, 1, TimeUnit.Years);
        var outputs = new Dictionary<string, object?>();
        Elements(outputs, "volatility", [.. m.Volatility]);
        Elements(outputs, "params", m.Params().AsSpan().ToArray());
        double[][] numerairePoints = ModelInputs.Points(c, "numerairePoints");
        for (int i = 0; i < numerairePoints.Length; i++)
        {
            outputs[$"numeraire[{i}]"] = m.Numeraire(numerairePoints[i][0], numerairePoints[i][1]);
        }

        double[][] zerobondPoints = ModelInputs.Points(c, "zerobondPoints");
        for (int i = 0; i < zerobondPoints.Length; i++)
        {
            outputs[$"zerobond[{i}]"] = m.Zerobond(zerobondPoints[i][0], zerobondPoints[i][1], zerobondPoints[i][2]);
        }

        outputs["numeraireOther"] = m.Numeraire(2.0, 0.4, other);
        outputs["zerobondOther"] = m.Zerobond(6.0, 2.0, 0.4, other);
        outputs["forwardRate"] = m.ForwardRate(fixing, context, reference, 0.3, euribor);
        outputs["swapRate"] = m.SwapRate(fixing, new Period(5, TimeUnit.Years), context, reference, 0.3, swapIndex);
        outputs["swapAnnuity"] = m.SwapAnnuity(fixing, new Period(5, TimeUnit.Years), context, reference, 0.3, swapIndex);
        Date expiry = cal.Advance(today, 2, TimeUnit.Years), maturity = cal.Advance(today, 7, TimeUnit.Years);
        outputs["zerobondCall"] = m.ZerobondOption(OptionType.Call, expiry, expiry, maturity, 0.88, reference, 0.2);
        outputs["zerobondPut"] = m.ZerobondOption(OptionType.Put, expiry, expiry, maturity, 0.88, reference, 0.2);
        return outputs;
    }

    private static Dictionary<string, object?> Calibrated(VectorCase c, MarkovFunctional model, EvaluationContext context)
    {
        List<SwaptionHelper> helpers = [.. c.Input("helpers").EnumerateArray().Select(spec =>
            CalibrationInputs.SwaptionHelperOf(spec, context)
                .SetPricingEngine<MarkovFunctional>((s, ctx, m) => Gaussian1dSwaptionEngine.Calculate(s, ctx, m, 64, 7.0)))];
        OptimizationMethod method = CalibrationInputs.MethodOf(c.Input("method"));
        EndCriteria endCriteria = OptimizationSpecs.EndCriteria(c.Input("endCriteria"));
        CalibrationResults<MarkovFunctional> result = model.Calibrate(helpers, method, endCriteria, context);
        MarkovFunctional calibrated = result.Model;
        var outputs = new Dictionary<string, object?>
        {
            ["endCriteria"] = result.EndCriteria,
            ["functionEvaluation"] = result.FunctionEvaluation,
        };
        Elements(outputs, "params", calibrated.Params().AsSpan().ToArray());
        Elements(outputs, "problemValues", [.. result.ProblemValues]);
        Elements(outputs, "calibrationErrors", [.. helpers.Select(h => h.CalibrationError(calibrated, context))]);
        return outputs;
    }

    // A list output's elements, each its own output named with its index, as the filter writes them.
    private static void Elements(Dictionary<string, object?> outputs, string name, double[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            outputs[$"{name}[{i}]"] = values[i];
        }
    }
}
