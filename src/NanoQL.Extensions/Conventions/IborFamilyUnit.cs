// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Extensions.Conventions;

/// <summary>A tenor unit an Ibor family takes, with its business-day convention and end-of-month rule.</summary>
/// <param name="TimeUnit">The tenor's unit.</param>
/// <param name="Convention">The convention at that unit.</param>
/// <param name="EndOfMonth">Whether the end-of-month rule applies at that unit.</param>
public sealed record IborFamilyUnit(TimeUnit TimeUnit, BusinessDayConvention Convention, bool EndOfMonth);
