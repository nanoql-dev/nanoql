// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/forwardvanillaoption.hpp ql/instruments/forwardvanillaoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A forward-start vanilla option: its strike is set at a reset date, at a moneyness times the spot there.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib: the quanto forward option derives from it. Immutable. QuantLib's arguments check
/// the reset date against the evaluation date, which <see cref="Instrument.Validate()"/> can't read, so the forward
/// engines call <see cref="Validate(EvaluationContext)"/>, QuantLib's checks in its order.
/// </remarks>
public class ForwardVanillaOption : OneAssetOption
{
    /// <summary>Initializes a new instance of the <see cref="ForwardVanillaOption"/> class.</summary>
    /// <param name="moneyness">The strike's multiple of the spot at the reset date.</param>
    /// <param name="resetDate">The date the strike is set on.</param>
    /// <param name="payoff">The payoff, whose type the option takes.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The reset date is <c>default(Date)</c> (QuantLib's text).</exception>
    public ForwardVanillaOption(double moneyness, Date resetDate, StrikedTypePayoff payoff, Exercise exercise)
        : base(payoff, exercise)
    {
        if (resetDate == default)
        {
            throw new ArgumentException("null reset date given", nameof(resetDate));
        }

        Moneyness = moneyness;
        ResetDate = resetDate;
    }

    /// <summary>Gets the strike's multiple of the spot at the reset date.</summary>
    public double Moneyness { get; }

    /// <summary>Gets the date the strike is set on.</summary>
    public Date ResetDate { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The moneyness isn't positive, or the reset date isn't before maturity.</exception>
    public override void Validate()
    {
        base.Validate();
        ValidateMoneyness();
        ValidateMaturity();
    }

    /// <summary>QuantLib's checks of the forward option's terms, the reset date against the evaluation date among them.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The moneyness isn't positive, the reset date is before the evaluation date, or it isn't before maturity.
    /// </exception>
    public void Validate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        base.Validate();
        ValidateMoneyness();
        if (!(ResetDate >= context.EvaluationDate))
        {
            throw new NanoQLException("reset date in the past");
        }

        ValidateMaturity();
    }

    private void ValidateMoneyness()
    {
        if (!(Moneyness > 0.0))
        {
            throw new NanoQLException("negative or zero moneyness given");
        }
    }

    private void ValidateMaturity()
    {
        if (!(Exercise.LastDate() > ResetDate))
        {
            throw new NanoQLException("reset date later or equal to maturity");
        }
    }
}
