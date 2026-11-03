// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A Heston calibration returns a new model, with QuantLib's record of the calibration, and leaves the receiver as it
/// was: QuantLib's writes each trial's parameters, and then the result, into the model its helpers' engines hold, and
/// rebuilds the model's process from them (<c>hestonmodel.cpp:45-50</c>). Here each trial is a copy whose process the
/// copy's parameters build, so the calibrated model's process carries its parameters and the given process's curves and
/// spot.
/// </summary>
public class HestonCalibrationTests
{
    [Fact]
    public void ACalibrationReturnsANewModelAndLeavesTheReceiver()
    {
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        YieldTermStructure riskFree = new FlatForward(today, 0.03, dc);
        YieldTermStructure dividends = new FlatForward(today, 0.01, dc);
        HestonModelHelper[] helpers =
        [
            .. new[] { 3, 6, 12 }.SelectMany(months => new[] { 90.0, 100.0, 110.0 }.Select(strike => new HestonModelHelper(
                new Period(months, TimeUnit.Months), new NullCalendar(), 100.0, strike, 0.2 + ((100.0 - strike) * 0.001), riskFree, dividends, context))),
        ];
        var process = new HestonProcess(riskFree, dividends, 100.0, 0.05, 1.0, 0.05, 0.4, -0.5);
        var model = new HestonModel(process);
        Vector before = model.Params();
        HestonProcess modelProcess = model.Process;

        CalibrationResults<HestonModel> result = model.Calibrate(
            helpers, new LevenbergMarquardt(), new EndCriteria(200, 40, 1e-8, 1e-8, 1e-8), context);

        // the receiver and its process unchanged; the model built its own process at construction, as QuantLib's
        Assert.Equal(before.ToArray(), model.Params().ToArray());
        Assert.Same(modelProcess, model.Process);
        Assert.NotSame(process, model.Process);
        Assert.Equal(0.05, model.Process.V0);

        // a new model, its process built from its parameters on the given curves and spot
        HestonModel calibrated = result.Model;
        Assert.NotSame(model, calibrated);
        Assert.NotEqual(before.ToArray(), calibrated.Params().ToArray());
        Assert.Equal(
            [calibrated.Theta, calibrated.Kappa, calibrated.Sigma, calibrated.Rho, calibrated.V0],
            new[] { calibrated.Process.Theta, calibrated.Process.Kappa, calibrated.Process.Sigma, calibrated.Process.Rho, calibrated.Process.V0 });
        Assert.Same(riskFree, calibrated.Process.RiskFreeRate);
        Assert.Same(dividends, calibrated.Process.DividendYield);
        Assert.Equal(100.0, calibrated.Process.S0);

        // QuantLib's record: one problem value per helper, and the evaluations counted
        Assert.Equal(helpers.Length, result.ProblemValues.Length);
        Assert.True(result.FunctionEvaluation > 0);
        double cost = Math.Sqrt(helpers.Sum(h => Square(h.CalibrationError(calibrated, context))));
        Assert.Equal(cost, Math.Sqrt(result.ProblemValues.Sum(Square)), 1e-12);
        Assert.Equal(cost, calibrated.Value(calibrated.Params(), helpers, context), 1e-12);
    }

    private static double Square(double x) => x * x;
}
