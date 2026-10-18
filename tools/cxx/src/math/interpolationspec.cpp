// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "math/interpolationspec.hpp"

#include <ql/math/interpolations/backwardflatinterpolation.hpp>
#include <ql/math/interpolations/linearinterpolation.hpp>

#include <algorithm>
#include <set>

using namespace QuantLib;

namespace harness {

    namespace {

        const std::set<std::string>& cubicTypes() {
            static const std::set<std::string> types = {
                "CubicInterpolation", "CubicNaturalSpline", "MonotonicCubicNaturalSpline",
                "CubicSplineOvershootingMinimization1", "CubicSplineOvershootingMinimization2",
                "AkimaCubicInterpolation", "KrugerCubic", "HarmonicCubic", "FritschButlandCubic", "Parabolic",
                "MonotonicParabolic"};
            return types;
        }

        const std::set<std::string>& otherTypes() {
            static const std::set<std::string> types = {
                "LinearInterpolation", "BackwardFlatInterpolation", "LogLinearInterpolation", "LogCubicInterpolation",
                "LogCubicNaturalSpline", "MonotonicLogCubicNaturalSpline", "KrugerLogCubic", "HarmonicLogCubic",
                "FritschButlandLogCubic", "LogParabolic", "MonotonicLogParabolic"};
            return types;
        }

        // The mixed interpolations, which take a switch point and a behavior.
        const std::set<std::string>& mixedTypes() {
            static const std::set<std::string> types = {
                "MixedLinearCubicInterpolation", "MixedLinearCubicNaturalSpline",
                "MixedLinearMonotonicCubicNaturalSpline", "MixedLinearKrugerCubic", "MixedLinearFritschButlandCubic",
                "MixedLinearParabolic", "MixedLinearMonotonicParabolic", "LogMixedLinearCubicInterpolation",
                "LogMixedLinearCubicNaturalSpline"};
            return types;
        }

        const char* const parameterKeys[] = {"derivativeApprox", "monotonic", "leftCondition",
                                             "leftConditionValue", "rightCondition", "rightConditionValue"};

        bool takesParameters(const std::string& type) {
            return type == "CubicInterpolation" || type == "LogCubicInterpolation" ||
                   type == "MixedLinearCubicInterpolation" || type == "LogMixedLinearCubicInterpolation";
        }

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        bool isParameterKey(std::string_view k) {
            return std::any_of(std::begin(parameterKeys), std::end(parameterKeys),
                               [&](const char* p) { return k == p; });
        }

        // The six parameters, read into either spec.
        template <class S>
        void readParameters(const json::object& o, S& s, const std::string& what) {
            s.da = enumeration<CubicInterpolation::DerivativeApprox>(key(o, "derivativeApprox", what));
            s.monotonic = boolean(key(o, "monotonic", what));
            s.leftCondition = enumeration<CubicInterpolation::BoundaryCondition>(key(o, "leftCondition", what));
            s.leftValue = real(key(o, "leftConditionValue", what));
            s.rightCondition = enumeration<CubicInterpolation::BoundaryCondition>(key(o, "rightCondition", what));
            s.rightValue = real(key(o, "rightConditionValue", what));
        }

        bool isSwitchKey(std::string_view k) { return k == "n" || k == "behavior"; }

        // The switch point and the behavior, read into either spec: n a Size, so a non-negative integer.
        template <class S>
        void readSwitch(const json::object& o, S& s, const std::string& what) {
            const json::value& n = key(o, "n", what);
            if (!n.is_int64() || n.as_int64() < 0)
                throw InvalidParams(what + "'s n is a non-negative integer, got " + json::serialize(n));
            s.n = static_cast<Size>(n.as_int64());
            s.behavior = enumeration<MixedInterpolation::Behavior>(key(o, "behavior", what));
        }

    }

    void checkSizes(const std::vector<Real>& x, const std::vector<Real>& y) {
        if (x.size() != y.size())
            throw InvalidParams("an interpolation's x and y values have one size, since QuantLib reads one y per x");
    }

    InterpolationSpec interpolationSpec(const json::value& v) {
        if (!v.is_object())
            throw InvalidParams("expected an interpolation spec, got " + json::serialize(v));
        const json::object& o = v.get_object();
        const json::value* type = o.if_contains("type");
        if (type == nullptr || !type->is_string())
            throw InvalidParams("an interpolation spec needs a type: " + json::serialize(v));
        InterpolationSpec s;
        s.type = std::string(type->get_string());
        if (cubicTypes().count(s.type) == 0 && otherTypes().count(s.type) == 0 && mixedTypes().count(s.type) == 0)
            throw InvalidParams("unknown interpolation " + s.type);
        const bool parameters = takesParameters(s.type);
        const bool mixed = mixedTypes().count(s.type) != 0;
        for (const auto& kv : o) {
            const std::string_view k = kv.key();
            if (k != "type" && k != "x" && k != "y" && !(parameters && isParameterKey(k)) &&
                !(mixed && isSwitchKey(k)))
                throw InvalidParams(s.type + " takes no key " + std::string(k));
        }
        s.x = reals(key(o, "x", s.type));
        s.y = reals(key(o, "y", s.type));
        checkSizes(s.x, s.y);
        if (parameters)
            readParameters(o, s, s.type);
        if (mixed)
            readSwitch(o, s, s.type);
        return s;
    }

