// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/prices.hpp ql/prices.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>QuantLib's price functions: the mid equivalent and the safe mid of quoted prices.</summary>
/// <remarks>A price QuantLib marks with <c>Null&lt;Real&gt;()</c> is <see langword="null"/>.</remarks>
public static class PricesFunctions
{
    /// <summary>
    /// The MidEquivalent price: the mid if available, or a suitable substitute if the proper mid is not available.
    /// </summary>
    /// <param name="bid">The bid price, or <see langword="null"/>.</param>
    /// <param name="ask">The ask price, or <see langword="null"/>.</param>
    /// <param name="last">The last price, or <see langword="null"/>.</param>
    /// <param name="close">The close price, or <see langword="null"/>.</param>
    /// <returns>The mid of a positive bid and ask, else the positive one of them, else a positive last, else the
    /// close.</returns>
    /// <exception cref="NanoQLException">No price is positive.</exception>
    public static double MidEquivalent(double? bid, double? ask, double? last, double? close)
    {
        if (bid is double b && b > 0.0)
        {
            if (ask is double a && a > 0.0)
            {
                return (b + a) / 2.0;
            }
            else
            {
                return b;
            }
        }
        else
        {
            if (ask is double a && a > 0.0)
            {
                return a;
            }
            else if (last is double l && l > 0.0)
            {
                return l;
            }
            else
            {
                if (!(close is double c && c > 0.0))
                {
                    throw new NanoQLException("all input prices are invalid");
                }

                return c;
            }
        }
    }

    /// <summary>The MidSafe price: the mid only if both bid and ask prices are available.</summary>
    /// <param name="bid">The bid price, or <see langword="null"/>.</param>
    /// <param name="ask">The ask price, or <see langword="null"/>.</param>
    /// <returns>The mid.</returns>
    /// <exception cref="NanoQLException">The bid or the ask isn't positive.</exception>
    public static double MidSafe(double? bid, double? ask)
    {
        if (!(bid is double b && b > 0.0))
        {
            throw new NanoQLException("invalid bid price");
        }

        if (!(ask is double a && a > 0.0))
        {
            throw new NanoQLException("invalid ask price");
        }

        return (b + a) / 2.0;
    }
}
