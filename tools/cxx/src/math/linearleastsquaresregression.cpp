// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/linearleastsquaresregression.hpp, which the wheel doesn't bind: linear regressions on one variable and on
// several, and the general least squares on named basis functions.
//
//     LinearRegression             (x, y, intercept):        a result, x the samples' values
//     LinearRegressionMulti        (x rows, y, intercept):   a result, a row per sample
//     LinearLeastSquaresRegression (x, y, [basis names]):    a result, the basis from "1", "x", "x x", "sin(x)",
//                                                            "exp(-x)"
//
// A result is {"coefficients": [...], "residuals": [...], "standardErrors": [...], "error": [...], "dim": n,
// "size": n}.

#include "convert.hpp"

#include <ql/math/array.hpp>
#include <ql/math/linearleastsquaresregression.hpp>

#include <cmath>
#include <cstdint>
#include <functional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::array array(const Array& v) {
            json::array out;
            for (Real x : v)
                out.emplace_back(x);
            return out;
        }

        json::value result(const GeneralLinearLeastSquares& r) {
            json::object out;
            out["coefficients"] = array(r.coefficients());
            out["residuals"] = array(r.residuals());
            out["standardErrors"] = array(r.standardErrors());
            out["error"] = array(r.error());
            out["dim"] = static_cast<std::int64_t>(r.dim());
            out["size"] = static_cast<std::int64_t>(r.size());
            return json::value(std::move(out));
        }

        std::function<Real(Real)> basis(const std::string& name) {
            if (name == "1")
                return [](Real) { return 1.0; };
            if (name == "x")
                return [](Real x) { return x; };
            if (name == "x x")
                return [](Real x) { return x * x; };
            if (name == "sin(x)")
                return [](Real x) { return std::sin(x); };
            if (name == "exp(-x)")
                return [](Real x) { return std::exp(-x); };
            throw InvalidParams("unknown basis function " + name);
        }

        const Register unit({
            {"LinearRegression",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 std::vector<Real> x = reals(a[0]), y = reals(a[1]);
                 Real intercept = real(a[2]);
                 return [=] { return result(LinearRegression(x, y, intercept)); };
             }},
            {"LinearRegressionMulti",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 if (!a[0].is_array() || a[0].get_array().empty())
                     throw InvalidParams("expected the samples' rows");
                 std::vector<Array> x;
                 for (const json::value& row : a[0].get_array()) {
                     std::vector<Real> r = reals(row);
                     x.emplace_back(r.begin(), r.end());
                 }
                 std::vector<Real> y = reals(a[1]);
                 Real intercept = real(a[2]);
                 return [=] { return result(LinearRegression(x, y, intercept)); };
             }},
            {"LinearLeastSquaresRegression",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 std::vector<Real> x = reals(a[0]), y = reals(a[1]);
                 if (!a[2].is_array())
                     throw InvalidParams("expected the basis' names");
                 std::vector<std::function<Real(Real)>> v;
                 for (const json::value& name : a[2].get_array())
                     v.push_back(basis(text(name)));
                 return [=] { return result(LinearLeastSquaresRegression<Real>(x, y, v)); };
             }},
        });

    }

}
