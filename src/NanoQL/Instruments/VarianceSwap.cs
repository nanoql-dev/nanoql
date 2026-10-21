// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/varianceswap.hpp ql/instruments/varianceswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A variance swap: at maturity, the notional times the difference between the variance realized from the start date
/// and the strike, received long and paid short.
/// </summary>
/// <remarks>
/// Immutable. As in QuantLib, it doesn't manage seasoned variance swaps: an engine prices the variance from the
/// evaluation date. Its results add the fair variance, <see cref="VarianceSwapResults.Variance"/>.
/// </remarks>
public sealed class VarianceSwap : Instrument
{
    /// <summary>Initializes a new instance of the <see cref="VarianceSwap"/> class.</summary>
    /// <param name="position">Long or short the realized variance.</param>
    /// <param name="strike">The variance strike.</param>
    /// <param name="notional">The notional.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="maturityDate">The maturity date.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> isn't a member of its
    /// enum.</exception>
    /// <exception cref="ArgumentException">A date is <see langword="default"/>.</exception>
    public VarianceSwap(PositionType position, double strike, double notional, Date startDate, Date maturityDate)
    {
        EnumArguments.Check(position, nameof(position));
        Date.ThrowIfDefault(startDate, nameof(startDate));
        Date.ThrowIfDefault(maturityDate, nameof(maturityDate));
        Position = position;
        Strike = strike;
        Notional = notional;
        StartDate = startDate;
        MaturityDate = maturityDate;
    }

    /// <summary>Gets the variance strike.</summary>
    public double Strike { get; }

    /// <summary>Gets the position: long or short the realized variance.</summary>
    public PositionType Position { get; }

    /// <summary>Gets the start date.</summary>
    public Date StartDate { get; }

    /// <summary>Gets the maturity date.</summary>
    public Date MaturityDate { get; }

    /// <summary>Gets the notional.</summary>
    public double Notional { get; }

    /// <inheritdoc/>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SimpleEvent(MaturityDate).HasOccurred(context);
    }

    /// <inheritdoc/>
    /// <remarks>An expired swap's variance is <see langword="null"/>, as QuantLib's.</remarks>
    public override VarianceSwapResults SetupExpired()
    {
        InstrumentResults expired = base.SetupExpired();
        return new VarianceSwapResults
        {
            NPV = expired.NPV,
            ErrorEstimate = expired.ErrorEstimate,
            ValuationDate = expired.ValuationDate,
            AdditionalResults = expired.AdditionalResults,
            Variance = null,
        };
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The strike or the notional isn't positive.</exception>
    public override void Validate()
    {
        if (!(Strike > 0.0))
        {
            throw new NanoQLException("negative or null strike given");
        }

        if (!(Notional > 0.0))
        {
            throw new NanoQLException("negative or null notional given");
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The engine's results aren't a variance swap's.</exception>
    public override VarianceSwapResults FetchResults(InstrumentResults results)
    {
        InstrumentResults fetched = base.FetchResults(results);
        if (fetched is not VarianceSwapResults r)
        {
            throw new ArgumentException("wrong results type", nameof(results));
        }

        return r;
    }
}
