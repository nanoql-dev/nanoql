// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/integrals/twodimensionalintegral.hpp, which the wheel doesn't bind: the nested integral of a named f(x, y)
// over a rectangle, by two named one-dimensional integrators.
//
//     TwoDimensionalIntegral  ([outer kind, accuracy, maxEvaluations], [inner kind, accuracy, maxEvaluations],
//                              integrand, [ax, ay], [bx, by]):  the integral
//
// The kinds are "Trapezoid", "Simpson" and "GaussLobatto"; the integrands "x y", "exp(-x) sin(x y)" and
// "1/(1+x x+y y)".

#include "convert.hpp"

#include <ql/math/integrals/gausslobattointegral.hpp>
#include <ql/math/integrals/simpsonintegral.hpp>
#include <ql/math/integrals/trapezoidintegral.hpp>
#include <ql/math/integrals/twodimensionalintegral.hpp>

#include <cmath>
#include <functional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct IntegratorSpec {
            std::string kind;
            Real accuracy;
            Integer maxEvaluations;
        };

        IntegratorSpec spec(const json::value& v) {
            if (!v.is_array() || v.get_array().size() != 3)
                throw InvalidParams("expected [kind, accuracy, maxEvaluations], got " + json::serialize(v));
            const json::array& a = v.get_array();
            IntegratorSpec s{text(a[0]), real(a[1]), integer(a[2])};
            if (s.maxEvaluations < 0)
                throw InvalidParams("a maximum must be non-negative");
            if (s.kind != "Trapezoid" && s.kind != "Simpson" && s.kind != "GaussLobatto")
                throw InvalidParams("unknown integrator " + s.kind);
            return s;
        }

        ext::shared_ptr<Integrator> make(const IntegratorSpec& s) {
            Size n = static_cast<Size>(s.maxEvaluations);
            if (s.kind == "Trapezoid")
                return ext::make_shared<TrapezoidIntegral<Default>>(s.accuracy, n);
            if (s.kind == "Simpson")
                return ext::make_shared<SimpsonIntegral>(s.accuracy, n);
            return ext::make_shared<GaussLobattoIntegral>(n, s.accuracy);
        }

        std::function<Real(Real, Real)> integrand(const std::string& name) {
            if (name == "x y")
                return [](Real x, Real y) { return x * y; };
            if (name == "exp(-x) sin(x y)")
                return [](Real x, Real y) { return std::exp(-x) * std::sin(x * y); };
            if (name == "1/(1+x x+y y)")
                return [](Real x, Real y) { return 1.0 / (1.0 + x * x + y * y); };
            throw InvalidParams("unknown integrand " + name);
        }

        std::pair<Real, Real> corner(const json::value& v) {
            std::vector<Real> c = reals(v);
            if (c.size() != 2)
                throw InvalidParams("a corner is [x, y]");
            return {c[0], c[1]};
        }

        const Register unit({
            {"TwoDimensionalIntegral",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 IntegratorSpec outer = spec(a[0]), inner = spec(a[1]);
                 std::function<Real(Real, Real)> f = integrand(text(a[2]));
                 std::pair<Real, Real> lower = corner(a[3]), upper = corner(a[4]);
                 return [=] { return json::value(TwoDimensionalIntegral(make(outer), make(inner))(f, lower, upper)); };
             }},
        });

    }

}
