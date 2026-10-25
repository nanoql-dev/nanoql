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

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The PiecewiseTimeDependentHestonModel vectors, <c>tests/vectors/models/equity/piecewisetimedependenthestonmodel.json</c>:
/// three models' parameters at eight times, v₀, s₀, the time grid and the parameter vector; and a calibration to twenty
/// Heston helpers through the analytic time-dependent engine, its parameters and errors where the stability filter keeps
/// them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PiecewiseTimeDependentHestonModelVectorTests
{
    [Fact]
    public void PiecewiseTimeDependentHestonModelVector()
    {
        var file = VectorFile.Load("models/equity/piecewisetimedependenthestonmodel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            PiecewiseTimeDependentHestonModel model = ModelOf(c.Input("model"));
            if (c.Input("kind").GetString() == "members")
            {
                double[] times = Doubles(c.Input("times"));
                check.Output(c, "theta", times.Select(t => (object?)model.Theta(t)).ToArray());
                check.Output(c, "kappa", times.Select(t => (object?)model.Kappa(t)).ToArray());
                check.Output(c, "sigma", times.Select(t => (object?)model.Sigma(t)).ToArray());
                check.Output(c, "rho", times.Select(t => (object?)model.Rho(t)).ToArray());
                check.Output(c, "v0", model.V0);
                check.Output(c, "s0", model.S0);
                check.Output(c, "timeGrid", Enumerable.Range(0, model.TimeGrid.Size).Select(i => (object?)model.TimeGrid[i]).ToArray());
                check.Output(c, "params", model.Params().Select(x => (object?)x).ToArray());
                continue;
            }

            JsonElement h = c.Input("helpers");
            double[] vols = Doubles(h.GetProperty("vols"));
            double[] strikes = Doubles(h.GetProperty("strikes"));
            var helpers = new List<HestonModelHelper>();
            int i = 0;
            foreach (JsonElement days in h.GetProperty("maturities").EnumerateArray())
            {
                foreach (double k in strikes)
                {
                    helpers.Add(new HestonModelHelper(
                            new Period(days.GetInt32(), TimeUnit.Days),
                            new NullCalendar(),
                            model.S0,
                            k,
                            vols[i++],
                            model.RiskFreeRate,
                            model.DividendYield,
                            context,
                            BlackCalibrationHelperCalibrationErrorType.ImpliedVolError)
                        .SetPricingEngine<PiecewiseTimeDependentHestonModel>(
                            (option, ctx, m) => AnalyticPTDHestonEngine.Calculate(option, ctx, m, 144)));
                }
            }

            PiecewiseTimeDependentHestonModel calibrated = model.Calibrate(
                helpers, new LevenbergMarquardt(1e-8, 1e-8, 1e-8), new EndCriteria(400, 40, 1e-8, 1e-8, 1e-8), context).Model;
            check.Output(c, "params", Kept(c, "params", calibrated.Params().Select(x => (Func<object?>)(() => x))).ToArray());
            check.Output(
                c,
                "errors",
                Kept(c, "errors", helpers.Select(helper => (Func<object?>)(() => helper.CalibrationError(calibrated, context)))).ToArray());
        }

        check.Complete();
    }

    // A model spec: its curves, spot and v0, its grid's mandatory times, its parameters' step times and their values,
    // one value a constant parameter
    internal static PiecewiseTimeDependentHestonModel ModelOf(JsonElement spec)
    {
        double[] pTimes = Doubles(spec.GetProperty("pTimes"));

        Parameter ParameterOf(string name, Constraint constraint)
        {
            double[] values = Doubles(spec.GetProperty(name));
            if (values.Length == 1)
            {
                return new ConstantParameter(values[0], constraint);
            }

            Parameter p = new PiecewiseConstantParameter(pTimes, constraint);
            for (int i = 0; i < values.Length; ++i)
            {
                p = p.SetParam(i, values[i]);
            }

            return p;
        }

        return new PiecewiseTimeDependentHestonModel(
            CurveOf(spec.GetProperty("riskFree")),
            CurveOf(spec.GetProperty("dividend")),
            spec.GetProperty("s0").GetDouble(),
            spec.GetProperty("v0").GetDouble(),
            ParameterOf("theta", new PositiveConstraint()),
            ParameterOf("kappa", new PositiveConstraint()),
            ParameterOf("sigma", new PositiveConstraint()),
            ParameterOf("rho", new BoundaryConstraint(-1.0, 1.0)),
            new TimeGrid(Doubles(spec.GetProperty("grid"))));
    }
}
