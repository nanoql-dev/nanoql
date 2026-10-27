// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/prices.hpp ql/prices.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>The four prices of an interval, QuantLib's <c>IntervalPrice::Type</c>.</summary>
public enum IntervalPriceType
{
    /// <summary>The opening price.</summary>
    Open,

    /// <summary>The closing price.</summary>
    Close,

    /// <summary>The highest price.</summary>
    High,

    /// <summary>The lowest price.</summary>
    Low,
}
