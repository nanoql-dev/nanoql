// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/energycommodity.hpp ql/experimental/commodities/energycommodity.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The period a quantity is given per: QuantLib's <c>EnergyCommodity::QuantityPeriodicity</c>.</summary>
[Experimental("NQL0001")]
public enum EnergyCommodityQuantityPeriodicity
{
    /// <summary>The whole quantity.</summary>
    Absolute = 0,

    /// <summary>Per hour.</summary>
    PerHour = 1,

    /// <summary>Per day.</summary>
    PerDay = 2,

    /// <summary>Per week.</summary>
    PerWeek = 3,

    /// <summary>Per month.</summary>
    PerMonth = 4,

    /// <summary>Per quarter.</summary>
    PerQuarter = 5,

    /// <summary>Per year.</summary>
    PerYear = 6,
}
