// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.CreditInputs;
using static NanoQL.Tests.Vectors.EquityInputs;
using Vector = NanoQL.Numerics.Vector;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The RoughHestonModel vectors, <c>tests/vectors/models/equity/roughhestonmodel.json</c>: three models' parameters,
/// Hurst exponent and parameter vector, and their parameters after <c>WithParams</c>; and a calibration to twenty Heston
/// helpers through the Padé engine, each evaluation's helpers priced through one engine on the model the calibration
/// hands them, its parameters and errors where the stability filter keeps them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RoughHestonModelVectorTests
{
    [Fact]
    public void RoughHestonModelVector()
    {
        var file = VectorFile.Load("models/equity/roughhestonmodel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            RoughHestonModel model = ModelOf(c.Input("model"));
            if (c.Input("kind").GetString() == "members")
            {
                check.Output(c, "theta", model.Theta);
                check.Output(c, "kappa", model.Kappa);
                check.Output(c, "sigma", model.Sigma);
                check.Output(c, "rho", model.Rho);
                check.Output(c, "v0", model.V0);
                check.Output(c, "hurst", model.Hurst);
                check.Output(c, "params", model.Params().Select(x => (object?)x).ToArray());
                var moved = (RoughHestonModel)model.WithParams(new Vector(Doubles(c.Input("newParams"))));
                check.Output(c, "afterSetParams", new object?[] { moved.Theta, moved.Kappa, moved.Sigma, moved.Rho, moved.V0, moved.Hurst });
                continue;
            }

            JsonElement h = c.Input("helpers");
            double[] vols = Doubles(h.GetProperty("vols"));
            double[] strikes = Doubles(h.GetProperty("strikes"));
            var engines = new System.Runtime.CompilerServices.ConditionalWeakTable<RoughHestonModel, AnalyticRoughHestonEngine>();
            var helpers = new List<HestonModelHelper>();
            int i = 0;
            foreach (JsonElement days in h.GetProperty("maturities").EnumerateArray())
            {
                foreach (double k in strikes)
                {
                    helpers.Add(new HestonModelHelper(
                            new Period(days.GetInt32(), TimeUnit.Days),
                            new NullCalendar(),
                            model.Process.S0,
                            k,
                            vols[i++],
                            model.Process.RiskFreeRate,
                            model.Process.DividendYield,
                            context,
                            BlackCalibrationHelperCalibrationErrorType.ImpliedVolError)
                        .SetPricingEngine<RoughHestonModel>((option, ctx, m) => engines.GetValue(
                            m, mm => new AnalyticRoughHestonEngine(mm, 64, 256, AnalyticRoughHestonEngineApproximation.Pade)).Calculate(option, ctx)));
                }
            }

            RoughHestonModel calibrated = model.Calibrate(
                helpers, new LevenbergMarquardt(1e-8, 1e-8, 1e-8), new EndCriteria(400, 40, 1e-8, 1e-8, 1e-8), context).Model;
            check.Output(c, "params", Kept(c, "params", calibrated.Params().Select(x => (Func<object?>)(() => x))).ToArray());
            check.Output(
                c,
                "errors",
                Kept(c, "errors", helpers.Select(helper => (Func<object?>)(() => helper.CalibrationError(calibrated, context)))).ToArray());
        }

        check.Complete();
    }

    // A model spec: its process (HestonProcessVectorTests' spec) and its Hurst exponent
    internal static RoughHestonModel ModelOf(JsonElement spec) =>
        new(HestonProcessVectorTests.HestonProcessOf(spec.GetProperty("process")), spec.GetProperty("hurst").GetDouble());
}
