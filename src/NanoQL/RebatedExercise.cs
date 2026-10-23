// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/rebatedexercise.hpp ql/rebatedexercise.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL;

/// <summary>
/// An option exercise with rebate payments: in case of exercise the holder receives the rebate (if positive) or pays it
/// (if negative) on the rebate settlement date.
/// </summary>
/// <remarks>
/// Immutable and sealed, an <see cref="Exercise"/> of the given exercise's type and dates (QuantLib copies the base, so
/// an early exercise's payoff-at-expiry flag isn't kept), with one rebate per exercise date.
/// </remarks>
public sealed class RebatedExercise : Exercise
{
    private readonly int rebateSettlementDays;
    private readonly Calendar rebatePaymentCalendar;
    private readonly BusinessDayConvention rebatePaymentConvention;

    /// <summary>Initializes a new instance of the <see cref="RebatedExercise"/> class, with one rebate for every
    /// exercise date.</summary>
    /// <param name="exercise">The exercise whose type and dates it takes.</param>
    /// <param name="rebate">The rebate, received if positive and paid if negative.</param>
    /// <param name="rebateSettlementDays">The business days from an exercise date to its rebate's payment.</param>
    /// <param name="rebatePaymentCalendar">The rebate payment's calendar; <see langword="null"/>, the default, for a
    /// <see cref="NullCalendar"/>.</param>
    /// <param name="rebatePaymentConvention">The rebate payment's business-day convention.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exercise"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rebateSettlementDays"/> is negative, or
    /// <paramref name="rebatePaymentConvention"/> isn't a member of its type.</exception>
    public RebatedExercise(
        Exercise exercise,
        double rebate = 0.0,
        int rebateSettlementDays = 0,
        Calendar? rebatePaymentCalendar = null,
        BusinessDayConvention rebatePaymentConvention = BusinessDayConvention.Following)
        : base(Source(exercise).Type, exercise.Dates)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rebateSettlementDays);
        EnumArguments.Check(rebatePaymentConvention, nameof(rebatePaymentConvention));
        Rebates = [.. Enumerable.Repeat(rebate, Dates.Length)];
        this.rebateSettlementDays = rebateSettlementDays;
        this.rebatePaymentCalendar = rebatePaymentCalendar ?? new NullCalendar();
        this.rebatePaymentConvention = rebatePaymentConvention;
    }

    /// <summary>Initializes a new instance of the <see cref="RebatedExercise"/> class, with a rebate per exercise date
    /// of a Bermudan exercise.</summary>
    /// <param name="exercise">The Bermudan exercise whose dates it takes.</param>
    /// <param name="rebates">The rebates, one per exercise date, each received if positive and paid if
    /// negative.</param>
    /// <param name="rebateSettlementDays">The business days from an exercise date to its rebate's payment.</param>
    /// <param name="rebatePaymentCalendar">The rebate payment's calendar; <see langword="null"/>, the default, for a
    /// <see cref="NullCalendar"/>.</param>
    /// <param name="rebatePaymentConvention">The rebate payment's business-day convention.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exercise"/> or <paramref name="rebates"/> is
    /// null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rebateSettlementDays"/> is negative, or
    /// <paramref name="rebatePaymentConvention"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The exercise isn't Bermudan, or the rebates aren't one per exercise
    /// date.</exception>
    public RebatedExercise(
        Exercise exercise,
        IEnumerable<double> rebates,
        int rebateSettlementDays = 0,
        Calendar? rebatePaymentCalendar = null,
        BusinessDayConvention rebatePaymentConvention = BusinessDayConvention.Following)
        : base(Source(exercise).Type, exercise.Dates)
    {
        ArgumentNullException.ThrowIfNull(rebates);
        ArgumentOutOfRangeException.ThrowIfNegative(rebateSettlementDays);
        EnumArguments.Check(rebatePaymentConvention, nameof(rebatePaymentConvention));
        ImmutableArray<double> given = [.. rebates];
        if (!(Type == ExerciseType.Bermudan))
        {
            throw new ArgumentException("a rebate vector is allowed only for a bermudan style exercise", nameof(exercise));
        }

        // QuantLib's text, its closing parenthesis missing
        if (!(given.Length == Dates.Length))
        {
            throw new ArgumentException(
                $"the number of rebates ({given.Length}) must be equal to the number of exercise dates ({Dates.Length}",
                nameof(rebates));
        }

        Rebates = given;
        this.rebateSettlementDays = rebateSettlementDays;
        this.rebatePaymentCalendar = rebatePaymentCalendar ?? new NullCalendar();
        this.rebatePaymentConvention = rebatePaymentConvention;
    }

    /// <summary>Gets the rebates, one per exercise date.</summary>
    public ImmutableArray<double> Rebates { get; }

    /// <summary>The rebate of an exercise date.</summary>
    /// <param name="index">The exercise date's index.</param>
    /// <returns>The rebate.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    /// <exception cref="ArgumentException">No rebate has the index.</exception>
    public double Rebate(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (!(index < Rebates.Length))
        {
            throw new ArgumentException(
                $"rebate with index {index} does not exist (0...{(ulong)(uint)Rebates.Length - 1})", nameof(index));
        }

        return Rebates[index];
    }

    /// <summary>The payment date of an exercise date's rebate: the date advanced by the settlement days on the
    /// payment calendar, with its convention.</summary>
    /// <param name="index">The exercise date's index.</param>
    /// <returns>The payment date.</returns>
    /// <exception cref="NanoQLException">The exercise is American.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative or past the last exercise
    /// date, which QuantLib reads unchecked.</exception>
    public Date RebatePaymentDate(int index)
    {
        if (!(Type == ExerciseType.European || Type == ExerciseType.Bermudan))
        {
            throw new NanoQLException(
                "for american style exercises the rebate payment date has to be calculted in the client code");
        }

        return rebatePaymentCalendar.Advance(DateAt(index), rebateSettlementDays, TimeUnit.Days, rebatePaymentConvention);
    }

    private static Exercise Source(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        return exercise;
    }
}
