// SPDX-FileCopyrightText: Copyright (C) 2019 SoftSolutions! S.r.l.
// SPDX-FileCopyrightText: Copyright (C) 2025 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/globalbootstrap.hpp ql/termstructures/globalbootstrap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.TermStructures;

// QuantLib's MultiCurveBootstrap: the joint bootstrap of a multicurve's members, one optimization over every
// contributor's nodes. A multicurve's Build creates one, its members hand it their contributors in add order, and it
// runs once their handles are linked. QuantLib's addObserver, through which the cost function updates the
// non-bootstrapped members after each argument, has no counterpart: those members read the curves they reference when
// queried. Its setOtherContributorsToValid and finalizeCalculation are declared and never defined.
internal sealed class MultiCurveBootstrap
{
    private readonly OptimizationMethod optimizer;
    private readonly EndCriteria endCriteria;
    private readonly List<MultiCurveBootstrapContributor> contributors = [];

    // QuantLib's MultiCurveBootstrap(accuracy).
    public MultiCurveBootstrap(double accuracy)
    {
        optimizer = new LevenbergMarquardt(accuracy, accuracy, accuracy);
        endCriteria = new EndCriteria(1000, 10, accuracy, accuracy, accuracy);
    }

    // QuantLib's MultiCurveBootstrap(optimizer, endCriteria): what isn't given is the default at an accuracy of 1e-10.
    public MultiCurveBootstrap(OptimizationMethod? optimizer, EndCriteria? endCriteria)
    {
        const double accuracy = 1E-10;
        this.optimizer = optimizer ?? new LevenbergMarquardt(accuracy, accuracy, accuracy);
        this.endCriteria = endCriteria ?? new EndCriteria(1000, 10, accuracy, accuracy, accuracy);
    }

    // Whether a curve has joined: QuantLib's joint bootstrap runs when a contributing curve is first read.
    public bool HasContributors => contributors.Count > 0;

    public void Add(MultiCurveBootstrapContributor c) => contributors.Add(c);

    public void RunMultiCurveBootstrap()
    {
        var guessSizes = new List<int>();
        var globalGuess = new List<double>();

        foreach (MultiCurveBootstrapContributor c in contributors)
        {
            Vector guess = c.SetupCostFunction();
            globalGuess.AddRange(guess.AsSpan());
            guessSizes.Add(guess.Size);
        }

        var costFunction = new SimpleCostFunction(x =>
        {
            // call the contributors' cost functions' set part: every curve's data are set before any helper is priced
            int offset = 0;
            for (int c = 0; c < contributors.Count; ++c)
            {
                contributors[c].SetCostFunctionArgument(x.AsSpan().Slice(offset, guessSizes[c]));
                offset += guessSizes[c];
            }

            // collect the contributors' result
            var results = new List<Vector>(contributors.Count);
            foreach (MultiCurveBootstrapContributor contributor in contributors)
            {
                results.Add(contributor.EvaluateCostFunction());
            }

            // concatenate the contributors' values and return the concatenation as the result
            int resultSize = 0;
            foreach (Vector r in results)
            {
                resultSize += r.Size;
            }

            var result = new Vector(resultSize);
            offset = 0;
            foreach (Vector r in results)
            {
                r.AsSpan().CopyTo(result.AsSpan().Slice(offset));
                offset += r.Size;
            }

            return result;
        });

        var problem = new Problem(costFunction, new NoConstraint(), [.. globalGuess]);
        EndCriteriaType endType = optimizer.Minimize(problem, endCriteria).EndCriteriaType;

        if (!EndCriteria.Succeeded(endType))
        {
            throw new NanoQLException(
                $"global bootstrap failed to minimize to required accuracy (during multi curve bootstrap): {endType}");
        }

        // set all contributors to valid
        foreach (MultiCurveBootstrapContributor c in contributors)
        {
            c.SetToValid();
        }
    }
}
