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
/// The Cox-Ingersoll-Ross model's Feller constraint (K1): QuantLib's is σ's own <c>VolatilityConstraint</c>, built at
/// the constructor's k and θ (<c>coxingersollross.cpp:26-40</c>, <c>:52</c>), so a calibration that moves k or θ tests
/// σ against values the model no longer has, and can end outside the Feller region: on the wheel, from (r0, θ, k, σ) =
/// (0.03, 0.035, 0.3, 0.05), Levenberg-Marquardt on the six swaption helpers of
/// <c>tools/vectors/gen/models/model.py</c>'s 2025 set ends at θ = 536.3132523984605, k = 5.407661646569119e-07, σ =
/// 0.033099933008455235, r0 = 0.02927429342689504, where σ² = 1.10e-3 exceeds 2kθ = 5.80e-4. nanoql tests σ² &lt; 2kθ
/// on each trial vector's own k and θ, through the model's parameter constraint. QuantLib's check: the test equals
/// QuantLib's constraint built at each vector's k and θ; and a calibration with k and θ fixed, where the two tests
/// agree at every trial, is QuantLib's to the last bit.
/// </summary>
public class CirFellerConstraintTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void TheTestIsQuantLibsConstraintAtEachVectorsSpeedAndLevel()
    {
        Constraint constraint = new CoxIngersollRoss(0.03, 0.035, 0.3, 0.05).Constraint;
        foreach (double theta in new[] { 0.01, 0.035, 0.1 })
        {
            foreach (double k in new[] { 0.05, 0.3, 2.0 })
            {
                foreach (double sigma in new[] { 0.01, 0.05, 0.1, 0.3 })
                {
                    // QuantLib's VolatilityConstraint(k, theta) on sigma, and PositiveConstraint on theta, k and r0
                    bool quantLib = sigma > 0.0 && sigma * sigma < 2.0 * k * theta;
                    Assert.Equal(quantLib, constraint.Test(new Vector([theta, k, sigma, 0.03])));
                }
            }
        }
    }

    [Fact]
    public void ACalibrationWithTheSpeedAndLevelFixedIsQuantLibs()
    {
        CalibrationResults<CoxIngersollRoss> result = new CoxIngersollRoss(0.03, 0.035, 0.3, 0.05)
            .Calibrate(Helpers(), Method(), Criteria(), Context, new NoConstraint(), [], [true, true, false, false]);
        Vector p = result.Model.Params();
        Assert.Equal([0.035, 0.3, 0.12536795457135252, 0.03615517511820093], (double[])[p[0], p[1], p[2], p[3]]);
        Assert.Equal(EndCriteriaType.StationaryFunctionValue, result.EndCriteria);
        Assert.Equal(17, result.FunctionEvaluation);
    }

    [Fact]
    public void AFreeCalibrationStaysInTheFellerRegion()
    {
        Vector p = new CoxIngersollRoss(0.03, 0.035, 0.3, 0.05).Calibrate(Helpers(), Method(), Criteria(), Context).Model.Params();
        double theta = p[0], k = p[1], sigma = p[2];
        Assert.True(sigma * sigma < 2.0 * k * theta, $"theta {theta}, k {k}, sigma {sigma}");
    }

    // model.py's 2025 set: Euribor 6M forecasting on a flat 3.1%, the helpers on a flat 2.7%, each priced by the
    // Jamshidian engine on the model and the helpers' curve
    private static List<CalibrationHelper> Helpers()
    {
        var discount = new FlatForward(Today, 0.027, new Actual365Fixed());
        var index = new Euribor6M(new FlatForward(Today, 0.031, new Actual365Fixed()));
        var helpers = new List<CalibrationHelper>();
        foreach ((int option, int length, double vol) in new[] { (1, 5, 0.22), (2, 4, 0.21), (3, 3, 0.20), (5, 2, 0.19), (7, 3, 0.185), (10, 5, 0.18) })
        {
            helpers.Add(new SwaptionHelper(
                    new Period(option, TimeUnit.Years),
                    new Period(length, TimeUnit.Years),
                    vol,
                    index,
                    new Period(1, TimeUnit.Years),
                    new Thirty360(Thirty360Convention.BondBasis),
                    new Actual360(),
                    discount,
                    Context)
                .SetPricingEngine<OneFactorAffineModel>((s, c, m) => JamshidianSwaptionEngine.Calculate(s, c, m, discount)));
        }

        return helpers;
    }

    private static LevenbergMarquardt Method() => new(1e-8, 1e-8, 1e-8);

    private static EndCriteria Criteria() => new(10000, 100, 1e-6, 1e-8, 1e-8);
}
