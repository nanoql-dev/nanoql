// SPDX-FileCopyrightText: Copyright (C) 2013, 2018 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2013, 2015, 2018 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/smilesectionutils.hpp ql/termstructures/volatility/smilesectionutils.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// A smile section's grid of strikes and call prices and its arbitrage-free region: the widest range of the grid,
/// around the at-the-money point, on which the call prices decrease and are convex in the strike.
/// </summary>
/// <remarks>
/// <para>
/// The moneyness is absolute (the strike less the at-the-money level) for a normal section and relative (the shifted
/// strike over the shifted level) for a shifted lognormal one. A shifted lognormal grid starts at a moneyness of 0, the
/// strike at minus the shift, whose call price is the shifted level. Where a strike falls outside the section's strike
/// range, the range's end takes its place, once.
/// </para>
/// <para>
/// Immutable: the grids and the region are computed at construction. The relative moneyness of a range's end is its
/// shifted strike over the shifted level, as at the grid's other points. QuantLib divides the shifted strike by the
/// unshifted level (<c>smilesectionutils.cpp:100-101</c>, <c>:107-108</c>), so with a shift the endpoints' moneyness
/// is off: the grid can come out unsorted, and the at-the-money point is then located in the wrong place.
/// </para>
/// </remarks>
public sealed class SmileSectionUtils
{
    // QuantLib's defaultMoney and defaultMoneyNormal.
    private static readonly double[] DefaultMoney =
    [
        0.0, 0.01, 0.05, 0.10, 0.25, 0.40, 0.50, 0.60, 0.70, 0.80, 0.90, 1.0, 1.25, 1.5, 1.75, 2.0, 5.0, 7.5, 10.0, 15.0,
        20.0,
    ];

    private static readonly double[] DefaultMoneyNormal =
    [
        -0.20, -0.15, -0.10, -0.075, -0.05, -0.04, -0.03, -0.02, -0.015, -0.01, -0.0075, -0.0050, -0.0025, 0.0, 0.0025,
        0.0050, 0.0075, 0.01, 0.015, 0.02, 0.03, 0.04, 0.05, 0.075, 0.10, 0.15, 0.20,
    ];

    private readonly ImmutableArray<double> m;
    private readonly ImmutableArray<double> c;
    private readonly ImmutableArray<double> k;
    private readonly int leftIndex;
    private readonly int rightIndex;
    private readonly double f;

