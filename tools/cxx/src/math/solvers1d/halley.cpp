// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/solvers1d/halley.hpp: Halley's solver, which the wheel doesn't expose. "Halley::solve" takes (function,
// accuracy, guess, step, maxEvaluations) and "Halley::solve(bracketed)" (function, accuracy, guess, xMin, xMax,
// maxEvaluations); the function ["polynomial", c0, c1, ...] (the sum of c_k x^k, each power built by multiplying up
// from 1) or ["exponential", a, b, c] (a exp(bx) - c), each with its first and second derivatives; and returns the
// root.

#include "convert.hpp"

#include <ql/math/solvers1d/halley.hpp>

#include <cmath>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct Function {
            bool polynomial;
            std::vector<Real> c;

            Real operator()(Real x) const {
                if (!polynomial)
                    return c[0] * std::exp(c[1] * x) - c[2];
                Real s = 0.0, p = 1.0;
                for (Real ck : c) {
                    s += ck * p;
                    p *= x;
                }
                return s;
            }

            Real derivative(Real x) const {
                if (!polynomial)
                    return c[0] * c[1] * std::exp(c[1] * x);
                Real s = 0.0, p = 1.0;
                for (Size k = 1; k < c.size(); ++k) {
                    s += Real(k) * c[k] * p;
                    p *= x;
                }
                return s;
            }

            Real secondDerivative(Real x) const {
                if (!polynomial)
                    return c[0] * c[1] * c[1] * std::exp(c[1] * x);
                Real s = 0.0, p = 1.0;
                for (Size k = 2; k < c.size(); ++k) {
                    s += Real(k * (k - 1)) * c[k] * p;
                    p *= x;
                }
                return s;
            }
        };

        Function functionOf(const json::value& v) {
            if (!v.is_array() || v.get_array().empty())
                throw InvalidParams("expected a function, got " + json::serialize(v));
            const json::array& f = v.get_array();
            const std::string kind = text(f[0]);
            Function out{kind == "polynomial", {}};
            if (!out.polynomial && kind != "exponential")
                throw InvalidParams("unknown function " + kind);
            for (std::size_t i = 1; i < f.size(); ++i)
                out.c.push_back(real(f[i]));
            if (!out.polynomial && out.c.size() != 3)
                throw InvalidParams("an exponential is [a, b, c]");
            return out;
        }

        const Register unit({
            {"Halley::solve",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 const Function f = functionOf(a[0]);
                 const Real accuracy = real(a[1]), guess = real(a[2]), step = real(a[3]);
                 const Integer maxEvaluations = integer(a[4]);
                 return [=] {
                     Halley solver;
                     solver.setMaxEvaluations(maxEvaluations);
                     return json::value(solver.solve(f, accuracy, guess, step));
                 };
             }},
            {"Halley::solve(bracketed)",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 const Function f = functionOf(a[0]);
                 const Real accuracy = real(a[1]), guess = real(a[2]), xMin = real(a[3]), xMax = real(a[4]);
                 const Integer maxEvaluations = integer(a[5]);
                 return [=] {
                     Halley solver;
                     solver.setMaxEvaluations(maxEvaluations);
                     return json::value(solver.solve(f, accuracy, guess, xMin, xMax));
                 };
             }},
        });

    }

}
