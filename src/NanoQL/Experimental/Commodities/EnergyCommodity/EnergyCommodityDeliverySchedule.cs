// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/energycommodity.hpp ql/experimental/commodities/energycommodity.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>When an energy commodity is delivered: QuantLib's <c>EnergyCommodity::DeliverySchedule</c>.</summary>
[Experimental("NQL0001")]
public enum EnergyCommodityDeliverySchedule
{
    /// <summary>Constantly.</summary>
    Constant = 0,

    /// <summary>Over a window.</summary>
    Window = 1,

    /// <summary>Hourly.</summary>
    Hourly = 2,

    /// <summary>Daily.</summary>
    Daily = 3,

    /// <summary>Weekly.</summary>
    Weekly = 4,

    /// <summary>Monthly.</summary>
    Monthly = 5,

    /// <summary>Quarterly.</summary>
    Quarterly = 6,

    /// <summary>Yearly.</summary>
    Yearly = 7,
}