    /// <summary>Initializes a new instance of the <see cref="SmileSectionUtils"/> class.</summary>
    /// <param name="section">The smile section.</param>
    /// <param name="moneynessGrid">The moneyness grid, strictly increasing and, for a shifted lognormal section, non
    /// negative; empty for QuantLib's default grid of the section's type.</param>
    /// <param name="atm">The at-the-money level; <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for the
    /// section's own.</param>
    /// <param name="deleteArbitragePoints">Whether the points next to the arbitrage-free region are removed until the
    /// region holds the whole grid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="section"/> is null.</exception>
    /// <exception cref="ArgumentException">The moneyness grid isn't strictly increasing, or holds a negative value for a
    /// shifted lognormal section; or no at-the-money level is given and the section has none.</exception>
    /// <exception cref="NanoQLException">The at-the-money point lies too close to the grid's ends, or the
    /// arbitrage-free region holds fewer than two points.</exception>
    public SmileSectionUtils(
        SmileSection section,
        ReadOnlySpan<double> moneynessGrid = default,
        double? atm = null,
        bool deleteArbitragePoints = false)
    {
        ArgumentNullException.ThrowIfNull(section);
        bool normal = section.VolatilityType == VolatilityType.Normal;
        if (!moneynessGrid.IsEmpty)
        {
            if (!(normal || moneynessGrid[0] >= 0.0))
            {
                throw new ArgumentException(
                    $"moneyness grid should only contain non negative values ({Show(moneynessGrid[0])})", nameof(moneynessGrid));
            }

            for (int i = 0; i < moneynessGrid.Length - 1; i++)
            {
                if (!(moneynessGrid[i] < moneynessGrid[i + 1]))
                {
                    throw new ArgumentException(
                        $"moneyness grid should contain strictly increasing values ({Show(moneynessGrid[i])},"
                        + $"{Show(moneynessGrid[i + 1])} at indices {i}, {i + 1})",
                        nameof(moneynessGrid));
                }
            }
        }

        f = atm ?? section.AtmLevel()
            ?? throw new ArgumentException(
                "atm level must be provided by source section or given in the constructor", nameof(atm));

        ReadOnlySpan<double> tmp = moneynessGrid.IsEmpty ? (normal ? DefaultMoneyNormal : DefaultMoney) : moneynessGrid;

        double shift = section.Shift;
        var mGrid = new List<double>();
        var kGrid = new List<double>();
        if (!normal && tmp[0] > NumericLimits.QL_EPSILON)
        {
            mGrid.Add(0.0);
            kGrid.Add(-shift);
        }

        bool minStrikeAdded = false, maxStrikeAdded = false;
        foreach (double i in tmp)
        {
            double strike = normal ? f + i : (i * (f + shift)) - shift;
            if ((!normal && i <= NumericLimits.QL_EPSILON) || (strike >= section.MinStrike() && strike <= section.MaxStrike()))
            {
                if (!minStrikeAdded || !ComparisonFunctions.Close(strike, section.MinStrike()))
                {
                    mGrid.Add(i);
                    kGrid.Add(strike);
                }

                if (ComparisonFunctions.Close(strike, section.MaxStrike()))
                {
                    maxStrikeAdded = true;
                }
            }
            else
            {
                // if the section provides a limited strike range
                // we put the respective endpoint in our grid
                // in order to not loose too much information
                if (strike < section.MinStrike() && !minStrikeAdded)
                {
                    mGrid.Add(normal ? section.MinStrike() - f : (section.MinStrike() + shift) / (f + shift));
                    kGrid.Add(section.MinStrike());
                    minStrikeAdded = true;
                }

                if (strike > section.MaxStrike() && !maxStrikeAdded)
                {
                    mGrid.Add(normal ? section.MaxStrike() - f : (section.MaxStrike() + shift) / (f + shift));
                    kGrid.Add(section.MaxStrike());
                    maxStrikeAdded = true;
                }
            }
        }

        // only known for shifted lognormal vols, otherwise we include
        // the lower strike in the loop below
        var cGrid = new List<double>();
        if (!normal)
        {
            cGrid.Add(f + shift);
        }

        for (int i = normal ? 0 : 1; i < kGrid.Count; i++)
        {
            cGrid.Add(section.OptionPrice(kGrid[i], OptionType.Call, 1.0));
        }

        int centralIndex = Interpolation.UpperBound(
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(mGrid), (normal ? 0.0 : 1.0) - NumericLimits.QL_EPSILON);
        if (!(centralIndex < kGrid.Count - 1 && centralIndex > 1))
        {
            throw new NanoQLException($"Atm point in moneyness grid ({centralIndex}) too close to boundary.");
        }

        // shift central index to the right if necessary
        // (sometimes even the atm point lies in an arbitrageable area)
        while (centralIndex < kGrid.Count - 1 && !Af(cGrid, kGrid, centralIndex, centralIndex, centralIndex + 1))
        {
            centralIndex++;
        }

        if (!(centralIndex < kGrid.Count))
        {
            throw new NanoQLException("central index is at right boundary");
        }

        int left = centralIndex, right = centralIndex;
        bool done = false;
        while (!done)
        {
            bool isAf = true;
            done = true;

            while (isAf && right < kGrid.Count - 1)
            {
                right++;
                isAf = Af(cGrid, kGrid, left, right, right) && Af(cGrid, kGrid, left, right - 1, right);
            }

            if (!isAf)
            {
                right--;
            }

            isAf = true;
            while (isAf && left > 1)
            {
                left--;
                isAf = Af(cGrid, kGrid, left, left, right) && Af(cGrid, kGrid, left, left + 1, right);
            }

            if (!isAf)
            {
                left++;
            }

            if (right < left)
            {
                right = left;
            }

            if (deleteArbitragePoints && left > 1)
            {
                mGrid.RemoveAt(left - 1);
                kGrid.RemoveAt(left - 1);
                cGrid.RemoveAt(left - 1);
                left--;
                if (right > 0)
                {
                    right--;
                }

                done = false;
            }

            if (deleteArbitragePoints && right < kGrid.Count - 1)
            {
                mGrid.RemoveAt(right + 1);
                kGrid.RemoveAt(right + 1);
                cGrid.RemoveAt(right + 1);
                if (right > 0)
                {
                    right--;
                }

                done = false;
            }
        }

        if (!(right > left))
        {
            throw new NanoQLException($"arbitrage free region must at least contain two points (only index is {left})");
        }

        m = [.. mGrid];
        k = [.. kGrid];
        c = [.. cGrid];
        leftIndex = left;
        rightIndex = right;
    }

    /// <summary>The arbitrage-free region's strikes.</summary>
    /// <returns>Its first and last strike.</returns>
    public (double Left, double Right) ArbitragefreeRegion() => (k[leftIndex], k[rightIndex]);

    /// <summary>The arbitrage-free region's indices in the grid.</summary>
    /// <returns>Its first and last index.</returns>
    public (int Left, int Right) ArbitragefreeIndices() => (leftIndex, rightIndex);

    /// <summary>The moneyness grid.</summary>
    /// <returns>The moneyness of each strike.</returns>
    public ImmutableArray<double> MoneyGrid() => m;

    /// <summary>The strike grid.</summary>
    /// <returns>The strikes.</returns>
    public ImmutableArray<double> StrikeGrid() => k;

    /// <summary>The undiscounted call prices at the strikes.</summary>
    /// <returns>The prices.</returns>
    public ImmutableArray<double> CallPrices() => c;

    /// <summary>The at-the-money level.</summary>
    /// <returns>The level given, or the section's.</returns>
    public double AtmLevel() => f;

    // QuantLib's af(i0, i, i1): whether the price at i falls from its left neighbour (from the grid's first point
    // where i is the region's first, i0) at a slope in [-1, 0], and, short of i1, is convex with its right neighbour.
    private static bool Af(List<double> c, List<double> k, int i0, int i, int i1)
    {
        if (i == 0)
        {
            return true;
        }

        int im = i - 1 >= i0 ? i - 1 : 0;
        double q1 = (c[i] - c[im]) / (k[i] - k[im]);
        if (q1 < -1.0 || q1 > 0.0)
        {
            return false;
        }

        if (i >= i1)
        {
            return true;
        }

        double q2 = (c[i + 1] - c[i]) / (k[i + 1] - k[i]);
        return q1 <= q2 && q2 <= 0.0;
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}
