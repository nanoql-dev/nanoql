// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/recoveryratemodel.hpp ql/experimental/credit/recoveryratemodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A recovery-rate model returning its quote's value, whatever the date and the seniority.</summary>
/// <remarks>QuantLib's quote handle is the quote itself; a new value is a new model.</remarks>
[Experimental("NQL0001")]
public sealed class ConstantRecoveryModel : RecoveryRateModel
{
    /// <summary>Initializes a new instance of the <see cref="ConstantRecoveryModel"/> class on a quote.</summary>
    /// <param name="quote">The recovery quote.</param>
    /// <exception cref="ArgumentNullException"><paramref name="quote"/> is <see langword="null"/>.</exception>
    public ConstantRecoveryModel(RecoveryRateQuote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        Quote = quote;
    }

    /// <summary>Initializes a new instance of the <see cref="ConstantRecoveryModel"/> class on a value.</summary>
    /// <param name="recovery">The recovery rate, a fraction.</param>
    /// <param name="seniority">The seniority it is quoted for; none by default.</param>
    /// <exception cref="ArgumentException"><paramref name="recovery"/> is outside [0, 1].</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seniority"/> isn't a member of its type.</exception>
    public ConstantRecoveryModel(double recovery, Seniority seniority = Seniority.NoSeniority)
        : this(new RecoveryRateQuote(recovery, seniority))
    {
    }

    /// <summary>Gets the recovery quote.</summary>
    public RecoveryRateQuote Quote { get; }

    /// <inheritdoc/>
    public override bool AppliesToSeniority(Seniority seniority) => true;

    /// <summary>The quote's value, with no check that the quote's seniority matches the request's.</summary>
    /// <param name="defaultDate">The default date, unused.</param>
    /// <param name="defaultKey">The default key, unused.</param>
    /// <returns>The quote's value.</returns>
    protected override double? RecoveryValueImpl(Date defaultDate, DefaultProbKey defaultKey) =>

        // no match on requested seniority, all pass
        Quote.Value;
}
