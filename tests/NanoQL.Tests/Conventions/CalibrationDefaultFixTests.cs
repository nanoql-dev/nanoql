// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The calibration's default fixed parameters (the S-g1d plan's G1R2 (ii); master plan §2.2's calibration row):
/// <see cref="ICalibratedModel.DefaultFixParameters"/> is empty by default, QuantLib's, for every landed model; a model
/// that gives one, as QuantLib's Markov-functional model overrides <c>calibrate</c> to fix its first volatility, has it
/// read by <c>Calibrate</c> when the caller fixes nothing, and a caller's own flags win. The Markov-functional model
/// gives its first volatility fixed.
/// </summary>
public class CalibrationDefaultFixTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void EveryLandedModelFixesNothingByDefault()
    {
        ICalibratedModel[] models =
        [
            new HullWhite(Curve), new Vasicek(), new BlackKarasinski(Curve), new CoxIngersollRoss(), new ExtendedCoxIngersollRoss(Curve), new G2(Curve),
            Gaussian1dInputs.Constant(Curve, 0.01, 0.01), new HestonModel(new HestonProcess(Curve, Curve, 100.0, 0.04, 1.5, 0.04, 0.3, -0.6)),
        ];
        Assert.All(models, m => Assert.True(m.DefaultFixParameters.IsEmpty, m.GetType().Name));
    }

    [Fact]
    public void TheMarkovFunctionalModelFixesItsFirstVolatility()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        var target = new TARGET();
        Date[] steps = [.. new[] { 1, 2, 3 }.Select(i => target.Advance(Today, i, TimeUnit.Years))];
        Date[] expiries = [.. Enumerable.Range(1, 5).Select(i => target.Advance(Today, i, TimeUnit.Years))];
        var model = new MarkovFunctional(
            Curve, 0.01, steps, [1.0, 1.0, 1.0, 1.0],
            new ConstantSwaptionVolatility(Today, target, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed()), expiries,
            [.. Enumerable.Repeat(new Period(10, TimeUnit.Years), 5)], new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years)), context,
            new MarkovFunctionalModelSettings(32, 7.0, 16, 1e-5, 1e-7, 0.0, 2.0, MarkovFunctionalModelSettingsAdjustments.KahaleSmile));
        Assert.Equal([true, false, false, false], model.DefaultFixParameters);
        Assert.Equal(model.FixedFirstVolatility(), model.DefaultFixParameters);
    }

    [Fact]
    public void CalibrateReadsTheModelsDefaultWhenTheCallerFixesNothing()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        SwaptionHelper[] helpers = [.. new (int O, int L, double V)[] { (1, 4, 0.20), (2, 3, 0.19), (3, 2, 0.18) }.Select(h =>
            new SwaptionHelper(
                    new Period(h.O, TimeUnit.Years), new Period(h.L, TimeUnit.Years), h.V, new Euribor6M(Curve), new Period(1, TimeUnit.Years),
                    new Thirty360(Thirty360Convention.BondBasis), new Actual360(), Curve, context)
                .SetPricingEngine<FixedReversionHullWhite>((s, c, m) => JamshidianSwaptionEngine.Calculate(s, c, m.Inner)))];
        var model = new FixedReversionHullWhite(new HullWhite(Curve, 0.05, 0.01));
        var method = new LevenbergMarquardt(1e-8, 1e-8, 1e-8);
        var criteria = new EndCriteria(10000, 100, 1e-6, 1e-8, 1e-8);

        CalibrationResults<FixedReversionHullWhite> byDefault = model.Calibrate(helpers, method, criteria, context);
        CalibrationResults<FixedReversionHullWhite> explicitly = model.Calibrate(helpers, method, criteria, context, null, default, [true, false]);
        Assert.Equal(0.05, byDefault.Model.Params()[0]);
        Assert.Equal(explicitly.Model.Params(), byDefault.Model.Params());

        // The caller's own flags win: both free.
        CalibrationResults<FixedReversionHullWhite> free = model.Calibrate(helpers, method, criteria, context, null, default, [false, false]);
        Assert.NotEqual(0.05, free.Model.Params()[0]);
    }

    // A Hull-White model whose calibration fixes its reversion by default.
    private sealed class FixedReversionHullWhite(HullWhite inner) : ICalibratedModel
    {
        public HullWhite Inner { get; } = inner;

        public ImmutableArray<Parameter> Arguments => Inner.Arguments;

        public Constraint Constraint => Inner.Constraint;

        public ImmutableArray<bool> DefaultFixParameters => [true, false];

        public Vector Params() => Inner.Params();

        public ICalibratedModel WithParams(Vector parameters) => new FixedReversionHullWhite((HullWhite)Inner.WithParams(parameters));
    }
}
