// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/timebasket.hpp: the members the wheel doesn't bind. "TimeBasket::plus" and "TimeBasket::minus" take
// two baskets, each (dates, values), and return the first after += or -= the second, [dates, values] in date order;
// "TimeBasket::hasDate" takes a basket and a date and returns hasDate.

#include "convert.hpp"

#include <ql/cashflows/timebasket.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        TimeBasket basket(const json::value& dates, const json::value& values) {
            std::vector<Date> d;
            for (Integer serial : integers(dates))
                d.emplace_back(serial);
            return {d, reals(values)};
        }

        json::value items(const TimeBasket& b) {
            json::array dates, values;
            for (const auto& item : b) {
                dates.push_back(result(item.first));
                values.emplace_back(item.second);
            }
            json::array out;
            out.emplace_back(dates);
            out.emplace_back(values);
            return out;
        }

        const Register unit({
            // (dates, values, other dates, other values)
            {"TimeBasket::plus",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 json::value d1 = a[0], v1 = a[1], d2 = a[2], v2 = a[3];
                 return [=] {
                     TimeBasket b = basket(d1, v1);
                     b += basket(d2, v2);
                     return items(b);
                 };
             }},
            {"TimeBasket::minus",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 json::value d1 = a[0], v1 = a[1], d2 = a[2], v2 = a[3];
                 return [=] {
                     TimeBasket b = basket(d1, v1);
                     b -= basket(d2, v2);
                     return items(b);
                 };
             }},
            // (dates, values, date)
            {"TimeBasket::hasDate",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 json::value d = a[0], v = a[1];
                 DateSpec probe = date(a[2]);
                 return [=] { return json::value(basket(d, v).hasDate(makeDate(probe))); };
             }},
        });

    }

}
