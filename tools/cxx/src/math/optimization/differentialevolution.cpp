// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/differentialevolution.hpp, .cpp: DifferentialEvolution::minimize on a new optimizer, so its
// Mersenne Twister starts from the seed, with what it leaves in the Problem. "DifferentialEvolution::minimize" takes
// (cost function, constraint, initial value, end criteria, configuration), the specs as math/optimization/specs.hpp's,
// the configuration {"strategy", "crossoverType" (the enums' integers), "populationMembers", "stepsizeWeight",
// "crossoverProbability", "applyBounds", "adaptiveCrossover", "seed"} with optional "lowerBound", "upperBound" (lists
// of reals) and "initialPopulation" (a list of them), set through QuantLib's with... setters in that order; it returns
// specs.hpp's Problem result with the end criterion.

#include "math/optimization/specs.hpp"

#include <ql/math/optimization/differentialevolution.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        const json::value& field(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("the configuration needs ") + name);
            return *v;
        }

        struct ConfigurationSpec {
            Integer strategy, crossoverType;
            Size populationMembers;
            Real stepsizeWeight, crossoverProbability;
            bool applyBounds, adaptiveCrossover;
            long long seed;
            std::vector<Real> lowerBound, upperBound;
            std::vector<std::vector<Real>> initialPopulation;
        };

        ConfigurationSpec configuration(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a configuration object");
            const json::object& o = v.get_object();
            ConfigurationSpec s;
            s.strategy = integer(field(o, "strategy"));
            s.crossoverType = integer(field(o, "crossoverType"));
            s.populationMembers = size(field(o, "populationMembers"));
            s.stepsizeWeight = real(field(o, "stepsizeWeight"));
            s.crossoverProbability = real(field(o, "crossoverProbability"));
            s.applyBounds = boolean(field(o, "applyBounds"));
            s.adaptiveCrossover = boolean(field(o, "adaptiveCrossover"));
            s.seed = int64(field(o, "seed"));
            if (s.strategy < 0 || s.strategy > 6 || s.crossoverType < 0 || s.crossoverType > 2)
                throw InvalidParams("a strategy in [0, 6] and a crossover type in [0, 2]");
            if (s.seed < 1 || s.seed > 4294967295LL)
                throw InvalidParams("a seed in [1, 4294967295]");
            if (const json::value* lo = o.if_contains("lowerBound"))
                s.lowerBound = reals(*lo);
            if (const json::value* up = o.if_contains("upperBound"))
                s.upperBound = reals(*up);
            if (const json::value* population = o.if_contains("initialPopulation")) {
                if (!population->is_array() || population->get_array().empty())
                    throw InvalidParams("an initial population is a list of members, at least one");
                for (const json::value& member : population->get_array())
                    s.initialPopulation.push_back(reals(member));
            }
            return s;
        }

        DifferentialEvolution::Configuration makeConfiguration(const ConfigurationSpec& s) {
            DifferentialEvolution::Configuration c =
                DifferentialEvolution::Configuration()
                    .withStrategy(DifferentialEvolution::Strategy(s.strategy))
                    .withCrossoverType(DifferentialEvolution::CrossoverType(s.crossoverType))
                    .withPopulationMembers(s.populationMembers)
                    .withStepsizeWeight(s.stepsizeWeight)
                    .withCrossoverProbability(s.crossoverProbability)
                    .withBounds(s.applyBounds)
                    .withAdaptiveCrossover(s.adaptiveCrossover)
                    .withSeed(static_cast<unsigned long>(s.seed));
            if (!s.lowerBound.empty())
                c.withLowerBound(makeArray(s.lowerBound));
            if (!s.upperBound.empty())
                c.withUpperBound(makeArray(s.upperBound));
            if (!s.initialPopulation.empty()) {
                std::vector<Array> population;
                for (const auto& member : s.initialPopulation)
                    population.push_back(makeArray(member));
                c.withInitialPopulation(population);
            }
            return c;
        }

        const Register unit({
            {"DifferentialEvolution::minimize",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 CostFunctionSpec f = costFunction(a[0]);
                 ConstraintSpec c = constraint(a[1]);
                 std::vector<Real> x = reals(a[2]);
                 EndCriteriaSpec e = endCriteria(a[3]);
                 ConfigurationSpec s = configuration(a[4]);
                 checkPoint(f, x);
                 return [=] {
                     std::unique_ptr<CostFunction> cost = makeCostFunction(f);
                     Constraint bounds = makeConstraint(c);
                     Problem p(*cost, bounds, makeArray(x));
                     DifferentialEvolution method(makeConfiguration(s));
                     EndCriteria::Type type = method.minimize(p, makeEndCriteria(e));
                     return minimizationResult(p, type);
                 };
             }},
        });

    }

}
