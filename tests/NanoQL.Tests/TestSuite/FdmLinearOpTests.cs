// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009, 2010, 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/fdmlinearop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testUniformGridMesher, testFirstDerivativesMapApply, testSecondDerivativesMapApply, testDerivativeWeightsOnNonUniformGrids, testSecondOrderMixedDerivativesMapApply, testTripleBandMapSolve, testFdmHestonBarrier, testFdmHestonAmerican, testFdmHestonExpress, testFdmHestonHullWhiteOp, testBiCGstab, testGMRES (B6.2); testFdmMesherIntegral (B4.2)

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
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
}
