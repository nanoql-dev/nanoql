// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/capfloor/discretizedcapfloor.hpp ql/pricingengines/capfloor/discretizedcapfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>A discretized cap, floor or collar: its value on a lattice, each optionlet added as the rollback reaches it.</summary>
/// <remarks>
/// <para>
/// A working type (<see cref="DiscretizedAsset"/>). A caplet still to be fixed is added in the pre-adjustment at its
/// fixing time, as the option to pay (1 + Kτ) at its payment time for 1 at its start, the two discount bonds rolled back
/// there: (1 + Kτ)·max(P(start)/(1 + Kτ) − P(end), 0) times its nominal and gearing, a floorlet likewise; one fixed after
/// its start, in arrears, at its fixing time too, on its index's own period from the fixing date, three bonds rolled
/// back there: τ·P(pay)·max(L − K, 0), L = (P(s)/P(e) − 1)/τᵢ over the period (s, e) and its index's year fraction τᵢ,
/// times its nominal and gearing; one fixed on its start at its start time, as QuantLib adds every caplet,
/// (1 + Kτ)·max(1/(1 + Kτ) − P, 0). A
/// caplet fixed on or before the reference date, as the analytic engine knows it (its fixing time at most 0), is added in
/// the post-adjustment at its payment time, as its known amount, max(F − K, 0)·τ times its nominal and gearing; one paid
/// on the reference date counts only when the context includes today's cash flows, as the discretized swap's known
/// coupon does. The times are the given day counter's year fractions from the reference date.
/// </para>
/// <para>
/// <b>The fixing time</b>: QuantLib exercises every caplet still to be fixed at its start
/// (<c>discretizedcapfloor.cpp:53-55</c>), where the instrument fixes its rate and QuantLib's Monte Carlo engine reads
/// it at the fixing date, so a caplet with a fixing lag is priced on the variance to its start; a divergence
/// (<c>Divergences/CapletFixingExpiryTests.cs</c>). On a caplet fixed on its start, the fixing time is the start time
/// and the value QuantLib's, bit for bit. A rollback must reach the earliest fixing time a caplet is valued at, which
/// precedes the first start QuantLib's engine rolls back to (<see cref="EarliestFixingTime"/>).
/// </para>
/// <para>
/// <b>An in-arrears caplet</b>: QuantLib values it as the caplet in advance, at its start on its accrual period,
/// reading neither its later fixing nor its index's period; a divergence (<c>Divergences/CapletInArrearsTests.cs</c>)
/// with no QuantLib check, checked on Hull-White against each caplet's Gaussian integral at its fixing. Its fixing time
/// and its index period's start and end are mandatory times, the end past the cap's last payment.
/// </para>
/// <para>
/// <b>A seasoned cap</b> (widened): its mandatory times are its non-negative start and payment times, as the
/// discretized swap's are, then the fixing times of the caplets valued there, and a caplet is known by its fixing date:
/// a divergence, QuantLib's keeping the started caplets' negative times, so its engine refuses "negative times not
/// allowed", and knowing a caplet by its start, so between its fixing and its start it values the caplet as an option,
/// ignoring its fixing (<c>discretizedcapfloor.cpp:55</c>, <c>:91</c>; <c>Divergences/TreeCapFloorSeasonedTests.cs</c>,
/// <c>Divergences/TreeKnownCouponTests.cs</c>). A caplet still to be fixed whose start is before the reference date can
/// only be in arrears, and is valued at its fixing as any other, its start unread, where QuantLib's grid refuses its
/// negative start time ("negative times not allowed").
/// </para>
/// <para>
/// <b>Its discount bonds.</b> As <see cref="DiscretizedSwap"/>'s: one bond to the payment, one to the start (or the
/// index period's start) and one to the index period's end, re-initialized for each optionlet, their buffers reserved by
/// <see cref="Reset"/> at the widest step of the grid, so no optionlet's bonds allocate.
/// </para>
/// </remarks>
[WorkingType]
public sealed class DiscretizedCapFloor : DiscretizedAsset
{
    private readonly CapFloorArguments arguments;
    private readonly double[] startTimes;
    private readonly double[] endTimes;
    private readonly double[] fixingTimes;
    private readonly double[] indexStartTimes;
    private readonly double[] indexEndTimes;
    private readonly double[] indexTaus;
    private readonly bool[] known;
    private readonly bool[] atFixing;
    private readonly bool[] inArrears;
    private readonly DiscretizedDiscountBond bond = new();
    private readonly DiscretizedDiscountBond startBond = new();
    private readonly DiscretizedDiscountBond indexEndBond = new();

