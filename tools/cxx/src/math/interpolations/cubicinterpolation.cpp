// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolations/cubicinterpolation.hpp: CubicInterpolation's coefficient accessors and boundary-value updates,
// on a spec of the cubic family (math/interpolationspec.hpp), and the Cubic factory's interpolate and traits. A
// factory's interpolation is read back through its values at the given points, extrapolating where needed.

#include "math/interpolationspec.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        json::value realArray(const std::vector<Real>& v) {
            json::array out;
            for (Real x : v)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

        json::value boolArray(const std::vector<bool>& v) {
            json::array out;
            for (bool b : v)
                out.emplace_back(b);
            return json::value(std::move(out));
        }

        // An accessor of the cubic: (spec).
        Call accessor(const Args& a, json::value (*f)(const CubicInterpolation&)) {
            arity(a, 1);
            InterpolationSpec s = cubicSpec(a[0]);
            return [=] { return f(makeCubic(s)); };
        }

        // updateLeftConditionValue or updateRightConditionValue, then update(): (spec, value, points). Extrapolation
        // is enabled first, so the values read without the call's flag show that the update keeps it.
        Call update(const Args& a, void (*set)(CubicInterpolation&, Real)) {
            arity(a, 3);
            InterpolationSpec s = cubicSpec(a[0]);
            Real value = real(a[1]);
            std::vector<Real> points = reals(a[2]);
            return [=] {
                CubicInterpolation f = makeCubic(s);
                f.enableExtrapolation();
                set(f, value);
                f.update();
                json::array v, d, d2, p;
                for (Real x : points) {
                    v.emplace_back(f(x));
                    d.emplace_back(f.derivative(x));
                    d2.emplace_back(f.secondDerivative(x));
                    p.emplace_back(f.primitive(x));
                }
                return json::value(json::object{
                    {"value", std::move(v)}, {"derivative", std::move(d)}, {"secondDerivative", std::move(d2)},
                    {"primitive", std::move(p)}});
            };
        }

        const Register unit({
            {"CubicInterpolation::primitiveConstants",
             [](const Args& a) {
                 return accessor(a, [](const CubicInterpolation& f) { return realArray(f.primitiveConstants()); });
             }},
            {"CubicInterpolation::aCoefficients",
             [](const Args& a) {
                 return accessor(a, [](const CubicInterpolation& f) { return realArray(f.aCoefficients()); });
             }},
            {"CubicInterpolation::bCoefficients",
             [](const Args& a) {
                 return accessor(a, [](const CubicInterpolation& f) { return realArray(f.bCoefficients()); });
             }},
            {"CubicInterpolation::cCoefficients",
             [](const Args& a) {
                 return accessor(a, [](const CubicInterpolation& f) { return realArray(f.cCoefficients()); });
             }},
            {"CubicInterpolation::monotonicityAdjustments",
             [](const Args& a) {
                 return accessor(a, [](const CubicInterpolation& f) { return boolArray(f.monotonicityAdjustments()); });
             }},
            {"CubicInterpolation::updateLeftConditionValue",
             [](const Args& a) {
                 return update(a, [](CubicInterpolation& f, Real value) { f.updateLeftConditionValue(value); });
             }},
            {"CubicInterpolation::updateRightConditionValue",
             [](const Args& a) {
                 return update(a, [](CubicInterpolation& f, Real value) { f.updateRightConditionValue(value); });
             }},
            // (spec, x, y, points): the spec {"type": "Cubic"} with no parameter or all six.
            {"Cubic::interpolate",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 InterpolatorSpec s = interpolatorSpec(a[0], {"Cubic"});
                 std::vector<Real> x = reals(a[1]), y = reals(a[2]), points = reals(a[3]);
                 checkSizes(x, y);
                 return [=] {
                     const Cubic factory = makeCubicInterpolator(s);
                     return valuesAt(factory.interpolate(x.begin(), x.end(), y.begin()), points);
                 };
             }},
            {"Cubic::global",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] { return json::value(Cubic::global); };
             }},
            {"Cubic::requiredPoints",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] { return json::value(static_cast<std::int64_t>(Cubic::requiredPoints)); };
             }},
        });

    }

}
