// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/rounding.hpp: the members of Rounding the wheel doesn't expose (the constructor from a precision, a type
// and a digit; precision, type and roundingDigit), and operator() over a rounding so built, each on a rounding the
// call builds from its spec (roundingspec.hpp). A default Rounding() leaves its precision and digit uninitialized
// (rounding.hpp:87, 89), so the harness refuses to read them.

#include "roundingspec.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a Rounding spec needs ") + name);
            return *v;
        }

        // A rounding built with a precision, a type and a digit; Rounding() has no precision or digit to read.
        RoundingSpec explicitRounding(const json::value& v, const char* member) {
            RoundingSpec s = rounding(v);
            if (s.isDefault)
                throw InvalidParams(std::string("Rounding() leaves its ") + member + " uninitialized");
            return s;
        }

        const Register unit({
            {"Rounding::operator()",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 RoundingSpec s = rounding(a[0]);
                 Decimal value = real(a[1]);
                 return [=] { return json::value(makeRounding(s)(value)); };
             }},
            {"Rounding::precision",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 RoundingSpec s = explicitRounding(a[0], "precision");
                 return [=] { return json::value(makeRounding(s).precision()); };
             }},
            {"Rounding::type",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 RoundingSpec s = rounding(a[0]);
                 return [=] { return json::value(static_cast<int>(makeRounding(s).type())); };
             }},
            {"Rounding::roundingDigit",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 RoundingSpec s = explicitRounding(a[0], "rounding digit");
                 return [=] { return json::value(makeRounding(s).roundingDigit()); };
             }},
        });

    }

    RoundingSpec rounding(const json::value& v) {
        if (v.is_null())
            return {true, 0, Rounding::None, 0};
        if (!v.is_object())
            throw InvalidParams("expected a Rounding spec, got " + json::serialize(v));
        const json::object& o = v.get_object();
        for (const auto& kv : o) {
            if (kv.key() != "precision" && kv.key() != "type" && kv.key() != "digit")
                throw InvalidParams("Rounding takes no key " + std::string(kv.key()));
        }
        const json::value& p = key(o, "precision");
        if (p.is_null())
            throw InvalidParams("a Rounding spec needs a precision");
        Integer precision = integer(p);
        if (precision < 0 || precision > 16)
            throw InvalidParams("a rounding precision outside [0, 16], whose result QuantLib leaves undefined: " +
                                std::to_string(precision));
        const json::value& d = key(o, "digit");
        if (d.is_null())
            throw InvalidParams("a Rounding spec needs a digit");
        return {false, precision, enumeration<Rounding::Type>(key(o, "type")), integer(d)};
    }

    Rounding makeRounding(const RoundingSpec& s) {
        if (s.isDefault)
            return {};
        return Rounding(s.precision, s.type, s.digit);
    }

}
