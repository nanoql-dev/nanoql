// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Theo Boafo
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/bond/discretizedconvertible.hpp ql/pricingengines/bond/discretizedconvertible.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// A convertible bond on a Tsiveriotis-Fernandes lattice: its redemption rolled back with its coupons added, its
/// callabilities applied and its conversion taken where it pays more, with each node's conversion probability and the
/// blended rate it is discounted at.
/// </summary>
/// <remarks>
/// <para>
/// A working type (<see cref="DiscretizedAsset"/>), built for one calculation from the bond's arguments at the context's
/// settlement date. Times are the risk-free curve's year fractions from the bond's settlement date, moved to the closest
/// times of the given grid. The dividends not yet paid at settlement are added back to the underlying at each node, each
/// discounted from its date on the risk-free curve. A node's discount rate blends the risk-free rate and the risk-free
/// rate plus the credit spread by its conversion probability.
/// </para>
/// <para>
/// <b>The American window</b> is bounded by its two times compared with <c>IsOnTime</c> as well, upstream's fix after
/// v1.44 (#2878, the S-sr plan's SSR5): the grid's last time can be an ulp off the maturity the engine starts the asset
/// at, where QuantLib 1.44's plain comparison skips the conversion at expiry or rejects a window ending there
/// (<c>Divergences/AmericanBoundsOnTimeTests.cs</c>).
/// </para>
/// <para>
/// Its conversion probabilities and blended rates are double-buffered, as its values are, so a rollback allocates none
/// (the S-sr plan's SSD9); the underlying's grid is read into a buffer reserved at its widest step.
/// </para>
/// </remarks>
[WorkingType]
public sealed class DiscretizedConvertible : DiscretizedAsset
{
    private readonly ConvertibleBondArguments arguments;
    private readonly GeneralizedBlackScholesProcess process;
    private readonly double creditSpread;
    private readonly ImmutableArray<Dividend> dividends;
    private readonly ImmutableArray<double> dividendValues;
    private readonly double[] stoppingTimes;
    private readonly double[] callabilityTimes;
    private readonly double[] couponTimes;
    private readonly double[] couponAmounts;
    private readonly double[] dividendTimes;
    private double[] conversionProbability = [];
    private double[] spareConversionProbability = [];
    private double[] spreadAdjustedRate = [];
    private double[] spareSpreadAdjustedRate = [];
    private double[] grid = [];

    /// <summary>Initializes a new instance of the <see cref="DiscretizedConvertible"/> class.</summary>
    /// <param name="bond">The convertible bond, whose arguments are computed at the context's settlement date.</param>
    /// <param name="process">The Black-Scholes process of the underlying, whose risk-free curve gives the times.</param>
    /// <param name="dividends">The underlying's dividends; none for <c>default</c>.</param>
    /// <param name="creditSpread">The credit spread.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="grid">
    /// The time grid the asset's times are moved to the closest of, or <see langword="null"/> for none, QuantLib's empty
    /// grid.
    /// </param>
    /// <exception cref="ArgumentNullException">The bond, the process or the context is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A dividend is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The bond's arguments, a cash flow's amount or a discount can't be computed.</exception>
    public DiscretizedConvertible(
        ConvertibleBond bond,
        GeneralizedBlackScholesProcess process,
        ImmutableArray<Dividend> dividends,
        double creditSpread,
        EvaluationContext context,
        TimeGrid? grid = null)
        : this(ArgumentsOf(bond, context), process, Dividends(dividends), creditSpread, context, grid)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DiscretizedConvertible"/> class from the bond's arguments.</summary>
    /// <param name="args">The bond's arguments.</param>
    /// <param name="process">The Black-Scholes process of the underlying.</param>
    /// <param name="dividends">The underlying's dividends, none of them null.</param>
    /// <param name="creditSpread">The credit spread.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="grid">The time grid the times are moved to the closest of, or <see langword="null"/> for none.</param>
    internal DiscretizedConvertible(
        ConvertibleBondArguments args,
        GeneralizedBlackScholesProcess process,
        ImmutableArray<Dividend> dividends,
        double creditSpread,
        EvaluationContext context,
        TimeGrid? grid)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(context);
        arguments = args;
        this.process = process;
        this.creditSpread = creditSpread;

