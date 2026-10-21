// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/fx/fxresetcashflows.hpp ql/experimental/fx/fxresetcashflows.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A netted notional exchange of an FX-resetting leg.</summary>
/// <remarks>
/// The exchange receives the notional of the period that ends and pays that of the period that starts, each the
/// constant leg's notional converted at its observation's FX rate: the first exchange has no ending period and the
/// last no starting one. Immutable: <see cref="SetFxResetPricer"/> returns a copy holding the pricer.
/// </remarks>
[Experimental("NQL0001")]
public sealed class FxResetNotionalExchange : CashFlow
{
    private readonly Date paymentDate;

    // Written only on a fresh copy (SetFxResetPricer).
    private FxResetPricer? fxResetPricer;

    /// <summary>Initializes a new instance of the <see cref="FxResetNotionalExchange"/> class.</summary>
    /// <param name="paymentDate">The payment date.</param>
    /// <param name="constantLegNotional">The constant leg's notional, in its own currency.</param>
    /// <param name="previousReset">The observation of the period that ends, or <see langword="null"/> for none.</param>
    /// <param name="currentReset">The observation of the period that starts, or <see langword="null"/> for
    /// none.</param>
    /// <exception cref="ArgumentException">The payment date is <c>default(Date)</c>, or neither observation is
    /// given.</exception>
    public FxResetNotionalExchange(
        Date paymentDate, double constantLegNotional, FxReset? previousReset, FxReset? currentReset)
    {
        if (paymentDate == default)
        {
            throw new ArgumentException("null payment date", nameof(paymentDate));
        }

        if (previousReset is null && currentReset is null)
        {
            throw new ArgumentException("at least one of the reset dates must be given", nameof(currentReset));
        }

        this.paymentDate = paymentDate;
        ConstantLegNotional = constantLegNotional;
        PreviousReset = previousReset;
        CurrentReset = currentReset;
    }

    /// <inheritdoc/>
    public override Date Date => paymentDate;

    /// <summary>Gets the constant leg's notional, in its own currency.</summary>
    public double ConstantLegNotional { get; }

    /// <summary>Gets the observation of the period that ends, or <see langword="null"/> for none.</summary>
    public FxReset? PreviousReset { get; }

    /// <summary>Gets the observation of the period that starts, or <see langword="null"/> for none.</summary>
    public FxReset? CurrentReset { get; }

    /// <summary>Gets the FX-reset pricer, or <see langword="null"/> when none is set.</summary>
    public FxResetPricer? FxResetPricer => fxResetPricer;

    /// <summary>The netted amount: the ending period's notional less the starting period's.</summary>
    /// <param name="context">The evaluation context, which the FX-reset pricer reads.</param>
    /// <returns>The amount.</returns>
    /// <exception cref="NanoQLException">No FX-reset pricer is set.</exception>
    public override double Amount(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        FxResetPricer pricer = fxResetPricer ?? throw new NanoQLException(
            "no FX reset pricer set; the exchange amount is available only after assigning an FX reset pricer");
        double amount = 0.0;
        if (PreviousReset is FxReset previous)
        {
            amount += ConstantLegNotional * pricer.FxRate(previous, context);
        }

        if (CurrentReset is FxReset current)
        {
            amount -= ConstantLegNotional * pricer.FxRate(current, context);
        }

        return amount;
    }

    /// <summary>A copy of the exchange holding an FX-reset pricer: QuantLib's <c>setFxResetPricer</c>.</summary>
    /// <param name="pricer">The pricer, or <see langword="null"/> for none.</param>
    /// <returns>The copy; the receiver is unchanged.</returns>
    public FxResetNotionalExchange SetFxResetPricer(FxResetPricer? pricer)
    {
        var copy = (FxResetNotionalExchange)MemberwiseClone();
        copy.fxResetPricer = pricer;
        return copy;
    }
}
