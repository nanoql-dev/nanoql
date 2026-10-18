// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The interpolation and interpolator specs the interpolation methods share (ql/math/interpolation.hpp and
// ql/math/interpolations/*.hpp).
//
// An interpolation is the spec {"type", "x", "y"}, its type one of the interpolation classes by QuantLib's name
// (LinearInterpolation, BackwardFlatInterpolation, LogLinearInterpolation, CubicInterpolation, LogCubicInterpolation and
// the named cubic and log-cubic classes), and CubicInterpolation and LogCubicInterpolation add their six parameters,
// "derivativeApprox", "monotonic", "leftCondition", "leftConditionValue", "rightCondition" and "rightConditionValue".
// The mixed interpolations (MixedLinearCubicInterpolation, LogMixedLinearCubicInterpolation and their named classes)
// add the switch point "n" and the "behavior", and the two parameterised ones the six parameters, a null
// "leftConditionValue" being QuantLib's Null<Real>(). The x and y values must be of one size, since QuantLib reads as
// many y values as there are x values. QuantLib's interpolations keep iterators into their data, so an interpolation
// the call builds reads the spec's own vectors, which outlive it.
//
// An interpolator (a factory) is the spec {"type"} with type Cubic, LogCubic, MonotonicLogCubic or KrugerLog: Cubic
// with no parameter (QuantLib's Cubic()) or all six, LogCubic with "derivativeApprox" alone or all six; or
// MixedLinearCubic and LogMixedLinearCubic with "n", "behavior" and all six, MonotonicLogMixedLinearCubic and
// KrugerLogMixedLinearCubic with "n" and "behavior".

#ifndef NANOQL_CXX_MATH_INTERPOLATIONSPEC_HPP
#define NANOQL_CXX_MATH_INTERPOLATIONSPEC_HPP

#include "convert.hpp"

#include <ql/math/interpolation.hpp>
#include <ql/math/interpolations/cubicinterpolation.hpp>
#include <ql/math/interpolations/loginterpolation.hpp>
#include <ql/math/interpolations/mixedinterpolation.hpp>

#include <string>
#include <vector>

namespace harness {

    struct InterpolationSpec {
        std::string type;
        std::vector<QuantLib::Real> x, y;
        // CubicInterpolation's and LogCubicInterpolation's parameters, read only for those two types.
        QuantLib::CubicInterpolation::DerivativeApprox da = QuantLib::CubicInterpolation::Spline;
        bool monotonic = false;
        QuantLib::CubicInterpolation::BoundaryCondition leftCondition = QuantLib::CubicInterpolation::NotAKnot;
        QuantLib::Real leftValue = 0.0;
        QuantLib::CubicInterpolation::BoundaryCondition rightCondition = QuantLib::CubicInterpolation::NotAKnot;
        QuantLib::Real rightValue = 0.0;
        // The mixed interpolations' switch point and behavior, read only for those types.
        QuantLib::Size n = 0;
        QuantLib::MixedInterpolation::Behavior behavior = QuantLib::MixedInterpolation::ShareRanges;
    };

    // The check: any interpolation type, or one of the cubic family (CubicInterpolation and the ten named cubic
    // classes), whose coefficients the cubic methods read.
    InterpolationSpec interpolationSpec(const json::value& v);
    InterpolationSpec cubicSpec(const json::value& v);

    // The call: the interpolation over the spec's own x and y.
    QuantLib::Interpolation makeInterpolation(const InterpolationSpec& s);
    QuantLib::CubicInterpolation makeCubic(const InterpolationSpec& s);

    struct InterpolatorSpec {
        std::string type;
        bool defaults = true;  // Cubic() or LogCubic(da), with QuantLib's defaults for the rest
        QuantLib::CubicInterpolation::DerivativeApprox da = QuantLib::CubicInterpolation::Spline;
        bool monotonic = false;
        QuantLib::CubicInterpolation::BoundaryCondition leftCondition = QuantLib::CubicInterpolation::NotAKnot;
        QuantLib::Real leftValue = 0.0;
        QuantLib::CubicInterpolation::BoundaryCondition rightCondition = QuantLib::CubicInterpolation::NotAKnot;
        QuantLib::Real rightValue = 0.0;
        QuantLib::Size n = 0;
        QuantLib::MixedInterpolation::Behavior behavior = QuantLib::MixedInterpolation::ShareRanges;
    };

    // The check, for a factory of the given types.
    InterpolatorSpec interpolatorSpec(const json::value& v, std::initializer_list<const char*> types);

    // The call.
    QuantLib::Cubic makeCubicInterpolator(const InterpolatorSpec& s);
    QuantLib::LogCubic makeLogCubicInterpolator(const InterpolatorSpec& s);
    QuantLib::MixedLinearCubic makeMixedLinearCubicInterpolator(const InterpolatorSpec& s);
    QuantLib::LogMixedLinearCubic makeLogMixedLinearCubicInterpolator(const InterpolatorSpec& s);

    // An interpolation's values at points, extrapolating where needed: how a factory's interpolate is read back.
    json::value valuesAt(const QuantLib::Interpolation& f, const std::vector<QuantLib::Real>& points);

    // The x and y values of a factory method's arguments, of one size.
    void checkSizes(const std::vector<QuantLib::Real>& x, const std::vector<QuantLib::Real>& y);

}

#endif
