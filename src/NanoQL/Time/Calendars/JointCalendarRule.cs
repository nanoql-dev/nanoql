// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2020 Piotr Siejda
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/jointcalendar.hpp ql/time/calendars/jointcalendar.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>Rules for joining calendars.</summary>
public enum JointCalendarRule
{
    /// <summary>A date is a holiday for the joint calendar if it is a holiday for any of the given calendars.</summary>
    JoinHolidays,

    /// <summary>A date is a business day for the joint calendar if it is a business day for any of the given calendars.</summary>
    JoinBusinessDays,
}
