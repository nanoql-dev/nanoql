// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/operators/numericaldifferentiation.hpp, .cpp, which the wheel doesn't bind: Fornberg's
// weights on given offsets or a scheme, and the derivative of a named function.
//
//     NumericalDifferentiation        (function, order, offsets, [x, ...]):
//                                     {"offsets": [...], "weights": [...], "derivative": [...]}
//     NumericalDifferentiationScheme  (function, order, stepSize, steps, scheme ("Central", "Backward", "Forward"),
//                                      [x, ...]):  the same
//
// The functions are "sin(x)", "exp(x)", "x x x" and "1/(1+x x)".

#include "convert.hpp"

#include <ql/methods/finitedifferences/operators/numericaldifferentiation.hpp>

#include <cmath>
#include <functional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        std::function<Real(Real)> function(const std::string& name) {
            if (name == "sin(x)")
                return [](Real x) { return std::sin(x); };
            if (name == "exp(x)")
                return [](Real x) { return std::exp(x); };
            if (name == "x x x")
                return [](Real x) { return x * x * x; };
            if (name == "1/(1+x x)")
                return [](Real x) { return 1.0 / (1.0 + x * x); };
            throw InvalidParams("unknown function " + name);
        }

        json::value report(const NumericalDifferentiation& nd, const std::vector<Real>& at) {
            json::array offsets, weights, derivative;
            for (Real o : nd.offsets())
                offsets.emplace_back(o);
            for (Real w : nd.weights())
                weights.emplace_back(w);
            for (Real x : at)
                derivative.emplace_back(nd(x));
            json::object out;
            out["offsets"] = std::move(offsets);
            out["weights"] = std::move(weights);
            out["derivative"] = std::move(derivative);
            return json::value(std::move(out));
        }

        Size order(const json::value& v) {
            Integer n = integer(v);
            if (n < 0)
                throw InvalidParams("an order is non-negative");
            return static_cast<Size>(n);
        }

        const Register unit({
            {"NumericalDifferentiation",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 std::function<Real(Real)> f = function(text(a[0]));
                 Size m = order(a[1]);
                 std::vector<Real> offsets = reals(a[2]), at = reals(a[3]);
                 return [=] {
                     return report(NumericalDifferentiation(f, m, Array(offsets.begin(), offsets.end())), at);
                 };
             }},
            {"NumericalDifferentiationScheme",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 std::function<Real(Real)> f = function(text(a[0]));
                 Size m = order(a[1]);
                 Real h = real(a[2]);
                 Integer steps = integer(a[3]);
                 if (steps < 0)
                     throw InvalidParams("a number of steps is non-negative");
                 std::string s = text(a[4]);
                 NumericalDifferentiation::Scheme scheme = s == "Central"    ? NumericalDifferentiation::Central
                                                           : s == "Backward" ? NumericalDifferentiation::Backward
                                                           : s == "Forward"  ? NumericalDifferentiation::Forward
                                                                             : throw InvalidParams("unknown scheme " + s);
                 std::vector<Real> at = reals(a[5]);
                 return [=] { return report(NumericalDifferentiation(f, m, h, static_cast<Size>(steps), scheme), at); };
             }},
        });

    }

}
