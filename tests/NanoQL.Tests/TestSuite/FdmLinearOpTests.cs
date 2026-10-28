// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009, 2010, 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/fdmlinearop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using SparseMatrixReference = NanoQL.Numerics.SparseMatrix;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class FdmLinearOpTests
{
    // qldefines.hpp's QL_EPSILON, which nanoql keeps internal.
    private const double QL_EPSILON = 2.220446049250313E-16;

    [Fact]
    public void TestSpareMatrixReference()
    {
        // Testing SparseMatrixReference type...

        const Size rows = 10;
        const Size columns = 10;
        const Size nMatrices = 5;
        const Size nElements = 50;

        var rng = new MersenneTwisterUniformRng(1234);

        var expected = new SparseMatrix(rows, columns);
        SparseMatrix[] v = [.. Enumerable.Range(0, nMatrices).Select(_ => new SparseMatrix(rows, columns))];
        var refs = new List<SparseMatrixReference>();

        foreach (SparseMatrix i in v)
        {
            SparseMatrixReference m = i;
            for (Size j = 0; j < nElements; ++j)
            {
                Size row = (Size)(rng.Next().Value * rows);
                Size column = (Size)(rng.Next().Value * columns);

                Real value = rng.Next().Value;
                m[row, column] += value;
                expected[row, column] += value;
            }

            refs.Add(m);
        }

        // std::accumulate(refs.begin()+1, refs.end(), SparseMatrix(refs.front()))
        SparseMatrix calculated = refs.Skip(1).Aggregate(refs[0].Clone(), (sum, r) => sum + r);

        for (Size i = 0; i < rows; ++i)
        {
            for (Size j = 0; j < columns; ++j)
            {
                if (Math.Abs(calculated[i, j] - expected[i, j]) > 100 * QL_EPSILON)
                {
                    Assert.Fail(
                        "Error using sparse matrix references in "
                        + $"Element ({i}, {j})"
                        + $"\n expected  : {expected[i, j]}"
                        + $"\n calculated: {calculated[i, j]}");
                }
            }
        }
    }

    [Fact]
    public void TestSparseMatrixZeroAssignment()
    {
        // Testing assignment to zero in sparse matrix...

        var m = new SparseMatrix(5, 5);
        if (NrElementsOfSparseMatrix(m) != 0)
        {
            Assert.Fail("non zero return for an emtpy matrix");
        }

        m[0, 0] = 0.0;
        m[1, 2] = 0.0;
        if (NrElementsOfSparseMatrix(m) != 2)
        {
            Assert.Fail("two elements expected");
        }

        m[1, 3] = 1.0;
        if (NrElementsOfSparseMatrix(m) != 3)
        {
            Assert.Fail("three elements expected");
        }

        m[1, 3] = 0.0;
        if (NrElementsOfSparseMatrix(m) != 3)
        {
            Assert.Fail("three elements expected");
        }
    }

    [Fact]
    public void TestFdmLinearOpLayout()
    {
        // Testing indexing of a linear operator...

        Size[] dim = [5, 7, 8];

        var layout = new FdmLinearOpLayout(dim);

        Size calculatedDim = layout.Dim.Length;
        Size expectedDim = dim.Length;
        if (calculatedDim != expectedDim)
        {
            Assert.Fail($"index.dimensions() should be {expectedDim}, but is {calculatedDim}");
        }

        Size calculatedSize = layout.Size;
        Size expectedSize = dim.Aggregate(1, (a, b) => a * b);

        if (calculatedSize != expectedSize)
        {
            Assert.Fail($"index.size() should be {expectedSize}, but is {calculatedSize}");
        }

        for (Size k = 0; k < dim[0]; ++k)
        {
            for (Size l = 0; l < dim[1]; ++l)
            {
                for (Size m = 0; m < dim[2]; ++m)
                {
                    Size[] tmp = [k, l, m];

                    Size calculatedIndex = layout.Index(tmp);
                    Size expectedIndex = k + (l * dim[0]) + (m * dim[0] * dim[1]);

                    if (expectedIndex != layout.Index(tmp))
                    {
                        Assert.Fail($"index.size() should be {expectedIndex}, but is {calculatedIndex}");
                    }
                }
            }
        }

        FdmLinearOpIterator iter = layout.Begin();

        for (Size m = 0; m < dim[2]; ++m)
        {
            for (Size l = 0; l < dim[1]; ++l)
            {
                for (Size k = 0; k < dim[0]; ++k, iter.Increment())
                {
                    for (Size n = 1; n < 4; ++n)
                    {
                        Size nn = layout.Neighbourhood(iter, 1, n);
                        Size calculatedIndex = k + (m * dim[0] * dim[1])
                            + ((l < dim[1] - n) ? l + n : dim[1] - 1 - (l + n - (dim[1] - 1))) * dim[0];

                        if (nn != calculatedIndex)
                        {
                            Assert.Fail($"next neighbourhood index is {nn} but should be {calculatedIndex}");
                        }
                    }

                    for (Size n = 1; n < 7; ++n)
                    {
                        Size nn = layout.Neighbourhood(iter, 2, -n);
                        Size calculatedIndex = k + (l * dim[0]) + ((m < n) ? n - m : m - n) * dim[0] * dim[1];
                        if (nn != calculatedIndex)
                        {
                            Assert.Fail($"next neighbourhood index is {nn} but should be {calculatedIndex}");
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TestUniformGridMesher()
    {
        // Testing uniform grid mesher...
        Size[] dim = [5, 7, 8];

        var layout = new FdmLinearOpLayout(dim);
        (double, double)[] boundaries = [(-5, 10), (5, 100), (10, 20)];

        var mesher = new UniformGridMesher(layout, boundaries);

        Real dx1 = 15.0 / (dim[0] - 1);
        Real dx2 = 95.0 / (dim[1] - 1);
        Real dx3 = 10.0 / (dim[2] - 1);

        const double tol = 100 * QL_EPSILON;
        if (Math.Abs(dx1 - mesher.Dminus(layout.Begin(), 0)) > tol
            || Math.Abs(dx1 - mesher.Dplus(layout.Begin(), 0)) > tol
            || Math.Abs(dx2 - mesher.Dminus(layout.Begin(), 1)) > tol
            || Math.Abs(dx2 - mesher.Dplus(layout.Begin(), 1)) > tol
            || Math.Abs(dx3 - mesher.Dminus(layout.Begin(), 2)) > tol
            || Math.Abs(dx3 - mesher.Dplus(layout.Begin(), 2)) > tol)
        {
            Assert.Fail("inconsistent uniform mesher object");
        }
    }

    [Fact]
    public void TestFirstDerivativesMapApply()
    {
        // Testing application of first-derivatives map...
        Size[] dim = [400, 100, 50];

        var index = new FdmLinearOpLayout(dim);

        (double First, double Second)[] boundaries = [(-5, 5), (0, 10), (5, 15)];

        FdmMesher mesher = new UniformGridMesher(index, boundaries);

        var map = new FirstDerivativeOp(2, mesher);

        var r = new Vector(mesher.Layout.Size);
        FdmLinearOpIterator iter = index.Begin();
        for (Size i = 0; i < index.Size; ++i, iter.Increment())
        {
            r[iter.Index] = Math.Sin(mesher.Location(iter, 0)) + Math.Cos(mesher.Location(iter, 2));
        }

        Vector t = map.Apply(r);
        Real dz = (boundaries[2].Second - boundaries[2].First) / (dim[2] - 1);
        iter = index.Begin();
        for (Size i = 0; i < index.Size; ++i, iter.Increment())
        {
            Size z = iter.Coordinates[2];

            Size z0 = (z > 0) ? z - 1 : 1;
            Size z2 = (z < dim[2] - 1) ? z + 1 : dim[2] - 2;
            Real lz0 = boundaries[2].First + (z0 * dz);
            Real lz2 = boundaries[2].First + (z2 * dz);

            Real expected;
            if (z == 0)
            {
                expected = (Math.Cos(boundaries[2].First + dz) - Math.Cos(boundaries[2].First)) / dz;
            }
            else if (z == dim[2] - 1)
            {
                expected = (Math.Cos(boundaries[2].Second) - Math.Cos(boundaries[2].Second - dz)) / dz;
            }
            else
            {
                expected = (Math.Cos(lz2) - Math.Cos(lz0)) / (2 * dz);
            }

            Real calculated = t[iter.Index];
            if (Math.Abs(calculated - expected) > 1e-10)
            {
                Assert.Fail($"first derivative calculation failed.\n    calculated: {calculated}\n    expected:   {expected}");
            }
        }
    }

    [Fact]
    public void TestSecondDerivativesMapApply()
    {
        // Testing application of second-derivatives map...
        Size[] dim = [50, 50, 50];

        var index = new FdmLinearOpLayout(dim);

        (double, double)[] boundaries = [(0, 0.5), (0, 0.5), (0, 0.5)];

        FdmMesher mesher = new UniformGridMesher(index, boundaries);
        var r = new Vector(mesher.Layout.Size);
        FdmLinearOpIterator iter = index.Begin();
        for (Size k = 0; k < index.Size; ++k, iter.Increment())
        {
            Real x = mesher.Location(iter, 0);
            Real y = mesher.Location(iter, 1);
            Real z = mesher.Location(iter, 2);

            r[iter.Index] = Math.Sin(x) * Math.Cos(y) * Math.Exp(z);
        }

        Vector t = new SecondDerivativeOp(0, mesher).Apply(r);

        const Real tol = 5e-2;
        iter = index.Begin();
        for (Size k = 0; k < index.Size; ++k, iter.Increment())
        {
            Size i = iter.Index;
            Real x = mesher.Location(iter, 0);
            Real y = mesher.Location(iter, 1);
            Real z = mesher.Location(iter, 2);

            Real d = -Math.Sin(x) * Math.Cos(y) * Math.Exp(z);
            if (iter.Coordinates[0] == 0 || iter.Coordinates[0] == dim[0] - 1)
            {
                d = 0;
            }

            if (Math.Abs(d - t[i]) > tol)
            {
                Assert.Fail($"numerical derivative in dx^2 deviation is too big\n  found at {x} {y} {z}");
            }
        }

        t = new SecondDerivativeOp(1, mesher).Apply(r);
        iter = index.Begin();
        for (Size k = 0; k < index.Size; ++k, iter.Increment())
        {
            Size i = iter.Index;
            Real x = mesher.Location(iter, 0);
            Real y = mesher.Location(iter, 1);
            Real z = mesher.Location(iter, 2);

            Real d = -Math.Sin(x) * Math.Cos(y) * Math.Exp(z);
            if (iter.Coordinates[1] == 0 || iter.Coordinates[1] == dim[1] - 1)
            {
                d = 0;
            }

            if (Math.Abs(d - t[i]) > tol)
            {
                Assert.Fail($"numerical derivative in dy^2 deviation is too big\n  found at {x} {y} {z}");
            }
        }

        t = new SecondDerivativeOp(2, mesher).Apply(r);
        iter = index.Begin();
        for (Size k = 0; k < index.Size; ++k, iter.Increment())
        {
            Size i = iter.Index;
            Real x = mesher.Location(iter, 0);
            Real y = mesher.Location(iter, 1);
            Real z = mesher.Location(iter, 2);

            Real d = Math.Sin(x) * Math.Cos(y) * Math.Exp(z);
            if (iter.Coordinates[2] == 0 || iter.Coordinates[2] == dim[2] - 1)
            {
                d = 0;
            }

            if (Math.Abs(d - t[i]) > tol)
            {
                Assert.Fail($"numerical derivative in dz^2 deviation is too big\n  found at {x} {y} {z}");
            }
        }
    }

    [Fact]
    public void TestDerivativeWeightsOnNonUniformGrids()
    {
        // Testing finite differences coefficients...

        var mesherX = new Concentrating1dMesher(-2.0, 3.0, 50, (0.5, 0.01));
        var mesherY = new Concentrating1dMesher(0.5, 5.0, 25, (0.5, 0.1));
        var mesherZ = new Concentrating1dMesher(-1.0, 2.0, 31, (1.5, 0.01));

        var meshers = new FdmMesherComposite(mesherX, mesherY, mesherZ);

        const Real tol = 1e-13;
        for (Size direction = 0; direction < 3; ++direction)
        {
            SparseMatrix dfdx = new FirstDerivativeOp(direction, meshers).ToMatrix();
            SparseMatrix d2fdx2 = new SecondDerivativeOp(direction, meshers).ToMatrix();

            Vector gridPoints = meshers.Locations(direction);

            FdmLinearOpIterator iter = meshers.Layout.Begin();
            for (Size k = 0; k < meshers.Layout.Size; ++k, iter.Increment())
            {
                Size c = iter.Coordinates[direction];
                Size index = iter.Index;
                Size indexM1 = meshers.Layout.Neighbourhood(iter, direction, -1);
                Size indexP1 = meshers.Layout.Neighbourhood(iter, direction, +1);

                // test only if not on the boundary
                if (c == 0)
                {
                    Real[] twoPoints = [0.0, gridPoints[indexP1] - gridPoints[index]];

                    ImmutableArray<Real> ndWeights1st = new NumericalDifferentiation(null, 1, twoPoints).Weights();

                    Real beta1 = dfdx[index, index];
                    Real gamma1 = dfdx[index, indexP1];
                    if (Math.Abs((beta1 - ndWeights1st[0]) / beta1) > tol
                        || Math.Abs((gamma1 - ndWeights1st[1]) / gamma1) > tol)
                    {
                        Assert.Fail(
                            "can not reproduce the weights of the first order derivative operator on the lower boundary"
                            + $"\n expected beta:    {ndWeights1st[0]}"
                            + $"\n calculated beta:  {beta1}"
                            + $"\n difference beta:  {beta1 - ndWeights1st[0]}"
                            + $"\n expected gamma:   {ndWeights1st[1]}"
                            + $"\n calculated gamma: {gamma1}"
                            + $"\n difference gamma: {gamma1 - ndWeights1st[1]}");
                    }

                    // free boundary condition by default
                    Real beta2 = d2fdx2[index, index];
                    Real gamma2 = d2fdx2[index, indexP1];

                    if (Math.Abs(beta2) > QL_EPSILON || Math.Abs(gamma2) > QL_EPSILON)
                    {
                        Assert.Fail(
                            "can not reproduce the weights of the second order derivative operator on the lower boundary"
                            + $"\n expected beta:    {0.0}"
                            + $"\n calculated beta:  {beta2}"
                            + $"\n expected gamma:   {0.0}"
                            + $"\n calculated gamma: {gamma2}");
                    }
                }
                else if (c == meshers.Layout.Dim[direction] - 1)
                {
                    Real[] twoPoints = [gridPoints[indexM1] - gridPoints[index], 0.0];

                    ImmutableArray<Real> ndWeights1st = new NumericalDifferentiation(null, 1, twoPoints).Weights();

                    Real alpha1 = dfdx[index, indexM1];
                    Real beta1 = dfdx[index, index];
                    if (Math.Abs((alpha1 - ndWeights1st[0]) / alpha1) > tol
                        || Math.Abs((beta1 - ndWeights1st[1]) / beta1) > tol)
                    {
                        Assert.Fail(
                            "can not reproduce the weights of the first order derivative operator on the upper boundary"
                            + $"\n expected alpha:   {ndWeights1st[0]}"
                            + $"\n calculated alpha: {alpha1}"
                            + $"\n difference alpha: {alpha1 - ndWeights1st[0]}"
                            + $"\n expected beta:    {ndWeights1st[1]}"
                            + $"\n calculated beta:  {beta1}"
                            + $"\n difference beta:  {beta1 - ndWeights1st[1]}");
                    }

                    // free boundary condition by default
                    Real alpha2 = d2fdx2[index, indexM1];
                    Real beta2 = d2fdx2[index, index];

                    if (Math.Abs(alpha2) > QL_EPSILON || Math.Abs(beta2) > QL_EPSILON)
                    {
                        Assert.Fail(
                            "can not reproduce the weights of the second order derivative operator on the upper boundary"
                            + $"\n expected alpha:   {0.0}"
                            + $"\n calculated alpha: {alpha2}"
                            + $"\n expected beta:    {0.0}"
                            + $"\n calculated beta:  {beta2}");
                    }
                }
                else
                {
                    Real[] threePoints =
                    [
                        gridPoints[indexM1] - gridPoints[index], 0.0, gridPoints[indexP1] - gridPoints[index],
                    ];

                    ImmutableArray<Real> ndWeights1st = new NumericalDifferentiation(null, 1, threePoints).Weights();

                    Real alpha1 = dfdx[index, indexM1];
                    Real beta1 = dfdx[index, index];
                    Real gamma1 = dfdx[index, indexP1];

                    if (Math.Abs((alpha1 - ndWeights1st[0]) / alpha1) > tol
                        || Math.Abs((beta1 - ndWeights1st[1]) / beta1) > tol
                        || Math.Abs((gamma1 - ndWeights1st[2]) / gamma1) > tol)
                    {
                        Assert.Fail(
                            "can not reproduce the weights of the first order derivative operator"
                            + $"\n expected alpha:   {ndWeights1st[0]}"
                            + $"\n calculated alpha: {alpha1}"
                            + $"\n difference alpha: {alpha1 - ndWeights1st[0]}"
                            + $"\n expected beta:    {ndWeights1st[1]}"
                            + $"\n calculated beta:  {beta1}"
                            + $"\n difference beta:  {beta1 - ndWeights1st[1]}"
                            + $"\n expected gamma:   {ndWeights1st[2]}"
                            + $"\n calculated gamma: {gamma1}"
                            + $"\n difference gamma: {gamma1 - ndWeights1st[2]}");
                    }

                    ImmutableArray<Real> ndWeights2nd = new NumericalDifferentiation(null, 2, threePoints).Weights();

                    Real alpha2 = d2fdx2[index, indexM1];
                    Real beta2 = d2fdx2[index, index];
                    Real gamma2 = d2fdx2[index, indexP1];
                    if (Math.Abs((alpha2 - ndWeights2nd[0]) / alpha2) > tol
                        || Math.Abs((beta2 - ndWeights2nd[1]) / beta2) > tol
                        || Math.Abs((gamma2 - ndWeights2nd[2]) / gamma2) > tol)
                    {
                        Assert.Fail(
                            "can not reproduce the weights of the second order derivative operator"
                            + $"\n expected alpha:   {ndWeights2nd[0]}"
                            + $"\n calculated alpha: {alpha2}"
                            + $"\n difference alpha: {alpha2 - ndWeights2nd[0]}"
                            + $"\n expected beta:    {ndWeights2nd[1]}"
                            + $"\n calculated beta:  {beta2}"
                            + $"\n difference beta:  {beta2 - ndWeights2nd[1]}"
                            + $"\n expected gamma:   {ndWeights2nd[2]}"
                            + $"\n calculated gamma: {gamma2}"
                            + $"\n difference gamma: {gamma2 - ndWeights2nd[2]}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestSecondOrderMixedDerivativesMapApply()
    {
        // Testing application of second-order mixed-derivatives map...
        Size[] dim = [50, 50, 50];

        var index = new FdmLinearOpLayout(dim);

        (double, double)[] boundaries = [(0, 0.5), (0, 0.5), (0, 0.5)];

        FdmMesher mesher = new UniformGridMesher(index, boundaries);

        var r = new Vector(mesher.Layout.Size);

        FdmLinearOpIterator iter = index.Begin();
        for (Size k = 0; k < index.Size; ++k, iter.Increment())
        {
            Real x = mesher.Location(iter, 0);
            Real y = mesher.Location(iter, 1);
            Real z = mesher.Location(iter, 2);

            r[iter.Index] = Math.Sin(x) * Math.Cos(y) * Math.Exp(z);
        }

        Vector t = new SecondOrderMixedDerivativeOp(0, 1, mesher).Apply(r);
        Vector u = new SecondOrderMixedDerivativeOp(1, 0, mesher).Apply(r);

        const Real tol = 5e-2;
        iter = index.Begin();
        for (Size k = 0; k < index.Size; ++k, iter.Increment())
        {
            Size i = iter.Index;
            Real x = mesher.Location(iter, 0);
            Real y = mesher.Location(iter, 1);
            Real z = mesher.Location(iter, 2);

            Real d = -Math.Cos(x) * Math.Sin(y) * Math.Exp(z);

            if (Math.Abs(d - t[i]) > tol)
            {
                Assert.Fail($"numerical derivative in dxdy deviation is too big\n  found at {x} {y} {z}");
            }

            if (Math.Abs(t[i] - u[i]) > 1e5 * QL_EPSILON)
            {
                Assert.Fail($"numerical derivative in dxdy not equal to dydx\n  found at {x} {y} {z}\n  value    {Math.Abs(t[i] - u[i])}");
            }
        }

        t = new SecondOrderMixedDerivativeOp(0, 2, mesher).Apply(r);
        u = new SecondOrderMixedDerivativeOp(2, 0, mesher).Apply(r);
        iter = index.Begin();
        for (Size k = 0; k < index.Size; ++k, iter.Increment())
        {
            Size i = iter.Index;
            Real x = mesher.Location(iter, 0);
            Real y = mesher.Location(iter, 1);
            Real z = mesher.Location(iter, 2);

            Real d = Math.Cos(x) * Math.Cos(y) * Math.Exp(z);

            if (Math.Abs(d - t[i]) > tol)
            {
                Assert.Fail($"numerical derivative in dxdy deviation is too big\n  found at {x} {y} {z}");
            }

            if (Math.Abs(t[i] - u[i]) > 1e5 * QL_EPSILON)
            {
                Assert.Fail($"numerical derivative in dxdz not equal to dzdx\n  found at {x} {y} {z}\n  value    {Math.Abs(t[i] - u[i])}");
            }
        }

        t = new SecondOrderMixedDerivativeOp(1, 2, mesher).Apply(r);
        u = new SecondOrderMixedDerivativeOp(2, 1, mesher).Apply(r);
        iter = index.Begin();
        for (Size k = 0; k < index.Size; ++k, iter.Increment())
        {
            Size i = iter.Index;
            Real x = mesher.Location(iter, 0);
            Real y = mesher.Location(iter, 1);
            Real z = mesher.Location(iter, 2);

            Real d = -Math.Sin(x) * Math.Sin(y) * Math.Exp(z);

            if (Math.Abs(d - t[i]) > tol)
            {
                Assert.Fail($"numerical derivative in dydz deviation is too big\n  found at {x} {y} {z}");
            }

            if (Math.Abs(t[i] - u[i]) > 1e5 * QL_EPSILON)
            {
                Assert.Fail($"numerical derivative in dydz not equal to dzdy\n  found at {x} {y} {z}\n  value    {Math.Abs(t[i] - u[i])}");
            }
        }
    }

    [Fact]
    public void TestTripleBandMapSolve()
    {
        // Testing triple-band map solution...
        Size[] dim = [100, 400];

        var layout = new FdmLinearOpLayout(dim);

        (double, double)[] boundaries = [(0, 1.0), (0, 1.0)];

        FdmMesher mesher = new UniformGridMesher(layout, boundaries);

        var dy = new FirstDerivativeOp(1, mesher);
        dy.Axpyb(new Vector(1, 2.0), dy, dy, new Vector(1, 1.0));

        // check copy constructor
        var copyOfDy = new TripleBandLinearOp(dy);

        var u = new Vector(layout.Size);
        for (Size i = 0; i < layout.Size; ++i)
        {
            u[i] = Math.Sin(0.1 * i) + Math.Cos(0.35 * i);
        }

        Vector t = dy.SolveSplitting(copyOfDy.Apply(u), 1.0, 0.0);
        for (Size i = 0; i < u.Size; ++i)
        {
            if (Math.Abs(u[i] - t[i]) > 1e-6)
            {
                Assert.Fail($"solve and apply are not consistent \n expected      : {u[i]}\n calculated    : {t[i]}");
            }
        }

        var dx = new FirstDerivativeOp(0, mesher);
        dx.Axpyb(new Vector(), dx, dx, new Vector(1, 1.0));

        TripleBandLinearOp copyOfDx = new FirstDerivativeOp(0, mesher);

        // check assignment
        copyOfDx = dx.Clone();

        t = dx.SolveSplitting(copyOfDx.Apply(u), 1.0, 0.0);
        for (Size i = 0; i < u.Size; ++i)
        {
            if (Math.Abs(u[i] - t[i]) > 1e-6)
            {
                Assert.Fail($"solve and apply are not consistent \n expected      : {u[i]}\n calculated    : {t[i]}");
            }
        }

        var dxx = new SecondDerivativeOp(0, mesher);
        dxx.Axpyb(new Vector(1, 0.5), dxx, dx, new Vector(1, 1.0));

        // check of copy constructor
        var copyOfDxx = new TripleBandLinearOp(dxx);

        t = dxx.SolveSplitting(copyOfDxx.Apply(u), 1.0, 0.0);

        for (Size i = 0; i < u.Size; ++i)
        {
            if (Math.Abs(u[i] - t[i]) > 1e-6)
            {
                Assert.Fail($"solve and apply are not consistent \n expected      : {u[i]}\n calculated    : {t[i]}");
            }
        }

        // check assignment operator
        _ = copyOfDxx.Add(new SecondDerivativeOp(1, mesher));
        copyOfDxx = dxx.Clone();

        t = dxx.SolveSplitting(copyOfDxx.Apply(u), 1.0, 0.0);

        for (Size i = 0; i < u.Size; ++i)
        {
            if (Math.Abs(u[i] - t[i]) > 1e-6)
            {
                Assert.Fail($"solve and apply are not consistent \n expected      : {u[i]}\n calculated    : {t[i]}");
            }
        }
    }

    [Fact]
    public void TestFdmHestonBarrier()
    {
        // Testing FDM with barrier option in Heston model...
        Size[] dim = [200, 100];

        var index = new FdmLinearOpLayout(dim);

        (double, double)[] boundaries = [(3.8, 4.905274778), (0.0, 1.0)];

        FdmMesher mesher = new UniformGridMesher(index, boundaries);

        Real s0 = 100.0;

        var context = new EvaluationContext { EvaluationDate = new Date(28, Month.March, 2004) };
        YieldTermStructure rTS = FlatRate(0.05, new Actual365Fixed(), context);
        YieldTermStructure qTS = FlatRate(0.0, new Actual365Fixed(), context);

        var hestonProcess = new HestonProcess(rTS, qTS, s0, 0.04, 2.5, 0.04, 0.66, -0.8);

        FdmLinearOpComposite hestonOp = new FdmHestonOp(mesher, hestonProcess);

        var rhs = new Vector(mesher.Layout.Size);
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (Size k = 0; k < mesher.Layout.Size; ++k, iter.Increment())
        {
            rhs[iter.Index] = Math.Max(Math.Exp(mesher.Location(iter, 0)) - 100, 0.0);
        }

        ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet =
        [
            new FdmDirichletBoundary(mesher, 0.0, 0, BoundaryConditionSide.Upper),
        ];

        Real theta = 0.5 + (Math.Sqrt(3.0) / 6.0);
        var hsEvolver = new HundsdorferScheme(theta, 0.5, hestonOp, bcSet);
        var hsModel = new FiniteDifferenceModel<HundsdorferScheme>(hsEvolver);
        hsModel.Rollback(rhs.AsSpan(), 1.0, 0.0, 50);

        var ret = new Matrix(dim[0], dim[1]);
        for (Size i = 0; i < dim[0]; ++i)
        {
            for (Size j = 0; j < dim[1]; ++j)
            {
                ret[i, j] = rhs[i + (j * dim[0])];
            }
        }

        var tx = new List<Real>();
        var ty = new List<Real>();
        iter = mesher.Layout.Begin();
        for (Size k = 0; k < mesher.Layout.Size; ++k, iter.Increment())
        {
            if (iter.Coordinates[1] == 0)
            {
                tx.Add(mesher.Location(iter, 0));
            }

            if (iter.Coordinates[0] == 0)
            {
                ty.Add(mesher.Location(iter, 1));
            }
        }

        var interpolate = new BilinearInterpolation(ty.ToArray(), tx.ToArray(), ret);

        const Real x = 100;
        const Real v0 = 0.04;

        Real npv = interpolate.Evaluate(v0, Math.Log(x));
        Real delta = 0.5 * (interpolate.Evaluate(v0, Math.Log(x + 1)) - interpolate.Evaluate(v0, Math.Log(x - 1)));
        Real gamma = interpolate.Evaluate(v0, Math.Log(x + 1)) + interpolate.Evaluate(v0, Math.Log(x - 1)) - (2 * npv);

        const Real npvExpected = 9.049016;
        const Real deltaExpected = 0.511285;
        const Real gammaExpected = -0.034296;

        if (Math.Abs(npv - npvExpected) > 0.000001)
        {
            Assert.Fail("Error in calculating PV for Heston barrier option");
        }

        if (Math.Abs(delta - deltaExpected) > 0.000001)
        {
            Assert.Fail("Error in calculating Delta for Heston barrier option");
        }

        if (Math.Abs(gamma - gammaExpected) > 0.000001)
        {
            Assert.Fail("Error in calculating Gamma for Heston barrier option");
        }
    }

    [Fact]
    public void TestFdmHestonAmerican()
    {
        // Testing FDM with American option in Heston model...
        Size[] dim = [200, 100];

        var index = new FdmLinearOpLayout(dim);

        (double, double)[] boundaries = [(3.8, Math.Log(220.0)), (0.0, 1.0)];

        FdmMesher mesher = new UniformGridMesher(index, boundaries);

        Real s0 = 100.0;

        var context = new EvaluationContext { EvaluationDate = new Date(28, Month.March, 2004) };
        YieldTermStructure rTS = FlatRate(0.05, new Actual365Fixed(), context);
        YieldTermStructure qTS = FlatRate(0.0, new Actual365Fixed(), context);

        var hestonProcess = new HestonProcess(rTS, qTS, s0, 0.04, 2.5, 0.04, 0.66, -0.8);

        FdmLinearOpComposite linearOp = new FdmHestonOp(mesher, hestonProcess);

        Payoff payoff = new PlainVanillaPayoff(OptionType.Put, 100.0);
        var rhs = new Vector(mesher.Layout.Size);
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (Size k = 0; k < mesher.Layout.Size; ++k, iter.Increment())
        {
            rhs[iter.Index] = payoff.Evaluate(Math.Exp(mesher.Location(iter, 0)));
        }

        var condition = new FdmAmericanStepCondition(mesher, new FdmLogInnerValue(payoff, mesher, 0));
        Real theta = 0.5 + (Math.Sqrt(3.0) / 6.0);
        var hsEvolver = new HundsdorferScheme(theta, 0.5, linearOp);
        var hsModel = new FiniteDifferenceModel<HundsdorferScheme>(hsEvolver);
        hsModel.Rollback(rhs.AsSpan(), 1.0, 0.0, 50, condition);

        var ret = new Matrix(dim[0], dim[1]);
        for (Size i = 0; i < dim[0]; ++i)
        {
            for (Size j = 0; j < dim[1]; ++j)
            {
                ret[i, j] = rhs[i + (j * dim[0])];
            }
        }

        var tx = new List<Real>();
        var ty = new List<Real>();
        iter = mesher.Layout.Begin();
        for (Size k = 0; k < mesher.Layout.Size; ++k, iter.Increment())
        {
            if (iter.Coordinates[1] == 0)
            {
                tx.Add(mesher.Location(iter, 0));
            }

            if (iter.Coordinates[0] == 0)
            {
                ty.Add(mesher.Location(iter, 1));
            }
        }

        var interpolate = new BilinearInterpolation(ty.ToArray(), tx.ToArray(), ret);

        const Real x = 100;
        const Real v0 = 0.04;

        Real npv = interpolate.Evaluate(v0, Math.Log(x));
        const Real npvExpected = 5.641648;

        if (Math.Abs(npv - npvExpected) > 0.000001)
        {
            Assert.Fail("Error in calculating PV for Heston American Option");
        }
    }

    [Fact]
    public void TestFdmHestonExpress()
    {
        // Testing FDM with express certificate in Heston model...
        Size[] dim = [200, 100];

        var index = new FdmLinearOpLayout(dim);

        (double, double)[] boundaries = [(3.8, Math.Log(220.0)), (0.0, 1.0)];

        FdmMesher mesher = new UniformGridMesher(index, boundaries);

        Real s0 = 100.0;

        var evaluationDate = new Date(28, Month.March, 2004);
        var context = new EvaluationContext { EvaluationDate = evaluationDate };

        YieldTermStructure rTS = FlatRate(0.05, new Actual365Fixed(), context);
        YieldTermStructure qTS = FlatRate(0.0, new Actual365Fixed(), context);

        var hestonProcess = new HestonProcess(rTS, qTS, s0, 0.04, 2.5, 0.04, 0.66, -0.8);

        Real[] triggerLevels = [100.0, 100.0];
        Real[] redemptions = [108.0, 108.0];
        double[] exerciseTimes = [0.333, 0.666];

        ImmutableArray<Dividend> dividendSchedule =
            [new FixedDividend(2.5, evaluationDate + new Period(6, TimeUnit.Months))];
        var dividendCondition = new FdmDividendHandler(
            dividendSchedule, mesher, rTS.ReferenceDate, rTS.DayCounter, 0, context);

        StepCondition expressCondition = new FdmHestonExpressCondition(redemptions, triggerLevels, exerciseTimes, mesher);

        IEnumerable<double>[] stoppingTimes = [exerciseTimes, dividendCondition.DividendTimes];

        StepCondition[] conditions = [expressCondition, dividendCondition];

        var condition = new FdmStepConditionComposite(stoppingTimes, conditions);

        Payoff payoff = new ExpressPayoff();

        FdmInnerValueCalculator calculator = new FdmLogInnerValue(payoff, mesher, 0);

        ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet = [];
        var solverDesc = new FdmSolverDesc(mesher, bcSet, condition, calculator, 1.0, 50, 0);
        var solver = new FdmHestonSolver(hestonProcess, solverDesc);

        Real s = s0;
        const Real v0 = 0.04;

        if (Math.Abs(solver.ValueAt(s, v0) - 101.027) > 0.01)
        {
            Assert.Fail("Error in calculating PV for Heston Express Certificate");
        }

        if (Math.Abs(solver.DeltaAt(s, v0) - 0.4181) > 0.001)
        {
            Assert.Fail("Error in calculating Delta for Heston Express Certificate");
        }

        if (Math.Abs(solver.GammaAt(s, v0) + 0.0400) > 0.001)
        {
            Assert.Fail("Error in calculating Gamma for Heston Express Certificate");
        }

        if (Math.Abs(solver.MeanVarianceDeltaAt(s, v0) - 0.6602) > 0.001)
        {
            Assert.Fail("Error in calculating mean variance Delta for Heston Express Certificate");
        }

        if (Math.Abs(solver.MeanVarianceGammaAt(s, v0) + 0.0316) > 0.001)
        {
            Assert.Fail("Error in calculating mean variance Delta for Heston Express Certificate");
        }
    }

    [Fact]
    public void TestFdmHestonHullWhiteOp()
    {
        // Testing FDM with Heston Hull-White model...

        var today = new Date(28, Month.March, 2004);

        var exerciseDate = new Date(28, Month.March, 2012);
        double maturity = new Actual365Fixed().YearFraction(today, exerciseDate);

        Size[] dim = [51, 31, 31];

        HybridHestonHullWhiteProcess jointProcess = CreateHestonHullWhite(today, maturity);
        FdmSolverDesc desc = CreateSolverDesc(dim, jointProcess);
        FdmMesher mesher = desc.Mesher;

        HullWhiteForwardProcess hwFwdProcess = jointProcess.HullWhiteProcess;

        var hwProcess = new HullWhiteProcess(
            jointProcess.HestonProcess.RiskFreeRate, hwFwdProcess.A, hwFwdProcess.Sigma);

        FdmLinearOpComposite linearOp = new FdmHestonHullWhiteOp(
            mesher, jointProcess.HestonProcess, hwProcess, jointProcess.Eta);

        Real[] rhs = new Real[mesher.Layout.Size];
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int n = 0; n < mesher.Layout.Size; ++n, iter.Increment())
        {
            rhs[iter.Index] = desc.Calculator.AvgInnerValue(iter, maturity);
        }

        Real theta = 0.5 + (Math.Sqrt(3.0) / 6.0);
        var hsEvolver = new HundsdorferScheme(theta, 0.5, linearOp);
        var hsModel = new FiniteDifferenceModel<HundsdorferScheme>(hsEvolver);

        hsModel.Rollback(rhs, maturity, 0.0, desc.TimeSteps);

        List<Real> tx = [], ty = [], tr = [], y = [];
        iter = mesher.Layout.Begin();
        for (int n = 0; n < mesher.Layout.Size; ++n, iter.Increment())
        {
            if (iter.Coordinates[1] == 0 && iter.Coordinates[2] == 0)
            {
                tx.Add(mesher.Location(iter, 0));
            }

            if (iter.Coordinates[0] == 0 && iter.Coordinates[2] == 0)
            {
                ty.Add(mesher.Location(iter, 1));
            }

            if (iter.Coordinates[0] == 0 && iter.Coordinates[1] == 0)
            {
                tr.Add(mesher.Location(iter, 2));
            }
        }

        const Real x0 = 100;
        Real v0 = jointProcess.HestonProcess.V0;
        const Real r0 = 0;
        for (Size k = 0; k < dim[2]; ++k)
        {
            var ret = new Matrix(dim[0], dim[1]);
            for (Size i = 0; i < dim[0]; ++i)
            {
                for (Size j = 0; j < dim[1]; ++j)
                {
                    ret[i, j] = rhs[i + (j * dim[0]) + (k * dim[0] * dim[1])];
                }
            }

            y.Add(new BicubicSpline([.. ty], [.. tx], ret).Evaluate(v0, Math.Log(x0)));
        }

        Real directCalc = new MonotonicCubicNaturalSpline([.. tr], [.. y]).Evaluate(r0);

        Real[] x = [Math.Log(x0), v0, r0];

        var solver3d = new Fdm3DimSolver(desc, FdmSchemeDesc.Hundsdorfer(), linearOp);
        Real solverCalc = solver3d.InterpolateAt(x[0], x[1], x[2]);
        Real solverTheta = solver3d.ThetaAt(x[0], x[1], x[2])!.Value;

        if (Math.Abs(directCalc - solverCalc) > 1e-4)
        {
            Assert.Fail("Error in calculating PV for Heston Hull White Option");
        }

        var solverNd = new FdmNdimSolver(desc, FdmSchemeDesc.Hundsdorfer(), linearOp);
        Real solverNdCalc = solverNd.InterpolateAt(x);
        Real solverNdTheta = solverNd.ThetaAt(x)!.Value;

        if (Math.Abs(solverNdCalc - solverCalc) > 1e-4)
        {
            Assert.Fail("Error in calculating PV for Heston Hull White Option");
        }

        if (Math.Abs(solverNdTheta - solverTheta) > 1e-4)
        {
            Assert.Fail("Error in calculating PV for Heston Hull White Option");
        }

        var option = new VanillaOption(
            new PlainVanillaPayoff(OptionType.Call, 160.0), new EuropeanExercise(exerciseDate));

        const Real tol = 0.025;

        // QuantLib sets the engine and prices nothing with it
        Func<VanillaOption, EvaluationContext, OneAssetOptionResults> engine =
            new MakeMCHestonHullWhiteEngine<PseudoRandom, GeneralStatistics>(jointProcess, 42)
                .WithSteps(200)
                .WithAntitheticVariate()
                .WithControlVariate()
                .WithAbsoluteTolerance(tol)
                .ToEngine();
        _ = (option, engine);

        // the following takes far too long
        // const Real expected = option.NPV();
        // use precalculated value instead
        const Real expected = 4.73;

        if (Math.Abs(directCalc - expected) > 3 * tol)
        {
            Assert.Fail("Error in calculating MC PV for Heston Hull White Option");
        }
    }

    [Fact]
    public void TestBiCGstab()
    {
        // Testing bi-conjugated gradient stabilized algorithm...

        const Size n = 41, m = 21;
        const Real theta = 1.0;
        SparseMatrix a = CreateTestMatrix(n, m, theta);

        Vector Matmult(Vector x) => Axpy(a, x);

        var ilu = new SparseILUPreconditioner(a, 4);
        Vector Precond(Vector x) => ilu.Apply(x);

        var b = new Vector(n * m);
        var rng = new MersenneTwisterUniformRng(1234);
        for (Size i = 0; i < b.Size; ++i)
        {
            b[i] = rng.Next().Value;
        }

        const Real tol = 1e-10;

        var biCGstab = new BiCGstab(Matmult, n * m, tol, Precond);
        Vector x = biCGstab.Solve(b).X;

        Real error = Math.Sqrt(ArrayFunctions.DotProduct(b - Axpy(a, x), b - Axpy(a, x)) / ArrayFunctions.DotProduct(b, b));

        if (error > tol)
        {
            Assert.Fail($"Error calculating the inverse using BiCGstab\n tolerance:  {tol}\n error:      {error}");
        }
    }

    [Fact]
    public void TestGMRES()
    {
        // Testing GMRES algorithm...

        const Size n = 41, m = 21;
        const Real theta = 1.0;
        SparseMatrix a = CreateTestMatrix(n, m, theta);

        Vector Matmult(Vector x) => Axpy(a, x);

        var ilu = new SparseILUPreconditioner(a, 4);
        Vector Precond(Vector x) => ilu.Apply(x);

        var b = new Vector(n * m);
        var rng = new MersenneTwisterUniformRng(1234);
        for (Size i = 0; i < b.Size; ++i)
        {
            b[i] = rng.Next().Value;
        }

        const Real tol = 1e-10;

        var gmres = new GMRES(Matmult, n * m, tol, Precond);
        GMRESResult result = gmres.Solve(b, b);
        Vector x = result.X;
        Real errorCalculated = result.Errors[^1];

        Real error = Math.Sqrt(ArrayFunctions.DotProduct(b - Axpy(a, x), b - Axpy(a, x)) / ArrayFunctions.DotProduct(b, b));

        if (error > tol)
        {
            Assert.Fail($"Error calculating the inverse using GMRES\n tolerance:  {tol}\n error:      {error}");
        }

        if (Math.Abs(error - errorCalculated) > 10 * QL_EPSILON)
        {
            Assert.Fail(
                $"Calculation if the error in GMRES went wrong\n calculated: {errorCalculated}\n error:      {error}");
        }

        var gmresRestart = new GMRES(Matmult, 5, tol, Precond);
        GMRESResult resultRestart = gmresRestart.SolveWithRestart(5, b, b);
        Real errorWithRestart = resultRestart.Errors[^1];

        if (errorWithRestart > tol)
        {
            Assert.Fail(
                "Error calculating the inverse using GMRES with restarts"
                + $"\n tolerance:  {tol}\n error:      {errorWithRestart}");
        }
    }

    [Fact]
    public void TestCrankNicolsonWithDamping()
    {
        // Testing Crank-Nicolson with initial implicit damping steps for a digital option...

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        Real spot = 100.0;
        YieldTermStructure qTS = new FlatForward(today, 0.06, dc);
        YieldTermStructure rTS = new FlatForward(today, 0.06, dc);
        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), 0.35, dc);

        StrikedTypePayoff payoff = new CashOrNothingPayoff(OptionType.Put, 100, 10.0);

        double maturity = 0.75;
        Date exDate = today + TimeToDays(maturity);
        Exercise exercise = new EuropeanExercise(exDate);

        var process = new BlackScholesMertonProcess(spot, qTS, rTS, volTS);

        var opt = new VanillaOption(payoff, exercise);
        OneAssetOptionResults analytic = AnalyticEuropeanEngine.Calculate(opt, context, process);
        Real expectedPV = analytic.NPV!.Value;
        Real expectedGamma = analytic.Gamma!.Value;

        // fd pricing using implicit damping steps and Crank Nicolson
        const Size csSteps = 25, dampingSteps = 3, xGrid = 400;
        Size[] dim = [xGrid];

        var layout = new FdmLinearOpLayout(dim);
        Fdm1dMesher equityMesher = new FdmBlackScholesMesher(
            dim[0], process, maturity, payoff.Strike, null, null, 0.0001, 1.5, (payoff.Strike, 0.01));

        FdmMesher mesher = new FdmMesherComposite(equityMesher);

        var map = new FdmBlackScholesOp(mesher, process, payoff.Strike);

        FdmInnerValueCalculator calculator = new FdmLogInnerValue(payoff, mesher, 0);

        var rhs = new Vector(layout.Size);
        var x = new Vector(layout.Size);

        FdmLinearOpIterator iter = layout.Begin();
        for (Size i = 0; i < layout.Size; ++i, iter.Increment())
        {
            rhs[iter.Index] = calculator.AvgInnerValue(iter, maturity);
            x[iter.Index] = mesher.Location(iter, 0);
        }

        var solver = new FdmBackwardSolver(map, [], null, FdmSchemeDesc.Douglas());
        solver.Rollback(rhs.AsSpan(), maturity, 0.0, csSteps, dampingSteps);

        var spline = new MonotonicCubicNaturalSpline(x.AsSpan(), rhs.AsSpan());

        Real s = spot;
        Real calculatedPV = spline.Evaluate(Math.Log(s));
        Real calculatedGamma = (spline.SecondDerivative(Math.Log(s)) - spline.Derivative(Math.Log(s))) / (s * s);

        Real relTol = 2e-3;

        if (Math.Abs(calculatedPV - expectedPV) > relTol * expectedPV)
        {
            Assert.Fail(
                $"Error calculating the PV of the digital option\n rel. tolerance:  {relTol}\n expected:        "
                + $"{expectedPV}\n calculated:      {calculatedPV}");
        }

        if (Math.Abs(calculatedGamma - expectedGamma) > relTol * expectedGamma)
        {
            Assert.Fail(
                $"Error calculating the Gamma of the digital option\n rel. tolerance:  {relTol}\n expected:        "
                + $"{expectedGamma}\n calculated:      {calculatedGamma}");
        }
    }

    [Fact]
    public void TestFdmMesherIntegral()
    {
        // Testing integrals over meshers functions...

        var mesher = new FdmMesherComposite(
            new Concentrating1dMesher(-1, 1.6, 21, (0, 0.1)),
            new Concentrating1dMesher(-3, 4, 11, (1, 0.01)),
            new Concentrating1dMesher(-2, 1, 5, (0.5, 0.1)));

        var f = new Vector(mesher.Layout.Size);
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int k = 0; k < mesher.Layout.Size; ++k, iter.Increment())
        {
            double x = mesher.Location(iter, 0);
            double y = mesher.Location(iter, 1);
            double z = mesher.Location(iter, 2);

            f[iter.Index] = (x * x) + (3 * y * y) - (3 * z * z)
                + (2 * x * y) - (x * z) - (3 * y * z)
                + (4 * x) - y - (3 * z) + 2;
        }

        const double tol = 1e-12;

        // Simpson's rule has to be exact here, Mathematica code gives
        // Integrate[x*x+3*y*y-3*z*z+2*x*y-x*z-3*y*z+4*x-y-3*z+2,
        //           {x, -1, 16/10}, {y, -3, 4}, {z, -2, 1}]
        const double expectedSimpson = 876.512;
        double calculatedSimpson = new FdmMesherIntegral(
            mesher, (x, y) => DiscreteSimpsonIntegral.Integrate(x.AsSpan(), y.AsSpan())).Integrate(f);

        Assert.False(
            Math.Abs(calculatedSimpson - expectedSimpson) > tol * expectedSimpson,
            "discrete mesher integration using Simpson's rule failed: "
            + $"\n    calculated: {calculatedSimpson:G16}\n    expected:   {expectedSimpson:G16}");

        const double expectedTrapezoid = 917.0148209153263;
        double calculatedTrapezoid = new FdmMesherIntegral(
            mesher, (x, y) => DiscreteTrapezoidIntegral.Integrate(x.AsSpan(), y.AsSpan())).Integrate(f);

        Assert.False(
            Math.Abs(calculatedTrapezoid - expectedTrapezoid) > tol * expectedTrapezoid,
            "discrete mesher integration using Trapezoid rule failed: "
            + $"\n    calculated: {calculatedTrapezoid:G16}\n    expected:   {expectedTrapezoid:G16}");
    }

    [Fact]
    public void TestHighInterestRateBlackScholesMesher()
    {
        // Testing Black-Scholes mesher in a high interest rate scenario...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(11, Month.February, 2018);

        Real spot = 100;
        Rate r = 0.21;
        Rate q = 0.02;
        Volatility v = 0.25;

        var process = new GeneralizedBlackScholesProcess(
            spot,
            new FlatForward(today, q, dc),
            new FlatForward(today, r, dc),
            new BlackConstantVol(today, new NullCalendar(), v, dc));

        const Size size = 10;
        double maturity = 2.0;
        Real strike = 100;
        Real eps = 0.05;
        Real normInvEps = 1.64485363;
        Real scaleFactor = 2.5;

        ImmutableArray<double> loc = new FdmBlackScholesMesher(
            size, process, maturity, strike, null, null, eps, scaleFactor).Locations();

        Real calculatedMin = Math.Exp(loc[0]);
        Real calculatedMax = Math.Exp(loc[^1]);

        Real minimum = spot * Math.Exp(-normInvEps * scaleFactor * v * Math.Sqrt(maturity));
        Real maximum = spot / process.RiskFreeRate.Discount(maturity) * process.DividendYield.Discount(maturity)
            * Math.Exp(normInvEps * scaleFactor * v * Math.Sqrt(maturity));

        Real relTol = 1e-7;

        Real maxDiff = Math.Abs(calculatedMax - maximum);
        if (maxDiff > relTol * maximum)
        {
            Assert.Fail(
                $"Upper bound for Black-Scholes mesher failed: \n    calculated: {calculatedMax}\n    expected:   "
                + $"{maximum}\n    difference: {maxDiff}\n    tolerance:  {relTol * maximum}");
        }

        Real minDiff = Math.Abs(calculatedMin - minimum);
        if (minDiff > relTol * minimum)
        {
            Assert.Fail(
                $"Lower bound for Black-Scholes mesher failed: \n    calculated: {calculatedMin}\n    expected:   "
                + $"{minimum}\n    difference: {minDiff}\n    tolerance:  {relTol * minimum}");
        }
    }

    // Adapted: the dividends' amounts are read in an evaluation context, which the mesher takes with them.
    [Fact]
    public void TestLowVolatilityHighDiscreteDividendBlackScholesMesher()
    {
        // Testing Black-Scholes mesher in a low volatility and high discrete dividend scenario...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(28, Month.January, 2018);
        var context = new EvaluationContext { EvaluationDate = today };

        Real spot = 100.0;
        YieldTermStructure qTS = new FlatForward(today, 0.07, dc);
        YieldTermStructure rTS = new FlatForward(today, 0.16, dc);
        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), 0.0, dc);

        var process = new GeneralizedBlackScholesProcess(spot, qTS, rTS, volTS);

        Date firstDivDate = today + new Period(7, TimeUnit.Months);
        Real firstDivAmount = 10.0;
        Date secondDivDate = today + new Period(11, TimeUnit.Months);
        Real secondDivAmount = 5.0;

        ImmutableArray<Dividend> divSchedule =
        [
            new FixedDividend(firstDivAmount, firstDivDate),
            new FixedDividend(secondDivAmount, secondDivDate),
        ];

        const Size size = 5;
        double maturity = 1.0;
        Real strike = 100;
        Real eps = 0.0001;
        Real scaleFactor = 1.5;

        ImmutableArray<double> loc = new FdmBlackScholesMesher(
            size,
            process,
            maturity,
            strike,
            null,
            null,
            eps,
            scaleFactor,
            (null, null),
            divSchedule,
            context: context).Locations();

        Real maximum = spot * qTS.Discount(firstDivDate) / rTS.Discount(firstDivDate);

        Real minimum = ((1 - (firstDivAmount / (spot * qTS.Discount(firstDivDate) / rTS.Discount(firstDivDate))))
            * spot * qTS.Discount(secondDivDate) / rTS.Discount(secondDivDate))
            - secondDivAmount;

        Real calculatedMax = Math.Exp(loc[^1]);
        Real calculatedMin = Math.Exp(loc[0]);

        const double relTol = 1e5 * QL_EPSILON;

        Real maxDiff = Math.Abs(calculatedMax - maximum);
        if (maxDiff > relTol * maximum)
        {
            Assert.Fail(
                $"Upper bound for Black-Scholes mesher failed: \n    calculated: {calculatedMax}\n    expected:   "
                + $"{maximum}\n    difference: {maxDiff}\n    tolerance:  {relTol * maximum}");
        }

        Real minDiff = Math.Abs(calculatedMin - minimum);
        if (minDiff > relTol * minimum)
        {
            Assert.Fail(
                $"Lower bound for Black-Scholes mesher failed: \n    calculated: {calculatedMin}\n    expected:   "
                + $"{minimum}\n    difference: {minDiff}\n    tolerance:  {relTol * minimum}");
        }
    }

    // The test suite's flatRate(forward, dc): a flat forward from the evaluation date.
    // fdmlinearop.cpp's axpy (lines 217-225): uBLAS's axpy_prod, the product A·x.
    private static Vector Axpy(SparseMatrix a, Vector x) => SparseMatrixFunctions.Prod(a, x);

    // fdmlinearop.cpp's createTestMatrix (lines 227-252).
    private static SparseMatrix CreateTestMatrix(Size n, Size m, Real theta)
    {
        var a = new SparseMatrix(n * m, n * m);

        for (Size i = 0; i < n; ++i)
        {
            for (Size j = 0; j < m; ++j)
            {
                Size k = (i * m) + j;
                a[k, k] = 1.0;

                if (i > 0 && j > 0 && i < n - 1 && j < m - 1)
                {
                    Size im1 = i - 1;
                    Size ip1 = i + 1;
                    Size jm1 = j - 1;
                    Size jp1 = j + 1;
                    Real delta = theta / ((ip1 - im1) * (jp1 - jm1));

                    a[k, (im1 * m) + jm1] = delta;
                    a[k, (im1 * m) + jp1] = -delta;
                    a[k, (ip1 * m) + jm1] = -delta;
                    a[k, (ip1 * m) + jp1] = delta;
                }
            }
        }

        return a;
    }

    private static YieldTermStructure FlatRate(Rate forward, DayCounter dc, EvaluationContext context) =>
        FlatForward.Create(0, new NullCalendar(), forward, dc, context);

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static Integer TimeToDays(double t, Integer daysPerYear = 360) =>
        (Integer)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    // QuantLib's nrElementsOfSparseMatrix: the stored elements counted row by row, as uBLAS's row iterator visits
    // them (none beyond the last row written to).
    private static Size NrElementsOfSparseMatrix(SparseMatrix m)
    {
        Size retVal = 0;
        for (int i = 0; i < m.Size1; ++i)
        {
            if (i + 1 < m.Filled1)
            {
                retVal += m.Index1Data[i + 1] - m.Index1Data[i];
            }
        }

        return retVal;
    }

    // The test suite's FdmHestonExpressCondition: at each exercise time, the redemption above the trigger.
    private sealed class FdmHestonExpressCondition : StepCondition
    {
        private readonly Real[] redemptions;
        private readonly Real[] triggerLevels;
        private readonly double[] exerciseTimes;
        private readonly FdmMesher mesher;

        public FdmHestonExpressCondition(Real[] redemptions, Real[] triggerLevels, double[] exerciseTimes, FdmMesher mesher)
        {
            this.redemptions = redemptions;
            this.triggerLevels = triggerLevels;
            this.exerciseTimes = exerciseTimes;
            this.mesher = mesher;
        }

        public override void ApplyTo(Span<double> a, double t)
        {
            int index = Array.IndexOf(exerciseTimes, t);

            if (index >= 0)
            {
                FdmLinearOpIterator iter = mesher.Layout.Begin();
                for (Size k = 0; k < mesher.Layout.Size; ++k, iter.Increment())
                {
                    Real s = Math.Exp(mesher.Location(iter, 0));

                    if (s > triggerLevels[index])
                    {
                        a[iter.Index] = redemptions[index];
                    }
                }
            }
        }
    }

    // The test suite's ExpressPayoff.
    private sealed class ExpressPayoff : Payoff
    {
        public override string Name => "ExpressPayoff";

        public override string Description => "ExpressPayoff";

        public override double Evaluate(double price) =>
            ((price >= 100.0) ? 108.0 : 100.0) - ((price <= 75.0) ? 100.0 - price : 0.0);
    }

    // fdmlinearop.cpp's createHestonHullWhite (lines 133-167), at the evaluation date it sets.
    private static HybridHestonHullWhiteProcess CreateHestonHullWhite(Date today, double maturity)
    {
        DayCounter dc = new Actual365Fixed();
        const Real s0 = 100.0;

        List<Date> dates = [];
        List<Rate> rates = [], divRates = [];

        for (Size i = 0; i <= 25; ++i)
        {
            dates.Add(today + new Period(i, TimeUnit.Years));
            rates.Add(0.05);
            divRates.Add(0.02);
        }

        YieldTermStructure rTS = new ZeroCurve(dates, rates, dc);
        YieldTermStructure qTS = new ZeroCurve(dates, divRates, dc);

        const Real v0 = 0.04;
        var hestonProcess = new HestonProcess(rTS, qTS, s0, v0, 1.0, v0 * 0.75, 0.4, -0.7);

        HullWhiteForwardProcess hwFwdProcess = new HullWhiteForwardProcess(rTS, 0.00883, 0.01)
            .WithForwardMeasureTime(maturity);

        const Real equityShortRateCorr = -0.7;

        return new HybridHestonHullWhiteProcess(hestonProcess, hwFwdProcess, equityShortRateCorr);
    }

    // fdmlinearop.cpp's createSolverDesc (lines 169-213).
    private static FdmSolverDesc CreateSolverDesc(Size[] dim, HybridHestonHullWhiteProcess process)
    {
        double maturity = process.HullWhiteProcess.ForwardMeasureTime;

        Fdm1dMesher[] mesher1d =
        [
            new Uniform1dMesher(Math.Log(22.0), Math.Log(440.0), dim[0]),
            new FdmHestonVarianceMesher(dim[1], process.HestonProcess, maturity),
            new Uniform1dMesher(-0.15, 0.15, dim[2]),
        ];

        FdmMesher mesher = new FdmMesherComposite(mesher1d);

        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        var conditions = new FdmStepConditionComposite([], []);

        StrikedTypePayoff payoff = new PlainVanillaPayoff(OptionType.Call, 160.0);

        FdmInnerValueCalculator calculator = new FdmLogInnerValue(payoff, mesher, 0);

        const Size tGrid = 100;
        const Size dampingSteps = 0;

        return new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, dampingSteps);
    }
}
