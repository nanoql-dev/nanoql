// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2013 BGC Partners L.P.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/daycounters/actual365fixed.hpp ql/time/daycounters/actual365fixed.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The conventions of <see cref="Actual365Fixed"/>.</summary>
public enum Actual365FixedConvention
{
    /// <summary>Actual/365 (Fixed): the actual number of days over 365.</summary>
    Standard = 0,

    /// <summary>Actual/365 (Fixed) Canadian Bond, which needs the reference period.</summary>
    Canadian = 1,

    /// <summary>Actual/365 (No Leap): February 29th is not counted.</summary>
    NoLeap = 2,
}
