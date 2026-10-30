// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// The defaults <see cref="MakeMultipleResetsSwap"/> takes for an option it isn't given, which
/// <see cref="ConventionProvider.ResolveMultipleResetsSwapDefaults"/> returns: QuantLib's. The settlement days and the
/// fixed leg's day counter are the index's, and the fixed leg's frequency the coupons' resets times the index's
/// tenor, so they aren't defaults.
/// </summary>
/// <param name="FixedLegConvention">The fixed leg's business-day convention.</param>
/// <param name="AveragingMethod">Whether a coupon's resets are compounded or averaged.</param>
public sealed record MultipleResetsSwapDefaults(BusinessDayConvention FixedLegConvention, RateAveragingType AveragingMethod);
