// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/polynomialmathfunction.hpp, .cpp and ql/math/pascaltriangle.hpp, .cpp, which the wheel doesn't bind: a
// polynomial's values, coefficients and definite coefficients, and Pascal's rows.
//
//     PolynomialFunction  (coefficients, [t, ...], [[t1, t2], ...]):
//                         {"order": n, "value": [...], "derivative": [...], "primitive": [...],
//                          "definiteIntegral": [...], "coefficients": [...], "derivativeCoefficients": [...],
//                          "primitiveCoefficients": [...], "definiteIntegralCoefficients": [[...], ...],
//                          "definiteDerivativeCoefficients": [[...], ...]}
//     PascalTriangle::get  (order):  the row, as decimal strings (BigNatural is MSVC's 32-bit unsigned long)

#include "convert.hpp"

#include <ql/math/pascaltriangle.hpp>
#include <ql/math/polynomialmathfunction.hpp>

#include <cstdint>
#include <string>
#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::array array(const std::vector<Real>& v) {
            json::array out;
            for (Real x : v)
                out.emplace_back(x);
            return out;
        }

        const Register unit({
            {"PolynomialFunction",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 std::vector<Real> c = reals(a[0]);
                 std::vector<Real> ts = reals(a[1]);
                 if (!a[2].is_array())
                     throw InvalidParams("expected the windows");
                 std::vector<std::pair<Real, Real>> windows;
                 for (const json::value& w : a[2].get_array()) {
                     std::vector<Real> p = reals(w);
                     if (p.size() != 2)
                         throw InvalidParams("a window is [t1, t2]");
                     windows.emplace_back(p[0], p[1]);
                 }
                 return [=] {
                     PolynomialFunction f(c);
                     json::array value, derivative, primitive, definite, integralCoefficients, derivativeCoefficients;
                     for (Real t : ts) {
                         value.emplace_back(f(t));
                         derivative.emplace_back(f.derivative(t));
                         primitive.emplace_back(f.primitive(t));
                     }
                     for (const auto& w : windows) {
                         definite.emplace_back(f.definiteIntegral(w.first, w.second));
                         integralCoefficients.emplace_back(array(f.definiteIntegralCoefficients(w.first, w.second)));
                         derivativeCoefficients.emplace_back(
                             array(f.definiteDerivativeCoefficients(w.first, w.second)));
                     }
                     json::object out;
                     out["order"] = static_cast<std::int64_t>(f.order());
                     out["value"] = std::move(value);
                     out["derivative"] = std::move(derivative);
                     out["primitive"] = std::move(primitive);
                     out["definiteIntegral"] = std::move(definite);
                     out["coefficients"] = array(f.coefficients());
                     out["derivativeCoefficients"] = array(f.derivativeCoefficients());
                     out["primitiveCoefficients"] = array(f.primitiveCoefficients());
                     out["definiteIntegralCoefficients"] = std::move(integralCoefficients);
                     out["definiteDerivativeCoefficients"] = std::move(derivativeCoefficients);
                     return json::value(std::move(out));
                 };
             }},
            {"PascalTriangle::get",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Integer order = integer(a[0]);
                 if (order < 0)
                     throw InvalidParams("an order is non-negative");
                 return [=] {
                     json::array out;
                     for (BigNatural c : PascalTriangle::get(static_cast<Size>(order)))
                         out.emplace_back(std::to_string(c));
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
