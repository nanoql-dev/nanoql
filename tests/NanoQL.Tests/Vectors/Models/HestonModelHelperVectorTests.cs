// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The HestonModelHelper vectors, <c>tests/vectors/models/equity/hestonmodelhelper.json</c>: helpers over three
/// maturities, strikes, two volatilities and the three error types, their market values, volatilities, Black prices,
/// and their model values, calibration errors and implied volatilities against a Heston model priced by the helper's
/// default engine, the analytic one.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HestonModelHelperVectorTests
{
    [Fact]
    public void HestonModelHelperVector()
    {
        var file = VectorFile.Load("models/equity/hestonmodelhelper");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("helper");
            var helper = new HestonModelHelper(
                SwapInputs.PeriodOf(spec.GetProperty("maturity")),
                new TARGET(),
                spec.GetProperty("s0").GetDouble(),
                spec.GetProperty("strike").GetDouble(),
                spec.GetProperty("volatility").GetDouble(),
                CurveOf(spec.GetProperty("riskFree")),
                CurveOf(spec.GetProperty("dividend")),
                context,
                (BlackCalibrationHelperCalibrationErrorType)spec.GetProperty("errorType").GetInt32());
            JsonElement m = c.Input("model");
            var model = new HestonModel(new HestonProcess(
                CurveOf(spec.GetProperty("riskFree")),
                CurveOf(spec.GetProperty("dividend")),
                100.0,
                m.GetProperty("v0").GetDouble(),
                m.GetProperty("kappa").GetDouble(),
                m.GetProperty("theta").GetDouble(),
                m.GetProperty("sigma").GetDouble(),
                m.GetProperty("rho").GetDouble()));

            check.Output(c, "marketValue", helper.MarketValue);
            check.Output(c, "volatility", helper.Volatility);
            check.Output(c, "volatilityType", (int)helper.VolatilityType);
            check.Output(c, "blackPrice", Doubles(c.Input("blackVols")).Select(v => (object?)helper.BlackPrice(v, context)).ToArray());
            check.Output(c, "modelValue", Lazy(() => helper.ModelValue(model, context)));
            check.Output(c, "calibrationError", Lazy(() => helper.CalibrationError(model, context)));
            check.Output(
                c,
                "impliedVolatility",
                Lazy(() => helper.ImpliedVolatility(c.Double("impliedTarget"), 1e-12, 500, 0.001, 4.0, context)));
        }

        check.Complete();
    }
}