    InterpolationSpec cubicSpec(const json::value& v) {
        InterpolationSpec s = interpolationSpec(v);
        if (cubicTypes().count(s.type) == 0)
            throw InvalidParams(s.type + " isn't a CubicInterpolation");
        return s;
    }

    CubicInterpolation makeCubic(const InterpolationSpec& s) {
        auto b = s.x.begin(), e = s.x.end();
        auto y = s.y.begin();
        if (s.type == "CubicInterpolation")
            return CubicInterpolation(b, e, y, s.da, s.monotonic, s.leftCondition, s.leftValue, s.rightCondition,
                                      s.rightValue);
        if (s.type == "CubicNaturalSpline")
            return CubicNaturalSpline(b, e, y);
        if (s.type == "MonotonicCubicNaturalSpline")
            return MonotonicCubicNaturalSpline(b, e, y);
        if (s.type == "CubicSplineOvershootingMinimization1")
            return CubicSplineOvershootingMinimization1(b, e, y);
        if (s.type == "CubicSplineOvershootingMinimization2")
            return CubicSplineOvershootingMinimization2(b, e, y);
        if (s.type == "AkimaCubicInterpolation")
            return AkimaCubicInterpolation(b, e, y);
        if (s.type == "KrugerCubic")
            return KrugerCubic(b, e, y);
        if (s.type == "HarmonicCubic")
            return HarmonicCubic(b, e, y);
        if (s.type == "FritschButlandCubic")
            return FritschButlandCubic(b, e, y);
        if (s.type == "Parabolic")
            return Parabolic(b, e, y);
        if (s.type == "MonotonicParabolic")
            return MonotonicParabolic(b, e, y);
        throw InvalidParams("the interpolation " + s.type + " was not checked as a CubicInterpolation");
    }

    Interpolation makeInterpolation(const InterpolationSpec& s) {
        if (cubicTypes().count(s.type) != 0)
            return makeCubic(s);
        auto b = s.x.begin(), e = s.x.end();
        auto y = s.y.begin();
        if (s.type == "LinearInterpolation")
            return LinearInterpolation(b, e, y);
        if (s.type == "BackwardFlatInterpolation")
            return BackwardFlatInterpolation(b, e, y);
        if (s.type == "LogLinearInterpolation")
            return LogLinearInterpolation(b, e, y);
        if (s.type == "LogCubicInterpolation")
            return LogCubicInterpolation(b, e, y, s.da, s.monotonic, s.leftCondition, s.leftValue, s.rightCondition,
                                         s.rightValue);
        if (s.type == "LogCubicNaturalSpline")
            return LogCubicNaturalSpline(b, e, y);
        if (s.type == "MonotonicLogCubicNaturalSpline")
            return MonotonicLogCubicNaturalSpline(b, e, y);
        if (s.type == "KrugerLogCubic")
            return KrugerLogCubic(b, e, y);
        if (s.type == "HarmonicLogCubic")
            return HarmonicLogCubic(b, e, y);
        if (s.type == "FritschButlandLogCubic")
            return FritschButlandLogCubic(b, e, y);
        if (s.type == "LogParabolic")
            return LogParabolic(b, e, y);
        if (s.type == "MonotonicLogParabolic")
            return MonotonicLogParabolic(b, e, y);
        if (s.type == "MixedLinearCubicInterpolation")
            return MixedLinearCubicInterpolation(b, e, y, s.n, s.behavior, s.da, s.monotonic, s.leftCondition,
                                                 s.leftValue, s.rightCondition, s.rightValue);
        if (s.type == "MixedLinearCubicNaturalSpline")
            return MixedLinearCubicNaturalSpline(b, e, y, s.n, s.behavior);
        if (s.type == "MixedLinearMonotonicCubicNaturalSpline")
            return MixedLinearMonotonicCubicNaturalSpline(b, e, y, s.n, s.behavior);
        if (s.type == "MixedLinearKrugerCubic")
            return MixedLinearKrugerCubic(b, e, y, s.n, s.behavior);
        if (s.type == "MixedLinearFritschButlandCubic")
            return MixedLinearFritschButlandCubic(b, e, y, s.n, s.behavior);
        if (s.type == "MixedLinearParabolic")
            return MixedLinearParabolic(b, e, y, s.n, s.behavior);
        if (s.type == "MixedLinearMonotonicParabolic")
            return MixedLinearMonotonicParabolic(b, e, y, s.n, s.behavior);
        if (s.type == "LogMixedLinearCubicInterpolation")
            return LogMixedLinearCubicInterpolation(b, e, y, s.n, s.behavior, s.da, s.monotonic, s.leftCondition,
                                                    s.leftValue, s.rightCondition, s.rightValue);
        if (s.type == "LogMixedLinearCubicNaturalSpline")
            return LogMixedLinearCubicNaturalSpline(b, e, y, s.n, s.behavior);
        throw InvalidParams("the interpolation " + s.type + " was not checked");
    }

