// SPDX-FileCopyrightText: Copyright (C) 2009 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/riskyassetswapoption.hpp ql/experimental/credit/riskyassetswapoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// An option on a risky asset swap: Bachelier's formula on the swap's spread against the market's, its annuity the
/// swap's float annuity times its nominal.
/// </summary>
/// <remarks>
/// An instrument QuantLib prices without an engine: <see cref="Calculate"/> computes it. At a zero standard deviation,
/// an expiry on the evaluation date or a zero volatility, the value is the formula's limit, the intrinsic value, where
/// QuantLib's reads 0·∞, NaN (the S-xcr plan's XR8 (xxii); <c>Divergences/RiskyAssetSwapOptionTests.cs</c>).
/// </remarks>
[Experimental("NQL0001")]
public sealed class RiskyAssetSwapOption : Instrument
{
    private static readonly CumulativeNormalDistribution Phi = new();
    private static readonly NormalDistribution Density = new();

    private readonly RiskyAssetSwap asw;
    private readonly Date expiry;
    private readonly double marketSpread;
    private readonly double spreadVolatility;

    /// <summary>Initializes a new instance of the <see cref="RiskyAssetSwapOption"/> class.</summary>
    /// <param name="asw">The underlying swap.</param>
    /// <param name="expiry">The option's expiry.</param>
    /// <param name="marketSpread">The market's spread.</param>
    /// <param name="spreadVolatility">The spread's normal volatility.</param>
    /// <exception cref="ArgumentNullException"><paramref name="asw"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="expiry"/> is <c>default(Date)</c>.</exception>
    public RiskyAssetSwapOption(RiskyAssetSwap asw, Date expiry, double marketSpread, double spreadVolatility)
    {
        ArgumentNullException.ThrowIfNull(asw);
        Date.ThrowIfDefault(expiry, nameof(expiry));
        this.asw = asw;
        this.expiry = expiry;
        this.marketSpread = marketSpread;
        this.spreadVolatility = spreadVolatility;
    }

    /// <inheritdoc/>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(expiry).HasOccurred(context);
    }

    /// <summary>Prices the option.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The results; an expired option's NPV is 0.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The swap's discount curve refuses a read before the evaluation date or past its
    /// maximum date.</exception>
    public InstrumentResults Calculate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IsExpired(context))
        {
            return SetupExpired();
        }

        double w;

        // strike receiver = asw call = spread put
        if (asw.FixedPayer)
        {
            w = -1.0;
        }
        else
        {
            w = 1.0;
        }

        Date today = context.EvaluationDate;
        double expiryTime = new Actual365Fixed().YearFraction(today, expiry);
        double stdDev = spreadVolatility * Math.Sqrt(expiryTime);
        double a0 = asw.Nominal * asw.FloatAnnuity(context);
        if (stdDev == 0.0)
        {
            // the formula's limit, the intrinsic value, where QuantLib's reads 0 times infinity
            return new InstrumentResults { NPV = a0 * Math.Max(w * (asw.Spread - marketSpread), 0.0) };
        }

        double d = (asw.Spread - marketSpread) / stdDev;
        double npv = a0 * stdDev * ((w * d * Phi.Evaluate(w * d)) + Density.Evaluate(d));
        return new InstrumentResults { NPV = npv };
    }
}
