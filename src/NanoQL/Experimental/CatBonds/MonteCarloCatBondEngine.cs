// SPDX-FileCopyrightText: Copyright (C) 2012, 2013 Grzegorz Andruszkiewicz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/catbonds/montecarlocatbondengine.hpp ql/experimental/catbonds/montecarlocatbondengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The Monte Carlo cat bond engine: each simulated path of the catastrophe risk's events writes the bond's notional
/// path, whose cash flows, each scaled by the notional left on its date, are discounted and averaged.
/// </summary>
/// <remarks>
/// The path count is a parameter, 10,000 by default and refused at 0, and a simulation with no path is refused ("no
/// Monte Carlo paths were generated"), where 1.44 caps the count at a constant and divides 0 by 0: QuantLib's fix from
/// its master after the pin (#2839; <c>Divergences/MonteCarloCatBondEngineTests.cs</c>). The loss statistics are the
/// valuation date's simulation's.
/// </remarks>
[Experimental("NQL0001")]
public static class MonteCarloCatBondEngine
{
    /// <summary>Prices a cat bond.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="catRisk">The catastrophe risk.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="includeSettlementDateFlows">Whether flows on the settlement date count; the context's
    /// <see cref="EvaluationContext.IncludeReferenceDateEvents"/> when <see langword="null"/>.</param>
    /// <param name="maxPaths">The paths simulated at most.</param>
    /// <returns>The bond's results.</returns>
    /// <exception cref="ArgumentNullException">An argument but <paramref name="includeSettlementDateFlows"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxPaths"/> isn't positive ("maxPaths must be
    /// positive").</exception>
    /// <exception cref="NanoQLException">The simulation gives no path ("no Monte Carlo paths were generated"), or a
    /// curve refuses a date.</exception>
    public static CatBondResults Calculate(
        CatBond bond,
        EvaluationContext context,
        CatRisk catRisk,
        YieldTermStructure discountCurve,
        bool? includeSettlementDateFlows = null,
        int maxPaths = 10000)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catRisk);
        ArgumentNullException.ThrowIfNull(discountCurve);
        if (!(maxPaths > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(maxPaths), maxPaths, "maxPaths must be positive");
        }

        if (bond.IsExpired(context))
        {
            return bond.SetupExpired();
        }

        // QuantLib's setupArguments: the settlement date and the start date, the issue date
        Date settlementDate = bond.SettlementDate(context);
        bond.Validate();

        Date valuationDate = discountCurve.ReferenceDate;
        bool includeRefDateFlows = includeSettlementDateFlows ?? context.IncludeReferenceDateEvents;

        double value = Npv(
            bond, context, catRisk, discountCurve, maxPaths, includeRefDateFlows, valuationDate, valuationDate,
            out double lossProbability, out double exhaustionProbability, out double expectedLoss);

        // a bond's cashflow on settlement date is never taken into
        // account, so we might have to play it safe and recalculate
        double settlementValue;
        if (!includeRefDateFlows && valuationDate == settlementDate)
        {
            // same parameters as above, we can avoid another call
            settlementValue = value;
        }
        else
        {
            // no such luck
            settlementValue = Npv(
                bond, context, catRisk, discountCurve, maxPaths, includeRefDateFlows, settlementDate, settlementDate,
                out _, out _, out _);
        }

        return bond.FetchResults(new CatBondResults
        {
            NPV = value,
            ValuationDate = valuationDate,
            SettlementValue = settlementValue,
            LossProbability = lossProbability,
            ExhaustionProbability = exhaustionProbability,
            ExpectedLoss = expectedLoss,
        });
    }

    private static double Npv(
        CatBond bond,
        EvaluationContext context,
        CatRisk catRisk,
        YieldTermStructure discountCurve,
        int maxPaths,
        bool includeSettlementDateFlows,
        Date settlementDate,
        Date npvDate,
        out double lossProbability,
        out double exhaustionProbability,
        out double expectedLoss)
    {
        lossProbability = 0.0;
        exhaustionProbability = 0.0;
        expectedLoss = 0.0;

        // QuantLib's empty leg, its default dates and its null start date can't reach here: the bond is validated, the
        // dates are set, and a null issue date is before every settlement date.
        double totalNPV = 0.0;
        Date effectiveDate = bond.IssueDate is Date start && start > settlementDate ? start : settlementDate;
        Date maturityDate = bond.Cashflows[^1].Date;
        CatSimulation catSimulation = catRisk.NewSimulation(effectiveDate, maturityDate);
        var eventsPath = new List<KeyValuePair<Date, double>>();
        var notionalPath = new NotionalPath();
        double riskFreeNPV = PathNpv(bond, context, discountCurve, includeSettlementDateFlows, settlementDate, notionalPath);
        int pathCount = 0;
        while (catSimulation.NextPath(eventsPath) && pathCount < maxPaths)
        {
            bond.NotionalRisk.UpdatePath(eventsPath, notionalPath);

            // optimization: most paths will not include any loss
            if (notionalPath.Loss() > 0)
            {
                totalNPV += PathNpv(bond, context, discountCurve, includeSettlementDateFlows, settlementDate, notionalPath);
                lossProbability += 1;
                if (notionalPath.Loss() == 1)
                {
                    exhaustionProbability += 1;
                }

                expectedLoss += notionalPath.Loss();
            }
            else
            {
                totalNPV += riskFreeNPV;
            }

            pathCount++;
        }

        if (!(pathCount > 0))
        {
            throw new NanoQLException("no Monte Carlo paths were generated");
        }

        lossProbability /= pathCount;
        exhaustionProbability /= pathCount;
        expectedLoss /= pathCount;
        return totalNPV / (pathCount * discountCurve.Discount(npvDate));
    }

    private static double PathNpv(
        CatBond bond,
        EvaluationContext context,
        YieldTermStructure discountCurve,
        bool includeSettlementDateFlows,
        Date settlementDate,
        NotionalPath notionalPath)
    {
        double totalNPV = 0.0;
        foreach (CashFlow cashflow in bond.Cashflows)
        {
            if (!cashflow.HasOccurred(context, settlementDate, includeSettlementDateFlows))
            {
                // TODO: fix for more complicated cashflows
                double amount = cashflow.Amount(context) * notionalPath.NotionalRate(cashflow.Date);
                totalNPV += amount * discountCurve.Discount(cashflow.Date);
            }
        }

        return totalNPV;
    }
}
