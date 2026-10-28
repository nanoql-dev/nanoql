// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/cmaes.hpp, .cpp: Cmaes::minimize on a new optimizer, so its Mersenne Twister starts from the
// seed, with what it leaves in the Problem. "Cmaes::minimize" takes (cost function, constraint, initial value, end
// criteria, configuration), the specs as math/optimization/specs.hpp's, the configuration {"populationSize", "sigma",
// "seed"} with optional "lowerBound" and "upperBound" (set together through withBounds) and "initialMean", lists of
// reals; it returns specs.hpp's Problem result with the end criterion.

#include "math/optimization/specs.hpp"

#include <ql/math/optimization/cmaes.hpp>

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
            Size populationSize;
            Real sigma;
            long long seed;
            bool bounded = false;
            std::vector<Real> lowerBound, upperBound, initialMean;
        };

        ConfigurationSpec configuration(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a configuration object");
            const json::object& o = v.get_object();
            ConfigurationSpec s;
            s.populationSize = size(field(o, "populationSize"));
            s.sigma = real(field(o, "sigma"));
            s.seed = int64(field(o, "seed"));
            if (s.seed < 1 || s.seed > 4294967295LL)
                throw InvalidParams("a seed in [1, 4294967295]");
            const json::value* lo = o.if_contains("lowerBound");
            const json::value* up = o.if_contains("upperBound");
            if ((lo == nullptr) != (up == nullptr))
                throw InvalidParams("the bounds come together");
            if (lo != nullptr) {
                s.bounded = true;
                s.lowerBound = reals(*lo);
                s.upperBound = reals(*up);
            }
            if (const json::value* m = o.if_contains("initialMean"))
                s.initialMean = reals(*m);
            return s;
        }

        const Register unit({
            {"Cmaes::minimize",
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
                     Cmaes::Configuration cfg = Cmaes::Configuration()
                                                    .withPopulationSize(s.populationSize)
                                                    .withSigma(s.sigma)
                                                    .withSeed(static_cast<unsigned long>(s.seed));
                     if (s.bounded)
                         cfg.withBounds(makeArray(s.lowerBound), makeArray(s.upperBound));
                     if (!s.initialMean.empty())
                         cfg.withInitialMean(makeArray(s.initialMean));
                     Cmaes method(cfg);
                     EndCriteria::Type type = method.minimize(p, makeEndCriteria(e));
                     return minimizationResult(p, type);
                 };
             }},
        });

    }

}
