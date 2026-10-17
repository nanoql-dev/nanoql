// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "convert.hpp"

#include <ql/time/daycounters/actual360.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>
#include <ql/time/daycounters/actualactual.hpp>
#include <ql/time/daycounters/thirty360.hpp>
#include <ql/utilities/null.hpp>

#include <cstdio>
#include <cstdlib>

using namespace QuantLib;

namespace harness {

    namespace {

        std::map<std::string, Method>& registry() {
            static std::map<std::string, Method> m;
            return m;
        }

        std::string shown(const json::value& v) { return json::serialize(v); }

        // Integers a double holds exactly.
        constexpr long long exactInDouble = 1LL << 53;

    }

    Register::Register(std::initializer_list<std::pair<const char*, Method>> methods) {
        for (const auto& [name, method] : methods) {
            if (!registry().emplace(name, method).second) {
                std::fprintf(stderr, "cxx-harness: the method %s is registered twice\n", name);
                std::_Exit(70);
            }
        }
    }

    const std::map<std::string, Method>& methods() { return registry(); }

    void arity(const Args& a, std::size_t n) {
        if (a.size() != n)
            throw InvalidParams("expected " + std::to_string(n) + " arguments, got " + std::to_string(a.size()));
    }

    Real real(const json::value& v) {
        if (v.is_null())
            return Null<Real>();
        if (v.is_double())
            return v.get_double();
        if (v.is_int64() && v.get_int64() >= -exactInDouble && v.get_int64() <= exactInDouble)
            return static_cast<Real>(v.get_int64());
        throw InvalidParams("expected a real, got " + shown(v));
    }

    Integer integer(const json::value& v) {
        if (v.is_null())
            return Null<Integer>();
        if (!v.is_int64() || v.get_int64() < INT_MIN || v.get_int64() > INT_MAX)
            throw InvalidParams("expected an Integer, got " + shown(v));
        return static_cast<Integer>(v.get_int64());
    }

    bool boolean(const json::value& v) {
        if (!v.is_bool())
            throw InvalidParams("expected a boolean, got " + shown(v));
        return v.get_bool();
    }

    std::optional<bool> optionalBoolean(const json::value& v) {
        if (v.is_null())
            return std::nullopt;
        return boolean(v);
    }

    std::string text(const json::value& v) {
        if (!v.is_string())
            throw InvalidParams("expected a string, got " + shown(v));
        return std::string(v.get_string());
    }

    std::vector<Real> reals(const json::value& v) {
        if (!v.is_array())
            throw InvalidParams("expected an array of reals, got " + shown(v));
        std::vector<Real> out;
        for (const json::value& x : v.get_array())
            out.push_back(real(x));
        return out;
    }

    DateSpec date(const json::value& v) {
        if (v.is_null())
            return {true, 0};
        if (!v.is_int64() || v.get_int64() < INT_FAST32_MIN || v.get_int64() > INT_FAST32_MAX)
            throw InvalidParams("expected a date serial, got " + shown(v));
        return {false, static_cast<Date::serial_type>(v.get_int64())};
    }

    namespace {

        // The day counters the harness knows, and whether each takes a convention.
        const std::map<std::string, bool>& dayCounterTypes() {
            static const std::map<std::string, bool> types = {
                {"Actual360", false}, {"Actual365Fixed", false}, {"ActualActual", true}, {"Thirty360", true}};
            return types;
        }

    }

    DayCounterSpec dayCounter(const json::value& v) {
        if (!v.is_object())
            throw InvalidParams("expected a day counter spec, got " + shown(v));
        const json::object& o = v.get_object();
        const json::value* type = o.if_contains("type");
        if (type == nullptr || !type->is_string())
            throw InvalidParams("a day counter spec needs a type: " + shown(v));
        const std::string t(type->get_string());
        auto known = dayCounterTypes().find(t);
        if (known == dayCounterTypes().end())
            throw InvalidParams("unknown day counter " + t);
        const bool takesConvention = known->second;
        for (const auto& kv : o) {
            if (kv.key() != "type" && !(takesConvention && kv.key() == "convention"))
                throw InvalidParams(t + " takes no key " + std::string(kv.key()));
        }
        if (!takesConvention)
            return {t, 0};
        const json::value* convention = o.if_contains("convention");
        if (convention == nullptr)
            throw InvalidParams(t + " needs a convention");
        return {t, enumeration<int>(*convention)};
    }

    Date makeDate(const DateSpec& d) { return d.null ? Date() : Date(d.serial); }

    DayCounter makeDayCounter(const DayCounterSpec& d) {
        if (d.type == "Actual360")
            return Actual360();
        if (d.type == "Actual365Fixed")
            return Actual365Fixed();
        if (d.type == "ActualActual")
            return ActualActual(static_cast<ActualActual::Convention>(d.convention));
        if (d.type == "Thirty360")
            return Thirty360(static_cast<Thirty360::Convention>(d.convention));
        throw InvalidParams("the day counter " + d.type + " was not checked");
    }

    json::value result(const Date& d) {
        if (d == Date())
            return nullptr;
        return static_cast<std::int64_t>(d.serialNumber());
    }

}
