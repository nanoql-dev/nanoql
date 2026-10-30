// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// The defaults <see cref="MakeCms"/> takes for an option it isn't given, which
/// <see cref="ConventionProvider.ResolveCmsDefaults"/> returns: QuantLib's. The CMS leg's calendar is the swap
/// index's, and the floating leg's calendar, tenor, conventions and day counter its Ibor index's, so they aren't
/// defaults.
/// </summary>
/// <param name="CmsLegTenor">The CMS leg's tenor.</param>
/// <param name="CmsLegConvention">The CMS leg's business-day convention.</param>
/// <param name="CmsLegTerminationDateConvention">The CMS leg's termination-date convention.</param>
/// <param name="CmsLegRule">The CMS leg's date-generation rule.</param>
/// <param name="CmsLegEndOfMonth">Whether the CMS leg's dates stay at month end.</param>
/// <param name="CmsLegDayCounter">The CMS leg's payment day counter.</param>
/// <param name="FloatingLegRule">The floating leg's date-generation rule.</param>
/// <param name="FloatingLegEndOfMonth">Whether the floating leg's dates stay at month end.</param>
public sealed record CmsDefaults(
    Period CmsLegTenor,
    BusinessDayConvention CmsLegConvention,
    BusinessDayConvention CmsLegTerminationDateConvention,
    DateGenerationRule CmsLegRule,
    bool CmsLegEndOfMonth,
    DayCounter CmsLegDayCounter,
    DateGenerationRule FloatingLegRule,
    bool FloatingLegEndOfMonth);
