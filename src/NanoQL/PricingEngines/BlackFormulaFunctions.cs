// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2013 Gary Kennedy
// SPDX-FileCopyrightText: Copyright (C) 2015, 2024 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2017 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2019 Wojciech Ślusarski
// SPDX-FileCopyrightText: Copyright (C) 2020 Marcin Rybacki
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2008, 2012 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/blackformula.hpp ql/pricingengines/blackformula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: every function but checkParameters, blackFormula(Option::Type, ...) and bachelierBlackFormula(Option::Type, ...), which wait for S6

using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>Black and Bachelier formulas.</summary>
public static class BlackFormulaFunctions
{
    /// <summary>
    /// Black's formula: the price of an option on a forward, with a shifted lognormal volatility when a displacement is
    /// given.
    /// </summary>
    /// <param name="optionType">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The price comes out negative.</exception>
    public static double BlackFormula(
        OptionType optionType,
        double strike,
        double forward,
        double stdDev,
        double discount = 1.0,
        double displacement = 0.0)
    {
        EnumArguments.Check(optionType, nameof(optionType));
        CheckParameters(strike, forward, displacement);
        if (!(stdDev >= 0.0))
        {
            throw new ArgumentException($"stdDev ({Show(stdDev)}) must be non-negative", nameof(stdDev));
        }

        if (!(discount > 0.0))
        {
            throw new ArgumentException($"discount ({Show(discount)}) must be positive", nameof(discount));
        }

        int sign = (int)optionType;

        if (stdDev == 0.0)
        {
            return Max((forward - strike) * sign, 0.0) * discount;
        }

        forward += displacement;
        strike += displacement;

        // since displacement is non-negative strike==0 iff displacement==0
        // so returning forward*discount is OK
        if (strike == 0.0)
        {
            return optionType == OptionType.Call ? forward * discount : 0.0;
        }

        double d1 = (Math.Log(forward / strike) / stdDev) + (0.5 * stdDev);
        double d2 = d1 - stdDev;
        var phi = new CumulativeNormalDistribution();
        double nd1 = phi.Evaluate(sign * d1);
        double nd2 = phi.Evaluate(sign * d2);
        double result = discount * sign * ((forward * nd1) - (strike * nd2));
        if (!(result >= 0.0))
        {
            throw new NanoQLException(
                $"negative value ({Show(result)}) for {Show(stdDev)} stdDev, {optionType} option, {Show(strike)} strike , "
                + $"{Show(forward)} forward");
        }

        return result;
    }

    /// <summary>The Bachelier formula: the price of an option on a forward, with a normal volatility.</summary>
    /// <param name="optionType">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the normal volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The price comes out negative.</exception>
    public static double BachelierBlackFormula(
        OptionType optionType,
        double strike,
        double forward,
        double stdDev,
        double discount = 1.0)
    {
        EnumArguments.Check(optionType, nameof(optionType));
        if (!(stdDev >= 0.0))
        {
            throw new ArgumentException($"stdDev ({Show(stdDev)}) must be non-negative", nameof(stdDev));
        }

        if (!(discount > 0.0))
        {
            throw new ArgumentException($"discount ({Show(discount)}) must be positive", nameof(discount));
        }

        double d = (forward - strike) * (int)optionType, h = d / stdDev;
        if (stdDev == 0.0)
        {
            return discount * Max(d, 0.0);
        }

        var phi = new CumulativeNormalDistribution();
        double result = discount * ((stdDev * phi.Derivative(h)) + (d * phi.Evaluate(h)));
        if (!(result >= 0.0))
        {
            throw new NanoQLException(
                $"negative value ({Show(result)}) for {Show(stdDev)} stdDev, {optionType} option, {Show(strike)} strike , "
                + $"{Show(forward)} forward");
        }

        return result;
    }

    private static void CheckParameters(double strike, double forward, double displacement)
    {
        if (!(displacement >= 0.0))
        {
            throw new ArgumentException($"displacement ({Show(displacement)}) must be non-negative", nameof(displacement));
        }

        if (!(strike + displacement >= 0.0))
        {
            throw new ArgumentException(
                $"strike + displacement ({Show(strike)} + {Show(displacement)}) must be non-negative", nameof(strike));
        }

        if (!(forward + displacement > 0.0))
        {
            throw new ArgumentException(
                $"forward + displacement ({Show(forward)} + {Show(displacement)}) must be positive", nameof(forward));
        }
    }

    // std::max's semantics, which keep or drop a NaN by position where Math.Max propagates it.
    private static double Max(double a, double b) => a < b ? b : a;

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}
