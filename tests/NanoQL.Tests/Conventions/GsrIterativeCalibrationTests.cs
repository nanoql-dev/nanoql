// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// GSR's iterative calibrations: QuantLib's run one calibration per helper on the model, each moving one parameter, and
/// the model then reports the last calibration's end criteria and values (<c>gsr.hpp:104-137</c>); here each returns
/// the last step's <see cref="CalibrationResults{TModel}"/>, its model the final one, the steps' own calibrations run
/// in turn on the model the step before returned; an empty set of helpers, which leaves QuantLib's model and record as
/// they were, has no record to return and is refused.
/// </summary>
public class GsrIterativeCalibrationTests
{
    [Fact]
    public void EachReturnsTheLastStepsRecordAndModel()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var index = new Euribor6M(curve);
        var calendar = new TARGET();
        SwaptionHelper[] helpers = [.. new (int Option, double Vol)[] { (1, 0.20), (2, 0.19), (3, 0.18) }.Select(h =>
            new SwaptionHelper(
                    new Period(h.Option, TimeUnit.Years), new Period(4 - h.Option, TimeUnit.Years), h.Vol, index, new Period(1, TimeUnit.Years),
                    new Thirty360(Thirty360Convention.BondBasis), new Actual360(), curve, context)
                .SetPricingEngine<Gsr>((s, c, m) => Gaussian1dSwaptionEngine.Calculate(s, c, m)))];
        Date[] steps = [calendar.Advance(today, 1, TimeUnit.Years), calendar.Advance(today, 2, TimeUnit.Years)];
        var model = new Gsr(curve, steps, [0.006, 0.006, 0.006], [0.02, 0.03, 0.04]);
        var method = new LevenbergMarquardt(1e-8, 1e-8, 1e-8);
        var criteria = new EndCriteria(10000, 100, 1e-6, 1e-8, 1e-8);

        foreach ((Func<Gsr, CalibrationResults<Gsr>> iterative, Func<Gsr, int, System.Collections.Immutable.ImmutableArray<bool>> move) in
            new (Func<Gsr, CalibrationResults<Gsr>>, Func<Gsr, int, System.Collections.Immutable.ImmutableArray<bool>>)[]
            {
                (m => m.CalibrateVolatilitiesIterative(helpers, method, criteria, context), (m, i) => m.MoveVolatility(i)),
                (m => m.CalibrateReversionsIterative(helpers, method, criteria, context), (m, i) => m.MoveReversion(i)),
            })
        {
            CalibrationResults<Gsr> result = iterative(model);
            Gsr byHand = model;
            CalibrationResults<Gsr>? last = null;
            for (int i = 0; i < helpers.Length; i++)
            {
                last = byHand.Calibrate([helpers[i]], method, criteria, context, null, default, move(byHand, i).AsSpan());
                byHand = last.Model;
            }

            Assert.Equal(byHand.Params(), result.Model.Params());
            Assert.Equal(last!.EndCriteria, result.EndCriteria);
            Assert.Equal(last.FunctionEvaluation, result.FunctionEvaluation);
            Assert.Equal(last.ProblemValues, result.ProblemValues);
        }

        // The receiver doesn't change; no helper is refused.
        Assert.Equal(new Vector([0.02, 0.03, 0.04, 0.006, 0.006, 0.006]), model.Params());
        ArgumentException none = Assert.Throws<ArgumentException>(() => model.CalibrateVolatilitiesIterative([], method, criteria, context));
        Assert.Equal("helpers", none.ParamName);
        Assert.StartsWith("no instruments provided", none.Message, StringComparison.Ordinal);
    }
}
