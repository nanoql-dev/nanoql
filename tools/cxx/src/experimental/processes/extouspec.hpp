// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The energy processes' specs for the harness's experimental files:
//
//     an extended OU process  {"speed", "sigma", "x0", "level", "discretization", "intEps"}: the level
//                             {"type": "constant", "b"} or {"type": "linear", "c0", "c1"} (c0 + c1 t), the
//                             discretization an ExtendedOrnsteinUhlenbeckProcess::Discretization, "intEps" the
//                             Gauss-Lobatto rule's accuracy
//     a jump process          {"ou", "y0", "beta", "jumpIntensity", "eta"}
//     a Kluge-OU process      {"kluge", "gas", "rho"}

#pragma once

#include "convert.hpp"

#include <ql/experimental/processes/extendedornsteinuhlenbeckprocess.hpp>
#include <ql/experimental/processes/extouwithjumpsprocess.hpp>
#include <ql/experimental/processes/klugeextouprocess.hpp>

#include <functional>
#include <string>

namespace harness::processes {

    inline const json::value& field(const json::value& v, const char* name, const char* what) {
        if (!v.is_object())
            throw InvalidParams(std::string("expected ") + what + ", got " + json::serialize(v));
        const json::value* f = v.get_object().if_contains(name);
        if (f == nullptr)
            throw InvalidParams(std::string(what) + " needs " + name);
        return *f;
    }

    inline std::function<QuantLib::Real(QuantLib::Real)> level(const json::value& v) {
        const std::string type = text(field(v, "type", "a level"));
        if (type == "constant") {
            const QuantLib::Real b = real(field(v, "b", "a constant level"));
            return [=](QuantLib::Real) { return b; };
        }
        if (type == "linear") {
            const QuantLib::Real c0 = real(field(v, "c0", "a linear level"));
            const QuantLib::Real c1 = real(field(v, "c1", "a linear level"));
            return [=](QuantLib::Real t) { return c0 + c1 * t; };
        }
        throw InvalidParams("unknown level " + type);
    }

    inline QuantLib::ext::shared_ptr<QuantLib::ExtendedOrnsteinUhlenbeckProcess> extendedOU(const json::value& v) {
        const char* what = "an extended OU process";
        return QuantLib::ext::make_shared<QuantLib::ExtendedOrnsteinUhlenbeckProcess>(
            real(field(v, "speed", what)), real(field(v, "sigma", what)), real(field(v, "x0", what)),
            level(field(v, "level", what)),
            enumeration<QuantLib::ExtendedOrnsteinUhlenbeckProcess::Discretization>(field(v, "discretization", what)),
            real(field(v, "intEps", what)));
    }

    inline QuantLib::ext::shared_ptr<QuantLib::ExtOUWithJumpsProcess> jumps(const json::value& v) {
        const char* what = "a jump process";
        return QuantLib::ext::make_shared<QuantLib::ExtOUWithJumpsProcess>(
            extendedOU(field(v, "ou", what)), real(field(v, "y0", what)), real(field(v, "beta", what)),
            real(field(v, "jumpIntensity", what)), real(field(v, "eta", what)));
    }

    inline QuantLib::ext::shared_ptr<QuantLib::KlugeExtOUProcess> klugeOU(const json::value& v) {
        const char* what = "a Kluge-OU process";
        return QuantLib::ext::make_shared<QuantLib::KlugeExtOUProcess>(
            real(field(v, "rho", what)), jumps(field(v, "kluge", what)), extendedOU(field(v, "gas", what)));
    }

}
