// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/integrals/discreteintegrals.hpp, .cpp: the discrete trapezoid and Simpson rules, which the wheel doesn't
// expose. "DiscreteTrapezoidIntegral::apply" and "DiscreteSimpsonIntegral::apply" take (x, f), two arrays;
// "DiscreteTrapezoidIntegrator::integrate" and "DiscreteSimpsonIntegrator::integrate" take (integrand, a, b,
// evaluations), the integrand named as tools/cxx/src/math/integrals/trapezoidintegral.cpp names it, and return
// {"value", "evaluations"}.

#include "convert.hpp"

#include <ql/math/array.hpp>
#include <ql/math/integrals/discreteintegrals.hpp>

#include <cmath>
#include <functional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        std::function<Real(Real)> integrand(const std::string& name) {
            if (name == "exp(x)")
                return [](Real x) { return std::exp(x); };
            if (name == "sin(x)")
                return [](Real x) { return std::sin(x); };
            if (name == "x x x x")
                return [](Real x) { return x * x * x * x; };
            if (name == "1/(1+x x)")
                return [](Real x) { return 1.0 / (1.0 + x * x); };
            if (name == "sqrt(x)")
                return [](Real x) { return std::sqrt(x); };
            throw InvalidParams("unknown integrand " + name);
        }

        template <class Rule>
        Call apply(const Args& a) {
            arity(a, 2);
            const std::vector<Real> x = reals(a[0]), f = reals(a[1]);
            return [=] {
                Array xs(x.begin(), x.end()), fs(f.begin(), f.end());
                return json::value(Rule()(xs, fs));
            };
        }

        template <class Integrator>
        Call integrate(const Args& a) {
            arity(a, 4);
            const std::function<Real(Real)> f = integrand(text(a[0]));
            const Real lower = real(a[1]), upper = real(a[2]);
            const Integer evaluations = integer(a[3]);
            if (evaluations < 0)
                throw InvalidParams("the evaluations must be non-negative");
            return [=] {
                Integrator integrator(static_cast<Size>(evaluations));
                const Real value = integrator(f, lower, upper);
                json::object out;
                out["value"] = json::value(value);
                out["evaluations"] = json::value(static_cast<std::int64_t>(integrator.numberOfEvaluations()));
                return json::value(out);
            };
        }

        const Register unit({
            {"DiscreteTrapezoidIntegral::apply",
             [](const Args& a) -> Call { return apply<DiscreteTrapezoidIntegral>(a); }},
            {"DiscreteSimpsonIntegral::apply", [](const Args& a) -> Call { return apply<DiscreteSimpsonIntegral>(a); }},
            {"DiscreteTrapezoidIntegrator::integrate",
             [](const Args& a) -> Call { return integrate<DiscreteTrapezoidIntegrator>(a); }},
            {"DiscreteSimpsonIntegrator::integrate",
             [](const Args& a) -> Call { return integrate<DiscreteSimpsonIntegrator>(a); }},
        });

    }

}
