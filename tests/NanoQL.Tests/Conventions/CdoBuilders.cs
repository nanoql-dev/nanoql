// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The CDO facts' common inputs, the S-xcr run's <c>c2_facts.cpp</c>'s: eight names at hazards 1% to 3% and notional 10,
/// an inhomogeneous pool model at correlation 0.3, the tests' discount curve and five-year schedule.
/// </summary>
internal static class CdoBuilders
{
    /// <summary>Gets the eight names' hazard rates.</summary>
    public static double[] EightHazards { get; } = [0.01, 0.02, 0.015, 0.03, 0.025, 0.01, 0.02, 0.015];

    /// <summary>Gets the eight names.</summary>
    public static string[] Eight { get; } = ["A", "B", "C", "D", "E", "F", "G", "H"];

    /// <summary>A basket of the eight names on flat curves from the evaluation date.</summary>
    /// <param name="a">The attachment ratio.</param>
    /// <param name="d">The detachment ratio.</param>
    /// <param name="curves">The curves, by default flat from the evaluation date.</param>
    /// <returns>The basket.</returns>
    public static Basket EightNames(double a, double d, DefaultProbabilityTermStructure[]? curves = null) =>
        new(Today, Eight, Enumerable.Repeat(10.0, 8), PoolOf(Eight, curves ?? [.. EightHazards.Select(h => (DefaultProbabilityTermStructure)Flat(Today, h))]), Context, a, d);

    /// <summary>An inhomogeneous pool model on n names at a correlation, every recovery 0.4: 100 buckets, 15 steps on
    /// [−5, 5].</summary>
    /// <param name="n">The number of names.</param>
    /// <param name="correlation">The correlation.</param>
    /// <returns>The model.</returns>
    public static IHGaussPoolLossModel Pool(int n, double correlation) =>
        new(new GaussianConstantLossLM(correlation, [.. Enumerable.Repeat(0.4, n)], LatentModelIntegrationType.GaussianQuadrature, n), 100, 5.0, -5.0, 15);

    /// <summary>A Gaussian random default model on n names at a correlation, every recovery 0.4, at QuantLib's seed.</summary>
    /// <param name="n">The number of names.</param>
    /// <param name="correlation">The correlation.</param>
    /// <param name="paths">The number of paths.</param>
    /// <param name="maxHorizon">The horizon in days.</param>
    /// <returns>The model.</returns>
    public static GaussianRandomDefaultLM Random(int n, double correlation, int paths, int maxHorizon = 4050) =>
        new(new GaussianConstantLossLM(correlation, [.. Enumerable.Repeat(0.4, n)], LatentModelIntegrationType.GaussianQuadrature, n), paths, 2863311530, maxHorizon: maxHorizon);

    /// <summary>A CDO on Actual/360 with a following payment convention.</summary>
    /// <param name="basket">The bound basket.</param>
    /// <param name="side">The side.</param>
    /// <param name="upfront">The upfront rate.</param>
    /// <param name="running">The running rate.</param>
    /// <param name="notional">The notional, by default the tranche's.</param>
    /// <param name="schedule">The schedule, by default the tests' five years.</param>
    /// <returns>The CDO.</returns>
    public static SyntheticCDO Cdo(
        Basket basket, ProtectionSide side, double upfront, double running, double? notional = null, Schedule? schedule = null) =>
        new(basket, side, schedule ?? FiveYears(), upfront, running, new Actual360(), BusinessDayConvention.Following, notional);

    /// <summary>A schedule from 1 March 2007 to 1 March 2012, quarterly on TARGET: a forward start.</summary>
    /// <returns>The schedule.</returns>
    public static Schedule Forward() => new MakeSchedule().From(new Date(1, March, 2007))
        .To(new Date(1, March, 2012)).WithTenor(new Period(3, TimeUnit.Months)).WithCalendar(new TARGET()).ToSchedule();

    /// <summary>The CDO on the mid-point engine.</summary>
    /// <param name="cdo">The CDO.</param>
    /// <returns>The results.</returns>
    public static SyntheticCDOResults Mid(SyntheticCDO cdo) => MidPointCDOEngine.Calculate(cdo, Context, Discount());

    /// <summary>The CDO on the integral engine at its three-month step.</summary>
    /// <param name="cdo">The CDO.</param>
    /// <returns>The results.</returns>
    public static SyntheticCDOResults Integral(SyntheticCDO cdo) => IntegralCDOEngine.Calculate(cdo, Context, Discount());
}
