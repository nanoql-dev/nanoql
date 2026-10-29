// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/commodity.hpp ql/experimental/commodities/commodity.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>A message a commodity's calculation records: QuantLib's <c>PricingError</c>.</summary>
/// <param name="ErrorLevel">How serious it is.</param>
/// <param name="Error">The message.</param>
/// <param name="Detail">The detail, empty for none.</param>
/// <remarks>QuantLib's <c>tradeId</c> member, which nothing sets, isn't ported.</remarks>
[Experimental("NQL0001")]
public sealed record PricingError(PricingErrorLevel ErrorLevel, string Error, string Detail = "")
{
    /// <summary>
    /// The message as QuantLib's <c>operator&lt;&lt;</c> writes it: its level ("info: ", "warning: ", "*** error: ",
    /// "*** fatal: "), the message, and ": " and the detail where there is one.
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString()
    {
        string level = ErrorLevel switch
        {
            PricingErrorLevel.Info => "info: ",
            PricingErrorLevel.Warning => "warning: ",
            PricingErrorLevel.Error => "*** error: ",
            PricingErrorLevel.Fatal => "*** fatal: ",
            _ => string.Empty,
        };
        return string.IsNullOrEmpty(Detail) ? level + Error : $"{level}{Error}: {Detail}";
    }
}
