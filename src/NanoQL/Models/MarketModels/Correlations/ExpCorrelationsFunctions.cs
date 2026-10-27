// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/correlations/expcorrelations.hpp ql/models/marketmodels/correlations/expcorrelations.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>The exponential correlation matrix of forward rates.</summary>
public static class ExpCorrelationsFunctions
{
    /// <summary>
    /// The exponential correlation of the forwards alive at a time: L + (1 − L) exp(−β |(tᵢ − t)^γ − (tⱼ − t)^γ|),
    /// with L the long-term correlation, β the decay between far-away forwards and γ the exponent of the time to go.
    /// </summary>
    /// <param name="rateTimes">The rate times, strictly increasing and positive.</param>
    /// <param name="longTermCorr">The long-term correlation, in [0, 1].</param>
    /// <param name="beta">The decay, not negative.</param>
    /// <param name="gamma">The exponent, in [0, 1].</param>
    /// <param name="t">The time; a forward that has fixed by then has a zero row and column.</param>
    /// <returns>A new matrix, one row and column per forward.</returns>
    /// <exception cref="ArgumentException">An input is outside its range.</exception>
    public static Matrix ExponentialCorrelations(
        ReadOnlySpan<double> rateTimes, double longTermCorr = 0.5, double beta = 0.2, double gamma = 1.0, double t = 0.0)
    {
        // preliminary checks
        UtilitiesFunctions.CheckIncreasingTimes(rateTimes);
        if (!(longTermCorr <= 1.0 && longTermCorr >= 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Long term correlation ({longTermCorr}) outside [0;1] interval"),
                nameof(longTermCorr));
        }

        if (!(beta >= 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"beta ({beta}) must be greater than zero"), nameof(beta));
        }

        if (!(gamma <= 1.0 && gamma >= 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"gamma ({gamma}) outside [0;1] interval"), nameof(gamma));
        }

        // Calculate correlation matrix
        int nbRows = rateTimes.Length - 1;
        var correlations = new Matrix(nbRows, nbRows, 0.0);
        for (int i = 0; i < nbRows; ++i)
        {
            // correlation is defined only between (alive) stochastic rates...
            if (t <= rateTimes[i])
            {
                correlations[i, i] = 1.0;
                for (int j = 0; j < i; ++j)
                {
                    if (t <= rateTimes[j])
                    {
                        double value = longTermCorr + ((1.0 - longTermCorr) * Math.Exp(
                            -beta * Math.Abs(Math.Pow(rateTimes[i] - t, gamma) - Math.Pow(rateTimes[j] - t, gamma))));
                        correlations[i, j] = value;
                        correlations[j, i] = value;
                    }
                }
            }
        }

        return correlations;
    }
}
