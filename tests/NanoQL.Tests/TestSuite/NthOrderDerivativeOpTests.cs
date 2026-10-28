// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/nthorderderivativeop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Time;
using NanoQL.TermStructures;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class NthOrderDerivativeOpTests
{
    // QL_EPSILON (qldefines.hpp) and M_LOG2E (mathconstants.hpp), which nanoql keeps internal.
    private const double QL_EPSILON = 2.220446049250313e-16;
    private const double M_LOG2E = 1.44269504088896340736;

    // BOOST_CHECK records a failure and goes on; the case fails at its end.
    private readonly List<string> errors = [];

    [Fact]
    public void TestSparseMatrixApply()
    {
        // Testing sparse matrix apply...

        var sm = new SparseMatrix(5, 7);

        Assert.True(sm.Size1 == 5, "check sm.size1() == SparseMatrix::size_type(5) has failed");
        Assert.True(sm.Size2 == 7, "check sm.size2() == SparseMatrix::size_type(7) has failed");

        sm[1, 3] = 3.0;

        var x = new Vector(7, 0.0, 1.0);
        Vector y = SparseMatrixFunctions.Prod(sm, x);

        Assert.True(ComparisonFunctions.CloseEnough(y[0], 0.0), "check close_enough(y[0], 0.0) has failed");
        Assert.True(ComparisonFunctions.CloseEnough(y[1], 3.0 * 3.0), "check close_enough(y[1], 3.0*3.0) has failed");
        Assert.True(ComparisonFunctions.CloseEnough(y[2], 0.0), "check close_enough(y[2], 0.0) has failed");
        Assert.True(ComparisonFunctions.CloseEnough(y[3], 0.0), "check close_enough(y[3], 0.0) has failed");
        Assert.True(ComparisonFunctions.CloseEnough(y[4], 0.0), "check close_enough(y[4], 0.0) has failed");
    }

    [Fact]
    public void TestFirstOrder2PointsApply()
    {
        // Testing two points first order derivative operator apply on an uniform grid...

        const Real dx = 1 / 5.0;

        var op = new NthOrderDerivativeOp(0, 1, 3, new FdmMesherComposite(new Uniform1dMesher(0.0, 1.0, 6)));

        var x = new Vector(6, 0.0, 1.0);
        Vector y = op.Apply(x);

        for (Size i = 0; i < x.Size; ++i)
        {
            Check(ComparisonFunctions.CloseEnough(y[i], 1 / dx), $"close_enough(y[{i}], 1/dx)");
        }

        AssertNoErrors();
    }

    [Fact]
    public void TestFirstOrder3PointsOnUniformGrid()
    {
        // Testing three points first order derivative operator on an uniform grid...

        const Real ddx = 1.0 / 0.2;

        SparseMatrix m =
            new NthOrderDerivativeOp(0, 1, 3, new FdmMesherComposite(new Uniform1dMesher(0.0, 1.0, 6))).ToMatrix();

        // to reproduce the reference results use
        // http://web.media.mit.edu/~crtaylor/calculator.html

        CheckClose(m, 2, 0, 0.0);
        CheckClose(m, 2, 1, -0.5 * ddx);
        Check(m[2, 2] < 42 * QL_EPSILON, "m(2,2) < 42*QL_EPSILON");
        CheckClose(m, 2, 3, 0.5 * ddx);
        CheckClose(m, 2, 4, 0.0);
        CheckClose(m, 2, 5, 0.0);

        CheckClose(m, 0, 0, -3.0 / 2.0 * ddx);
        CheckClose(m, 0, 1, 2.0 * ddx);
        CheckClose(m, 0, 2, -0.5 * ddx);
        CheckClose(m, 0, 3, 0.0);
        CheckClose(m, 0, 4, 0.0);
        CheckClose(m, 0, 5, 0.0);

        CheckClose(m, 5, 0, 0.0);
        CheckClose(m, 5, 1, 0.0);
        CheckClose(m, 5, 2, 0.0);
        CheckClose(m, 5, 3, 0.5 * ddx);
        CheckClose(m, 5, 4, -2.0 * ddx);
        CheckClose(m, 5, 5, 3.0 / 2.0 * ddx);

        AssertNoErrors();
    }

    [Fact]
    public void TestFirstOrder5PointsOnUniformGrid()
    {
        // Testing five points first order derivative operator on an uniform grid...

        const Real ddx = 1.0 / 0.4;

        SparseMatrix m =
            new NthOrderDerivativeOp(0, 1, 5, new FdmMesherComposite(new Uniform1dMesher(0.0, 2.0, 6))).ToMatrix();

        CheckClose(m, 2, 0, 1.0 / 12.0 * ddx);
        CheckClose(m, 2, 1, -2.0 / 3.0 * ddx);
        Check(m[2, 2] < 42 * QL_EPSILON, "m(2,2) < 42*QL_EPSILON");
        CheckClose(m, 2, 3, 2.0 / 3.0 * ddx);
        CheckClose(m, 2, 4, -1.0 / 12.0 * ddx);
        CheckClose(m, 2, 5, 0.0);

        CheckClose(m, 0, 0, -25.0 / 12.0 * ddx);
        CheckClose(m, 0, 1, 4.0 * ddx);
        CheckClose(m, 0, 2, -3.0 * ddx);
        CheckClose(m, 0, 3, 4.0 / 3.0 * ddx);
        CheckClose(m, 0, 4, -0.25 * ddx);
        CheckClose(m, 0, 5, 0.0);

        CheckClose(m, 1, 0, -0.25 * ddx);
        CheckClose(m, 1, 1, -5.0 / 6.0 * ddx);
        CheckClose(m, 1, 2, 3.0 / 2.0 * ddx);
        CheckClose(m, 1, 3, -0.5 * ddx);
        CheckClose(m, 1, 4, 1.0 / 12.0 * ddx);
        CheckClose(m, 1, 5, 0.0);

        CheckClose(m, 4, 5, 0.25 * ddx);
        CheckClose(m, 4, 4, 5.0 / 6.0 * ddx);
        CheckClose(m, 4, 3, -3.0 / 2.0 * ddx);
        CheckClose(m, 4, 2, 0.5 * ddx);
        CheckClose(m, 4, 1, -1.0 / 12.0 * ddx);
        CheckClose(m, 4, 0, 0.0);

        CheckClose(m, 5, 0, 0.0);
        CheckClose(m, 5, 1, 0.25 * ddx);
        CheckClose(m, 5, 2, -4.0 / 3.0 * ddx);
        CheckClose(m, 5, 3, 3.0 * ddx);
        CheckClose(m, 5, 4, -4.0 * ddx);
        CheckClose(m, 5, 5, 25.0 / 12.0 * ddx);

        AssertNoErrors();
    }

    [Fact]
    public void TestFirstOrder2PointsOnUniformGrid()
    {
        // Testing two points first order derivative operator on an uniform grid...

        const Real ddx = 1.0 / 0.2;

        SparseMatrix m =
            new NthOrderDerivativeOp(0, 1, 2, new FdmMesherComposite(new Uniform1dMesher(0.0, 0.6, 4))).ToMatrix();

        CheckClose(m, 0, 0, -ddx);
        CheckClose(m, 0, 1, ddx);
        CheckClose(m, 0, 2, 0.0);
        CheckClose(m, 0, 3, 0.0);

        CheckClose(m, 1, 0, -ddx);
        CheckClose(m, 1, 1, ddx);
        CheckClose(m, 1, 2, 0.0);
        CheckClose(m, 1, 3, 0.0);

        CheckClose(m, 2, 0, 0.0);
        CheckClose(m, 2, 1, -ddx);
        CheckClose(m, 2, 2, ddx);
        CheckClose(m, 2, 3, 0.0);

        CheckClose(m, 3, 0, 0.0);
        CheckClose(m, 3, 1, 0.0);
        CheckClose(m, 3, 2, -ddx);
        CheckClose(m, 3, 3, ddx);

        AssertNoErrors();
    }

    [Fact]
    public void TestFirstOrder4PointsOnUniformGrid()
    {
        // Testing four points first order derivative operator on an uniform grid...

        const Real ddx = 1.0 / 0.2;

        SparseMatrix m =
            new NthOrderDerivativeOp(0, 1, 4, new FdmMesherComposite(new Uniform1dMesher(0.0, 0.6, 4))).ToMatrix();

        CheckClose(m, 0, 0, -11.0 / 6.0 * ddx);
        CheckClose(m, 0, 1, 3.0 * ddx);
        CheckClose(m, 0, 2, -1.5 * ddx);
        CheckClose(m, 2, 3, 1.0 / 3.0 * ddx);

        CheckClose(m, 1, 0, -1.0 / 3.0 * ddx);
        CheckClose(m, 1, 1, -0.5 * ddx);
        CheckClose(m, 1, 2, ddx);
        CheckClose(m, 1, 3, -1.0 / 6.0 * ddx);

        CheckClose(m, 2, 0, 1.0 / 6.0 * ddx);
        CheckClose(m, 2, 1, -ddx);
        CheckClose(m, 2, 2, 0.5 * ddx);
        CheckClose(m, 2, 3, 1.0 / 3.0 * ddx);

        CheckClose(m, 3, 0, -1.0 / 3.0 * ddx);
        CheckClose(m, 3, 1, 1.5 * ddx);
        CheckClose(m, 3, 2, -3.0 * ddx);
        CheckClose(m, 3, 3, 11.0 / 6.0 * ddx);

        AssertNoErrors();
    }

    [Fact]
    public void TestFirstOrder2PointsOn2DimUniformGrid()
    {
        // Testing two points first order derivative operator on a 2 dimensional uniform grid...

        const Real ddx = 1.0 / 0.2;

        const Size xGrid = 4;
        var mesher = new FdmMesherComposite(new Uniform1dMesher(0.0, 1, xGrid), new Uniform1dMesher(0.0, 0.4, 3));

        SparseMatrix m = new NthOrderDerivativeOp(1, 1, 2, mesher).ToMatrix();

        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (Size k = 0; k < mesher.Layout.Size; ++k, iter.Increment())
        {
            Size i = iter.Index;
            Size ix = iter.Coordinates[1];

            switch (ix)
            {
                case 0:
                    CheckClose(m, i, i, -ddx);
                    CheckClose(m, i, i + xGrid, ddx);
                    CheckClose(m, i, i + (2 * xGrid), 0.0);
                    break;
                case 1:
                    CheckClose(m, i, i - xGrid, -ddx);
                    CheckClose(m, i, i, ddx);
                    CheckClose(m, i, i + (2 * xGrid), 0.0);
                    break;
                case 2:
                    CheckClose(m, i, i - (2 * xGrid), 0.0);
                    CheckClose(m, i, i - xGrid, -ddx);
                    CheckClose(m, i, i, ddx);
                    break;
                default:
                    throw new NanoQLException("inconsistent coordinate");
            }
        }

        AssertNoErrors();
    }

    [Fact]
    public void TestSecondOrder3PointsNonUniformGrid()
    {
        // Testing three points second order derivative operator on a non-uniform grid...

        Real[] xValues = [0.5, 1.0, 2.0, 4.0];

        SparseMatrix m =
            new NthOrderDerivativeOp(0, 2, 3, new FdmMesherComposite(new Predefined1dMesher(xValues))).ToMatrix();

        CheckClose(m, 0, 0, 8.0 / 3.0);
        CheckClose(m, 0, 1, -4.0);
        CheckClose(m, 0, 2, 4.0 / 3.0);
        CheckClose(m, 0, 3, 0.0);

        CheckClose(m, 1, 0, 8.0 / 3.0);
        CheckClose(m, 1, 1, -4.0);
        CheckClose(m, 1, 2, 4.0 / 3.0);
        CheckClose(m, 1, 3, 0.0);

        CheckClose(m, 2, 0, 0.0);
        CheckClose(m, 2, 1, 2.0 / 3.0);
        CheckClose(m, 2, 2, -1.0);
        CheckClose(m, 2, 3, 1.0 / 3.0);

        CheckClose(m, 3, 0, 0.0);
        CheckClose(m, 3, 1, 2.0 / 3.0);
        CheckClose(m, 3, 2, -1.0);
        CheckClose(m, 3, 3, 1.0 / 3.0);

        AssertNoErrors();
    }

    [Fact]
    public void TestSecondOrder4PointsNonUniformGrid()
    {
        // Testing four points second order derivative operator on a non-uniform grid...

        Real[] xValues = [0.5, 1.0, 2.0, 4.0, 8.0];

        SparseMatrix m =
            new NthOrderDerivativeOp(0, 2, 4, new FdmMesherComposite(new Predefined1dMesher(xValues))).ToMatrix();

        CheckClose(m, 0, 0, 88.0 / 21.0);
        CheckClose(m, 0, 1, -140.0 / 21.0);
        CheckClose(m, 0, 2, 56.0 / 21.0);
        CheckClose(m, 0, 3, -4.0 / 21.0);
        CheckClose(m, 0, 4, 0.0);

        CheckClose(m, 1, 0, 64.0 / 21.0);
        CheckClose(m, 1, 1, -98.0 / 21.0);
        CheckClose(m, 1, 2, 35.0 / 21.0);
        CheckClose(m, 1, 3, -1.0 / 21.0);
        CheckClose(m, 1, 4, 0.0);

        CheckClose(m, 2, 0, 16.0 / 21.0);
        CheckClose(m, 2, 1, -2.0 / 3.0);
        CheckClose(m, 2, 2, -1.0 / 3.0);
        CheckClose(m, 2, 3, 5.0 / 21.0);
        CheckClose(m, 2, 4, 0.0);

        CheckClose(m, 3, 0, 0.0);
        CheckClose(m, 3, 1, 4.0 / 21.0);
        CheckClose(m, 3, 2, -1.0 / 6.0);
        CheckClose(m, 3, 3, -1.0 / 12.0);
        CheckClose(m, 3, 4, 5.0 / 84.0);

        CheckClose(m, 4, 0, 0.0);
        CheckClose(m, 4, 1, -20.0 / 21.0);
        CheckClose(m, 4, 2, 11.0 / 6.0);
        CheckClose(m, 4, 3, -13.0 / 12.0);
        CheckClose(m, 4, 4, 17.0 / 84.0);

        AssertNoErrors();
    }

    [Fact]
    public void TestThirdOrder4PointsUniformGrid()
    {
        // Testing four points third order derivative operator on a uniform grid...

        SparseMatrix m =
            new NthOrderDerivativeOp(0, 3, 4, new FdmMesherComposite(new Uniform1dMesher(0.0, 0.6, 4))).ToMatrix();

        for (Size i = 0; i < 4; ++i)
        {
            CheckClose(m, i, 0, -125.0);
            CheckClose(m, i, 1, 375.0);
            CheckClose(m, i, 2, -375.0);
            CheckClose(m, i, 3, 125);
        }

        AssertNoErrors();
    }

    // Adapted: Problem and LevenbergMarquardt's minimize return the results, which the case reads (playbook §2.2).
    [Fact]
    public void TestHigherOrderHestonOptionPricing()
    {
        // Testing Heston model option pricing convergence with higher order finite difference operators...

        Real[] strikes = [50, 75, 90, 100, 110, 125, 150, 200];

        var initSetup = new GridSetup(3.87773, 0.043847, true, false, 5, 21, 20, 11, FdmSchemeDesc.CrankNicolson());

        Real[] initialValues = [initSetup.Alpha, initSetup.Density];

        var costFct = new FdmMispricingCostFunction(initSetup, strikes);
        var noConstraint = new NoConstraint();

        var prob = new Problem(costFct, noConstraint, initialValues);

        ProblemResults result = new LevenbergMarquardt().Minimize(prob, new EndCriteria(400, 40, 1.0e-4, 1.0e-4, 1.0e-4));

        GridSetup optimalSetup = initSetup with
        {
            Alpha = result.CurrentValue[0],
            Density = result.CurrentValue[1],
            YGrid = initSetup.YGrid / 2,
        };

        Vector q = PriceReport(optimalSetup, strikes);
        Real ac = Math.Sqrt(ArrayFunctions.DotProduct(q, q) / q.Size);

        Vector p = PriceReport(initSetup, strikes);
        Real ap = Math.Sqrt(ArrayFunctions.DotProduct(p, p) / p.Size);

        Real convergence = Math.Log(ac / ap) * M_LOG2E;

        Assert.False(
            convergence < 3.6,
            "convergence order is too low"
            + "\n expected convergence: 4.0"
            + $"\n measured convergence: {convergence}"
            + "\n tolerance           : 0.4");
    }

    [Fact]
    public void TestHigherOrderAndRichardsonExtrapolation()
    {
        // Testing Heston option pricing convergence with higher order FDM operators and Richardson Extrapolation...

        Real n1 = PriceQuality(1.0 / 25);
        Real n3 = Math.Abs(new RichardsonExtrapolation(PriceQuality, 1.0 / 25, 4.0).Evaluate(2.0));

        Real r2 = Math.Log(n1 / n3) * M_LOG2E;

        Assert.False(
            r2 < 4.9,
            "convergence order is too low using Richardson extrapolation"
            + "\n expected convergence: 5.0"
            + $"\n measured convergence: {r2}"
            + "\n tolerance           : 0.1");
    }

    [Fact]
    public void TestCompareFirstDerivativeOpNonUniformGrid()
    {
        // Testing with FirstDerivativeOp on a non-uniform grid...

        Vector xValues = ArrayFunctions.Exp(new Vector(7, 0, 0.1));

        var m = new Predefined1dMesher(xValues.AsSpan().ToArray());

        var m1d = new FdmMesherComposite(m);

        var fx = new FirstDerivativeOp(0, m1d);

        var dx = new NthOrderDerivativeOp(0, 1, 3, m1d);

        SparseMatrix fm = fx.ToMatrix();
        SparseMatrix dm = dx.ToMatrix();

        // different boundary conditions
        for (Size i = 1; i < m.Size - 1; ++i)
        {
            for (Size j = 0; j < m.Size; ++j)
            {
                Check(Math.Abs(fm[i, j] - dm[i, j]) < 1e-12, $"std::fabs(fm({i}, {j})- dm({i}, {j})) < 1e-12");
            }
        }

        AssertNoErrors();
    }

    [Fact]
    public void TestCompareFirstDerivativeOp2dUniformGrid()
    {
        // Testing with FirstDerivativeOp on a 2d uniform grid...

        var m1 = new Uniform1dMesher(0.0, 0.6, 5);
        var m2 = new Uniform1dMesher(0.0, 1.6, 6);

        var mc = new FdmMesherComposite(m1, m2);

        Size n = mc.Layout.Dim[0];
        Size m = mc.Layout.Dim[1];

        SparseMatrix fm = new FirstDerivativeOp(0, mc).ToMatrix();
        SparseMatrix dm = new NthOrderDerivativeOp(0, 1, 3, mc).ToMatrix();

        for (Size k = 0; k < m; ++k)
        {
            Size idx = k * n;
            for (Size i = 1; i < n - 1; ++i)
            {
                for (Size j = 0; j < n * m; ++j)
                {
                    Check(
                        Math.Abs(fm[idx + i, j] - dm[idx + i, j]) < 1e-12,
                        $"std::fabs(fm({idx + i}, {j}) - dm({idx + i}, {j})) < 1e-12");
                }
            }
        }

        fm = new FirstDerivativeOp(1, mc).ToMatrix();
        dm = new NthOrderDerivativeOp(1, 1, 3, mc).ToMatrix();

        for (Size i = n; i < n * (m - 1); ++i)
        {
            for (Size j = 0; j < n * m; ++j)
            {
                Check(Math.Abs(fm[i, j] - dm[i, j]) < 1e-12, $"std::fabs(fm({i}, {j}) - dm({i}, {j})) < 1e-12");
            }
        }

        AssertNoErrors();
    }

    [Fact]
    public void TestMixedSecondOrder9PointsOnUniformGrid()
    {
        // Testing nine points mixed second order derivative operator on a uniform grid...

        var m = new Uniform1dMesher(0.0, 0.6, 5);

        var mc = new FdmMesherComposite(m, m);

        SparseMatrix cc = SparseProduct(
            new NthOrderDerivativeOp(0, 1, 3, mc).ToMatrix(), new NthOrderDerivativeOp(1, 1, 3, mc).ToMatrix());

        SparseMatrix mm = new SecondOrderMixedDerivativeOp(0, 1, mc).ToMatrix();

        Size n = m.Size;

        for (Size i = 1; i < n - 1; ++i)
        {
            for (Size j = 1; j < n - 1; ++j)
            {
                Size idx = (i * n) + j;
                for (Size k = 1; k < n - 1; ++k)
                {
                    for (Size l = 1; l < n - 1; ++l)
                    {
                        Size kdx = (k * n) + l;
                        Check(
                            Math.Abs(mm[idx, kdx] - cc[idx, kdx]) < 1e-12,
                            $"std::fabs(mm({idx},{kdx}) - cc({idx},{kdx})) < 1e-12");
                    }
                }
            }
        }

        AssertNoErrors();
    }

    // The products of sparse matrices QuantLib takes from uBLAS (prod, sparse_prod): row by row, each row's sum over
    // the first factor's stored elements, the elements a sum leaves at 0 not stored.
    private static SparseMatrix SparseProduct(SparseMatrix a, SparseMatrix b)
    {
        var result = new SparseMatrix(a.Size1, b.Size2);
        ReadOnlySpan<int> aIndex1 = a.Index1Data, aIndex2 = a.Index2Data;
        ReadOnlySpan<double> aValues = a.ValueData;
        ReadOnlySpan<int> bIndex1 = b.Index1Data, bIndex2 = b.Index2Data;
        ReadOnlySpan<double> bValues = b.ValueData;
        var row = new SortedDictionary<int, double>();
        for (int i = 0; i < a.Filled1 - 1; ++i)
        {
            row.Clear();
            for (int p = aIndex1[i]; p < aIndex1[i + 1]; ++p)
            {
                int k = aIndex2[p];
                if (k + 1 >= b.Filled1)
                {
                    continue;
                }

                for (int q = bIndex1[k]; q < bIndex1[k + 1]; ++q)
                {
                    row[bIndex2[q]] = row.GetValueOrDefault(bIndex2[q]) + (aValues[p] * bValues[q]);
                }
            }

            foreach ((int j, double value) in row)
            {
                if (value != 0.0)
                {
                    result[i, j] = value;
                }
            }
        }

        return result;
    }

    private static SparseMatrix Scaled(double s, SparseMatrix m)
    {
        var result = new SparseMatrix(m.Size1, m.Size2);
        ReadOnlySpan<int> index1 = m.Index1Data, index2 = m.Index2Data;
        ReadOnlySpan<double> values = m.ValueData;
        for (int i = 0; i < m.Filled1 - 1; ++i)
        {
            for (int p = index1[i]; p < index1[i + 1]; ++p)
            {
                result[i, index2[p]] = s * values[p];
            }
        }

        return result;
    }

    private static SparseMatrix Diagonal(Vector d)
    {
        var result = new SparseMatrix(d.Size, d.Size);
        for (int i = 0; i < d.Size; ++i)
        {
            result[i, i] = d[i];
        }

        return result;
    }

    private static Vector PriceReport(GridSetup setup, Real[] strikes)
    {
        var today = new Date(2, Month.May, 2018);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        Date maturity = today + new Period(1, TimeUnit.Years);
        Real T = dc.YearFraction(today, maturity);

        YieldTermStructure rTS = new FlatForward(today, 0.05, dc);
        YieldTermStructure qTS = new FlatForward(today, 0.0, dc);

        const Real S = 100.0;
        const Real vol = 0.2;
        const Real v0 = vol * vol;
        const Real kappa = 1.0;
        const Real theta = vol * vol;
        const Real sig = 0.2;
        const Real rho = -0.75;

        var hestonProcess = new HestonProcess(rTS, qTS, S, v0, kappa, theta, sig, rho);

        Real stdDev = vol * Math.Sqrt(T);
        Real df = qTS.Discount(maturity) / rTS.Discount(maturity);

        Real y = Math.Log(S);

        Real ymin = y - (setup.Alpha * stdDev);
        Real ymax = y + (setup.Alpha * stdDev);

        Size yGrid = setup.YGrid;
        Size vGrid = setup.VGrid;

        var diffs = new Vector(strikes.Length);
        for (Size k = 0; k < strikes.Length; ++k)
        {
            Real strike = strikes[k];
            Real specialPoint = Math.Log(strike / df) + (0.5 * vol * vol * T);

            var mesher1d = new Concentrating1dMesher(ymin, ymax, yGrid, (specialPoint, setup.Density));

            double[] loc = [.. mesher1d.Locations()];
            for (Size i = 0; setup.MidPoint && i < loc.Length - 1; ++i)
            {
                if (loc[i] < specialPoint && loc[i + 1] >= specialPoint)
                {
                    Real d = loc[i + 1] - loc[i];

                    Real offset = specialPoint - (0.5 * d) - loc[i];

                    for (int l = 0; l < loc.Length; ++l)
                    {
                        loc[l] += offset;
                    }

                    break;
                }
            }

            var mesher = new FdmMesherComposite(
                new Predefined1dMesher(loc), new FdmHestonVarianceMesher(vGrid, hestonProcess, 1.0));

            Vector g = mesher.Locations(0);
            Vector sT = ArrayFunctions.Exp(g - (0.5 * vol * vol * T)) * df;

            var rhs = new Vector(mesher.Layout.Size);

            var payoff = new PlainVanillaPayoff(OptionType.Put, strike);

            FdmLinearOpIterator iter = mesher.Layout.Begin();
            for (Size n = 0; n < mesher.Layout.Size; ++n, iter.Increment())
            {
                Size idx = iter.Index;
                Size idxm1 = mesher.Layout.Neighbourhood(iter, 0, -1);
                Size idxp1 = mesher.Layout.Neighbourhood(iter, 0, 1);

                Size nx = iter.Coordinates[0];

                if (nx != 0 && nx != yGrid - 1
                    && setup.CellAvg
                    && ((sT[idx] < strike && sT[idxp1] >= strike) || (sT[idxm1] < strike && sT[idx] >= strike)))
                {
                    Real gMin = 0.5 * (g[idxm1] + g[idx]);
                    Real gMax = 0.5 * (g[idxp1] + g[idx]);

                    Real F(Real x) => payoff.Evaluate(Math.Exp(x - (0.5 * vol * vol * T)) * df);

                    rhs[idx] = new GaussLobattoIntegral(1000, 1e-12).Integrate(F, gMin, gMax) / (gMax - gMin);
                }
                else
                {
                    rhs[idx] = payoff.Evaluate(sT[idx]);
                }
            }

            var heatEqn = new FdmHestonNthOrderOp(setup.NPoints, hestonProcess, mesher);

            var solver = new FdmBackwardSolver(heatEqn, [], null, setup.Scheme);

            solver.Rollback(rhs.AsSpan(), T, 0.0, setup.TGrid, 1);

            rhs *= rTS.Discount(maturity);

            ImmutableArray<double> x = mesher.GetFdm1dMeshers()[0].Locations();
            ImmutableArray<double> v = mesher.GetFdm1dMeshers()[1].Locations();

            var resultValues = new Matrix(mesher.Layout.Dim[1], mesher.Layout.Dim[0], rhs.AsSpan());

            var interpolation = new BicubicSpline(x.AsSpan(), v.AsSpan(), resultValues);

            Real fdmPrice = interpolation.Evaluate(y, hestonProcess.V0);

            var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, strike), new EuropeanExercise(maturity));
            Real npv = AnalyticHestonEngine.Calculate(option, context, new HestonModel(hestonProcess), 192).NPV!.Value;

            diffs[k] = npv - fdmPrice;
        }

        return diffs;
    }

    private static Real PriceQuality(Real h)
    {
        Real[] strikes = [100];

        var yGrid = (Size)(1 / h);
        var setup = new GridSetup(5.50966, 0.0130581, true, false, 5, 401, yGrid, 21, FdmSchemeDesc.CrankNicolson());

        return Math.Abs(PriceReport(setup, strikes)[0]);
    }

    private void Check(bool condition, string text)
    {
        if (!condition)
        {
            errors.Add($"check {text} has failed");
        }
    }

    private void CheckClose(SparseMatrix m, Size i, Size j, Real expected) =>
        Check(ComparisonFunctions.CloseEnough(m[i, j], expected), $"close_enough(m({i},{j}), {expected})");

    private void AssertNoErrors() => Assert.True(errors.Count == 0, string.Join("\n", errors));

    private sealed record GridSetup(
        Real Alpha,
        Real Density,
        bool CellAvg,
        bool MidPoint,
        Size NPoints,
        Size TGrid,
        Size YGrid,
        Size VGrid,
        FdmSchemeDesc Scheme);

    // The test suite's FdmHestonNthOrderOp: the Heston operator from the n-th order derivatives, not splittable.
    private sealed class FdmHestonNthOrderOp : FdmLinearOpComposite
    {
        private readonly SparseMatrix map;
        private readonly TripleBandLinearOp preconditioner;

        public FdmHestonNthOrderOp(Size nPoints, HestonProcess hestonProcess, FdmMesher mesher, Size direction = 0)
        {
            Real vol2 = 0.5 * hestonProcess.Theta;
            preconditioner =
                new SecondDerivativeOp(direction, mesher).Mult(new Vector(mesher.Layout.Size, vol2));

            Vector vv = mesher.Locations(1);
            Vector varianceValues = 0.5 * vv;

            FdmLinearOpLayout layout = mesher.Layout;
            FdmLinearOpIterator iter = layout.Begin();
            for (Size k = 0; k < layout.Size; ++k, iter.Increment())
            {
                if (iter.Coordinates[0] == 0 || iter.Coordinates[0] == layout.Dim[0] - 1)
                {
                    varianceValues[iter.Index] = 0.0;
                }
            }

            Size n = mesher.Layout.Size;

            var rV = new Vector(n);
            for (Size i = 0; i < n; ++i)
            {
                rV[i] = varianceValues[i] - (0.5 * hestonProcess.Theta);
            }

            SparseMatrix v = Diagonal(varianceValues), u = Diagonal(vv);

            SparseMatrix dx = new NthOrderDerivativeOp(0, 1, nPoints, mesher).ToMatrix();
            SparseMatrix dxx = new NthOrderDerivativeOp(0, 2, nPoints, mesher).ToMatrix();
            SparseMatrix dv = new NthOrderDerivativeOp(1, 1, nPoints, mesher).ToMatrix();
            SparseMatrix dvv = new NthOrderDerivativeOp(1, 2, nPoints, mesher).ToMatrix();

            Real kappa = hestonProcess.Kappa;
            Real theta = hestonProcess.Theta;
            Real sigma = hestonProcess.Sigma;
            Real rho = hestonProcess.Rho;

            var thetaMinusU = new Vector(n);
            for (Size i = 0; i < n; ++i)
            {
                thetaMinusU[i] = theta - vv[i];
            }

            map = SparseProduct(Diagonal(-1.0 * rV), dx)
                + SparseProduct(v, dxx)
                + Scaled(0.5 * rho * sigma, SparseProduct(u, SparseProduct(dx, dv) + SparseProduct(dv, dx)))
                + Scaled(0.5 * sigma * sigma, SparseProduct(u, dvv))
                + Scaled(kappa, SparseProduct(Diagonal(thetaMinusU), dv));
        }

        public override int Size => 2;

        public override SparseMatrix ToMatrix() => map.Clone();

        public override void SetTime(double t1, double t2)
        {
        }

        public override void Apply(ReadOnlySpan<double> r, Span<double> result) =>
            SparseMatrixFunctions.Prod(map, new Vector(r)).AsSpan().CopyTo(result);

        public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result) =>
            throw new NanoQLException("operator splitting is not supported");

        public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result) =>
            throw new NanoQLException("operator splitting is not supported");

        public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result) =>
            throw new NanoQLException("operator splitting is not supported");

        public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) =>
            preconditioner.SolveSplitting(r, s, 1.0, result);
    }

    // The test suite's FdmMispricingCostFunction: the grid's mispricings at the strikes, 1000 twice where pricing fails.
    private sealed class FdmMispricingCostFunction(GridSetup setup, Real[] strikes) : CostFunction
    {
        public override Vector Values(Vector x)
        {
            GridSetup g = setup with { Alpha = x[0], Density = x[1] };

            try
            {
                return PriceReport(g, strikes);
            }
            catch (Exception e) when (e is NanoQLException or ArgumentException)
            {
                return new Vector(2, 1000);
            }
        }
    }
}
