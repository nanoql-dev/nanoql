// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// GSR's iterative calibrations with weights: QuantLib's <c>calibrateVolatilitiesIterative</c> passes the whole weight
/// vector to each one-helper step (<c>gsr.hpp:116-120</c>), so every weight vector of more than one weight is refused:
/// "mismatch between number of instruments (1) and weights (3)". nanoql weights each step by its own helper's weight.
/// QuantLib's check: its one-helper calibration with that weight, step by step, which on the wheel ends at the
/// parameters (0.02, 0.006141458164186859, 0.005500939169047698, 0.004884008618675642), its last step's end criterion 3
/// (stationary function) after 12 evaluations.
/// </summary>
public class GsrIterativeWeightsTests
{
    [Fact]
    public void EachStepIsWeightedByItsOwnHelpersWeight()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today, UseAtParCoupons = true };
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var index = new Euribor6M(curve);
        var calendar = new TARGET();
        SwaptionHelper[] helpers = [.. new (int Option, double Vol)[] { (1, 0.20), (2, 0.19), (3, 0.18) }.Select(h =>
            new SwaptionHelper(
                    new Period(h.Option, TimeUnit.Years), new Period(4 - h.Option, TimeUnit.Years), h.Vol, index, new Period(1, TimeUnit.Years),
                    new Thirty360(Thirty360Convention.BondBasis), new Actual360(), curve, context)
                .SetPricingEngine<Gsr>((s, c, m) => Gaussian1dSwaptionEngine.Calculate(s, c, m)))];
        Date[] steps = [calendar.Advance(today, 1, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing), calendar.Advance(today, 2, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing)];
        var model = new Gsr(curve, steps, [0.006, 0.006, 0.006], 0.02);

        CalibrationResults<Gsr> result = model.CalibrateVolatilitiesIterative(
            helpers, new LevenbergMarquardt(1e-8, 1e-8, 1e-8), new EndCriteria(10000, 100, 1e-6, 1e-8, 1e-8), context, null, [1.0, 2.0, 0.5]);

        double[] expected = [0.02, 0.006141458164186859, 0.005500939169047698, 0.004884008618675642];
        Vector calibrated = result.Model.Params();
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(Math.Abs((calibrated[i] / expected[i]) - 1.0) <= 1e-9, $"{i}: {calibrated[i]} against {expected[i]}");
        }

        Assert.Equal(EndCriteriaType.StationaryFunctionValue, result.EndCriteria);
    }
}