        ImmutableArray<Dividend>.Builder kept = ImmutableArray.CreateBuilder<Dividend>();
        foreach (Dividend dividend in dividends)
        {
            if (!dividend.HasOccurred(context, arguments.SettlementDate, false))
            {
                kept.Add(dividend);
            }
        }

        this.dividends = kept.ToImmutable();

        double[] values = new double[this.dividends.Length];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = this.dividends[i].Amount(context) * process.RiskFreeRate.Discount(this.dividends[i].Date);
        }

        dividendValues = [.. values];

        DayCounter dayCounter = process.RiskFreeRate.DayCounter;
        Date bondSettlement = arguments.SettlementDate;

        stoppingTimes = new double[arguments.Exercise.Dates.Length];
        for (int i = 0; i < stoppingTimes.Length; ++i)
        {
            stoppingTimes[i] = dayCounter.YearFraction(bondSettlement, arguments.Exercise.DateAt(i));
        }

        callabilityTimes = new double[arguments.CallabilityDates.Length];
        for (int i = 0; i < callabilityTimes.Length; ++i)
        {
            callabilityTimes[i] = dayCounter.YearFraction(bondSettlement, arguments.CallabilityDates[i]);
        }

        var times = new List<double>();
        var amounts = new List<double>();
        for (int i = 0; i < arguments.Cashflows.Length - 1; ++i)
        {
            CashFlow cf = arguments.Cashflows[i];
            if (!cf.HasOccurred(context, bondSettlement, false))
            {
                times.Add(dayCounter.YearFraction(bondSettlement, cf.Date));
                amounts.Add(cf.Amount(context));
            }
        }

        couponTimes = [.. times];
        couponAmounts = [.. amounts];

        dividendTimes = new double[this.dividends.Length];
        for (int i = 0; i < dividendTimes.Length; ++i)
        {
            dividendTimes[i] = dayCounter.YearFraction(bondSettlement, this.dividends[i].Date);
        }

        if (grid is not null && !grid.Empty)
        {
            // adjust times to grid
            for (int i = 0; i < stoppingTimes.Length; i++)
            {
                stoppingTimes[i] = grid.ClosestTime(stoppingTimes[i]);
            }

            for (int i = 0; i < couponTimes.Length; i++)
            {
                couponTimes[i] = grid.ClosestTime(couponTimes[i]);
            }

            for (int i = 0; i < callabilityTimes.Length; i++)
            {
                callabilityTimes[i] = grid.ClosestTime(callabilityTimes[i]);
            }

            for (int i = 0; i < dividendTimes.Length; i++)
            {
                dividendTimes[i] = grid.ClosestTime(dividendTimes[i]);
            }
        }
    }

    /// <summary>Gets each node's probability of conversion.</summary>
    public ReadOnlySpan<double> ConversionProbability => conversionProbability.AsSpan(0, Values.Length);

    /// <summary>Gets each node's discount rate, the risk-free rate and the credit spread blended by the conversion probability.</summary>
    public ReadOnlySpan<double> SpreadAdjustedRate => spreadAdjustedRate.AsSpan(0, Values.Length);

    /// <summary>Gets the present values of the dividends not yet paid at settlement, on the risk-free curve.</summary>
    public ImmutableArray<double> DividendValues => dividendValues;

    /// <summary>
    /// Sets the values to the redemption, applies the conversion and the bond's other events, then sets each node's
    /// blended rate from its conversion probability.
    /// </summary>
    /// <param name="size">The number of values.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    /// <exception cref="NanoQLException">
    /// The asset isn't initialized, the exercise is of an unknown type, or the risk-free rate can't be computed.
    /// </exception>
    public override void Reset(int size)
    {
        // the widest step a rollback reads, the one it starts from
        if (grid.Length < size)
        {
            grid = new double[size];
            conversionProbability = new double[size];
            spareConversionProbability = new double[size];
            spreadAdjustedRate = new double[size];
            spareSpreadAdjustedRate = new double[size];
        }

        // Set to bond redemption values
        ResetValues(size, arguments.Redemption);

        // coupon amounts should be added when adjusting
        // values_ = Array(size, arguments_.cashFlows.back()->amount());
        conversionProbability.AsSpan(0, size).Clear();
        spreadAdjustedRate.AsSpan(0, size).Clear();

        DayCounter rfdc = process.RiskFreeRate.DayCounter;

        // this takes care of convertibility and conversion probabilities
        AdjustValues();

        Date exercise = arguments.Exercise.LastDate();
        double riskFreeRate = process.RiskFreeRate.ZeroRate(exercise, rfdc, Compounding.Continuous, Frequency.NoFrequency).Rate;

        // Calculate blended discount rate to be used on roll back.
        for (int j = 0; j < size; j++)
        {
            spreadAdjustedRate[j] = (conversionProbability[j] * riskFreeRate)
                + ((1 - conversionProbability[j]) * (riskFreeRate + creditSpread));
        }
    }

    /// <summary>The asset's mandatory times: its exercise times, then its callability times, then its coupon times.</summary>
    /// <returns>The times.</returns>
    public override ImmutableArray<double> MandatoryTimes() => [.. stoppingTimes, .. callabilityTimes, .. couponTimes];

    /// <summary>The spare buffers of a step back, for the conversion probabilities and the blended rates.</summary>
    /// <param name="size">The number of values of the step.</param>
    /// <param name="newConversionProbability">The buffer of the step's conversion probabilities.</param>
    /// <param name="newSpreadAdjustedRate">The buffer of the step's blended rates.</param>
    internal void StepArrays(int size, out Span<double> newConversionProbability, out Span<double> newSpreadAdjustedRate)
    {
        if (spareConversionProbability.Length < size)
        {
            spareConversionProbability = new double[size];
            spareSpreadAdjustedRate = new double[size];
        }

        newConversionProbability = spareConversionProbability.AsSpan(0, size);
        newSpreadAdjustedRate = spareSpreadAdjustedRate.AsSpan(0, size);
    }

    /// <summary>Makes the spare buffers of a step back the current ones, as <see cref="DiscretizedAsset.CommitStep"/> does the values.</summary>
    internal void CommitArrays()
    {
        (conversionProbability, spareConversionProbability) = (spareConversionProbability, conversionProbability);
        (spreadAdjustedRate, spareSpreadAdjustedRate) = (spareSpreadAdjustedRate, spreadAdjustedRate);
    }

    /// <summary>Applies the callabilities, adds the coupons and applies the conversion at their times.</summary>
    /// <exception cref="NanoQLException">The exercise or a callability is of an unknown type.</exception>
    protected override void PostAdjustValuesImpl()
    {
        bool convertible = false;
        switch (arguments.Exercise.Type)
        {
            case ExerciseType.American:
                // upstream's fix after v1.44 (#2878): both bounds also on time
                if ((Time <= stoppingTimes[1] || IsOnTime(stoppingTimes[1]))
                    && (Time >= stoppingTimes[0] || IsOnTime(stoppingTimes[0])))
                {
                    convertible = true;
                }

                break;
            case ExerciseType.European:
                if (IsOnTime(stoppingTimes[0]))
                {
                    convertible = true;
                }

                break;
            case ExerciseType.Bermudan:
                foreach (double stoppingTime in stoppingTimes)
                {
                    if (IsOnTime(stoppingTime))
                    {
                        convertible = true;
                    }
                }

                break;
            default:
                throw new NanoQLException("invalid option type");
        }

        for (int i = 0; i < callabilityTimes.Length; i++)
        {
            if (IsOnTime(callabilityTimes[i]))
            {
                ApplyCallability(i, convertible);
            }
        }

        for (int i = 0; i < couponTimes.Length; i++)
        {
            if (IsOnTime(couponTimes[i]))
            {
                AddCoupon(i);
            }
        }

        if (convertible)
        {
            ApplyConvertibility();
        }
    }

    // The public constructor's arguments, checked in its parameters' order.
    private static ConvertibleBondArguments ArgumentsOf(ConvertibleBond bond, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        return bond.SetupArguments(context);
    }

    // The schedule given: none for default, a null dividend refused.
    private static ImmutableArray<Dividend> Dividends(ImmutableArray<Dividend> dividends) =>
        FdBlackScholesBarrierEngine.Dividends(dividends, nameof(dividends));

    // std::min and std::max
    private static double Min(double a, double b) => b < a ? b : a;

    private static double Max(double a, double b) => a < b ? b : a;

    private void ApplyConvertibility()
    {
        ReadOnlySpan<double> adjusted = AdjustedGrid();
        Span<double> values = Values;
        for (int j = 0; j < values.Length; j++)
        {
            double payoff = arguments.ConversionRatio * adjusted[j];
            if (values[j] <= payoff)
            {
                values[j] = payoff;
                conversionProbability[j] = 1.0;
            }
        }
    }

    private void ApplyCallability(int i, bool convertible)
    {
        ReadOnlySpan<double> adjusted = AdjustedGrid();
        Span<double> values = Values;
        switch (arguments.CallabilityTypes[i])
        {
            case CallabilityType.Call:
                if (arguments.CallabilityTriggers[i] is double callabilityTrigger)
                {
                    double conversionValue = arguments.Redemption / arguments.ConversionRatio;
                    double trigger = conversionValue * callabilityTrigger;
                    for (int j = 0; j < values.Length; j++)
                    {
                        // the callability is conditioned by the trigger...
                        if (adjusted[j] >= trigger)
                        {
                            // ...and might trigger conversion
                            values[j] = Min(Max(arguments.CallabilityPrices[i], arguments.ConversionRatio * adjusted[j]), values[j]);
                        }
                    }
                }
                else if (convertible)
                {
                    for (int j = 0; j < values.Length; j++)
                    {
                        // exercising the callability might trigger conversion
                        values[j] = Min(Max(arguments.CallabilityPrices[i], arguments.ConversionRatio * adjusted[j]), values[j]);
                    }
                }
                else
                {
                    for (int j = 0; j < values.Length; j++)
                    {
                        values[j] = Min(arguments.CallabilityPrices[i], values[j]);
                    }
                }

                break;
            case CallabilityType.Put:
                for (int j = 0; j < values.Length; j++)
                {
                    values[j] = Max(values[j], arguments.CallabilityPrices[i]);
                }

                break;
            default:
                throw new NanoQLException("unknown callability type");
        }
    }

    private void AddCoupon(int i)
    {
        Span<double> values = Values;
        double amount = couponAmounts[i];
        for (int j = 0; j < values.Length; j++)
        {
            values[j] += amount;
        }
    }

    // The lattice's grid at the asset's time, with every dividend from then on added back, discounted to then.
    private ReadOnlySpan<double> AdjustedGrid()
    {
        double t = Time;
        int size = Values.Length;
        if (grid.Length < size)
        {
            grid = new double[size];
        }

        // The asset is adjusting, so it is initialized.
        Method!.Grid(t, grid);
        Span<double> adjusted = grid.AsSpan(0, size);

        // add back all dividend amounts in the future
        for (int i = 0; i < dividends.Length; i++)
        {
            double dividendTime = dividendTimes[i];
            if (dividendTime >= t || ComparisonFunctions.Close(dividendTime, t))
            {
                Dividend d = dividends[i];
                double dividendDiscount = process.RiskFreeRate.Discount(dividendTime) / process.RiskFreeRate.Discount(t);
                for (int j = 0; j < adjusted.Length; j++)
                {
                    adjusted[j] += d.Amount(adjusted[j]) * dividendDiscount;
                }
            }
        }

        return adjusted;
    }
}
