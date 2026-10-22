// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2012 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2012 Mateusz Kapturski
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/optimizers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testHybridSimulatedAnnealingTemperatureTermination, testHybridSimulatedAnnealingIterationTermination, testHybridSimulatedAnnealingStationaryTermination (S-xrt's hybrid simulated annealing); testLBFGSB, testLBFGSBActiveBounds, testLBFGSBCoverage, testDifferentialEvolution, testCMAES (S-core's optimizers)

using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class OptimizersTests
{
    // QuantLib's file-level vectors, which setup() fills; xUnit builds the class anew for each case.
    private readonly List<CostFunction> costFunctions_ = [];
    private readonly List<Constraint> constraints_ = [];
    private readonly List<Vector> initialValues_ = [];
    private readonly List<Size> maxIterations_ = [], maxStationaryStateIterations_ = [];
    private readonly List<Real> rootEpsilons_ = [], functionEpsilons_ = [], gradientNormEpsilons_ = [];
    private readonly List<EndCriteria> endCriterias_ = [];
    private readonly List<List<NamedOptimizationMethod>> optimizationMethods_ = [];
    private readonly List<Vector> xMinExpected_ = [], yMinExpected_ = [];

    // BOOST_ERROR records a failure and goes on; the case fails at its end.
    private readonly List<string> errors = [];

    private enum OptimizationMethodType
    {
        simplex,
        levenbergMarquardt,
        levenbergMarquardt2,
        conjugateGradient,
        conjugateGradient_goldstein,
        steepestDescent,
        steepestDescent_goldstein,
        bfgs,
        bfgs_goldstein,
        lbfgsb,
    }

    // Adapted: setup's methods (lines 314-317) are simplex, levenbergMarquardt, levenbergMarquardt2 and
    // conjugateGradient, the methods ported, without bfgs and lbfgsb (S-core's methods, not ported yet), whose
    // makeOptimizationMethod cases (lines 240-251) are dropped too. The problem (lines 342-343) is a ProblemState, the
    // working type with QuantLib's Problem API, so the loop calls setCurrentValue, minimize and values on it as
    // QuantLib's does.
    [Fact]
    public void Test()
    {
        // Testing optimizers...

        Setup();

        // Loop over problems (currently there is only 1 problem)
        for (Size i = 0; i < costFunctions_.Count; ++i)
        {
            var problem = new ProblemState(new Problem(costFunctions_[i], constraints_[i], initialValues_[i].AsSpan()));
            Vector initialValues = problem.CurrentValue.Clone();
            // Loop over optimizers
            for (Size j = 0; j < optimizationMethods_[i].Count; ++j)
            {
                Real rootEpsilon = endCriterias_[i].RootEpsilon;
                Size endCriteriaTests = 1;
                // Loop over rootEpsilon
                for (Size k = 0; k < endCriteriaTests; ++k)
                {
                    problem.CurrentValue = initialValues;
                    var endCriteria = new EndCriteria(
                        endCriterias_[i].MaxIterations,
                        endCriterias_[i].MaxStationaryStateIterations,
                        rootEpsilon,
                        endCriterias_[i].FunctionEpsilon,
                        endCriterias_[i].GradientNormEpsilon);
                    rootEpsilon *= .1;
                    EndCriteriaType endCriteriaResult =
                        optimizationMethods_[i][j].OptimizationMethod.Minimize(problem, endCriteria);
                    Vector xMinCalculated = problem.CurrentValue.Clone();
                    Vector yMinCalculated = problem.Values(xMinCalculated);

                    // Check optimization results vs known solution
                    bool completed;
                    switch (endCriteriaResult)
                    {
                        case EndCriteriaType.None:
                        case EndCriteriaType.MaxIterations:
                        case EndCriteriaType.Unknown:
                            completed = false;
                            break;
                        default:
                            completed = true;
                            break;
                    }

                    Real xError = MaxDifference(xMinCalculated, xMinExpected_[i]);
                    Real yError = MaxDifference(yMinCalculated, yMinExpected_[i]);

                    bool correct = xError <= endCriteria.RootEpsilon || yError <= endCriteria.FunctionEpsilon;

                    if ((!completed) || (!correct))
                    {
                        errors.Add(
                            $"costFunction # = {i}"
                            + $"\nOptimizer: {optimizationMethods_[i][j].Name}"
                            + $"\n    function evaluations: {problem.FunctionEvaluation}"
                            + $"\n    gradient evaluations: {problem.GradientEvaluation}"
                            + $"\n    x expected:           {xMinExpected_[i]}"
                            + $"\n    x calculated:         {xMinCalculated}"
                            + $"\n    x difference:         {xMinExpected_[i] - xMinCalculated}"
                            + $"\n    rootEpsilon:          {endCriteria.RootEpsilon}"
                            + $"\n    y expected:           {yMinExpected_[i]}"
                            + $"\n    y calculated:         {yMinCalculated}"
                            + $"\n    y difference:         {yMinExpected_[i] - yMinCalculated}"
                            + $"\n    functionEpsilon:      {endCriteria.FunctionEpsilon}"
                            + $"\n    endCriteriaResult:    {endCriteriaResult}");
                    }
                }
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void NestedOptimizationTest()
    {
        // Testing nested optimizations...
        var optimizationBasedCostFunction = new OptimizationBasedCostFunction();
        var constraint = new NoConstraint();
        var initialValues = new Vector(1, 0.0);
        var problem = new Problem(optimizationBasedCostFunction, constraint, initialValues.AsSpan());
        var optimizationMethod = new LevenbergMarquardt();
        //Simplex optimizationMethod(0.1);
        //ConjugateGradient optimizationMethod;
        //SteepestDescent optimizationMethod;
        var endCriteria = new EndCriteria(1000, 100, 1e-5, 1e-5, 1e-5);
        optimizationMethod.Minimize(problem, endCriteria);
    }

    private static string OptimizationMethodTypeToString(OptimizationMethodType type) => type switch
    {
        OptimizationMethodType.simplex => "Simplex",
        OptimizationMethodType.levenbergMarquardt => "Levenberg Marquardt",
        OptimizationMethodType.levenbergMarquardt2 => "Levenberg Marquardt (cost function's jacbobian)",
        OptimizationMethodType.conjugateGradient => "Conjugate Gradient",
        OptimizationMethodType.steepestDescent => "Steepest Descent",
        OptimizationMethodType.bfgs => "BFGS",
        OptimizationMethodType.lbfgsb => "L-BFGS-B",
        OptimizationMethodType.conjugateGradient_goldstein => "Conjugate Gradient (Goldstein line search)",
        OptimizationMethodType.steepestDescent_goldstein => "Steepest Descent (Goldstein line search)",
        OptimizationMethodType.bfgs_goldstein => "BFGS (Goldstein line search)",
        _ => throw new NanoQLException("unknown OptimizationMethod type"),
    };

    // The methods not ported yet have no case here, and fall to QuantLib's default.
    private static OptimizationMethod MakeOptimizationMethod(
        OptimizationMethodType optimizationMethodType,
        Real simplexLambda,
        Real levenbergMarquardtEpsfcn,
        Real levenbergMarquardtXtol,
        Real levenbergMarquardtGtol) => optimizationMethodType switch
        {
            OptimizationMethodType.simplex => new Simplex(simplexLambda),
            OptimizationMethodType.levenbergMarquardt =>
                new LevenbergMarquardt(levenbergMarquardtEpsfcn, levenbergMarquardtXtol, levenbergMarquardtGtol),
            OptimizationMethodType.levenbergMarquardt2 =>
                new LevenbergMarquardt(levenbergMarquardtEpsfcn, levenbergMarquardtXtol, levenbergMarquardtGtol, true),
            OptimizationMethodType.conjugateGradient => new ConjugateGradient(),
            _ => throw new NanoQLException("unknown OptimizationMethod type"),
        };

    private static List<NamedOptimizationMethod> MakeOptimizationMethods(
        IEnumerable<OptimizationMethodType> optimizationMethodTypes,
        Real simplexLambda,
        Real levenbergMarquardtEpsfcn,
        Real levenbergMarquardtXtol,
        Real levenbergMarquardtGtol)
    {
        var results = new List<NamedOptimizationMethod>();
        foreach (OptimizationMethodType optimizationMethodType in optimizationMethodTypes)
        {
            results.Add(new NamedOptimizationMethod(
                MakeOptimizationMethod(
                    optimizationMethodType,
                    simplexLambda,
                    levenbergMarquardtEpsfcn,
                    levenbergMarquardtXtol,
                    levenbergMarquardtGtol),
                OptimizationMethodTypeToString(optimizationMethodType)));
        }

        return results;
    }

    private static Real MaxDifference(Vector a, Vector b)
    {
        Vector diff = a - b;
        Real maxDiff = 0.0;
        foreach (Real i in diff)
        {
            maxDiff = Math.Max(maxDiff, Math.Abs(i));
        }

        return maxDiff;
    }

    // Set up, for each cost function, all the ingredients for optimization:
    // constraint, initial guess, end criteria, optimization methods.
    private void Setup()
    {
        // Cost function n. 1: 1D polynomial of degree 2 (parabolic function y=a*x^2+b*x+c)
        const Real a = 1;   // required a > 0
        const Real b = 1;
        const Real c = 1;
        var coefficients = new Vector(3);
        coefficients[0] = c;
        coefficients[1] = b;
        coefficients[2] = a;
        costFunctions_.Add(new OneDimensionalPolynomialDegreeN(coefficients));
        // Set constraint for optimizers: unconstrained problem
        constraints_.Add(new NoConstraint());
        // Set initial guess for optimizer
        var initialValue = new Vector(1);
        initialValue[0] = -100;
        initialValues_.Add(initialValue);
        // Set end criteria for optimizer
        maxIterations_.Add(10000);                // maxIterations
        maxStationaryStateIterations_.Add(100);   // MaxStationaryStateIterations
        rootEpsilons_.Add(1e-8);                  // rootEpsilon
        functionEpsilons_.Add(1e-8);              // functionEpsilon
        gradientNormEpsilons_.Add(1e-8);          // gradientNormEpsilon
        endCriterias_.Add(new EndCriteria(
            maxIterations_[^1],
            maxStationaryStateIterations_[^1],
            rootEpsilons_[^1],
            functionEpsilons_[^1],
            gradientNormEpsilons_[^1]));
        // Set optimization methods for optimizer
        OptimizationMethodType[] optimizationMethodTypes =
        [
            OptimizationMethodType.simplex, OptimizationMethodType.levenbergMarquardt,
            OptimizationMethodType.levenbergMarquardt2, OptimizationMethodType.conjugateGradient,
        ];
        Real simplexLambda = 0.1;                   // characteristic search length for simplex
        Real levenbergMarquardtEpsfcn = 1.0e-8;     // parameters specific for Levenberg-Marquardt
        Real levenbergMarquardtXtol = 1.0e-8;       //
        Real levenbergMarquardtGtol = 1.0e-8;       //
        optimizationMethods_.Add(MakeOptimizationMethods(
            optimizationMethodTypes,
            simplexLambda,
            levenbergMarquardtEpsfcn,
            levenbergMarquardtXtol,
            levenbergMarquardtGtol));
        // Set expected results for optimizer
        Vector xMinExpected = new(1), yMinExpected = new(1);
        xMinExpected[0] = -b / (2.0 * a);
        yMinExpected[0] = -((b * b) - (4.0 * a * c)) / (4.0 * a);
        xMinExpected_.Add(xMinExpected);
        yMinExpected_.Add(yMinExpected);
    }

    private sealed record NamedOptimizationMethod(OptimizationMethod OptimizationMethod, string Name);

    private sealed class OneDimensionalPolynomialDegreeN : CostFunction
    {
        private readonly Vector coefficients_;
        private readonly Size polynomialDegree_;

        public OneDimensionalPolynomialDegreeN(Vector coefficients)
        {
            coefficients_ = coefficients.Clone();
            polynomialDegree_ = coefficients.Size - 1;
        }

        public override Real Value(Vector x)
        {
            if (!(x.Size == 1))
            {
                throw new NanoQLException("independent variable must be 1 dimensional");
            }

            Real y = 0;
            for (Size i = 0; i <= polynomialDegree_; ++i)
            {
                y += coefficients_[i] * Math.Pow(x[0], i);
            }

            return y;
        }

        public override Vector Values(Vector x)
        {
            if (!(x.Size == 1))
            {
                throw new NanoQLException("independent variable must be 1 dimensional");
            }

            return new Vector(1, Value(x));
        }
    }

    // The goal of this cost function is simply to call another optimization inside
    // in order to test nested optimizations
    private sealed class OptimizationBasedCostFunction : CostFunction
    {
        public override Real Value(Vector x) => 1.0;

        public override Vector Values(Vector x)
        {
            // dummy nested optimization
            var coefficients = new Vector(3, 1.0);
            var oneDimensionalPolynomialDegreeN = new OneDimensionalPolynomialDegreeN(coefficients);
            var constraint = new NoConstraint();
            var initialValues = new Vector(1, 100.0);
            var problem = new Problem(oneDimensionalPolynomialDegreeN, constraint, initialValues.AsSpan());
            var optimizationMethod = new LevenbergMarquardt();
            //Simplex optimizationMethod(0.1);
            //ConjugateGradient optimizationMethod;
            //SteepestDescent optimizationMethod;
            var endCriteria = new EndCriteria(1000, 100, 1e-5, 1e-5, 1e-5);
            optimizationMethod.Minimize(problem, endCriteria);
            // return dummy result
            return new Vector(1, 0);
        }
    }
}
