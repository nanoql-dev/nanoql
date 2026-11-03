// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/fixedlocalvolsurface.hpp ql/termstructures/volatility/equityfx/fixedlocalvolsurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The stochastic-local volatility calibrations' working leverage: QuantLib's fixed local volatility surface on a
/// matrix and strikes the calibration writes, each time interpolated linearly in strike, read as QuantLib reads it.
/// </summary>
/// <remarks>
/// <para>
/// A working type, owned by one calibration. QuantLib's SLV models build their surface on a matrix and strike
/// vectors they share with it, write a time's column and re-interpolate every column (<c>setInterpolation</c>) before
/// the next read; here <see cref="Reinterpolate"/> rebuilds the column written, the others' interpolations unchanged,
/// the same values.
/// </para>
/// <para>
/// Its reads are QuantLib 1.44's: at a node time a strike beyond the time's strikes extrapolates by the time's line,
/// between nodes it is held at the nearest strike (<c>fixedlocalvolsurface.cpp:118-165</c>), so the calibrations are
/// QuantLib's bit for bit; the surface a model returns is <see cref="FixedLocalVolSurface"/>, <see cref="ToSurface"/>,
/// held at the nearest strike at a node too.
/// </para>
/// </remarks>
[WorkingType]
internal sealed class FixedLocalVolWorkingSurface : LocalVolTermStructure
{
    private readonly Date maxDate;
    private readonly double[] times;
    private readonly double[][] strikes;
    private readonly Matrix localVolMatrix;
    private readonly Interpolation[] localVolInterpol;
    private readonly double[] column;

    // QuantLib's (referenceDate, times, strikes, localVolMatrix, dayCounter) constructor with the constant
    // extrapolations, the times, strikes and matrix held, not copied: the calibration writes them.
    internal FixedLocalVolWorkingSurface(Date referenceDate, double[] times, double[][] strikes, Matrix localVolMatrix, DayCounter dayCounter)
        : base(referenceDate, new NullCalendar(), BusinessDayConvention.Following, dayCounter)
    {
        maxDate = YearFractionToDateFunctions.YearFractionToDate(dayCounter, referenceDate, times[^1]);
        this.times = times;
        this.strikes = strikes;
        this.localVolMatrix = localVolMatrix;
        if (!(times[0] >= 0))
        {
            throw new ArgumentException("cannot have times[0] < 0", nameof(times));
        }

        if (!(times.Length == strikes.Length))
        {
            throw new ArgumentException("need strikes for every time step", nameof(strikes));
        }

        CheckSurface();
        localVolInterpol = new Interpolation[times.Length];
        column = new double[localVolMatrix.Rows];
        for (int j = 0; j < times.Length; ++j)
        {
            Reinterpolate(j);
        }
    }

    public override Date MaxDate => maxDate;

    public override double MaxTime => times[^1];

    // A time's strikes, which the calibration writes before re-interpolating the time.
    internal double[] StrikesAt(int j) => strikes[j];

    // The matrix, a row per strike and a column per time, which the calibration writes.
    internal Matrix Values => localVolMatrix;

    public override double MinStrike() => strikes[^1][0];

    public override double MaxStrike() => strikes[^1][^1];

    // QuantLib's setInterpolation<Linear>() for one time: its line through the time's strikes and column.
    internal void Reinterpolate(int j)
    {
        for (int i = 0; i < column.Length; ++i)
        {
            column[i] = localVolMatrix[i, j];
        }

        localVolInterpol[j] = new LinearInterpolation(strikes[j], column);
    }

    // The calibrated leverage as the public surface, the fixed local-volatility surface, on these times, strikes and
    // values, copied.
    internal FixedLocalVolSurface ToSurface() =>
        new(ReferenceDate, times, strikes.Select(k => (IEnumerable<double>)k), localVolMatrix, DayCounter);

    protected override double LocalVolImpl(double t, double strike)
    {
        t = Math.Min(times[^1], Math.Max(t, times[0]));

        int idx = LowerBound(t);

        if (ComparisonFunctions.CloseEnough(t, times[idx]))
        {
            if (strikes[idx][0] < strikes[idx][^1])
            {
                return localVolInterpol[idx].Evaluate(strike, true);
            }
            else
            {
                return localVolMatrix[localVolMatrix.Rows / 2, idx];
            }
        }
        else
        {
            double earlierStrike = strike, laterStrike = strike;
            if (strike < strikes[idx - 1][0])
            {
                earlierStrike = strikes[idx - 1][0];
            }

            if (strike < strikes[idx][0])
            {
                laterStrike = strikes[idx][0];
            }

            if (strike > strikes[idx - 1][^1])
            {
                earlierStrike = strikes[idx - 1][^1];
            }

            if (strike > strikes[idx][^1])
            {
                laterStrike = strikes[idx][^1];
            }

            double earlyVol = strikes[idx - 1][0] < strikes[idx - 1][^1]
                ? localVolInterpol[idx - 1].Evaluate(earlierStrike, true)
                : localVolMatrix[localVolMatrix.Rows / 2, idx - 1];
            double laterVol = localVolInterpol[idx].Evaluate(laterStrike, true);

            return earlyVol + ((laterVol - earlyVol) / (times[idx] - times[idx - 1]) * (t - times[idx - 1]));
        }
    }

    private void CheckSurface()
    {
        if (!(times.Length == localVolMatrix.Columns))
        {
            throw new ArgumentException("mismatch between date vector and vol matrix colums", "localVolMatrix");
        }

        foreach (double[] strike in strikes)
        {
            if (!(strike.Length == localVolMatrix.Rows))
            {
                throw new ArgumentException("mismatch between money-strike vector and vol matrix rows", "localVolMatrix");
            }
        }

        for (int j = 1; j < times.Length; j++)
        {
            if (!(times[j] > times[j - 1]))
            {
                throw new ArgumentException("dates must be sorted unique!", "times");
            }
        }

        foreach (double[] strike in strikes)
        {
            for (int j = 1; j < strike.Length; j++)
            {
                if (!(strike[j] >= strike[j - 1]))
                {
                    throw new ArgumentException("strikes must be sorted", "strikes");
                }
            }
        }
    }

    // std::lower_bound: the first time not below t
    private int LowerBound(double t)
    {
        int lo = 0, hi = times.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (times[mid] < t)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }
}
