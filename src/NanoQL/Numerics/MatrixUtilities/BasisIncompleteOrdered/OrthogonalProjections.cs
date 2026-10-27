// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/basisincompleteordered.hpp ql/math/matrixutilities/basisincompleteordered.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Given vectors w_i, the vectors x_i with x_i orthogonal to every w_j but w_i and ⟨x_i, w_i⟩ = ⟨w_i, w_i⟩:
/// Gram-Schmidt on the others, then the projection onto their orthogonal space, each vector valid where its multiplier
/// is below the cut-off and the others independent to the tolerance.
/// </summary>
/// <remarks>
/// Immutable: its vectors are computed at construction, and its reads are immutable. As in QuantLib, an invalid
/// vector's projection is the last valid one's before it, zeros before any. A vector read past the vectors, which
/// QuantLib reads unchecked, is refused (the S-mm plan's MR3 (xx)).
/// </remarks>
public sealed class OrthogonalProjections
{
    private readonly ImmutableArray<ImmutableArray<double>> projectedVectors;

    /// <summary>Initializes a new instance of the <see cref="OrthogonalProjections"/> class.</summary>
    /// <param name="originalVectors">The vectors w_i, a row each.</param>
    /// <param name="multiplierCutOff">The multiplier ⟨w_i, w_i⟩/⟨w_i, x_i⟩ at or past which a vector is
    /// invalid.</param>
    /// <param name="tolerance">The norm below which a vector of the others' basis is dropped.</param>
    public OrthogonalProjections(Matrix originalVectors, double multiplierCutOff, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(originalVectors);
        Matrix original = originalVectors.Clone();
        int numberVectors = original.Rows;
        int dimension = original.Columns;
        bool[] validVectors = [.. Enumerable.Repeat(true, numberVectors)];
        var orthoNormalizedVectors = new Matrix(numberVectors, dimension);
        double[] currentVector = new double[dimension];
        var projected = new ImmutableArray<double>[numberVectors];
        for (int j = 0; j < numberVectors; ++j)
        {
            if (validVectors[j])
            {
                // create an orthormal basis not containing j
                for (int k = 0; k < numberVectors; ++k)
                {
                    for (int m = 0; m < dimension; ++m)
                    {
                        orthoNormalizedVectors[k, m] = original[k, m];
                    }

                    if (k != j && validVectors[k])
                    {
                        for (int l = 0; l < k; ++l)
                        {
                            if (validVectors[l] && l != j)
                            {
                                double dotProduct = InnerProduct(orthoNormalizedVectors, k, orthoNormalizedVectors, l);
                                for (int n = 0; n < dimension; ++n)
                                {
                                    orthoNormalizedVectors[k, n] -= dotProduct * orthoNormalizedVectors[l, n];
                                }
                            }
                        }

                        double normBeforeScaling = Norm(orthoNormalizedVectors, k);

                        if (normBeforeScaling < tolerance)
                        {
                            validVectors[k] = false;
                        }
                        else
                        {
                            double normBeforeScalingRecip = 1.0 / normBeforeScaling;
                            for (int m = 0; m < dimension; ++m)
                            {
                                orthoNormalizedVectors[k, m] *= normBeforeScalingRecip;
                            }
                        }
                    }
                }

                // we now have an o.n. basis for everything except j
                double prevNormSquared = NormSquared(original, j);

                for (int r = 0; r < numberVectors; ++r)
                {
                    if (validVectors[r] && r != j)
                    {
                        double dotProduct = InnerProduct(orthoNormalizedVectors, j, orthoNormalizedVectors, r);

                        for (int s = 0; s < dimension; ++s)
                        {
                            orthoNormalizedVectors[j, s] -= dotProduct * orthoNormalizedVectors[r, s];
                        }
                    }
                }

                double projectionOnOriginalDirection = InnerProduct(original, j, orthoNormalizedVectors, j);
                double sizeMultiplier = prevNormSquared / projectionOnOriginalDirection;

                if (Math.Abs(sizeMultiplier) < multiplierCutOff)
                {
                    for (int t = 0; t < dimension; ++t)
                    {
                        currentVector[t] = orthoNormalizedVectors[j, t] * sizeMultiplier;
                    }
                }
                else
                {
                    validVectors[j] = false;
                }
            }

            projected[j] = ImmutableArray.Create(currentVector);
        }

        projectedVectors = ImmutableArray.Create(projected);
        ValidVectors = ImmutableArray.Create(validVectors);
        NumberValidVectors = validVectors.Count(v => v);
    }

    /// <summary>Gets whether each vector is valid.</summary>
    public ImmutableArray<bool> ValidVectors { get; }

    /// <summary>Gets the number of valid vectors.</summary>
    public int NumberValidVectors { get; }

    /// <summary>The <paramref name="index"/>-th vector's projection x_i.</summary>
    /// <param name="index">The vector, below the number of vectors.</param>
    /// <returns>The projection.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> isn't a vector.</exception>
    public ImmutableArray<double> GetVector(int index)
    {
        // QuantLib reads its vectors unchecked (MR3 (xx)).
        if (!(index >= 0 && index < projectedVectors.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                string.Create(CultureInfo.InvariantCulture, $"index ({index}) must be less than the number of vectors ({projectedVectors.Length})"));
        }

        return projectedVectors[index];
    }

    private static double NormSquared(Matrix v, int row)
    {
        double x = 0.0;
        for (int i = 0; i < v.Columns; ++i)
        {
            x += v[row, i] * v[row, i];
        }

        return x;
    }

    private static double Norm(Matrix v, int row) => Math.Sqrt(NormSquared(v, row));

    private static double InnerProduct(Matrix v, int row1, Matrix w, int row2)
    {
        double x = 0.0;
        for (int i = 0; i < v.Columns; ++i)
        {
            x += v[row1, i] * w[row2, i];
        }

        return x;
    }
}
