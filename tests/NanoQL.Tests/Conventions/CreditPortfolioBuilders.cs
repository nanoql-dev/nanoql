// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The credit portfolio facts' shared inputs: QuantLib's n-th-to-default test market (31 August 2006, flat hazard
/// curves on Actual/365, a 5% continuous discount curve, a five-year quarterly schedule from 1 September 2006) and the
/// pools, baskets and constant-loss models the probes build on it (the S-xcr run's
/// <c>scratchpad\probes\c1_facts.cpp</c>, the planner's <c>xp_basket.cpp</c>, the plan review's <c>rv_basket.cpp</c>).
/// </summary>
internal static class CreditPortfolioBuilders
{
    /// <summary>Gets the evaluation date, 31 August 2006.</summary>
    public static Date Today { get; } = new(31, August, 2006);

    /// <summary>Gets five years on, 31 August 2011.</summary>
    public static Date D5 { get; } = new(31, August, 2011);

    /// <summary>Gets the evaluation context.</summary>
    public static EvaluationContext Context { get; } = new() { EvaluationDate = new Date(31, August, 2006) };

    /// <summary>The tests' key: North American corporate, EUR, senior secured, no grace, an amount of 1.</summary>
    /// <param name="seniority">The seniority.</param>
    /// <returns>The key.</returns>
    public static NorthAmericaCorpDefaultKey Key(Seniority seniority = Seniority.SeniorSec) =>
        new(new EURCurrency(), seniority, new Period(0, TimeUnit.Days), 1.0);

    /// <summary>A flat hazard curve on Actual/365.</summary>
    /// <param name="reference">The reference date.</param>
    /// <param name="h">The hazard rate.</param>
    /// <returns>The curve.</returns>
    public static FlatHazardRate Flat(Date reference, double h) => new(reference, h, new Actual365Fixed());

    /// <summary>An issuer of one curve on the tests' key.</summary>
    /// <param name="curve">The curve.</param>
    /// <param name="events">Its events.</param>
    /// <returns>The issuer.</returns>
    public static Issuer IssuerOn(DefaultProbabilityTermStructure curve, params DefaultEvent[] events) =>
        new([(Key(), curve)], events);

    /// <summary>A pool of names on the tests' key, each on its curve.</summary>
    /// <param name="names">The names.</param>
    /// <param name="curves">Their curves.</param>
    /// <param name="events">Each name's events, or <see langword="null"/>.</param>
    /// <returns>The pool.</returns>
    public static Pool PoolOf(string[] names, DefaultProbabilityTermStructure[] curves, DefaultEvent[]?[]? events = null)
    {
        var pool = new Pool();
        for (int i = 0; i < names.Length; i++)
        {
            pool = pool.Add(names[i], IssuerOn(curves[i], events?[i] ?? []), Key());
        }

        return pool;
    }

    /// <summary>A Gaussian constant-loss model by correlation, every recovery 0.4.</summary>
    /// <param name="n">The number of names.</param>
    /// <param name="correlation">The correlation.</param>
    /// <returns>The model.</returns>
    public static ConstantLossModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits> Constant(int n, double correlation) =>
        new(correlation, [.. Enumerable.Repeat(0.4, n)], LatentModelIntegrationType.GaussianQuadrature, n);

    /// <summary>A bankruptcy on 1 June 2006, settled on 1 July at 0.4 for senior secured debt.</summary>
    /// <returns>The event.</returns>
    public static BankruptcyEvent June2006Bankruptcy() =>
        new(new Date(1, June, 2006), new EURCurrency(), Seniority.SeniorSec, new Date(1, July, 2006), 0.4);

    /// <summary>The tests' five-year quarterly schedule from 1 September 2006 on TARGET.</summary>
    /// <returns>The schedule.</returns>
    public static Schedule FiveYears() => new MakeSchedule().From(new Date(1, September, 2006))
        .To(new Date(1, September, 2011)).WithTenor(new Period(3, TimeUnit.Months)).WithCalendar(new TARGET()).ToSchedule();

    /// <summary>The tests' discount curve: 5% continuous on Actual/365 from the evaluation date.</summary>
    /// <returns>The curve.</returns>
    public static FlatForward Discount() => new(Today, 0.05, new Actual365Fixed());

    /// <summary>A seller's n-th-to-default swap on the tests' schedule, at a premium of 2%, priced weekly.</summary>
    /// <param name="basket">The bound basket.</param>
    /// <param name="n">The order.</param>
    /// <param name="nominal">The nominal.</param>
    /// <param name="upfront">The upfront rate.</param>
    /// <returns>The results.</returns>
    public static NthToDefaultResults Ntd(Basket basket, int n, double nominal, double upfront = 0.0) =>
        IntegralNtdEngine.Calculate(
            new NthToDefault(basket, n, ProtectionSide.Seller, FiveYears(), upfront, 0.02, new Actual360(), nominal, true),
            basket.Context,
            new Period(1, TimeUnit.Weeks),
            Discount());
}
