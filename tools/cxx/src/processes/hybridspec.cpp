// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "processes/hybridspec.hpp"

#include <ql/processes/hullwhiteprocess.hpp>

#include <initializer_list>
#include <string>

using namespace QuantLib;

namespace harness::hybrid {

    namespace {

        const json::object& object(const json::value& v, const std::string& what) {
            if (!v.is_object())
                throw InvalidParams("expected " + what + " spec, got " + json::serialize(v));
            return v.get_object();
        }

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        void onlyKeys(const json::object& o, std::initializer_list<const char*> keys, const std::string& what) {
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : keys)
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
        }

    }

    ProcessSpec process(const json::value& v) {
        const std::string what = "a hybrid process";
        const json::object& o = object(v, what);
        onlyKeys(o, {"heston", "a", "sigma", "forwardMeasureTime", "correlation", "discretization"}, what);
        ProcessSpec s;
        s.heston = equity::process(key(o, "heston", what));
        s.a = real(key(o, "a", what));
        s.sigma = real(key(o, "sigma", what));
        s.forwardMeasureTime = real(key(o, "forwardMeasureTime", what));
        s.correlation = real(key(o, "correlation", what));
        s.discretization = enumeration<HybridHestonHullWhiteProcess::Discretization>(key(o, "discretization", what));
        return s;
    }

    ext::shared_ptr<HybridHestonHullWhiteProcess> makeProcess(const ProcessSpec& s) {
        ext::shared_ptr<HestonProcess> heston = equity::makeProcess(s.heston);
        auto hullWhite = ext::make_shared<HullWhiteForwardProcess>(heston->riskFreeRate(), s.a, s.sigma);
        hullWhite->setForwardMeasureTime(s.forwardMeasureTime);
        return ext::make_shared<HybridHestonHullWhiteProcess>(heston, hullWhite, s.correlation, s.discretization);
    }

}
