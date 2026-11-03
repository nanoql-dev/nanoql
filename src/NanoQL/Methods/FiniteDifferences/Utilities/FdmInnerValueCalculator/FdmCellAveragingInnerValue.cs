// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdminnervaluecalculator.hpp ql/methods/finitedifferences/utilities/fdminnervaluecalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The inner value of a payoff on one direction of the grid, through a mapping of the grid's coordinate to the
/// underlying; its cell average by Simpson's rule over the half-cells on each side of the point.
/// </summary>
/// <remarks>
/// A working type: the first <see cref="AvgInnerValue"/> fills a cache of every cell's average, at that call's time, as
/// QuantLib's does, so each cell is integrated once. An extension point, as in QuantLib: <see cref="FdmLogInnerValue"/>
/// derives from it.
/// </remarks>
[WorkingType]
public class FdmCellAveragingInnerValue : FdmInnerValueCalculator
{
    private readonly Payoff payoff;
    private readonly FdmMesher mesher;
    private readonly int direction;
    private readonly Func<double, double> gridMapping;
    private double[]? avgInnerValues;

    /// <summary>Initializes a new instance of the <see cref="FdmCellAveragingInnerValue"/> class.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="mesher">The mesher.</param>
    /// <param name="direction">The direction of the underlying.</param>
    /// <param name="gridMapping">The mapping of the grid's coordinate to the underlying; the identity by
    /// default.</param>
    /// <exception cref="ArgumentNullException">The payoff or the mesher is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the layout's.</exception>
    public FdmCellAveragingInnerValue(
        Payoff payoff, FdmMesher mesher, int direction, Func<double, double>? gridMapping = null)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentOutOfRangeException.ThrowIfNegative(direction);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(direction, mesher.Layout.Dim.Length);
        this.payoff = payoff;
        this.mesher = mesher;
        this.direction = direction;
        this.gridMapping = gridMapping ?? (x => x);
    }

    /// <inheritdoc/>
    public override double InnerValue(FdmLinearOpIterator iter, double t)
    {
        ArgumentNullException.ThrowIfNull(iter);
        double loc = mesher.Location(iter, direction);
        return payoff.Evaluate(gridMapping(loc));
    }

    /// <inheritdoc/>
    public override double AvgInnerValue(FdmLinearOpIterator iter, double t)
    {
        ArgumentNullException.ThrowIfNull(iter);
        if (avgInnerValues is null)
        {
            // set up the cache, each cell integrated once
            double[] values = new double[mesher.Layout.Dim[direction]];
            bool[] initialized = new bool[values.Length];

            FdmLinearOpIterator i = mesher.Layout.Begin();
            for (int k = 0; k < mesher.Layout.Size; ++k, i.Increment())
            {
                int xn = i.Coordinates[direction];
                if (!initialized[xn])
                {
                    initialized[xn] = true;
                    values[xn] = AvgInnerValueCalc(i, t);
                }
            }

            avgInnerValues = values;
        }

        return avgInnerValues[iter.Coordinates[direction]];
    }

    private double AvgInnerValueCalc(FdmLinearOpIterator iter, double t)
    {
        int dim = mesher.Layout.Dim[direction];
        int coord = iter.Coordinates[direction];

        if (coord == 0 || coord == dim - 1)
        {
            return InnerValue(iter, t);
        }

        double loc = mesher.Location(iter, direction);
        double a = loc - (mesher.Dminus(iter, direction) / 2.0);
        double b = loc + (mesher.Dplus(iter, direction) / 2.0);

        double F(double x) => payoff.Evaluate(gridMapping(x));

        double retVal;
        try
        {
            double acc = (F(a) != 0.0 || F(b) != 0.0) ? (F(a) + F(b)) * 5e-5 : 1e-4;
            retVal = new SimpsonIntegral(acc, 8).Integrate(F, a, b) / (b - a);
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            // use default value
            retVal = InnerValue(iter, t);
        }

        return retVal;
    }
}
