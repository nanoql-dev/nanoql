// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/bspline.hpp, .cpp, which the wheel doesn't expose: the B-spline basis functions. "BSpline::values" takes
// (p, n, knots, indices, xs), p and n Naturals, and returns {"values": one array per index of N_{i,p}(x) at each x};
// a construction QuantLib refuses throws, and so does an index past n.

#include "convert.hpp"

#include <ql/math/bspline.hpp>

#include <cstdint>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        Natural natural(const json::value& v) {
            if (!v.is_int64() || v.as_int64() < 0 || v.as_int64() > static_cast<std::int64_t>(UINT_MAX))
                throw InvalidParams("expected a Natural, got " + json::serialize(v));
            return static_cast<Natural>(v.as_int64());
        }

        const Register unit({
            {"BSpline::values",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Natural p = natural(a[0]), n = natural(a[1]);
                 std::vector<Real> knots = reals(a[2]), xs = reals(a[4]);
                 std::vector<Integer> indices = integers(a[3]);
                 return [=] {
                     BSpline spline(p, n, knots);
                     json::array values;
                     for (Integer i : indices) {
                         json::array row;
                         for (Real x : xs)
                             row.push_back(json::value(spline(static_cast<Natural>(i), x)));
                         values.push_back(row);
                     }
                     json::object out;
                     out["values"] = values;
                     return json::value(out);
                 };
             }},
        });

    }

}
