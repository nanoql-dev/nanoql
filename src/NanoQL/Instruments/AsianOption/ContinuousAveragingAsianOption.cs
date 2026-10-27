// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2025 Kareem Fareed
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/asianoption.hpp ql/instruments/asianoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A continuous-averaging Asian option: its payoff reads the arithmetic or geometric average of the underlying over the
/// averaging period.
/// </summary>
/// <remarks>
/// Immutable. The unseasoned option averages from the evaluation date; the seasoned one names the date its averaging
/// began, the current average being market data an engine takes.
/// </remarks>
public sealed class ContinuousAveragingAsianOption : OneAssetOption
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContinuousAveragingAsianOption"/> class for an unseasoned option,
    /// whose averaging hasn't started.
    /// </summary>
    /// <param name="averageType">The averaging type.</param>
    /// <param name="payoff">The striked payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="averageType"/> isn't a member of its
    /// type.</exception>
    public ContinuousAveragingAsianOption(AverageType averageType, StrikedTypePayoff payoff, Exercise exercise)
        : base(Checked(payoff), exercise)
    {
        EnumArguments.Check(averageType, nameof(averageType));
        AverageType = averageType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContinuousAveragingAsianOption"/> class for a seasoned option, whose
    /// averaging began on a date.
    /// </summary>
    /// <param name="averageType">The averaging type.</param>
    /// <param name="startDate">The date the averaging began.</param>
    /// <param name="payoff">The striked payoff.</param>
    /// <param name="exercise">The exercise.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="exercise"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="averageType"/> isn't a member of its type, or
    /// <paramref name="startDate"/> is the default date.</exception>
    public ContinuousAveragingAsianOption(AverageType averageType, Date startDate, StrikedTypePayoff payoff, Exercise exercise)
        : this(averageType, payoff, exercise)
    {
        Date.ThrowIfDefault(startDate, nameof(startDate));
        StartDate = startDate;
    }

    /// <summary>Gets the averaging type.</summary>
    public AverageType AverageType { get; }

    /// <summary>Gets the date the averaging began; <see langword="null"/> for an unseasoned option.</summary>
    public Date? StartDate { get; }

    private static StrikedTypePayoff Checked(StrikedTypePayoff payoff)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return payoff;
    }
}
