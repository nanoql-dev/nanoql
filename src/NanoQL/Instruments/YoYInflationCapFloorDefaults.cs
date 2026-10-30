// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// The defaults <see cref="MakeYoYInflationCapFloor"/> takes for an option it isn't given, which
/// <see cref="ConventionProvider.ResolveYoYInflationCapFloorDefaults"/> returns: QuantLib's. The calendar, the
/// observation lag and the interpolation are the builder's arguments, so they aren't defaults.
/// </summary>
/// <param name="PaymentDayCounter">The coupons' payment day counter, or <see langword="null"/>, QuantLib's empty day
/// counter.</param>
/// <param name="PaymentAdjustment">The payment dates' business-day convention.</param>
/// <param name="FixingDays">The days from the evaluation date to the start, on the calendar.</param>
public sealed record YoYInflationCapFloorDefaults(DayCounter? PaymentDayCounter, BusinessDayConvention PaymentAdjustment, int FixingDays);
