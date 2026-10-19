// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The refusals of calibration, its projection, the swaption helper, and the Jamshidian and tree swaption engines with
/// their discretized assets, by type, message and parameter (playbook §2), which the vectors record only as throws: a
/// check of an argument is <see cref="ArgumentException"/> or <see cref="ArgumentOutOfRangeException"/> naming it, a
/// member's own argument named as its own parameter, and a check of a value already stored, or of the instrument an
/// engine prices, <see cref="NanoQLException"/>; the undefined paths' refusals among them.
/// </summary>
public class CalibrationErrorTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.February, 2002) };
    private static readonly YieldTermStructure Curve =
        new FlatForward(new Date(19, Month.February, 2002), 0.04875825, new Actual365Fixed());

    [Fact]
    public void TheCalibrationAndSwaptionRefusals()
    {
        CalibrationRefusals();
        ProjectionRefusals();
        SwaptionHelperRefusals();
        SwaptionEngineRefusals();
        DiscretizedSwapRefusals();
    }

    // QuantLib's checks in their order (jamshidianswaptionengine.cpp:93-119): the settlement method, the exercise, the
    // spread, the nominals, then the curve, each refusal's text the first failing check's.
    [Fact]
    public void TheJamshidianEnginesChecksComeInQuantLibsOrder()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        var curve = new FlatForward(context.EvaluationDate, 0.027, new Actual365Fixed());
        var model = new HullWhite(curve, 0.05, 0.01);
        MakeVanillaSwap builder = new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(curve))
            .WithFixedRate(0.03)
            .WithForwardStart(new Period(1, TimeUnit.Years));
        VanillaSwap plain = builder.ToVanillaSwap(context);
        VanillaSwap spread = builder.WithFloatingLegSpread(0.001).ToVanillaSwap(context);
        double[] amortizing = [1e6, 9e5, 8e5, 7e5, 6e5];
        var notConstant = new OvernightIndexedSwap(
            SwapType.Payer, amortizing, plain.FixedSchedule, 0.03, new Actual360(), new Estr(curve));
        var notConstantWithSpread = new OvernightIndexedSwap(
            SwapType.Payer, amortizing, plain.FixedSchedule, 0.03, new Actual360(), new Estr(curve), 0.002);
        Date exercise = plain.FixedSchedule.Dates[0] - 2;
        var european = new EuropeanExercise(exercise);
        var bermudan = new BermudanExercise([exercise, exercise + 365]);

        string Refusal(Swaption swaption, OneFactorAffineModel m) =>
            Assert.Throws<NanoQLException>(() => JamshidianSwaptionEngine.Calculate(swaption, context, m)).Message;

        const string cash = "cash settled (ParYieldCurve) swaptions not priced with JamshidianSwaptionEngine";
        const string exotic = "cannot use the Jamshidian decomposition on exotic swaptions";
        Assert.Equal(cash, Refusal(new Swaption(spread, bermudan, SettlementType.Cash, SettlementMethod.ParYieldCurve), model));
        Assert.Equal(exotic, Refusal(new Swaption(spread, bermudan), model));
        Assert.Equal("non zero spread (0.002) not allowed", Refusal(new Swaption(notConstantWithSpread, european), model));
        Assert.Equal("non zero spread (0.001) not allowed", Refusal(new Swaption(spread, european), model));
        Assert.Equal("non-constant nominals are not supported yet", Refusal(new Swaption(notConstant, european), model));

        // The curve last: a model with none, its swaption refused first for its terms.
        Assert.Equal(exotic, Refusal(new Swaption(plain, bermudan), new Vasicek()));
        Assert.Equal("non zero spread (0.001) not allowed", Refusal(new Swaption(spread, european), new Vasicek()));
        Assert.Equal("empty Handle cannot be dereferenced", Refusal(new Swaption(plain, european), new Vasicek()));
    }

    private static void CalibrationRefusals()
    {
        var model = new HullWhite(Curve);
        List<CalibrationHelper> helpers = [Helper(1, 5, 0.1148), Helper(2, 4, 0.1108)];
        var method = new LevenbergMarquardt();
        var endCriteria = new EndCriteria(100, 10, 1e-8, 1e-8, 1e-8);

        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => model.Calibrate(helpers, method, endCriteria, null!)).ParamName);
        Expect<ArgumentException>(() => model.Calibrate([], method, endCriteria, Context), "helpers", "no instruments provided");
        Expect<ArgumentException>(
            () => model.Calibrate(helpers, method, endCriteria, Context, weights: [1.0]),
            "weights",
            "mismatch between number of instruments (2) and weights (1)");
        Expect<ArgumentException>(
            () => model.Calibrate(helpers, method, endCriteria, Context, fixParameters: [true, false, false]),
            "fixParameters",
            "mismatch between number of parameters (2) and fixed-parameter specs (3)");

        // QuantLib checks the weights before the fixed parameters.
        Expect<ArgumentException>(
            () => model.Calibrate(helpers, method, endCriteria, Context, null, [1.0], [true]),
            "weights",
            "mismatch between number of instruments (2) and weights (1)");
        Expect<ArgumentException>(
            () => model.Calibrate(helpers, method, endCriteria, Context, fixParameters: [true, true]),
            "fixParameters",
            "numberOfFreeParameters==0");
        Expect<ArgumentException>(
            () => model.Calibrate([helpers[0], null!], method, endCriteria, Context), "helpers", "null calibration helper");
        Expect<NanoQLException>(
            () => model.Calibrate([((SwaptionHelper)helpers[0]).SetPricingEngine<HullWhite>(null)], method, endCriteria, Context),
            null,
            "null pricing engine");

        // A model with no parameters: QuantLib's projection's refusal, of the model.
        Expect<ArgumentException>(
            () => new NoParameters().Calibrate(helpers, method, endCriteria, Context), "model", "numberOfFreeParameters==0");

        Expect<ArgumentException>(() => model.Value([], helpers, Context), "parameters", "numberOfFreeParameters==0");
        Expect<ArgumentException>(() => model.Value([0.1], helpers, Context), "parameters", "parameter array too small");
        Expect<ArgumentException>(() => model.Value([0.1, 0.01], [null!], Context), "helpers", "null calibration helper");
    }

    private static void ProjectionRefusals()
    {
        Expect<ArgumentException>(
            () => new Projection([1.0, 2.0], [true]), "fixParameters", "fixedParameters_.size()!=parametersFreedoms_.size()");
        Expect<ArgumentException>(() => new Projection([1.0, 2.0], [true, true]), "fixParameters", "numberOfFreeParameters==0");
        Expect<ArgumentException>(() => new Projection([]), "parameterValues", "numberOfFreeParameters==0");
        var projection = new Projection([1.0, 2.0, 3.0], [false, true, false]);
        Expect<ArgumentException>(() => projection.Project([1.0, 2.0]), "parameters", "parameters.size()!=parametersFreedoms_.size()");
        Expect<ArgumentException>(
            () => projection.Include([1.0]), "projectedParameters", "projectedParameters.size()!=numberOfFreeParameters");

        // The projected constraint's refusals are its projection's.
        var constraint = new ProjectedConstraint(new NoConstraint(), projection);
        Expect<ArgumentException>(
            () => constraint.Test([1.0]), "projectedParameters", "projectedParameters.size()!=numberOfFreeParameters");
        Assert.Equal("constraint", Assert.Throws<ArgumentNullException>(() => new ProjectedConstraint(null!, projection)).ParamName);
    }

    private static void SwaptionHelperRefusals()
    {
        Expect<ArgumentNullException>(() => Helper(1, 5, 0.2, nullIndex: true), "index", null);
        Expect<ArgumentOutOfRangeException>(
            () => Helper(1, 5, 0.2, errorType: (BlackCalibrationHelperCalibrationErrorType)7), "errorType", "unknown Calibration Error Type");
        Expect<ArgumentOutOfRangeException>(
            () => Helper(1, 5, 0.2, type: (VolatilityType)7), "type", "can not construct engine: Unknown volatility type (7)");
        Expect<ArgumentOutOfRangeException>(() => Helper(1, 5, 0.2, settlementDays: -1), "settlementDays", null);

        SwaptionHelper helper = Helper(1, 5, 0.1148);
        Expect<NanoQLException>(() => helper.ModelValue(new HullWhite(Curve), Context), null, "null pricing engine");
        Expect<ArgumentOutOfRangeException>(
            () => helper.ImpliedVolatility(0.01, 1e-10, -1, 0.001, 1.0, Context), "maxEvaluations", null);
        Expect<ArgumentException>(
            () => helper.ImpliedVolatility(0.01, 1e-10, 100, 1.0, 0.5, Context), "minVol", "invalid range: xMin_ (1) >= xMax_ (0.5)");
        Expect<ArgumentException>(
            () => helper.ImpliedVolatility(0.01, 0.0, 100, 0.001, 1.0, Context), "accuracy", "accuracy (0) must be positive");

        // The guess is the quoted volatility: the solver's own refusal, once the bracket holds the target.
        double target = helper.BlackPrice(0.5, Context);
        Expect<ArgumentException>(
            () => helper.ImpliedVolatility(target, 1e-10, 100, 0.25, 1.0, Context), "guess", "guess (0.1148) < xMin_ (0.25)");
        Expect<NanoQLException>(() => helper.ImpliedVolatility(10.0, 1e-10, 100, 0.01, 1.0, Context), null, "root not bracketed");
    }

    private static void SwaptionEngineRefusals()
    {
        Swaption swaption = Helper(1, 5, 0.1148).Swaption;
        var cash = new Swaption(swaption.Underlying, swaption.Exercise, SettlementType.Cash, SettlementMethod.ParYieldCurve);
        var bermudan = new Swaption(
            swaption.Underlying, new BermudanExercise([swaption.Exercise.Date(0), swaption.Exercise.Date(0) + 365]));
        var model = new HullWhite(Curve);

        Expect<NanoQLException>(
            () => JamshidianSwaptionEngine.Calculate(cash, Context, model),
            null,
            "cash settled (ParYieldCurve) swaptions not priced with JamshidianSwaptionEngine");
        Expect<NanoQLException>(
            () => JamshidianSwaptionEngine.Calculate(bermudan, Context, model), null, "cannot use the Jamshidian decomposition on exotic swaptions");
        Expect<NanoQLException>(
            () => JamshidianSwaptionEngine.Calculate(swaption, Context, new Vasicek()), null, "empty Handle cannot be dereferenced");
        Assert.Equal("model", Assert.Throws<ArgumentNullException>(() => JamshidianSwaptionEngine.Calculate(swaption, Context, null!)).ParamName);

        Expect<ArgumentException>(
            () => TreeSwaptionEngine.Calculate(swaption, Context, model, 0), "timeSteps", "timeSteps must be positive, 0 not allowed");
        Assert.Equal(
            "timeSteps",
            Assert.Throws<ArgumentOutOfRangeException>(() => TreeSwaptionEngine.Calculate(swaption, Context, model, -1)).ParamName);
        Expect<NanoQLException>(
            () => TreeSwaptionEngine.Calculate(cash, Context, model, 10),
            null,
            "cash settled (ParYieldCurve) swaptions not priced with TreeSwaptionEngine");
        Expect<NanoQLException>(
            () => TreeSwaptionEngine.Calculate(swaption, Context, new Vasicek(), 10), null, "empty Handle cannot be dereferenced");
        Assert.Equal("model", Assert.Throws<ArgumentNullException>(() => TreeSwaptionEngine.Calculate(swaption, Context, null!, 10)).ParamName);

        // No exercise time on or after the reference date, on a lattice that initializes the swaption at a negative
        // time, where QuantLib reads past its exercise times.
        var later = new FlatForward(swaption.Exercise.Date(0) + 7, 0.03, new Actual365Fixed());
        var laterModel = new HullWhite(later);
        var times = new DiscretizedSwaption(swaption, later.ReferenceDate, later.DayCounter, Context).MandatoryTimes();
        Expect<NanoQLException>(
            () => TreeSwaptionEngine.Calculate(
                swaption, Context, laterModel, new Divergences.TreeSwaptionEngineExerciseTimeTests.AnyTimeLattice(new TimeGrid(times.AsSpan(), 10))),
            null,
            "no exercise time on or after the reference date");

        // The discretized swaption reset before it is initialized: QuantLib initializes its underlying on its null
        // lattice, as its other members dereference it (MD13).
        var asset = new DiscretizedSwaption(swaption, Curve.ReferenceDate, Curve.DayCounter, Context);
        Expect<NanoQLException>(() => asset.Reset(3), null, "asset not initialized");
    }

    private static void DiscretizedSwapRefusals()
    {
        // A floating coupon reset before the reference date, its fixing missing, is refused at its payment.
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        var curve = new FlatForward(context.EvaluationDate, 0.03, new Actual365Fixed());
        VanillaSwap swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(curve))
            .WithFixedRate(0.03)
            .WithEffectiveDate(new Date(15, Month.October, 2024))
            .ToVanillaSwap(context);
        var asset = new DiscretizedSwap(swap, curve.ReferenceDate, curve.DayCounter, context);
        double[] times = [.. asset.MandatoryTimes()];
        asset.Initialize(new HullWhite(curve).Tree(new TimeGrid(times, 10)), times.Max());
        Expect<NanoQLException>(() => asset.Rollback(0.0), null, "current floating coupon not given");

        Assert.Equal(
            "swap",
            Assert.Throws<ArgumentNullException>(() => new DiscretizedSwap(null!, curve.ReferenceDate, curve.DayCounter, context)).ParamName);
        Assert.Equal(
            "referenceDate",
            Assert.Throws<ArgumentException>(() => new DiscretizedSwap(swap, default, curve.DayCounter, context)).ParamName);
    }

    // Asserts the refusal's exact type, its parameter (none for a NanoQLException) and the start of its message.
    private static void Expect<T>(Func<object?> f, string? paramName, string? text)
        where T : Exception => Check(Assert.Throws<T>(f), paramName, text);

    private static void Expect<T>(Action f, string? paramName, string? text)
        where T : Exception => Check(Assert.Throws<T>(f), paramName, text);

    private static void Check<T>(T e, string? paramName, string? text)
        where T : Exception
    {
        Assert.Equal(typeof(T), e.GetType());
        if (e is ArgumentException a)
        {
            Assert.Equal(paramName, a.ParamName);
        }

        if (text is not null)
        {
            Assert.StartsWith(text, e.Message, StringComparison.Ordinal);
        }
    }

    private static SwaptionHelper Helper(
        int start,
        int length,
        double volatility,
        bool nullIndex = false,
        BlackCalibrationHelperCalibrationErrorType errorType = BlackCalibrationHelperCalibrationErrorType.RelativePriceError,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        int? settlementDays = null) => new(
            new Period(start, TimeUnit.Years),
            new Period(length, TimeUnit.Years),
            volatility,
            nullIndex ? null! : new Euribor6M(Curve),
            new Period(1, TimeUnit.Years),
            new Thirty360(Thirty360Convention.BondBasis),
            new Actual360(),
            Curve,
            Context,
            errorType,
            type: type,
            settlementDays: settlementDays);

    // A calibrated model of one's own with no arguments.
    private sealed class NoParameters() : CalibratedModel(0);
}
