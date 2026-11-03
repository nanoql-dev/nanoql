// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/fx/fxresetcashflows.hpp ql/experimental/fx/fxresetcashflows.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The interface for obtaining the rate of an FX-reset observation.</summary>
/// <remarks>
/// An extension point, stateless as the coupon pricers are (§2.2's pricer row): the rate is a function of the
/// observation and the context. QuantLib's observer interface isn't ported.
/// </remarks>
[Experimental("NQL0001")]
public abstract class FxResetPricer
{
    /// <summary>The FX rate of an observation.</summary>
    /// <param name="reset">The observation.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The rate.</returns>
    public abstract double FxRate(FxReset reset, EvaluationContext context);
}
