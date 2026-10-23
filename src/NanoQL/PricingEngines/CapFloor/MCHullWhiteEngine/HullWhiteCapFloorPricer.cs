// SPDX-FileCopyrightText: Copyright (C) 2006 Banca Profilo S.p.A.
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/capfloor/mchullwhiteengine.hpp ql/pricingengines/capfloor/mchullwhiteengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Monte Carlo Hull-White cap/floor engine's path pricer: on a path of the short rate in the forward measure of
/// the last payment, each live optionlet's payoff, its forward forecast on the model's bonds at its fixing, accrued to
/// the last payment.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's detail class. The path holds the short rate at 0, at each fixing after the reference date and at the last
/// payment. A caplet paid on or before the reference date is skipped; one fixed on or before it pays on its known
/// forward; any other on (P(t, S)/P(t, E) − 1)/τ, the bonds at its fixing on the rate there. Each payoff is accrued from
/// its payment E to the last payment T by 1/P(E, T) on the rate at the path's next time after the caplet's fixing,
/// the next fixing or the last payment, not at E: QuantLib's approximation, kept (the S-sr plan's SSD18). The sum is
/// discounted from T on the curve.
/// </para>
/// <para>
/// <b>An in-arrears caplet</b> still to be fixed, fixed after its start, forecasts its rate on its index's own period
/// from the fixing date, (P(t, s)/P(t, e) − 1)/τᵢ, and its payment, a few days after the fixing, is valued at the
/// fixing exactly, P(t, E)/P(t, T) on the rate there: a divergence (the S-sr plan's B2-1, ruled by the user on
/// 2026-10-22; <c>Divergences/CapletInArrearsTests.cs</c>), QuantLib's forecast reading the bond to the coupon's start,
/// before the fixing, and its accrual the rate at the next fixing, about a period after the payment
/// (<c>mchullwhiteengine.cpp:80-90</c>).
/// </para>
/// <para>
/// <b>A collar</b> is its cap less its floor, as the analytic and tree engines price it: a divergence, QuantLib's
/// pricing it as its floor alone, long, since it reads the cap rates only for a cap
/// (<c>mchullwhiteengine.cpp:54</c>, <c>:94-99</c>;<c>Divergences/McHullWhiteCollarTests.cs</c>).
/// </para>
/// </remarks>
internal sealed class HullWhiteCapFloorPricer : PathPricer<MonteCarloPath>
{
    private readonly CapFloorArguments args;
    private readonly HullWhite model;
    private readonly double forwardMeasureTime;
    private readonly double endDiscount;
    private readonly double[] startTimes;
    private readonly double[] endTimes;
    private readonly double[] fixingTimes;
    private readonly bool[] inArrears;
    private readonly double[] indexStartTimes;
    private readonly double[] indexEndTimes;
    private readonly double[] indexTaus;

    /// <summary>Initializes a new instance of the <see cref="HullWhiteCapFloorPricer"/> class.</summary>
    /// <param name="args">The cap's arguments.</param>
    /// <param name="model">The Hull-White model.</param>
    /// <param name="forwardMeasureTime">The time of the last payment, the forward measure's.</param>
    public HullWhiteCapFloorPricer(CapFloorArguments args, HullWhite model, double forwardMeasureTime)
    {
        this.args = args;
        this.model = model;
        this.forwardMeasureTime = forwardMeasureTime;
        endDiscount = model.TermStructure.Discount(forwardMeasureTime);

        Date referenceDate = model.TermStructure.ReferenceDate;
        DayCounter dayCounter = model.TermStructure.DayCounter;

        startTimes = new double[args.StartDates.Length];
        for (int i = 0; i < startTimes.Length; ++i)
        {
            startTimes[i] = dayCounter.YearFraction(referenceDate, args.StartDates[i]);
        }

        endTimes = new double[args.EndDates.Length];
        for (int i = 0; i < endTimes.Length; ++i)
        {
            endTimes[i] = dayCounter.YearFraction(referenceDate, args.EndDates[i]);
        }

        fixingTimes = new double[args.FixingDates.Length];
        for (int i = 0; i < fixingTimes.Length; ++i)
        {
            fixingTimes[i] = dayCounter.YearFraction(referenceDate, args.FixingDates[i]);
        }

        // B2-1: a caplet still to be fixed after its start, in arrears, on its index's own period from the fixing date
        inArrears = new bool[fixingTimes.Length];
        indexStartTimes = new double[fixingTimes.Length];
        indexEndTimes = new double[fixingTimes.Length];
        indexTaus = new double[fixingTimes.Length];
        for (int i = 0; i < fixingTimes.Length; ++i)
        {
            inArrears[i] = fixingTimes[i] > 0.0 && fixingTimes[i] > startTimes[i];
            if (inArrears[i])
            {
                InterestRateIndex index = args.Indexes[i];
                Date valueDate = index.ValueDate(args.FixingDates[i]);
                Date maturityDate = index.MaturityDate(valueDate);
                indexStartTimes[i] = dayCounter.YearFraction(referenceDate, valueDate);
                indexEndTimes[i] = dayCounter.YearFraction(referenceDate, maturityDate);
                indexTaus[i] = index.DayCounter.YearFraction(valueDate, maturityDate);
            }
        }
    }

