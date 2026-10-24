// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The digital coupon specs (cashflows/digitalspec.hpp).

#include "cashflows/digitalspec.hpp"

#include <ql/cashflows/fixedratecoupon.hpp>
#include <ql/utilities/null.hpp>

#include <exception>
#include <initializer_list>
#include <string>

using namespace QuantLib;

namespace harness::digital {

    namespace {

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " needs " + name);
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

        const json::object& object(const json::value& v, const std::string& what) {
            if (!v.is_object())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            return v.get_object();
        }

        json::value rateOrNull(Rate x) { return x == Null<Rate>() ? json::value(nullptr) : json::value(x); }

        template <class F>
        json::value attempt(F f) {
            try {
                return json::value(f());
            } catch (const std::exception&) {
                json::object o;
                o["throws"] = true;
                return o;
            }
        }

    }

    ReplicationSpec replication(const json::value& v) {
        ReplicationSpec s;
        if (v.is_null())
            return s;
        const std::string what = "a replication";
        const json::object& o = object(v, what);
        onlyKeys(o, {"type", "gap"}, what);
        s.null = false;
        s.type = enumeration<Replication::Type>(key(o, "type", what));
        s.gap = real(key(o, "gap", what));
        return s;
    }

    TermsSpec terms(const json::value& v) {
        const std::string what = "digital terms";
        const json::object& o = object(v, what);
        onlyKeys(o,
                 {"callStrike", "callPosition", "isCallATMIncluded", "callDigitalPayoff", "putStrike", "putPosition",
                  "isPutATMIncluded", "putDigitalPayoff", "replication", "nakedOption"},
                 what);
        TermsSpec s;
        s.callStrike = real(key(o, "callStrike", what));
        s.callPosition = enumeration<Position::Type>(key(o, "callPosition", what));
        s.isCallATMIncluded = boolean(key(o, "isCallATMIncluded", what));
        s.callDigitalPayoff = real(key(o, "callDigitalPayoff", what));
        s.putStrike = real(key(o, "putStrike", what));
        s.putPosition = enumeration<Position::Type>(key(o, "putPosition", what));
        s.isPutATMIncluded = boolean(key(o, "isPutATMIncluded", what));
        s.putDigitalPayoff = real(key(o, "putDigitalPayoff", what));
        s.replication = replication(key(o, "replication", what));
        s.nakedOption = boolean(key(o, "nakedOption", what));
        return s;
    }

    LegTermsSpec legTerms(const json::object& o, const std::string& what) {
        LegTermsSpec s;
        s.callStrikes = reals(key(o, "callStrikes", what));
        s.longCallOption = enumeration<Position::Type>(key(o, "longCallOption", what));
        s.callATM = boolean(key(o, "callATM", what));
        s.callPayoffs = reals(key(o, "callPayoffs", what));
        s.putStrikes = reals(key(o, "putStrikes", what));
        s.longPutOption = enumeration<Position::Type>(key(o, "longPutOption", what));
        s.putATM = boolean(key(o, "putATM", what));
        s.putPayoffs = reals(key(o, "putPayoffs", what));
        s.replication = replication(key(o, "replication", what));
        s.nakedOption = boolean(key(o, "nakedOption", what));
        return s;
    }

    ext::shared_ptr<DigitalReplication> makeReplication(const ReplicationSpec& s) {
        if (s.null)
            return {};
        return ext::make_shared<DigitalReplication>(s.type, s.gap);
    }

    json::value inspectors(const DigitalCoupon& c) {
        json::object o;
        o["digital"] = true;
        o["callStrike"] = rateOrNull(c.callStrike());
        o["putStrike"] = rateOrNull(c.putStrike());
        o["callDigitalPayoff"] = rateOrNull(c.callDigitalPayoff());
        o["putDigitalPayoff"] = rateOrNull(c.putDigitalPayoff());
        o["hasCall"] = c.hasCall();
        o["hasPut"] = c.hasPut();
        o["hasCollar"] = c.hasCollar();
        o["isLongCall"] = c.isLongCall();
        o["isLongPut"] = c.isLongPut();
        o["date"] = result(c.date());
        o["accrualStartDate"] = result(c.accrualStartDate());
        o["accrualEndDate"] = result(c.accrualEndDate());
        o["fixingDate"] = result(c.fixingDate());
        o["nominal"] = c.nominal();
        o["gearing"] = c.gearing();
        o["spread"] = c.spread();
        return o;
    }

    json::value rates(const DigitalCoupon& c) {
        json::object o;
        o["rate"] = attempt([&] { return c.rate(); });
        o["callOptionRate"] = attempt([&] { return c.callOptionRate(); });
        o["putOptionRate"] = attempt([&] { return c.putOptionRate(); });
        return o;
    }

    json::value legOutputs(const Leg& leg, bool withRates) {
        json::array out;
        for (const auto& cf : leg) {
            if (auto d = ext::dynamic_pointer_cast<DigitalCoupon>(cf)) {
                json::object o = inspectors(*d).get_object();
                if (withRates) {
                    // held for the loop: a range over a temporary's member would dangle
                    json::value r = rates(*d);
                    for (const auto& kv : r.get_object())
                        o[kv.key()] = kv.value();
                }
                out.emplace_back(std::move(o));
            } else {
                auto f = ext::dynamic_pointer_cast<FixedRateCoupon>(cf);
                json::object o;
                o["digital"] = false;
                o["date"] = result(cf->date());
                o["rate"] = f != nullptr ? attempt([&] { return f->rate(); }) : json::value(nullptr);
                out.emplace_back(std::move(o));
            }
        }
        return out;
    }

}
