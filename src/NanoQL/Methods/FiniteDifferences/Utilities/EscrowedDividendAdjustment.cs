// SPDX-FileCopyrightText: Copyright (C) 2021 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/escroweddividendadjustment.hpp ql/methods/finitedifferences/utilities/escroweddividendadjustment.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Methods;

/// <summary>
/// The escrowed dividend model's adjustment of the spot: minus the value at a time of the dividends paid from then to
/// maturity, each discounted by the risk-free curve and compounded by the dividend curve.
/// </summary>
/// <remarks>Immutable (the S-eq plan's SEQR1).</remarks>
public sealed class EscrowedDividendAdjustment
{
    private readonly ImmutableArray<Dividend> dividendSchedule;
    private readonly double[] amounts;
    private readonly Func<Date, double> toTime;
    private readonly double maturity;

    /// <summary>Initializes a new instance of the <see cref="EscrowedDividendAdjustment"/> class.</summary>
    /// <param name="dividendSchedule">The cash dividends.</param>
    /// <param name="rTS">The risk-free curve.</param>
    /// <param name="qTS">The dividend curve.</param>
    /// <param name="toTime">The mapping of a date to a time.</param>
    /// <param name="maturity">The maturity, as a time.</param>
    /// <param name="context">The evaluation context the dividends' amounts are read in.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The schedule is <c>default(ImmutableArray)</c> or holds a null dividend.
    /// </exception>
    public EscrowedDividendAdjustment(
        ImmutableArray<Dividend> dividendSchedule,
        YieldTermStructure rTS,
        YieldTermStructure qTS,
        Func<Date, double> toTime,
        double maturity,
        EvaluationContext context)
    {
        if (dividendSchedule.IsDefault)
        {
            throw new ArgumentException(
                "default(ImmutableArray) is not a dividend schedule; an empty one is []", nameof(dividendSchedule));
        }

        ArgumentNullException.ThrowIfNull(rTS);
        ArgumentNullException.ThrowIfNull(qTS);
        ArgumentNullException.ThrowIfNull(toTime);
        ArgumentNullException.ThrowIfNull(context);
        if (dividendSchedule.Any(d => d is null))
        {
            throw new ArgumentException("null dividend in schedule", nameof(dividendSchedule));
        }

        this.dividendSchedule = dividendSchedule;
        amounts = [.. dividendSchedule.Select(d => d.Amount(context))];
        RiskFreeRate = rTS;
        DividendYield = qTS;
        this.toTime = toTime;
        this.maturity = maturity;
    }

    /// <summary>Gets the risk-free curve.</summary>
    public YieldTermStructure RiskFreeRate { get; }

    /// <summary>Gets the dividend curve.</summary>
    public YieldTermStructure DividendYield { get; }

    /// <summary>The adjustment of the spot at time <paramref name="t"/>.</summary>
    /// <param name="t">The time.</param>
    /// <returns>Minus the value at <paramref name="t"/> of the dividends paid from then to maturity.</returns>
    public double DividendAdjustment(double t)
    {
        double divAdj = 0.0;
        for (int i = 0; i < dividendSchedule.Length; ++i)
        {
            double divTime = toTime(dividendSchedule[i].Date);

            if (divTime >= t && divTime <= maturity)
            {
                divAdj -= amounts[i]
                    * RiskFreeRate.Discount(divTime) / RiskFreeRate.Discount(t)
                    * DividendYield.Discount(t) / DividendYield.Discount(divTime);
            }
        }

        return divAdj;
    }
}
