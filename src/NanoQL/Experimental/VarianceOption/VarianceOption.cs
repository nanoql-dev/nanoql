// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/varianceoption/varianceoption.hpp ql/experimental/varianceoption/varianceoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// An option on realized variance: at maturity it pays the notional times the payoff of the variance realized from the
/// start date.
/// </summary>
/// <remarks>
/// Immutable. Its engines return an <see cref="InstrumentResults"/>, QuantLib's results class adding nothing to the
/// instrument's. QuantLib doesn't manage seasoned variance options: an engine refuses a start before the evaluation date.
/// </remarks>
[Experimental("NQL0001")]
public sealed class VarianceOption : Instrument
{
    /// <summary>Initializes a new instance of the <see cref="VarianceOption"/> class.</summary>
    /// <param name="payoff">The payoff on the realized variance.</param>
    /// <param name="notional">The notional.</param>
    /// <param name="startDate">The start of the variance's realization.</param>
    /// <param name="maturityDate">The maturity.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A date is the default date ("null start date given", "null maturity date
    /// given", as QuantLib's validation reads them).</exception>
    public VarianceOption(Payoff payoff, double notional, Date startDate, Date maturityDate)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        Date.ThrowIfDefault(startDate, nameof(startDate));
        Date.ThrowIfDefault(maturityDate, nameof(maturityDate));
        Payoff = payoff;
        Notional = notional;
        StartDate = startDate;
        MaturityDate = maturityDate;
    }

    /// <summary>Gets the payoff on the realized variance.</summary>
    public Payoff Payoff { get; }

    /// <summary>Gets the notional.</summary>
    public double Notional { get; }

    /// <summary>Gets the start of the variance's realization.</summary>
    public Date StartDate { get; }

    /// <summary>Gets the maturity.</summary>
    public Date MaturityDate { get; }

    /// <inheritdoc/>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(MaturityDate).HasOccurred(context);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The notional isn't positive ("negative or null notional given").</exception>
    /// <remarks>QuantLib's "no strike given", "no notional given" and the null-date checks can't fail here: the
    /// constructor holds a payoff, a value and two dates.</remarks>
    public override void Validate()
    {
        if (!(Notional > 0.0))
        {
            throw new NanoQLException("negative or null notional given");
        }
    }
}
