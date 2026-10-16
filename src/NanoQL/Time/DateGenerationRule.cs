// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/dategenerationrule.hpp ql/time/dategenerationrule.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>Date-generation rules: the rule used to generate dates in a <see cref="Schedule"/>.</summary>
public enum DateGenerationRule
{
    /// <summary>Backward from termination date to effective date.</summary>
    Backward,

    /// <summary>Forward from effective date to termination date.</summary>
    Forward,

    /// <summary>No intermediate dates between effective date and termination date.</summary>
    Zero,

    /// <summary>
    /// All dates but effective date and termination date are taken to be on the third Wednesday of their month (with
    /// forward calculation).
    /// </summary>
    ThirdWednesday,

    /// <summary>
    /// All dates including effective date and termination date are taken to be on the third Wednesday of their month
    /// (with forward calculation).
    /// </summary>
    ThirdWednesdayInclusive,

    /// <summary>
    /// All dates but the effective date are taken to be the twentieth of their month (used for CDS schedules in
    /// emerging markets). The termination date is also modified.
    /// </summary>
    Twentieth,

    /// <summary>
    /// All dates but the effective date are taken to be the twentieth of an IMM month (used for CDS schedules). The
    /// termination date is also modified.
    /// </summary>
    TwentiethIMM,

    /// <summary>
    /// Same as <see cref="TwentiethIMM"/> with unrestricted date ends and long/short stub coupon period (old CDS
    /// convention).
    /// </summary>
    OldCDS,

    /// <summary>Credit derivatives standard rule since the 'Big Bang' changes in 2009.</summary>
    CDS,

    /// <summary>Credit derivatives standard rule since December 20th, 2015.</summary>
    CDS2015,
}
