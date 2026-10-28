// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/simulatedannealing.hpp: SimulatedAnnealing<MersenneTwisterUniformRng>::minimize on a new
// optimizer over a generator from the seed, with what it leaves in the Problem. "SimulatedAnnealing::minimize" takes
// (cost function, constraint, initial value, end criteria, scheme, seed), the specs as math/optimization/specs.hpp's,
// the scheme {"type": "ConstantFactor", "lambda", "T0", "epsilon", "m"} or {"type": "ConstantBudget", "lambda", "T0",
// "K", "alpha"}; it returns specs.hpp's Problem result with the end criterion.

#include "math/optimization/specs.hpp"

#include <ql/math/optimization/simulatedannealing.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        const json::value& field(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("the scheme needs ") + name);
            return *v;
        }

        struct SchemeSpec {
            bool constantFactor;
            Real lambda, t0, epsilon, alpha;
            Size m, k;
        };

        SchemeSpec scheme(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a scheme object");
            const json::object& o = v.get_object();
            SchemeSpec s{};
            std::string type = text(field(o, "type"));
            s.lambda = real(field(o, "lambda"));
            s.t0 = real(field(o, "T0"));
            if (type == "ConstantFactor") {
                s.constantFactor = true;
                s.epsilon = real(field(o, "epsilon"));
                s.m = size(field(o, "m"));
            } else if (type == "ConstantBudget") {
                s.constantFactor = false;
                s.k = size(field(o, "K"));
                s.alpha = real(field(o, "alpha"));
            } else {
                throw InvalidParams("unknown scheme " + type);
            }
            return s;
        }

        const Register unit({
            {"SimulatedAnnealing::minimize",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 CostFunctionSpec f = costFunction(a[0]);
                 ConstraintSpec c = constraint(a[1]);
                 std::vector<Real> x = reals(a[2]);
                 EndCriteriaSpec e = endCriteria(a[3]);
                 SchemeSpec s = scheme(a[4]);
                 long long seed = int64(a[5]);
                 if (seed < 1 || seed > 4294967295LL)
                     throw InvalidParams("a seed in [1, 4294967295]");
                 checkPoint(f, x);
                 return [=] {
                     std::unique_ptr<CostFunction> cost = makeCostFunction(f);
                     Constraint bounds = makeConstraint(c);
                     Problem p(*cost, bounds, makeArray(x));
                     MersenneTwisterUniformRng rng(static_cast<unsigned long>(seed));
                     std::unique_ptr<SimulatedAnnealing<>> method =
                         s.constantFactor
                             ? std::make_unique<SimulatedAnnealing<>>(s.lambda, s.t0, s.epsilon, s.m, rng)
                             : std::make_unique<SimulatedAnnealing<>>(s.lambda, s.t0, s.k, s.alpha, rng);
                     EndCriteria::Type type = method->minimize(p, makeEndCriteria(e));
                     return minimizationResult(p, type);
                 };
             }},
        });

    }

}
