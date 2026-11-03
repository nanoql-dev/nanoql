// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/vectorbsmprocessextractor.hpp ql/pricingengines/basket/vectorbsmprocessextractor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Processes;
using NanoQL.Time;
using static NanoQL.Numerics.ComparisonFunctions;

namespace NanoQL.PricingEngines;

// QuantLib's detail::VectorBsmProcessExtractor: the spots, variances, deviations and discounts of several Black-Scholes
// processes at a date, each read by date on its own structures, accruing nothing before a structure's reference date
// (the playbook's row for structures on another clock), where QuantLib's reads throw for an option expiring before a
// later structure's date; on structures dated alike the reads are QuantLib's.
internal sealed class VectorBsmProcessExtractor
{
    private readonly IReadOnlyList<GeneralizedBlackScholesProcess> processes;

    internal VectorBsmProcessExtractor(IReadOnlyList<GeneralizedBlackScholesProcess> processes)
    {
        this.processes = processes;
    }

    // QuantLib's getSpot: each process's x0
    internal double[] GetSpot() => Extract(p => p.X0);

    // QuantLib's getBlackStdDev: blackVol(date, x0)·√t, signed as the volatility is; 0 before the volatility's date
    internal double[] GetBlackStdDev(Date maturityDate) =>
        Extract(p =>
        {
            if (maturityDate < p.BlackVolatility.ReferenceDate)
            {
                return 0.0;
            }

            double maturity = p.BlackVolatility.TimeFromReference(maturityDate);
            return p.BlackVolatility.BlackVol(maturityDate, p.X0) * Math.Sqrt(maturity);
        });

    // QuantLib's getBlackVariance at the spot
    internal double[] GetBlackVariance(Date maturityDate) =>
        Extract(p => p.BlackVolatility.AccruedVariance(maturityDate, p.X0));

    // QuantLib's getDividendYieldDf
    internal double[] GetDividendYieldDf(Date maturityDate) => Extract(p => p.DividendYield.AccruedDiscount(maturityDate));

    // QuantLib's getInterestRateDf: the first process's discount, every process's close enough to it
    internal double GetInterestRateDf(Date maturityDate)
    {
        double[] dr = Extract(p => p.RiskFreeRate.AccruedDiscount(maturityDate));

        // std::equal(dr.begin()+1, dr.end(), dr.begin(), close_enough): each against the one before it
        for (int i = 1; i < dr.Length; ++i)
        {
            if (!CloseEnough(dr[i], dr[i - 1]))
            {
                throw new NanoQLException("interest rates need to be the same for all underlyings");
            }
        }

        return dr[0];
    }

    private double[] Extract(Func<GeneralizedBlackScholesProcess, double> f)
    {
        double[] x = new double[processes.Count];
        for (int i = 0; i < x.Length; ++i)
        {
            x[i] = f(processes[i]);
        }

        return x;
    }
}
