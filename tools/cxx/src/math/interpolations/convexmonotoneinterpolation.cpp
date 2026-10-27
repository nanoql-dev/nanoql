// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolations/convexmonotoneinterpolation.hpp: what the wheel's ConvexMonotoneInterpolation doesn't take or
// return, its primitive, its flat final period and the local interpolation over a previous one's sections.
// "ConvexMonotoneInterpolation" takes (x, y, quadraticity, monotonicity, forcePositive, flatFinalPeriod, points) and
// returns {"values", "primitives"} at the points, extrapolating where they are out of range.
// "ConvexMonotone::localInterpolate" takes (x, y, quadraticity, monotonicity, forcePositive, localisation, points) and
// builds the interpolation as the local bootstrap does, over the first localisation + 1 nodes and then a node more at a
// time, each through ConvexMonotone::localInterpolate on the previous one, the curve's size the number of nodes; it
// returns {"values", "primitives"}, one array per step, at the points, extrapolating.

#include "convert.hpp"

#include <ql/math/interpolations/convexmonotoneinterpolation.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        typedef std::vector<Real>::const_iterator It;

        json::object at(const Interpolation& f, const std::vector<Real>& points) {
            json::array values, primitives;
            for (Real p : points) {
                values.push_back(json::value(f(p, true)));
                primitives.push_back(json::value(f.primitive(p, true)));
            }
            json::object out;
            out["values"] = values;
            out["primitives"] = primitives;
            return out;
        }

        const Register unit({
            {"ConvexMonotoneInterpolation",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 std::vector<Real> x = reals(a[0]), y = reals(a[1]);
                 Real q = real(a[2]), m = real(a[3]);
                 bool forcePositive = boolean(a[4]), flatFinalPeriod = boolean(a[5]);
                 std::vector<Real> points = reals(a[6]);
                 if (x.size() != y.size())
                     throw InvalidParams("x and y differ in size");
                 return [=] {
                     ConvexMonotoneInterpolation<It, It> f(x.begin(), x.end(), y.begin(), q, m, forcePositive,
                                                           flatFinalPeriod);
                     return json::value(at(f, points));
                 };
             }},
            {"ConvexMonotone::localInterpolate",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 std::vector<Real> x = reals(a[0]), y = reals(a[1]);
                 Real q = real(a[2]), m = real(a[3]);
                 bool forcePositive = boolean(a[4]);
                 Integer localisation = integer(a[5]);
                 std::vector<Real> points = reals(a[6]);
                 if (x.size() != y.size())
                     throw InvalidParams("x and y differ in size");
                 if (localisation == Null<Integer>() || localisation < 1 ||
                     static_cast<Size>(localisation) + 1 > x.size())
                     throw InvalidParams("the localisation is at least 1 and leaves a node to build on");
                 return [=] {
                     ConvexMonotone cm(q, m, forcePositive);
                     Interpolation previous;
                     json::array values, primitives;
                     for (Size length = localisation + 1; length <= x.size(); ++length) {
                         Interpolation f = cm.localInterpolate(x.begin(), x.begin() + length, y.begin(),
                                                               localisation, previous, x.size());
                         json::object step = at(f, points);
                         values.push_back(step["values"]);
                         primitives.push_back(step["primitives"]);
                         previous = f;
                     }
                     json::object out;
                     out["values"] = values;
                     out["primitives"] = primitives;
                     return json::value(out);
                 };
             }},
        });

    }

}
