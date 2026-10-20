// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The credit facts' shared market, as the wheel's runs that recorded QuantLib's values for the divergences built it:
/// the evaluation date 15 January 2024, a discount curve flat at 3% and a hazard rate flat at 2%, each
/// Actual/365 (Fixed) and dated on the evaluation date; a credit default swap buying protection on 1,000,000, quarterly
/// on TARGET under the CDS rule, Following, Actual/360, either expired (20 March 2022 to 20 December 2023) or live
/// (20 December 2023 to 20 December 2028).
/// </summary>
internal static class CreditFixture
{
    public static readonly Date Today = new(15, Month.January, 2024);

    public static EvaluationContext Context() => new() { EvaluationDate = Today };

    public static FlatForward Discount() => new(Today, 0.03, new Actual365Fixed());

    public static FlatHazardRate Probability() => new(Today, 0.02, new Actual365Fixed());

    /// <summary>The swap's schedule: expired, its last date in December 2023, or live to December 2028.</summary>
    /// <param name="expired">Whether the schedule has expired.</param>
    /// <returns>The schedule.</returns>
    public static Schedule ScheduleOf(bool expired) =>
        new(
            expired ? new Date(20, Month.March, 2022) : new Date(20, Month.December, 2023),
            expired ? new Date(20, Month.December, 2023) : new Date(20, Month.December, 2028),
            new Period(Frequency.Quarterly),
            new TARGET(),
            BusinessDayConvention.Following,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.CDS,
            false,
            Context());

    /// <summary>The swap: expired, its last coupon paid in December 2023, or live to December 2028.</summary>
    /// <param name="expired">Whether the swap has expired.</param>
    /// <param name="spread">The running spread.</param>
    /// <returns>The swap.</returns>
    public static CreditDefaultSwap Swap(bool expired, double spread) =>
        new(ProtectionSide.Buyer, 1e6, spread, ScheduleOf(expired), BusinessDayConvention.Following, new Actual360());
}
