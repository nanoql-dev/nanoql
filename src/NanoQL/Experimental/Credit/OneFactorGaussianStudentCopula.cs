// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/onefactorstudentcopula.hpp ql/experimental/credit/onefactorstudentcopula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The one-factor Gaussian-Student t copula: Yᵢ = a·M + √(1 − a²)·Zᵢ, M Gaussian and Zᵢ Student t of N_z degrees of freedom, each Student variable scaled by √((ν − 2)/ν) to unit
/// variance. Y's distribution is tabulated at construction on 201 points of [−10, 10], each a double Euler sum of the
/// convolution on [−10, 10] in 400 steps, QuantLib's loop order.
/// </summary>
/// <remarks>
/// The factor is integrated over [−maximum, maximum]: QuantLib's Student classes pass no minimum, so its base's −5
/// stands and the integrals lose the factor's left tail, where defaults are nearly certain (the copula's integral of p
/// = 0.001 reads 0.000898 on Student 5/5 at correlation 0.3 and 0.000102 on Student-Gaussian ν 3 at 0.6;
/// <c>Divergences/OneFactorStudentCopulaRangeTests.cs</c>). The Student densities and distributions are nanoql's fixed
/// ones, so a tabulated value reads the last bits of their fixes where QuantLib's formula loses its digits.
/// </remarks>
[Experimental("NQL0001")]
public sealed class OneFactorGaussianStudentCopula : OneFactorCopula
{
    private static readonly NormalDistribution DensityOfM = new(); // density of M
    private readonly CumulativeStudentDistribution cumulative; // cumulated density of Z
    private readonly int nz; // degrees of freedom of Z
    private readonly double scaleZ; // scaling for z to ensure unit variance

    /// <summary>Initializes a new instance of the <see cref="OneFactorGaussianStudentCopula"/> class.</summary>
    /// <param name="correlation">The correlation a², in [0, 1].</param>
    /// <param name="nz">Z's degrees of freedom, above 2.</param>
    /// <param name="maximum">The factor grid's upper end.</param>
    /// <param name="integrationSteps">The factor grid's number of steps.</param>
    /// <exception cref="ArgumentOutOfRangeException">The correlation lies outside [0, 1], or Z's degrees of freedom isn't above 2
    /// ("degrees of freedom must be &gt; 2").</exception>
    public OneFactorGaussianStudentCopula(double correlation, int nz, double maximum = 10, int integrationSteps = 200)
        : base(correlation, maximum, integrationSteps, -maximum)
    {
        if (!(nz > 2))
        {
            throw new ArgumentOutOfRangeException(nameof(nz), "degrees of freedom must be > 2");
        }

        cumulative = new CumulativeStudentDistribution(nz);
        this.nz = nz;
        scaleZ = Math.Sqrt((double)(this.nz - 2) / this.nz);

        // FIXME:
        // compute F(ymin) and F(ymax) for the fattest case nm = nz = 2
        // set a desired confidence and work out ymin, ymax
        double ymin = -10;
        double ymax = +10;
        const int steps = 200;
        var y = ImmutableArray.CreateBuilder<double>(steps + 1);
        var cumulativeY = ImmutableArray.CreateBuilder<double>(steps + 1);
        for (int i = 0; i <= steps; i++)
        {
            double value = ymin + ((ymax - ymin) * i / steps);
            double c = CumulativeYIntegral(value);
            y.Add(value);
            cumulativeY.Add(c);
        }

        Tabulate(y.MoveToImmutable(), cumulativeY.MoveToImmutable());
    }

    /// <inheritdoc/>
    public override double Density(double m) => DensityOfM.Evaluate(m);

    /// <inheritdoc/>
    public override double CumulativeZ(double z) => cumulative.Evaluate(z / scaleZ);

    // The cumulative distribution of Y at a value, the table's entry.
    private double CumulativeYIntegral(double value)
    {
        double c = Correlation;

        if (c == 0)
        {
            return new CumulativeStudentDistribution(nz).Evaluate(value / scaleZ);
        }

        if (c == 1)
        {
            return new CumulativeNormalDistribution().Evaluate(value);
        }

        var dz = new StudentDistribution(nz);
        var dm = new NormalDistribution();

        // FIXME:
        // Find a sensitive way of setting these parameters,
        // e.g. depending on nm and nz, and the desired table range
        double minimum = -10;
        double maximum = +10;
        const int steps = 400;

        double delta = (maximum - minimum) / steps;
        double cumulated = 0;

        if (c < 0.5)
        {
            // outer integral -> 1 for c -> 0
            for (double m = minimum + (delta / 2); m < maximum; m += delta)
            {
                for (double z = minimum + (delta / 2); z < (value - (Math.Sqrt(c) * m)) / Math.Sqrt(1.0 - c); z += delta)
                {
                    cumulated += dm.Evaluate(m) * dz.Evaluate(z / scaleZ) / scaleZ;
                }
            }
        }
        else
        {
            // outer integral -> 1 for c -> 1
            for (double z = minimum + (delta / 2); z < maximum; z += delta)
            {
                for (double m = minimum + (delta / 2); m < (value - (Math.Sqrt(1.0 - c) * z)) / Math.Sqrt(c); m += delta)
                {
                    cumulated += dm.Evaluate(m) * dz.Evaluate(z / scaleZ) / scaleZ;
                }
            }
        }

        return cumulated * delta * delta;
    }
}
