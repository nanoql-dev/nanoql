// SPDX-FileCopyrightText: Copyright (C) 2008-2016  Andrea Maggiulli (a.maggiulli@gmail.com)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: src/QLNet/Instruments/Bonds/ConstantCPR.cs @ QLNet-v1.13.2
// Ported from QLNet and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Extensions.Instruments;

/// <summary>A prepayment model with one conditional prepayment rate at every date.</summary>
/// <remarks>
/// The SMM is the CPR's monthly equivalent, <c>1 − (1 − CPR)^(1/12)</c> (SIFMA's Uniform Practices, Chapter SF, SF-5).
/// A CPR outside [0, 1] is refused: SIFMA defines the rate as the fraction of a pool's outstanding balance assumed to
/// prepay, so none outside it is one, where QLNet takes it and returns NaN above 1 and a negative SMM below 0.
/// </remarks>
public sealed class ConstantCPR : IPrepayModel
{
    private readonly double cpr;

    /// <summary>Initializes a new instance of the <see cref="ConstantCPR"/> class.</summary>
    /// <param name="cpr">The conditional prepayment rate, an annual fraction: 0.06 for 6%.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cpr"/> isn't in [0, 1].</exception>
    public ConstantCPR(double cpr)
    {
        if (!(cpr >= 0.0 && cpr <= 1.0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cpr), cpr, "a conditional prepayment rate is a fraction in [0, 1]");
        }

        this.cpr = cpr;
    }

    /// <inheritdoc/>
    public double GetCPR(Date valDate) => cpr;

    /// <inheritdoc/>
    public double GetSMM(Date valDate) => 1 - Math.Pow(1 - GetCPR(valDate), 1 / 12d);
}
