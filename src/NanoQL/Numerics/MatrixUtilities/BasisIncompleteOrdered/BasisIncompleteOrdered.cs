// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/basisincompleteordered.hpp ql/math/matrixutilities/basisincompleteordered.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// An orthonormal basis built by Gram-Schmidt from vectors added in order, each kept where it is independent of those
/// before it.
/// </summary>
/// <remarks>A working type (the S-mm plan's MR1 (ii)): each added vector changes it, and it isn't
/// thread-safe.</remarks>
[WorkingType]
public sealed class BasisIncompleteOrdered
{
    private readonly List<double[]> currentBasis = [];
    private readonly int euclideanDimension;
    private readonly double[] newVector;

    /// <summary>Initializes a new instance of the <see cref="BasisIncompleteOrdered"/> class.</summary>
    /// <param name="euclideanDimension">The space's dimension.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="euclideanDimension"/> is negative.</exception>
    public BasisIncompleteOrdered(int euclideanDimension)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(euclideanDimension);
        this.euclideanDimension = euclideanDimension;
        newVector = new double[euclideanDimension];
    }

    /// <summary>Gets the number of vectors in the basis.</summary>
    public int BasisSize => currentBasis.Count;

    /// <summary>Gets the space's dimension.</summary>
    public int EuclideanDimension => euclideanDimension;

    /// <summary>Adds a vector: its part orthogonal to the basis, normalized, where its norm is at least
    /// 1e-12.</summary>
    /// <param name="newVector1">The vector, of the space's dimension.</param>
    /// <returns>Whether the vector was linearly independent of the basis, and added.</returns>
    /// <exception cref="ArgumentException">The vector isn't of the space's dimension.</exception>
    public bool AddVector(ReadOnlySpan<double> newVector1)
    {
        if (!(newVector1.Length == euclideanDimension))
        {
            throw new ArgumentException("missized vector passed to BasisIncompleteOrdered::addVector", nameof(newVector1));
        }

        newVector1.CopyTo(newVector);

        if (currentBasis.Count == euclideanDimension)
        {
            return false;
        }

        foreach (double[] currentBasi in currentBasis)
        {
            double innerProd = 0.0;
            for (int k = 0; k < euclideanDimension; ++k)
            {
                innerProd += newVector[k] * currentBasi[k];
            }

            for (int k = 0; k < euclideanDimension; ++k)
            {
                newVector[k] -= innerProd * currentBasi[k];
            }
        }

        double sum = 0.0;
        for (int k = 0; k < euclideanDimension; ++k)
        {
            sum += newVector[k] * newVector[k];
        }

        double norm = Math.Sqrt(sum);

        // maybe this should be a tolerance
        if (norm < 1e-12)
        {
            return false;
        }

        for (int l = 0; l < euclideanDimension; ++l)
        {
            newVector[l] /= norm;
        }

        currentBasis.Add((double[])newVector.Clone());

        return true;
    }

    /// <summary>The basis, a vector a row.</summary>
    /// <returns>A new matrix of the basis size by the space's dimension.</returns>
    public Matrix GetBasisAsRowsInMatrix()
    {
        var basis = new Matrix(currentBasis.Count, euclideanDimension);
        for (int i = 0; i < basis.Rows; ++i)
        {
            for (int j = 0; j < basis.Columns; ++j)
            {
                basis[i, j] = currentBasis[i][j];
            }
        }

        return basis;
    }
}
