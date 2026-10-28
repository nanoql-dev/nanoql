// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The default-probability traits' model-aware members (the S-xcr plan's XR3 (c)) and the hazard and density bases'
/// default survival (XR3 (a)). The members are additive: a user's traits that leave them out compile and bootstrap
/// unchanged, and refuse a model; the affine traits refuse a curve without one. A piecewise curve on the affine traits
/// reprices its helpers, and the interpolated affine curve on its nodes is that curve. The bases integrate with
/// QuantLib's 48-point Gauss-Chebyshev rule: on a hazard rate of 0.02 + 0.01t and a density of 0.03e^(−0.03t) (Actual/365
/// Fixed, from 31 August 2006) QuantLib reads S(0.5, 1, 5, 10) 0.9888110588521104, 0.97530555949635511,
/// 0.79848414736582995, 0.49652325618078996 and 0.98510928189932623, 0.97044025741200013, 0.86068306487848101,
/// 0.74077160751429916 (the S-xcr run's <c>c4_facts.cpp</c>).
/// </summary>
public class AffineCreditCurveConventionTests
{
    [Fact]
    public void AUsersTraitsCompileAndBootstrapUnchanged()
    {
        EvaluationContext context = CreditFixture.Context();
        SpreadCdsHelper[] helpers = Helpers(context);
        var landed = new PiecewiseDefaultCurve<HazardRate, BackwardFlat, IterativeBootstrap>(context.EvaluationDate, helpers, new Actual365Fixed(), context);
        var users = new PiecewiseDefaultCurve<UserHazardRate, BackwardFlat, IterativeBootstrap>(context.EvaluationDate, helpers, new Actual365Fixed(), context);
        Assert.Equal(landed.Nodes(), users.Nodes());
    }

    [Fact]
    public void TraitsWithoutAModelRefuseOne()
    {
        EvaluationContext context = CreditFixture.Context();
        var e = Assert.ThrowsAny<NanoQLException>(() => new PiecewiseDefaultCurve<HazardRate, BackwardFlat, IterativeBootstrap>(
            context.EvaluationDate, Helpers(context), new Actual365Fixed(), Model(), context));
        Assert.Contains("these traits take no model", e.Message);

        // each model-aware member's default refuses
        double[] times = [0.0, 1.0];
        double[] data = [0.01, 0.01];
        Interpolation interpolation = new BackwardFlatInterpolation(times, data);
        var flat = new FlatHazardRate(context.EvaluationDate, 0.01, new Actual365Fixed());
        Assert.Equal("these traits take no model", Assert.Throws<NanoQLException>(() => Density<HazardRate>(times, data, interpolation, Model())).Message);
        Assert.Equal("these traits take no model", Assert.Throws<NanoQLException>(() => Hazard<HazardRate>(times, data, interpolation, flat, Model())).Message);
    }

    [Fact]
    public void TheAffineTraitsRefuseACurveWithoutAModel()
    {
        EvaluationContext context = CreditFixture.Context();
        var e = Assert.ThrowsAny<NanoQLException>(() => new PiecewiseDefaultCurve<AffineHazardRate, BackwardFlat, IterativeBootstrap>(
            context.EvaluationDate, Helpers(context), new Actual365Fixed(), context));
        Assert.Contains("the affine hazard-rate traits take a model", e.Message);

        // each of the affine traits' model-free members refuses
        double[] times = [0.0, 1.0];
        double[] data = [0.01, 0.01];
        Interpolation interpolation = new BackwardFlatInterpolation(times, data);
        var flat = new FlatHazardRate(context.EvaluationDate, 0.01, new Actual365Fixed());
        Assert.Throws<NanoQLException>(() => AffineHazardRate.SurvivalProbabilityImpl(times, data, interpolation, 0.5));
        Assert.Throws<NanoQLException>(() => AffineHazardRate.DefaultDensityImpl(times, data, interpolation, 0.5));
        Assert.Throws<NanoQLException>(() => AffineHazardRate.HazardRateImpl(times, data, interpolation, 0.5, flat));
    }

    [Fact]
    public void AnAffineCurveRepricesItsHelpers()
    {
        EvaluationContext context = CreditFixture.Context();
        SpreadCdsHelper[] helpers = Helpers(context);
        var curve = new PiecewiseDefaultCurve<AffineHazardRate, BackwardFlat, IterativeBootstrap>(
            context.EvaluationDate, helpers, new Actual365Fixed(), Model(), context);
        foreach (SpreadCdsHelper helper in helpers)
        {
            Assert.Equal(helper.Quote, helper.SetTermStructure(curve).ImpliedQuote(context), 1e-10);
        }

        var nodes = InterpolatedAffineHazardRateCurve<BackwardFlat>.FromNodes(curve);
        foreach (double t in new[] { 0.5, 2.0, 4.0, 7.0 })
        {
            Assert.Equal(curve.SurvivalProbability(t, true), nodes.SurvivalProbability(t, true), 1e-15);
            Assert.Equal(curve.DefaultDensity(t, true), nodes.DefaultDensity(t, true), 1e-15);
            Assert.Equal(curve.HazardRate(t, true), nodes.HazardRate(t, true), 1e-15);
        }
    }

