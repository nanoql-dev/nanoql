// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2007 Katiuscia Manzoni
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/correlations/cotswapfromfwdcorrelation.hpp ql/models/marketmodels/correlations/cotswapfromfwdcorrelation.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The coterminal swap rates' correlation implied by a forward-rate correlation on a curve: each period's the
/// correlation of the coterminal Z matrix times the forwards' correlation times its transpose, the expired rates'
/// coefficients zero.
/// </summary>
/// <remarks>
/// Immutable: its matrices are computed at construction, and a read returns a copy. A forward correlation with fewer
/// times than matrices, whose times QuantLib reads past, is refused.
/// </remarks>
public sealed class CotSwapFromFwdCorrelation : PiecewiseConstantCorrelation
{
    private readonly PiecewiseConstantCorrelation fwdCorr;
    private readonly ImmutableArray<Matrix> swapCorrMatrices;

    /// <summary>Initializes a new instance of the <see cref="CotSwapFromFwdCorrelation"/> class.</summary>
    /// <param name="fwdCorr">The forward rates' correlation.</param>
    /// <param name="curveState">The curve, of the correlation's number of rates.</param>
    /// <param name="displacement">The rates' displacement.</param>
    /// <exception cref="ArgumentException">
    /// The curve's number of rates isn't the correlation's, or the correlation has fewer times than matrices.
    /// </exception>
    public CotSwapFromFwdCorrelation(PiecewiseConstantCorrelation fwdCorr, CurveState curveState, double displacement)
    {
        ArgumentNullException.ThrowIfNull(fwdCorr);
        ArgumentNullException.ThrowIfNull(curveState);
        this.fwdCorr = fwdCorr;
        NumberOfRates = fwdCorr.NumberOfRates;
        if (!(NumberOfRates == curveState.NumberOfRates))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"mismatch between number of rates in fwdCorr ({NumberOfRates}) and curveState ({curveState.NumberOfRates})"),
                nameof(curveState));
        }

        Matrix zed = SwapForwardMappings.CoterminalSwapZedMatrix(curveState, displacement);
        Matrix zedT = MatrixFunctions.Transpose(zed);
        ImmutableArray<Matrix> fwdCorrMatrices = fwdCorr.Correlations();
        ImmutableArray<double> rateTimes = curveState.RateTimes;
        ImmutableArray<double> corrTimes = fwdCorr.Times;
        if (!(corrTimes.Length >= fwdCorrMatrices.Length))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the forward correlation's times ({corrTimes.Length}) are fewer than its matrices ({fwdCorrMatrices.Length})"),
                nameof(fwdCorr));
        }

        var matrices = new Matrix[fwdCorrMatrices.Length];
        for (int k = 0; k < fwdCorrMatrices.Length; ++k)
        {
            matrices[k] = new CovarianceDecomposition(zed * fwdCorrMatrices[k] * zedT).CorrelationMatrix();

            // zero expired rates' correlation coefficients
            for (int i = 0; i < NumberOfRates; ++i)
            {
                for (int j = 0; j <= i; ++j)
                {
                    if (corrTimes[k] > rateTimes[j])
                    {
                        matrices[k][i, j] = matrices[k][j, i] = 0.0;
                    }
                }
            }
        }

        swapCorrMatrices = ImmutableArray.Create(matrices);
    }

    /// <inheritdoc/>
    public override ImmutableArray<double> Times => fwdCorr.Times;

    /// <inheritdoc/>
    public override ImmutableArray<double> RateTimes => fwdCorr.RateTimes;

    /// <inheritdoc/>
    public override int NumberOfRates { get; }

    /// <inheritdoc/>
    public override ImmutableArray<Matrix> Correlations() => [.. swapCorrMatrices.Select(m => m.Clone())];

    /// <inheritdoc/>
    public override Matrix Correlation(int i)
    {
        CheckIndex(i, swapCorrMatrices.Length);
        return swapCorrMatrices[i].Clone();
    }
}
