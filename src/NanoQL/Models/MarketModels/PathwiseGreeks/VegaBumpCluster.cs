// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwisegreeks/vegabumpcluster.hpp ql/models/marketmodels/pathwisegreeks/vegabumpcluster.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// A block of pseudo-root elements bumped together for a vega: the factors, rates and steps in three half-open
/// ranges.
/// </summary>
/// <remarks>Immutable (the S-mmp plan's MMPR1 (ii)).</remarks>
public sealed class VegaBumpCluster
{
    /// <summary>Initializes a new instance of the <see cref="VegaBumpCluster"/> class.</summary>
    /// <param name="factorBegin">The first factor.</param>
    /// <param name="factorEnd">The end of the factors, after the last.</param>
    /// <param name="rateBegin">The first rate.</param>
    /// <param name="rateEnd">The end of the rates, after the last.</param>
    /// <param name="stepBegin">The first step.</param>
    /// <param name="stepEnd">The end of the steps, after the last.</param>
    /// <exception cref="ArgumentException">A range is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A range starts below 0.</exception>
    public VegaBumpCluster(int factorBegin, int factorEnd, int rateBegin, int rateEnd, int stepBegin, int stepEnd)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(factorBegin);
        ArgumentOutOfRangeException.ThrowIfNegative(rateBegin);
        ArgumentOutOfRangeException.ThrowIfNegative(stepBegin);
        FactorBegin = factorBegin;
        FactorEnd = factorEnd;
        RateBegin = rateBegin;
        RateEnd = rateEnd;
        StepBegin = stepBegin;
        StepEnd = stepEnd;

        if (!(FactorBegin < FactorEnd))
        {
            throw new ArgumentException("must have factorBegin_ < factorEnd_ in VegaBumpCluster ", nameof(factorEnd));
        }

        if (!(RateBegin < RateEnd))
        {
            throw new ArgumentException("must have rateBegin_ < rateEnd_ in VegaBumpCluster ", nameof(rateEnd));
        }

        if (!(StepBegin < StepEnd))
        {
            throw new ArgumentException("must have stepBegin_ < stepEnd_ in VegaBumpCluster ", nameof(stepEnd));
        }
    }

    /// <summary>Gets the first factor.</summary>
    public int FactorBegin { get; }

    /// <summary>Gets the end of the factors, after the last.</summary>
    public int FactorEnd { get; }

    /// <summary>Gets the first rate.</summary>
    public int RateBegin { get; }

    /// <summary>Gets the end of the rates, after the last.</summary>
    public int RateEnd { get; }

    /// <summary>Gets the first step.</summary>
    public int StepBegin { get; }

    /// <summary>Gets the end of the steps, after the last.</summary>
    public int StepEnd { get; }

    /// <summary>Whether this cluster and another share an element.</summary>
    /// <param name="comparee">The other cluster.</param>
    /// <returns>Whether they intersect.</returns>
    public bool DoesIntersect(VegaBumpCluster comparee)
    {
        ArgumentNullException.ThrowIfNull(comparee);
        if (FactorEnd <= comparee.FactorBegin)
        {
            return false;
        }

        if (RateEnd <= comparee.RateBegin)
        {
            return false;
        }

        if (StepEnd <= comparee.StepBegin)
        {
            return false;
        }

        if (comparee.FactorEnd <= FactorBegin)
        {
            return false;
        }

        if (comparee.RateEnd <= RateBegin)
        {
            return false;
        }

        if (comparee.StepEnd <= StepBegin)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Whether the cluster lies in a model's elements: within its rates, steps and factors, its rates alive at the
    /// start of its last step.
    /// </summary>
    /// <param name="volStructure">The model.</param>
    /// <returns>Whether the cluster is compatible.</returns>
    public bool IsCompatible(MarketModel volStructure)
    {
        ArgumentNullException.ThrowIfNull(volStructure);
        if (RateEnd > volStructure.NumberOfRates)
        {
            return false;
        }

        if (StepEnd > volStructure.NumberOfSteps)
        {
            return false;
        }

        if (FactorEnd > volStructure.NumberOfFactors)
        {
            return false;
        }

        int firstAliveRate = volStructure.Evolution.FirstAliveRate[StepEnd - 1];

        // if the rate has reset after the beginning of the last step of the bump
        return RateBegin >= firstAliveRate;
    }
}