    [Theory]
    [InlineData(0.5, 0.9888110588521104, 0.98510928189932623)]
    [InlineData(1.0, 0.97530555949635511, 0.97044025741200013)]
    [InlineData(5.0, 0.79848414736582995, 0.86068306487848101)]
    [InlineData(10.0, 0.49652325618078996, 0.74077160751429916)]
    public void TheBasesDefaultSurvivalIsQuantLibs(double t, double hazardBase, double densityBase)
    {
        var today = new Date(31, Month.August, 2006);
        Assert.Equal(hazardBase, new LinearHazard(today).SurvivalProbability(t), 1e-15);
        Assert.Equal(densityBase, new ExpDensity(today).SurvivalProbability(t), 1e-15);
    }

    [Fact]
    public void TheStructuresDateFormsAreItsTimeForms()
    {
        EvaluationContext context = CreditFixture.Context();
        Calendar calendar = new TARGET();
        var structure = OneFactorAffineSurvivalStructure.Create(Model(), 2, calendar, new Actual365Fixed(), context);
        Assert.Equal(calendar.Advance(context.EvaluationDate, 2, TimeUnit.Days), structure.ReferenceDate);
        var fixedDate = new OneFactorAffineSurvivalStructure(Model(), structure.ReferenceDate, new Actual365Fixed());
        Assert.Equal(fixedDate.SurvivalProbability(3.0), structure.SurvivalProbability(3.0));

        Date d1 = structure.ReferenceDate + new Period(1, TimeUnit.Years), d2 = structure.ReferenceDate + new Period(4, TimeUnit.Years);
        Assert.Equal(
            structure.ConditionalSurvivalProbability(structure.TimeFromReference(d1), structure.TimeFromReference(d2), 0.03),
            structure.ConditionalSurvivalProbability(d1, d2, 0.03));
    }

    private static double Density<T>(double[] times, double[] data, Interpolation interpolation, OneFactorAffineModel model)
        where T : IDefaultProbabilityTraits => T.DefaultDensityImpl(times, data, interpolation, 0.5, model);

    private static double Hazard<T>(double[] times, double[] data, Interpolation interpolation, DefaultProbabilityTermStructure curve, OneFactorAffineModel model)
        where T : IDefaultProbabilityTraits => T.HazardRateImpl(times, data, interpolation, 0.5, curve, model);

    private static CoxIngersollRoss Model() => new(0.02, 0.03, 0.5, 0.05);

    private static SpreadCdsHelper[] Helpers(EvaluationContext context) =>
        [Helper(1, 0.005, context), Helper(3, 0.0075, context), Helper(5, 0.01, context)];

    private static SpreadCdsHelper Helper(int years, double spread, EvaluationContext context) =>
        new(
            spread,
            new Period(years, TimeUnit.Years),
            0,
            new WeekendsOnly(),
            Frequency.Quarterly,
            BusinessDayConvention.Following,
            DateGenerationRule.CDS,
            new Actual360(),
            0.4,
            CreditFixture.Discount(),
            context);

    // A user's traits written before the model-aware members: the hazard-rate traits' members, forwarded.
    private readonly struct UserHazardRate : IDefaultProbabilityTraits
    {
        public static int MaxIterations => HazardRate.MaxIterations;

        public static Date InitialDate(BootstrapState<DefaultProbabilityTermStructure> c) => HazardRate.InitialDate(c);

        public static double InitialValue(BootstrapState<DefaultProbabilityTermStructure> c) => HazardRate.InitialValue(c);

        public static double Guess(int i, BootstrapState<DefaultProbabilityTermStructure> c, bool validData, int firstAliveHelper) =>
            HazardRate.Guess(i, c, validData, firstAliveHelper);

        public static double MinValueAfter(int i, BootstrapState<DefaultProbabilityTermStructure> c, bool validData, int firstAliveHelper) =>
            HazardRate.MinValueAfter(i, c, validData, firstAliveHelper);

        public static double MaxValueAfter(int i, BootstrapState<DefaultProbabilityTermStructure> c, bool validData, int firstAliveHelper) =>
            HazardRate.MaxValueAfter(i, c, validData, firstAliveHelper);

        public static void UpdateGuess(double[] data, double rate, int i) => HazardRate.UpdateGuess(data, rate, i);

        public static double SurvivalProbabilityImpl(ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t) =>
            HazardRate.SurvivalProbabilityImpl(times, data, interpolation, t);

        public static double DefaultDensityImpl(ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t) =>
            HazardRate.DefaultDensityImpl(times, data, interpolation, t);

        public static double HazardRateImpl(
            ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t, DefaultProbabilityTermStructure curve) =>
            HazardRate.HazardRateImpl(times, data, interpolation, t, curve);

        public static Date MaxDate(ReadOnlySpan<Date> dates) => HazardRate.MaxDate(dates);
    }

    // The hazard base's default survival on 0.02 + 0.01t.
    private sealed class LinearHazard(Date referenceDate) : HazardRateStructure(referenceDate, null, new Actual365Fixed())
    {
        public override Date MaxDate => Date.MaxDate;

        protected override double HazardRateImpl(double t) => 0.02 + (0.01 * t);
    }

    // The density base's default survival on 0.03 e^(-0.03t).
    private sealed class ExpDensity(Date referenceDate) : DefaultDensityStructure(referenceDate, null, new Actual365Fixed())
    {
        public override Date MaxDate => Date.MaxDate;

        protected override double DefaultDensityImpl(double t) => 0.03 * Math.Exp(-0.03 * t);
    }
}
