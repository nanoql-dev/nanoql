// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolations/flatextrapolation.hpp: FlatExtrapolator, which the wheel doesn't expose, on any interpolation
// spec (math/interpolationspec.hpp), the decorated interpolation built inside the call. The value and its derivatives
// and primitive at one point each, with QuantLib's allowExtrapolation flag; the range accessors; and extrapolation
// enabled on the decorator, through Extrapolator's member, before a value is read without the flag. The decorated
// interpolation keeps iterators into the spec's vectors, which the call's own copy of the spec holds.
//
//     FlatExtrapolator::operator()           (spec, x, allowExtrapolation)
//     FlatExtrapolator::primitive            (spec, x, allowExtrapolation)
//     FlatExtrapolator::derivative           (spec, x, allowExtrapolation)
//     FlatExtrapolator::secondDerivative     (spec, x, allowExtrapolation)
//     FlatExtrapolator::xMin, xMax           (spec)
//     FlatExtrapolator::xValues, yValues     (spec)
//     FlatExtrapolator::isInRange            (spec, x)
//     FlatExtrapolator::enableExtrapolation  (spec, x): enableExtrapolation(), then the value at x without the flag

#include "math/interpolationspec.hpp"

#include <ql/math/interpolations/flatextrapolation.hpp>
#include <ql/shared_ptr.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        FlatExtrapolator decorate(const InterpolationSpec& s) {
            return FlatExtrapolator(ext::make_shared<Interpolation>(makeInterpolation(s)));
        }

        // A member read at one point: (spec, x, allowExtrapolation).
        Call atPoint(const Args& a, Real (*f)(const FlatExtrapolator&, Real, bool)) {
            arity(a, 3);
            InterpolationSpec s = interpolationSpec(a[0]);
            Real x = real(a[1]);
            bool allow = boolean(a[2]);
            return [=] { return json::value(f(decorate(s), x, allow)); };
        }

        // A member of the decorator alone: (spec).
        Call whole(const Args& a, json::value (*f)(const FlatExtrapolator&)) {
            arity(a, 1);
            InterpolationSpec s = interpolationSpec(a[0]);
            return [=] { return f(decorate(s)); };
        }

        json::value realArray(const std::vector<Real>& v) {
            json::array out;
            for (Real x : v)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

        const Register unit({
            {"FlatExtrapolator::operator()",
             [](const Args& a) {
                 return atPoint(a, [](const FlatExtrapolator& f, Real x, bool allow) { return f(x, allow); });
             }},
            {"FlatExtrapolator::primitive",
             [](const Args& a) {
                 return atPoint(a, [](const FlatExtrapolator& f, Real x, bool allow) { return f.primitive(x, allow); });
             }},
            {"FlatExtrapolator::derivative",
             [](const Args& a) {
                 return atPoint(a, [](const FlatExtrapolator& f, Real x, bool allow) {
                     return f.derivative(x, allow);
                 });
             }},
            {"FlatExtrapolator::secondDerivative",
             [](const Args& a) {
                 return atPoint(a, [](const FlatExtrapolator& f, Real x, bool allow) {
                     return f.secondDerivative(x, allow);
                 });
             }},
            {"FlatExtrapolator::xMin",
             [](const Args& a) { return whole(a, [](const FlatExtrapolator& f) { return json::value(f.xMin()); }); }},
            {"FlatExtrapolator::xMax",
             [](const Args& a) { return whole(a, [](const FlatExtrapolator& f) { return json::value(f.xMax()); }); }},
            {"FlatExtrapolator::xValues",
             [](const Args& a) {
                 return whole(a, [](const FlatExtrapolator& f) { return realArray(f.xValues()); });
             }},
            {"FlatExtrapolator::yValues",
             [](const Args& a) {
                 return whole(a, [](const FlatExtrapolator& f) { return realArray(f.yValues()); });
             }},
            {"FlatExtrapolator::isInRange",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 InterpolationSpec s = interpolationSpec(a[0]);
                 Real x = real(a[1]);
                 return [=] { return json::value(decorate(s).isInRange(x)); };
             }},
            {"FlatExtrapolator::enableExtrapolation",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 InterpolationSpec s = interpolationSpec(a[0]);
                 Real x = real(a[1]);
                 return [=] {
                     FlatExtrapolator f = decorate(s);
                     f.enableExtrapolation();
                     return json::value(f(x));
                 };
             }},
        });

    }

}
