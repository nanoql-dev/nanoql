// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/commodity.hpp ql/experimental/commodities/commodity.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>How serious a pricing error is: QuantLib's <c>PricingError::Level</c>.</summary>
[Experimental("NQL0001")]
public enum PricingErrorLevel
{
    /// <summary>For information.</summary>
    Info = 0,

    /// <summary>A warning: the price stands, on a fallback.</summary>
    Warning = 1,

    /// <summary>An error.</summary>
    Error = 2,

    /// <summary>A fatal error.</summary>
    Fatal = 3,
}