    /// <summary>Initializes a new instance of the <see cref="DiscretizedCapFloor"/> class.</summary>
    /// <param name="capFloor">The cap, floor or collar.</param>
    /// <param name="referenceDate">The date of time 0.</param>
    /// <param name="dayCounter">The day counter of the times.</param>
    /// <param name="context">
    /// The evaluation context, whose coupons' fixings and forecasts the known amounts read, and whose
    /// <see cref="EvaluationContext.IncludeTodaysCashFlows"/> decides a caplet paid on the reference date.
    /// </param>
    /// <exception cref="ArgumentNullException">The cap, the day counter or the context is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="referenceDate"/> is <see langword="default"/>.</exception>
    /// <exception cref="NanoQLException">A cash flow isn't a floating-rate coupon, or a forward can't be computed.</exception>
    public DiscretizedCapFloor(CapFloor capFloor, Date referenceDate, DayCounter dayCounter, EvaluationContext context)
        : this(Arguments(capFloor, referenceDate, dayCounter, context), referenceDate, dayCounter, context.IncludeTodaysCashFlows == true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DiscretizedCapFloor"/> class from the cap's arguments.</summary>
    /// <param name="arguments">The cap's arguments.</param>
    /// <param name="referenceDate">The date of time 0.</param>
    /// <param name="dayCounter">The day counter of the times.</param>
    /// <param name="includeTodaysCashFlows">Whether a caplet paid on the reference date counts.</param>
    internal DiscretizedCapFloor(CapFloorArguments arguments, Date referenceDate, DayCounter dayCounter, bool includeTodaysCashFlows)
    {
        this.arguments = arguments;

        startTimes = new double[arguments.StartDates.Length];
        for (int i = 0; i < startTimes.Length; ++i)
        {
            startTimes[i] = dayCounter.YearFraction(referenceDate, arguments.StartDates[i]);
        }

        endTimes = new double[arguments.EndDates.Length];
        for (int i = 0; i < endTimes.Length; ++i)
        {
            endTimes[i] = dayCounter.YearFraction(referenceDate, arguments.EndDates[i]);
        }

        fixingTimes = new double[arguments.FixingDates.Length];
        for (int i = 0; i < fixingTimes.Length; ++i)
        {
            fixingTimes[i] = dayCounter.YearFraction(referenceDate, arguments.FixingDates[i]);
        }

        // a caplet fixed on or before the reference date and paid after it, or on it with today's cash flows: the
        // discretized swap's rule for its known coupons; one still to be fixed before its start is valued at its
        // fixing, one fixed after its start, in arrears, at its fixing on its index's period, its start unread, so a
        // past one too, one fixed on its start at its start
        known = new bool[fixingTimes.Length];
        atFixing = new bool[fixingTimes.Length];
        inArrears = new bool[fixingTimes.Length];
        indexStartTimes = new double[fixingTimes.Length];
        indexEndTimes = new double[fixingTimes.Length];
        indexTaus = new double[fixingTimes.Length];
        for (int i = 0; i < known.Length; ++i)
        {
            double payTime = endTimes[i];
            known[i] = fixingTimes[i] <= 0.0 && (payTime > 0.0 || (includeTodaysCashFlows && payTime == 0.0));
            if (fixingTimes[i] > 0.0)
            {
                atFixing[i] = fixingTimes[i] < startTimes[i];
                inArrears[i] = fixingTimes[i] > startTimes[i];
                if (inArrears[i])
                {
                    // the index's own period from the fixing date, as the index forecasts its fixing
                    InterestRateIndex index = arguments.Indexes[i];
                    Date valueDate = index.ValueDate(arguments.FixingDates[i]);
                    Date maturityDate = index.MaturityDate(valueDate);
                    indexStartTimes[i] = dayCounter.YearFraction(referenceDate, valueDate);
                    indexEndTimes[i] = dayCounter.YearFraction(referenceDate, maturityDate);
                    indexTaus[i] = index.DayCounter.YearFraction(valueDate, maturityDate);
                }
            }
        }
    }

    /// <summary>
    /// Gets the earliest fixing time a caplet is valued at, before its start; <see cref="double.MaxValue"/> when none is.
    /// </summary>
    internal double EarliestFixingTime
    {
        get
        {
            double earliest = double.MaxValue;
            for (int i = 0; i < fixingTimes.Length; i++)
            {
                if (atFixing[i] && fixingTimes[i] < earliest)
                {
                    earliest = fixingTimes[i];
                }
            }

            return earliest;
        }
    }

    /// <summary>
    /// Sets the values to 0 and adjusts them; on a tree lattice, it first reserves its discount bond's buffers at the
    /// widest step from the asset's time down to the grid's start.
    /// </summary>
    /// <param name="size">The number of values.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    /// <exception cref="NanoQLException">The asset isn't initialized, or a known caplet's forward isn't available.</exception>
    public override void Reset(int size)
    {
        if (Method is TreeLattice lattice)
        {
            // the whole grid: an in-arrears caplet's index-end bond starts past the asset's time
            int widest = 0;
            for (int k = 0, i = lattice.TimeGrid.Size - 1; k <= i; k++)
            {
                int width = lattice.Size(k);
                if (width > widest)
                {
                    widest = width;
                }
            }

            bond.ReserveCapacity(widest);
            startBond.ReserveCapacity(widest);
            indexEndBond.ReserveCapacity(widest);
        }

        ResetValues(size, 0.0);
        AdjustValues();
    }

    /// <summary>
    /// The asset's mandatory times: its non-negative start times, then its non-negative payment times, then the fixing
    /// times of the caplets valued there, then each in-arrears caplet's index period's start and end.
    /// </summary>
    /// <returns>The times, not guaranteed to be sorted.</returns>
    public override ImmutableArray<double> MandatoryTimes()
    {
        var times = ImmutableArray.CreateBuilder<double>();
        foreach (double t in startTimes)
        {
            if (t >= 0.0)
            {
                times.Add(t);
            }
        }

        foreach (double t in endTimes)
        {
            if (t >= 0.0)
            {
                times.Add(t);
            }
        }

        for (int i = 0; i < fixingTimes.Length; i++)
        {
            if (atFixing[i] || inArrears[i])
            {
                times.Add(fixingTimes[i]);
            }
        }

        for (int i = 0; i < fixingTimes.Length; i++)
        {
            if (inArrears[i])
            {
                times.Add(indexStartTimes[i]);
                times.Add(indexEndTimes[i]);
            }
        }

        return times.ToImmutable();
    }

    /// <summary>
    /// Adds the optionlets still to be fixed whose fixing is now, or whose start is now for one fixed on its start, each
    /// an option on its discount bonds.
    /// </summary>
    protected override void PreAdjustValuesImpl()
    {
        for (int i = 0; i < startTimes.Length; i++)
        {
            if (known[i])
            {
                continue;
            }

            if (inArrears[i])
            {
                if (IsOnTime(fixingTimes[i]))
                {
                    // The asset is on time, so it is initialized.
                    bond.Initialize(Method!, endTimes[i]);
                    bond.Rollback(Time);
                    startBond.Initialize(Method!, indexStartTimes[i]);
                    startBond.Rollback(Time);
                    indexEndBond.Initialize(Method!, indexEndTimes[i]);
                    indexEndBond.Rollback(Time);
                    AddInArrearsOptionlet(i);
                }
            }
            else if (atFixing[i])
            {
                if (IsOnTime(fixingTimes[i]))
                {
                    // The asset is on time, so it is initialized.
                    bond.Initialize(Method!, endTimes[i]);
                    bond.Rollback(Time);
                    startBond.Initialize(Method!, startTimes[i]);
                    startBond.Rollback(Time);
                    AddOptionlet(i, startBond.Values);
                }
            }
            else if (startTimes[i] >= 0.0 && IsOnTime(startTimes[i]))
            {
                bond.Initialize(Method!, endTimes[i]);
                bond.Rollback(Time);
                AddOptionlet(i, default);
            }
        }
    }

    /// <summary>Adds the known optionlets whose payment is now, each its amount.</summary>
    /// <exception cref="NanoQLException">A known optionlet's forward isn't available.</exception>
    protected override void PostAdjustValuesImpl()
    {
        for (int i = 0; i < endTimes.Length; i++)
        {
            if (known[i] && IsOnTime(endTimes[i]))
            {
                double nominal = arguments.Nominals[i];
                double accrual = arguments.AccrualTimes[i];
                double fixing = arguments.Forwards[i] ?? throw new NanoQLException("the known caplet's forward is not available");
                double gearing = arguments.Gearings[i];
                CapFloorType type = arguments.Type;
                Span<double> values = Values;

                if (type is CapFloorType.Cap or CapFloorType.Collar)
                {
                    double cap = arguments.CapRates[i]!.Value;
                    double capletRate = Floor0(fixing - cap);
                    double amount = capletRate * accrual * nominal * gearing;
                    for (int j = 0; j < values.Length; j++)
                    {
                        values[j] += amount;
                    }
                }

                if (type is CapFloorType.Floor or CapFloorType.Collar)
                {
                    double floor = arguments.FloorRates[i]!.Value;
                    double floorletRate = Floor0(floor - fixing);
                    double amount = floorletRate * accrual * nominal * gearing;
                    for (int j = 0; j < values.Length; j++)
                    {
                        if (type == CapFloorType.Floor)
                        {
                            values[j] += amount;
                        }
                        else
                        {
                            values[j] -= amount;
                        }
                    }
                }
            }
        }
    }

    // The optionlet's value at the time of its bonds: QuantLib's option on the bond to its payment, struck at 1/(1 + Kτ),
    // the start bond's values multiplying the strike when it is valued at its fixing (none at its start, QuantLib's).
    private void AddOptionlet(int i, ReadOnlySpan<double> startBondValues)
    {
        double tenor = arguments.AccrualTimes[i];
        CapFloorType type = arguments.Type;
        double gearing = arguments.Gearings[i];
        double nominal = arguments.Nominals[i];
        Span<double> values = Values;
        ReadOnlySpan<double> bondValues = bond.Values;
        bool atStart = startBondValues.IsEmpty;

        if (type is CapFloorType.Cap or CapFloorType.Collar)
        {
            double accrual = 1.0 + (arguments.CapRates[i]!.Value * tenor);
            double strike = 1.0 / accrual;
            for (int j = 0; j < values.Length; j++)
            {
                double k = atStart ? strike : strike * startBondValues[j];
                values[j] += nominal * accrual * gearing * Floor0(k - bondValues[j]);
            }
        }

        if (type is CapFloorType.Floor or CapFloorType.Collar)
        {
            double accrual = 1.0 + (arguments.FloorRates[i]!.Value * tenor);
            double strike = 1.0 / accrual;
            double mult = type == CapFloorType.Floor ? 1.0 : -1.0;
            for (int j = 0; j < values.Length; j++)
            {
                double k = atStart ? strike : strike * startBondValues[j];
                values[j] += nominal * accrual * mult * gearing * Floor0(bondValues[j] - k);
            }
        }
    }

    // An in-arrears optionlet's value at its fixing: the index's rate over its own period from the bonds to its start
    // and end, paid at the payment, τ·P(pay)·max(L − K, 0) times the nominal and gearing.
    private void AddInArrearsOptionlet(int i)
    {
        double tenor = arguments.AccrualTimes[i];
        double tau = indexTaus[i];
        CapFloorType type = arguments.Type;
        double gearing = arguments.Gearings[i];
        double nominal = arguments.Nominals[i];
        Span<double> values = Values;
        ReadOnlySpan<double> pay = bond.Values;
        ReadOnlySpan<double> start = startBond.Values;
        ReadOnlySpan<double> end = indexEndBond.Values;

        if (type is CapFloorType.Cap or CapFloorType.Collar)
        {
            double strike = arguments.CapRates[i]!.Value;
            for (int j = 0; j < values.Length; j++)
            {
                double rate = ((start[j] / end[j]) - 1.0) / tau;
                values[j] += nominal * gearing * tenor * pay[j] * Floor0(rate - strike);
            }
        }

        if (type is CapFloorType.Floor or CapFloorType.Collar)
        {
            double strike = arguments.FloorRates[i]!.Value;
            double mult = type == CapFloorType.Floor ? 1.0 : -1.0;
            for (int j = 0; j < values.Length; j++)
            {
                double rate = ((start[j] / end[j]) - 1.0) / tau;
                values[j] += nominal * gearing * tenor * mult * pay[j] * Floor0(strike - rate);
            }
        }
    }

    // The public constructor's arguments, checked in its parameters' order.
    private static CapFloorArguments Arguments(CapFloor capFloor, Date referenceDate, DayCounter dayCounter, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(capFloor);
        Date.ThrowIfDefault(referenceDate, nameof(referenceDate));
        ArgumentNullException.ThrowIfNull(dayCounter);
        return capFloor.SetupArguments(context);
    }

    // std::max<Real>(x, 0.0): x < 0.0 ? 0.0 : x, a NaN kept.
    private static double Floor0(double x) => x < 0.0 ? 0.0 : x;
}
