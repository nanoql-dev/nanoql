// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/inflation/genericindexes.hpp: the generic region and its zero-inflation and year-on-year indexes,
// which the wheel doesn't bind. "GenericIndexes::values" takes (frequency, revised, lag, currency): QuantLib's integer
// frequency, the revised flag, the availability lag [length, units] and a currency code (EUR, USD or GBP); it builds a
// GenericCPI and a YYGenericCPI on empty curves and returns {"cpi", "yy"}, each [name, familyName, frequency, revised,
// lag length, lag units, region name, region code, currency code].

#include "convert.hpp"

#include <ql/currencies/america.hpp>
#include <ql/currencies/europe.hpp>
#include <ql/experimental/inflation/genericindexes.hpp>

#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        Currency currency(const std::string& code) {
            if (code == "EUR")
                return EURCurrency();
            if (code == "USD")
                return USDCurrency();
            if (code == "GBP")
                return GBPCurrency();
            throw InvalidParams("a currency is EUR, USD or GBP");
        }

        json::array describe(const InflationIndex& i) {
            return json::array{i.name(),
                               i.familyName(),
                               static_cast<int>(i.frequency()),
                               i.revised(),
                               i.availabilityLag().length(),
                               static_cast<int>(i.availabilityLag().units()),
                               i.region().name(),
                               i.region().code(),
                               i.currency().code()};
        }

        const Register unit({
            // (frequency, revised, lag, currency)
            {"GenericIndexes::values",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 auto frequency = enumeration<Frequency>(a[0]);
                 bool revised = boolean(a[1]);
                 if (!a[2].is_array() || a[2].as_array().size() != 2)
                     throw InvalidParams("a lag is [length, units]");
                 Integer length = integer(a[2].as_array()[0]);
                 auto units = enumeration<TimeUnit>(a[2].as_array()[1]);
                 std::string code = text(a[3]);
                 return [=] {
                     GenericCPI cpi(frequency, revised, Period(length, units), currency(code));
                     YYGenericCPI yy(frequency, revised, Period(length, units), currency(code));
                     json::object out;
                     out["cpi"] = describe(cpi);
                     out["yy"] = describe(yy);
                     return json::value(out);
                 };
             }},
        });

    }

}
