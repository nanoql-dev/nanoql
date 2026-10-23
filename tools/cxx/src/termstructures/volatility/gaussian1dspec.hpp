// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The specs the Gaussian1d smile section and swaption volatility methods share
// (ql/termstructures/volatility/gaussian1dsmilesection.hpp, swaption/gaussian1dswaptionvolatility.hpp): a swap index
// {"type": "EuriborSwapIsdaFixA", "tenor" ([length, units]), "forwarding", "discounting"}, its curves those of
// indexes/indexspec.hpp, null for QuantLib's empty handle, built with the constructor taking both; and an engine, null
// for the section's default, or {"integrationPoints", "stddevs", "extrapolatePayoff", "discountCurve"}
// (models/shortrate/onefactormodels/gsrspec.hpp's discount spec, null for the model's curve), a
// Gaussian1dSwaptionEngine on the model.

#ifndef NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_GAUSSIAN1DSPEC_HPP
#define NANOQL_CXX_TERMSTRUCTURES_VOLATILITY_GAUSSIAN1DSPEC_HPP

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "models/shortrate/onefactormodels/gsrspec.hpp"
#include "termstructures/volatility/optionlet/optionletvolspec.hpp"

#include <ql/indexes/swap/euriborswap.hpp>
#include <ql/pricingengines/swaption/gaussian1dswaptionengine.hpp>

#include <initializer_list>
#include <optional>
#include <string>

namespace harness::gaussian1d {

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

    struct SwapIndexSpec {
        termstructures::PeriodSpec tenor{1, QuantLib::Years};
        indexes::CurveSpec forwarding, discounting;
    };

    inline SwapIndexSpec swapIndex(const json::value& v) {
        const std::string what = "a swap index";
        if (!v.is_object())
            throw InvalidParams("expected " + what + ", got " + json::serialize(v));
        const json::object& o = v.get_object();
        onlyKeys(o, {"type", "tenor", "forwarding", "discounting"}, what);
        if (key(o, "type", what) != "EuriborSwapIsdaFixA")
            throw InvalidParams("unknown swap index " + json::serialize(o.at("type")));
        return {termstructures::tenor(key(o, "tenor", what)), indexes::curve(key(o, "forwarding", what)),
                indexes::curve(key(o, "discounting", what))};
    }

    inline QuantLib::ext::shared_ptr<QuantLib::SwapIndex> makeSwapIndex(const SwapIndexSpec& s) {
        return QuantLib::ext::make_shared<QuantLib::EuriborSwapIsdaFixA>(
            termstructures::makePeriod(s.tenor), indexes::makeCurve(s.forwarding), indexes::makeCurve(s.discounting));
    }

    struct EngineSpec {
        QuantLib::Integer integrationPoints = 64;
        QuantLib::Real stddevs = 7.0;
        bool extrapolatePayoff = true;
        std::optional<DiscountSpec> discountCurve;
    };

    inline std::optional<EngineSpec> engine(const json::value& v) {
        if (v.is_null())
            return std::nullopt;
        const std::string what = "an engine spec";
        if (!v.is_object())
            throw InvalidParams("expected " + what + ", got " + json::serialize(v));
        const json::object& o = v.get_object();
        onlyKeys(o, {"integrationPoints", "stddevs", "extrapolatePayoff", "discountCurve"}, what);
        EngineSpec e;
        e.integrationPoints = integer(key(o, "integrationPoints", what));
        e.stddevs = real(key(o, "stddevs", what));
        e.extrapolatePayoff = boolean(key(o, "extrapolatePayoff", what));
        const json::value& d = key(o, "discountCurve", what);
        if (!d.is_null())
            e.discountCurve = discount(d);
        return e;
    }

    // The engine a spec describes, or none for the section's default.
    inline QuantLib::ext::shared_ptr<QuantLib::Gaussian1dSwaptionEngine>
    makeEngine(const std::optional<EngineSpec>& s, const QuantLib::ext::shared_ptr<QuantLib::Gaussian1dModel>& model) {
        if (!s)
            return nullptr;
        QuantLib::Handle<QuantLib::YieldTermStructure> curve;
        if (s->discountCurve)
            curve = makeDiscount(*s->discountCurve);
        return QuantLib::ext::make_shared<QuantLib::Gaussian1dSwaptionEngine>(
            model, s->integrationPoints, s->stddevs, s->extrapolatePayoff, false, curve);
    }

}

#endif