    InterpolatorSpec interpolatorSpec(const json::value& v, std::initializer_list<const char*> types) {
        if (!v.is_object())
            throw InvalidParams("expected an interpolator spec, got " + json::serialize(v));
        const json::object& o = v.get_object();
        const json::value* type = o.if_contains("type");
        if (type == nullptr || !type->is_string())
            throw InvalidParams("an interpolator spec needs a type: " + json::serialize(v));
        InterpolatorSpec s;
        s.type = std::string(type->get_string());
        if (std::none_of(types.begin(), types.end(), [&](const char* t) { return s.type == t; }))
            throw InvalidParams("an interpolator of type " + s.type + " isn't taken here");
        const bool mixedFactory = s.type == "MixedLinearCubic" || s.type == "LogMixedLinearCubic";
        const bool mixedPreset = s.type == "MonotonicLogMixedLinearCubic" || s.type == "KrugerLogMixedLinearCubic";
        for (const auto& kv : o) {
            const std::string_view k = kv.key();
            const bool takes = ((s.type == "Cubic" || s.type == "LogCubic" || mixedFactory) && isParameterKey(k)) ||
                               ((mixedFactory || mixedPreset) && isSwitchKey(k));
            if (k != "type" && !takes)
                throw InvalidParams(s.type + " takes no key " + std::string(k));
        }
        const std::size_t given = o.size() - 1;
        if (mixedFactory || mixedPreset) {
            // every parameter given: QuantLib's factories have no defaults for the first three
            if (given != (mixedFactory ? 8u : 2u))
                throw InvalidParams(s.type + " takes all its parameters");
            s.defaults = false;
            readSwitch(o, s, s.type);
            if (mixedFactory)
                readParameters(o, s, s.type);
            return s;
        }
        if (s.type == "Cubic") {
            if (given != 0 && given != 6)
                throw InvalidParams("a Cubic spec takes no parameter or all six");
            s.defaults = given == 0;
            if (!s.defaults)
                readParameters(o, s, s.type);
        } else if (s.type == "LogCubic") {
            if (given == 1) {
                s.da = enumeration<CubicInterpolation::DerivativeApprox>(key(o, "derivativeApprox", s.type));
            } else if (given == 6) {
                s.defaults = false;
                readParameters(o, s, s.type);
            } else {
                throw InvalidParams("a LogCubic spec takes derivativeApprox alone or all six parameters");
            }
        }
        return s;
    }

    Cubic makeCubicInterpolator(const InterpolatorSpec& s) {
        if (s.type != "Cubic")
            throw InvalidParams("the interpolator " + s.type + " isn't a Cubic");
        if (s.defaults)
            return Cubic();
        return Cubic(s.da, s.monotonic, s.leftCondition, s.leftValue, s.rightCondition, s.rightValue);
    }

    LogCubic makeLogCubicInterpolator(const InterpolatorSpec& s) {
        // A preset is copied into the LogCubic it derives from, as a call taking a LogCubic receives it.
        if (s.type == "MonotonicLogCubic")
            return MonotonicLogCubic();
        if (s.type == "KrugerLog")
            return KrugerLog();
        if (s.type != "LogCubic")
            throw InvalidParams("the interpolator " + s.type + " isn't a LogCubic");
        if (s.defaults)
            return LogCubic(s.da);
        return LogCubic(s.da, s.monotonic, s.leftCondition, s.leftValue, s.rightCondition, s.rightValue);
    }

    MixedLinearCubic makeMixedLinearCubicInterpolator(const InterpolatorSpec& s) {
        if (s.type != "MixedLinearCubic")
            throw InvalidParams("the interpolator " + s.type + " isn't a MixedLinearCubic");
        return MixedLinearCubic(s.n, s.behavior, s.da, s.monotonic, s.leftCondition, s.leftValue, s.rightCondition,
                                s.rightValue);
    }

    LogMixedLinearCubic makeLogMixedLinearCubicInterpolator(const InterpolatorSpec& s) {
        // A preset is copied into the LogMixedLinearCubic it derives from, as a call taking one receives it.
        if (s.type == "MonotonicLogMixedLinearCubic")
            return MonotonicLogMixedLinearCubic(s.n, s.behavior);
        if (s.type == "KrugerLogMixedLinearCubic")
            return KrugerLogMixedLinearCubic(s.n, s.behavior);
        if (s.type != "LogMixedLinearCubic")
            throw InvalidParams("the interpolator " + s.type + " isn't a LogMixedLinearCubic");
        return LogMixedLinearCubic(s.n, s.behavior, s.da, s.monotonic, s.leftCondition, s.leftValue,
                                   s.rightCondition, s.rightValue);
    }

    json::value valuesAt(const Interpolation& f, const std::vector<Real>& points) {
        json::array out;
        for (Real p : points)
            out.emplace_back(f(p, true));
        return json::value(std::move(out));
    }

}
