// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolation.hpp: Interpolation's members, on any interpolation spec (math/interpolationspec.hpp), the
// interpolation built inside the call so that a constructor's refusal is QuantLib's. The value and its derivatives
// and primitive at one point each, with QuantLib's allowExtrapolation flag; the range accessors; and extrapolation
// enabled or disabled on the object, through Extrapolator's members, before a value is read without the flag.

#include "math/interpolationspec.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        // A member read at one point: (spec, x, allowExtrapolation).
        Call atPoint(const Args& a, Real (*f)(const Interpolation&, Real, bool)) {
            arity(a, 3);
            InterpolationSpec s = interpolationSpec(a[0]);
            Real x = real(a[1]);
            bool allow = boolean(a[2]);
            return [=] { return json::value(f(makeInterpolation(s), x, allow)); };
        }

        // A member of the interpolation alone: (spec).
        Call whole(const Args& a, json::value (*f)(const Interpolation&)) {
            arity(a, 1);
            InterpolationSpec s = interpolationSpec(a[0]);
            return [=] { return f(makeInterpolation(s)); };
        }

        json::value realArray(const std::vector<Real>& v) {
            json::array out;
            for (Real x : v)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

        const Register unit({
            {"Interpolation::operator()",
             [](const Args& a) {
                 return atPoint(a, [](const Interpolation& f, Real x, bool allow) { return f(x, allow); });
             }},
            {"Interpolation::primitive",
             [](const Args& a) {
                 return atPoint(a, [](const Interpolation& f, Real x, bool allow) { return f.primitive(x, allow); });
             }},
            {"Interpolation::derivative",
             [](const Args& a) {
                 return atPoint(a, [](const Interpolation& f, Real x, bool allow) { return f.derivative(x, allow); });
             }},
            {"Interpolation::secondDerivative",
             [](const Args& a) {
                 return atPoint(a, [](const Interpolation& f, Real x, bool allow) {
                     return f.secondDerivative(x, allow);
                 });
             }},
            {"Interpolation::xMin",
             [](const Args& a) { return whole(a, [](const Interpolation& f) { return json::value(f.xMin()); }); }},
            {"Interpolation::xMax",
             [](const Args& a) { return whole(a, [](const Interpolation& f) { return json::value(f.xMax()); }); }},
            {"Interpolation::xValues",
             [](const Args& a) { return whole(a, [](const Interpolation& f) { return realArray(f.xValues()); }); }},
            {"Interpolation::yValues",
             [](const Args& a) { return whole(a, [](const Interpolation& f) { return realArray(f.yValues()); }); }},
            {"Interpolation::isInRange",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 InterpolationSpec s = interpolationSpec(a[0]);
                 Real x = real(a[1]);
                 return [=] { return json::value(makeInterpolation(s).isInRange(x)); };
             }},
            // enableExtrapolation(b), then the value at x without the call's flag: (spec, b, x).
            {"Interpolation::enableExtrapolation",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 InterpolationSpec s = interpolationSpec(a[0]);
                 bool b = boolean(a[1]);
                 Real x = real(a[2]);
                 return [=] {
                     Interpolation f = makeInterpolation(s);
                     f.enableExtrapolation(b);
                     return json::value(f(x));
                 };
             }},
            // enableExtrapolation(), then disableExtrapolation(b), then the value at x without the call's flag:
            // (spec, b, x).
            {"Interpolation::disableExtrapolation",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 InterpolationSpec s = interpolationSpec(a[0]);
                 bool b = boolean(a[1]);
                 Real x = real(a[2]);
                 return [=] {
                     Interpolation f = makeInterpolation(s);
                     f.enableExtrapolation();
                     f.disableExtrapolation(b);
                     return json::value(f(x));
                 };
             }},
        });

    }

}
