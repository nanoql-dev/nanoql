// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The abcd fit spec the AbcdCalibration and AbcdInterpolation methods share
// (ql/termstructures/volatility/abcdcalibration.hpp, ql/math/interpolations/abcdinterpolation.hpp): {"t", "blackVols",
// "guess" ([a, b, c, d], each null for Null<Real>()), "fixed" ([aIsFixed, bIsFixed, cIsFixed, dIsFixed]),
// "vegaWeighted", "endCriteria", "method"}. The end criteria are null for the fit's default, or {"maxIterations",
// "maxStationaryStateIterations", "rootEpsilon", "functionEpsilon", "gradientNormEpsilon"}; the method null for the
// fit's default, or {"type": "LevenbergMarquardt", "epsfcn", "xtol", "gtol", "useCostFunctionsJacobian"} or {"type":
// "Simplex", "lambda"}.

#ifndef NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_ABCDSPEC_HPP
#define NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_ABCDSPEC_HPP

#include "convert.hpp"

#include <ql/math/optimization/endcriteria.hpp>
#include <ql/math/optimization/levenbergmarquardt.hpp>
#include <ql/math/optimization/simplex.hpp>

#include <array>
#include <initializer_list>
#include <optional>
#include <string>
#include <vector>

namespace harness::abcd {

    inline const json::value& key(const json::object& o, const char* name, const std::string& what) {
        const json::value* v = o.if_contains(name);
        if (v == nullptr)
            throw InvalidParams(what + " needs " + name);
        return *v;
    }

    inline void onlyKeys(const json::object& o, std::initializer_list<const char*> keys, const std::string& what) {
        for (const auto& kv : o) {
            bool known = false;
            for (const char* k : keys)
                known = known || kv.key() == k;
            if (!known)
                throw InvalidParams(what + " takes no key " + std::string(kv.key()));
        }
    }

    inline const json::object& object(const json::value& v, const std::string& what) {
        if (!v.is_object())
            throw InvalidParams("expected " + what + ", got " + json::serialize(v));
        return v.get_object();
    }

    struct EndCriteriaSpec {
        QuantLib::Size maxIterations = 0, maxStationaryStateIterations = 0;
        QuantLib::Real rootEpsilon = 0.0, functionEpsilon = 0.0, gradientNormEpsilon = 0.0;
    };

    struct MethodSpec {
        std::string type;
        QuantLib::Real epsfcn = 1e-8, xtol = 1e-8, gtol = 1e-8, lambda = 0.0;
        bool useCostFunctionsJacobian = false;
    };

    struct FitSpec {
        std::vector<QuantLib::Real> t, blackVols;
        std::array<QuantLib::Real, 4> guess{};
        std::array<bool, 4> fixed{};
        bool vegaWeighted = false;
        std::optional<EndCriteriaSpec> endCriteria;
        std::optional<MethodSpec> method;
    };

    inline FitSpec fit(const json::value& v) {
        const std::string what = "an abcd fit spec";
        const json::object& o = object(v, what);
        onlyKeys(o, {"t", "blackVols", "guess", "fixed", "vegaWeighted", "endCriteria", "method"}, what);
        FitSpec s;
        s.t = reals(key(o, "t", what));
        s.blackVols = reals(key(o, "blackVols", what));
        const json::value& guess = key(o, "guess", what);
        const json::value& fixed = key(o, "fixed", what);
        if (!guess.is_array() || guess.get_array().size() != 4 || !fixed.is_array() || fixed.get_array().size() != 4)
            throw InvalidParams("an abcd fit spec takes four guesses and four fixed flags");
        for (std::size_t i = 0; i < 4; ++i) {
            s.guess[i] = real(guess.get_array()[i]);
            s.fixed[i] = boolean(fixed.get_array()[i]);
        }
        s.vegaWeighted = boolean(key(o, "vegaWeighted", what));
        const json::value& ec = key(o, "endCriteria", what);
        if (!ec.is_null()) {
            const json::object& e = object(ec, "end criteria");
            onlyKeys(e, {"maxIterations", "maxStationaryStateIterations", "rootEpsilon", "functionEpsilon",
                         "gradientNormEpsilon"}, "end criteria");
            s.endCriteria = EndCriteriaSpec{
                static_cast<QuantLib::Size>(integer(key(e, "maxIterations", "end criteria"))),
                static_cast<QuantLib::Size>(integer(key(e, "maxStationaryStateIterations", "end criteria"))),
                real(key(e, "rootEpsilon", "end criteria")), real(key(e, "functionEpsilon", "end criteria")),
                real(key(e, "gradientNormEpsilon", "end criteria"))};
        }
        const json::value& m = key(o, "method", what);
        if (!m.is_null()) {
            const json::object& mo = object(m, "a method spec");
            MethodSpec ms;
            ms.type = text(key(mo, "type", "a method spec"));
            if (ms.type == "LevenbergMarquardt") {
                onlyKeys(mo, {"type", "epsfcn", "xtol", "gtol", "useCostFunctionsJacobian"}, "a method spec");
                ms.epsfcn = real(key(mo, "epsfcn", "a method spec"));
                ms.xtol = real(key(mo, "xtol", "a method spec"));
                ms.gtol = real(key(mo, "gtol", "a method spec"));
                ms.useCostFunctionsJacobian = boolean(key(mo, "useCostFunctionsJacobian", "a method spec"));
            } else if (ms.type == "Simplex") {
                onlyKeys(mo, {"type", "lambda"}, "a method spec");
                ms.lambda = real(key(mo, "lambda", "a method spec"));
            } else {
                throw InvalidParams("unknown method " + ms.type);
            }
            s.method = ms;
        }
        return s;
    }

    inline QuantLib::ext::shared_ptr<QuantLib::EndCriteria> makeEndCriteria(const FitSpec& s) {
        if (!s.endCriteria)
            return nullptr;
        const EndCriteriaSpec& e = *s.endCriteria;
        return QuantLib::ext::make_shared<QuantLib::EndCriteria>(
            e.maxIterations, e.maxStationaryStateIterations, e.rootEpsilon, e.functionEpsilon, e.gradientNormEpsilon);
    }

    inline QuantLib::ext::shared_ptr<QuantLib::OptimizationMethod> makeMethod(const FitSpec& s) {
        if (!s.method)
            return nullptr;
        const MethodSpec& m = *s.method;
        if (m.type == "Simplex")
            return QuantLib::ext::make_shared<QuantLib::Simplex>(m.lambda);
        return QuantLib::ext::make_shared<QuantLib::LevenbergMarquardt>(m.epsfcn, m.xtol, m.gtol,
                                                                         m.useCostFunctionsJacobian);
    }

    inline json::value realArray(const std::vector<QuantLib::Real>& v) {
        json::array out;
        for (QuantLib::Real x : v)
            out.emplace_back(x);
        return json::value(std::move(out));
    }

}

#endif
