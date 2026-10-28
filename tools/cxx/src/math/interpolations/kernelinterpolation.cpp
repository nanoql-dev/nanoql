// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolations/kernelinterpolation.hpp, kernelinterpolation2d.hpp and ql/math/kernelfunctions.hpp, which the
// wheel doesn't bind: the kernel interpolations' values and the Gaussian kernel's.
//
//     KernelInterpolation    (x, y, kernel, epsilon, [x, ...]):  the values, extrapolation enabled
//     KernelInterpolation2D  (x, y, z rows (a row per x), kernel, [[x, y], ...]):  the values, extrapolation enabled
//     GaussianKernel         (average, sigma, [x, ...]):  {"value": [...], "derivative": [...], "primitive": [...]}
//
// A kernel is {"gaussian": [average, sigma]} or "epanechnikov", interpolations.cpp's (3/4)(1 - u^2) on |u| <= 1.

#include "convert.hpp"

#include <ql/math/interpolations/kernelinterpolation.hpp>
#include <ql/math/interpolations/kernelinterpolation2d.hpp>
#include <ql/math/kernelfunctions.hpp>
#include <ql/math/matrix.hpp>

#include <cmath>
#include <functional>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        std::function<Real(Real)> kernel(const json::value& v) {
            if (v.is_string() && text(v) == "epanechnikov")
                return [](Real u) { return std::fabs(u) <= 1 ? (3.0 / 4.0) * (1 - u * u) : 0.0; };
            if (v.is_object()) {
                if (const json::value* g = v.get_object().if_contains("gaussian")) {
                    std::vector<Real> p = reals(*g);
                    if (p.size() != 2)
                        throw InvalidParams("a Gaussian kernel is [average, sigma]");
                    GaussianKernel k(p[0], p[1]);
                    return [k](Real u) { return k(u); };
                }
            }
            throw InvalidParams("expected {gaussian: [average, sigma]} or epanechnikov, got " + json::serialize(v));
        }

        const Register unit({
            {"KernelInterpolation",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 std::vector<Real> x = reals(a[0]), y = reals(a[1]);
                 if (x.size() != y.size())
                     throw InvalidParams("as many y values as x values");
                 std::function<Real(Real)> k = kernel(a[2]);
                 Real epsilon = real(a[3]);
                 std::vector<Real> at = reals(a[4]);
                 return [=] {
                     KernelInterpolation f(x.begin(), x.end(), y.begin(), k, epsilon);
                     f.enableExtrapolation();
                     json::array out;
                     for (Real p : at)
                         out.emplace_back(f(p));
                     return json::value(std::move(out));
                 };
             }},
            {"KernelInterpolation2D",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 std::vector<Real> x = reals(a[0]), y = reals(a[1]);
                 if (!a[2].is_array() || a[2].get_array().size() != x.size())
                     throw InvalidParams("a row of z values per x value");
                 Matrix z(x.size(), y.size());
                 for (Size i = 0; i < x.size(); ++i) {
                     std::vector<Real> row = reals(a[2].get_array()[i]);
                     if (row.size() != y.size())
                         throw InvalidParams("a z value per y value");
                     for (Size j = 0; j < y.size(); ++j)
                         z[i][j] = row[j];
                 }
                 std::function<Real(Real)> k = kernel(a[3]);
                 if (!a[4].is_array())
                     throw InvalidParams("expected the points");
                 std::vector<std::vector<Real>> at;
                 for (const json::value& p : a[4].get_array()) {
                     at.push_back(reals(p));
                     if (at.back().size() != 2)
                         throw InvalidParams("a point is [x, y]");
                 }
                 return [=] {
                     KernelInterpolation2D f(x.begin(), x.end(), y.begin(), y.end(), z, k);
                     f.enableExtrapolation();
                     json::array out;
                     for (const std::vector<Real>& p : at)
                         out.emplace_back(f(p[0], p[1]));
                     return json::value(std::move(out));
                 };
             }},
            {"GaussianKernel",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Real average = real(a[0]), sigma = real(a[1]);
                 std::vector<Real> at = reals(a[2]);
                 return [=] {
                     GaussianKernel k(average, sigma);
                     json::array value, derivative, primitive;
                     for (Real p : at) {
                         value.emplace_back(k(p));
                         derivative.emplace_back(k.derivative(p));
                         primitive.emplace_back(k.primitive(p));
                     }
                     json::object out;
                     out["value"] = std::move(value);
                     out["derivative"] = std::move(derivative);
                     out["primitive"] = std::move(primitive);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
