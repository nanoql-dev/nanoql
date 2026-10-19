// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolations/bicubicsplineinterpolation.hpp: BicubicSpline's five derivatives, which the wheel doesn't
// expose, and its value, on a spline built inside each call from the spec {"x", "y", "z"}: z the list of the matrix's
// rows, all of one size, a row per y value and a column per x value. QuantLib's 2-D interpolations keep iterators into
// the grid and a reference to the matrix, so the call builds the spline over the spec's own vectors and a matrix that
// outlives it. The matrix must hold exactly a row per y value and a column per x value: QuantLib reads past a smaller
// one, and its derivatives in x read the x values past their end for a wider one, so the harness refuses both.
//
//     BicubicSpline::derivativeX, derivativeY, secondDerivativeX, secondDerivativeY, derivativeXY   (spec, x, y)
//     BicubicSpline::operator()                                                                    (spec, x, y,
//                                                                                                  allowExtrapolation)

#include "convert.hpp"

#include <ql/math/interpolations/bicubicsplineinterpolation.hpp>
#include <ql/math/matrix.hpp>

#include <initializer_list>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct SplineSpec {
            std::vector<Real> x, y;
            Size rows = 0, columns = 0;
            std::vector<Real> z;  // row by row
        };

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        SplineSpec spline(const json::value& v) {
            const std::string what = "a bicubic spline";
            if (!v.is_object())
                throw InvalidParams("expected " + what + " spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                if (kv.key() != "x" && kv.key() != "y" && kv.key() != "z")
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
            SplineSpec s;
            s.x = reals(key(o, "x", what));
            s.y = reals(key(o, "y", what));
            const json::value& z = key(o, "z", what);
            if (!z.is_array())
                throw InvalidParams(what + " needs z as a list of rows");
            for (const json::value& row : z.get_array()) {
                std::vector<Real> r = reals(row);
                if (s.rows == 0)
                    s.columns = r.size();
                else if (r.size() != s.columns)
                    throw InvalidParams(what + " needs z's rows of one size");
                s.z.insert(s.z.end(), r.begin(), r.end());
                ++s.rows;
            }
            if (s.rows != s.y.size() || s.columns != s.x.size())
                throw InvalidParams(what + " needs a row of z per y value and a column per x value");
            return s;
        }

        // A derivative at a point: (spec, x, y).
        Call derivative(const Args& a, Real (*f)(const BicubicSpline&, Real, Real)) {
            arity(a, 3);
            SplineSpec s = spline(a[0]);
            Real x = real(a[1]);
            Real y = real(a[2]);
            return [=] {
                Matrix z(s.rows, s.columns, s.z.begin(), s.z.end());
                BicubicSpline b(s.x.begin(), s.x.end(), s.y.begin(), s.y.end(), z);
                return json::value(f(b, x, y));
            };
        }

        const Register unit({
            {"BicubicSpline::derivativeX",
             [](const Args& a) {
                 return derivative(a, [](const BicubicSpline& b, Real x, Real y) { return b.derivativeX(x, y); });
             }},
            {"BicubicSpline::derivativeY",
             [](const Args& a) {
                 return derivative(a, [](const BicubicSpline& b, Real x, Real y) { return b.derivativeY(x, y); });
             }},
            {"BicubicSpline::secondDerivativeX",
             [](const Args& a) {
                 return derivative(a, [](const BicubicSpline& b, Real x, Real y) { return b.secondDerivativeX(x, y); });
             }},
            {"BicubicSpline::secondDerivativeY",
             [](const Args& a) {
                 return derivative(a, [](const BicubicSpline& b, Real x, Real y) { return b.secondDerivativeY(x, y); });
             }},
            {"BicubicSpline::derivativeXY",
             [](const Args& a) {
                 return derivative(a, [](const BicubicSpline& b, Real x, Real y) { return b.derivativeXY(x, y); });
             }},
            // (spec, x, y, allowExtrapolation)
            {"BicubicSpline::operator()",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 SplineSpec s = spline(a[0]);
                 Real x = real(a[1]);
                 Real y = real(a[2]);
                 bool allow = boolean(a[3]);
                 return [=] {
                     Matrix z(s.rows, s.columns, s.z.begin(), s.z.end());
                     BicubicSpline b(s.x.begin(), s.x.end(), s.y.begin(), s.y.end(), z);
                     return json::value(b(x, y, allow));
                 };
             }},
        });

    }

}
