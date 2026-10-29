// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/energycommodity.hpp ql/experimental/commodities/energycommodity.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>When an energy commodity is paid for: QuantLib's <c>EnergyCommodity::PaymentSchedule</c>.</summary>
[Experimental("NQL0001")]
public enum EnergyCommodityPaymentSchedule
{
    /// <summary>At the window's end.</summary>
    WindowSettlement = 0,

    /// <summary>Monthly.</summary>
    MonthlySettlement = 1,

    /// <summary>Quarterly.</summary>
    QuarterlySettlement = 2,

    /// <summary>Yearly.</summary>
    YearlySettlement = 3,
}