    /// <summary>The path's discounted payoff.</summary>
    /// <param name="path">The short rate's path on the engine's grid.</param>
    /// <returns>The payoff.</returns>
    /// <exception cref="NanoQLException">A known optionlet's forward isn't available.</exception>
    public override double Evaluate(MonteCarloPath path)
    {
        CapFloorType type = args.Type;
        double npv = 0.0;
        double Tb = forwardMeasureTime;

        int pastFixings = 0;
        for (int i = 0; i < fixingTimes.Length; i++)
        {
            double tau = args.AccrualTimes[i];
            double start = startTimes[i],
                end = endTimes[i],
                fixing = fixingTimes[i];
            if (end <= 0.0)
            {
                // the fixing is in the past...
                pastFixings++;

                // ...and the caplet is expired; nothing more to do.
            }
            else
            {
                double ri_1, ri_2, currentLibor, accrualFactor;
                if (fixing <= 0.0)
                {
                    // current caplet. The fixing is in the past...
                    pastFixings++;

                    // ...so it is determined.
                    currentLibor = args.Forwards[i] ?? throw new NanoQLException("the fixed optionlet's forward is not available");

                    // However, the short rate at caplet expiry is not.
                    ri_2 = path[i - pastFixings + 2];
                    accrualFactor = 1.0 / model.DiscountBond(end, Tb, ri_2);
                }
                else if (inArrears[i])
                {
                    // B2-1: an in-arrears caplet's rate on its index's period, and its payment, a few days after the
                    // fixing, valued there against the forward measure's bond, both on the fixing's short rate
                    ri_1 = path[i - pastFixings + 1];
                    double d1 = model.DiscountBond(fixing, indexStartTimes[i], ri_1);
                    double d2 = model.DiscountBond(fixing, indexEndTimes[i], ri_1);
                    currentLibor = ((d1 / d2) - 1) / indexTaus[i];
                    accrualFactor = model.DiscountBond(fixing, end, ri_1) / model.DiscountBond(fixing, Tb, ri_1);
                }
                else
                {
                    // future caplet. Everything is to be forecast.
                    // The number of past fixings is used as an offset
                    // to index into the path.
                    ri_1 = path[i - pastFixings + 1];
                    ri_2 = path[i - pastFixings + 2];

                    double d1 = model.DiscountBond(fixing, start, ri_1);
                    double d2 = model.DiscountBond(fixing, end, ri_1);
                    currentLibor = ((d1 / d2) - 1) / tau;
                    accrualFactor = 1.0 / model.DiscountBond(end, Tb, ri_2);
                }

                double payoff = type == CapFloorType.Floor
                    ? Floor0(args.FloorRates[i]!.Value - currentLibor)
                    : Floor0(currentLibor - args.CapRates[i]!.Value);

                // a collar is short its floor
                if (type == CapFloorType.Collar)
                {
                    payoff -= Floor0(args.FloorRates[i]!.Value - currentLibor);
                }

                npv += payoff * tau * args.Gearings[i] * args.Nominals[i] * accrualFactor;
            }
        }

        npv *= endDiscount;
        return npv;
    }

    // std::max(x, 0.0): x < 0.0 ? 0.0 : x, a NaN kept.
    private static double Floor0(double x) => x < 0.0 ? 0.0 : x;
}
