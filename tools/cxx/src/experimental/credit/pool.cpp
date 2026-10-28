// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/pool.hpp, .cpp, which the wheel doesn't expose, on experimental/credit/portfoliospec.hpp's
// pool spec:
//
//     Pool::inspect  (pool): [size, names, [key summary of defaultKeys()...]]
//     Pool::has      (pool, name)
//     Pool::get      (pool, name, key, date): the name's issuer's default probability on the key's curve at the date
//     Pool::defaultKey (pool, name): its key's summary
//     Pool::getTime  (pool, name, [[name, time]...]): its time after the setTime calls

#include "experimental/credit/portfoliospec.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        using credit::portfolio::keySummary;
        using credit::portfolio::makeKey;
        using credit::portfolio::makePool;

        const Register unit({
            {"Pool::inspect",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 json::value p = a[0];
                 return [=] {
                     ext::shared_ptr<Pool> pool = makePool(p);
                     json::array names, keys;
                     for (const std::string& n : pool->names())
                         names.emplace_back(n);
                     for (const DefaultProbKey& k : pool->defaultKeys())
                         keys.push_back(keySummary(k));
                     json::array out;
                     out.emplace_back(static_cast<std::int64_t>(pool->size()));
                     out.emplace_back(std::move(names));
                     out.emplace_back(std::move(keys));
                     return json::value(std::move(out));
                 };
             }},
            {"Pool::has",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value p = a[0];
                 std::string n = text(a[1]);
                 return [=] { return json::value(makePool(p)->has(n)); };
             }},
            {"Pool::get",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 json::value p = a[0], k = a[2];
                 std::string n = text(a[1]);
                 DateSpec d = date(a[3]);
                 return [=] {
                     return json::value(
                         makePool(p)->get(n).defaultProbability(makeKey(k))->defaultProbability(makeDate(d)));
                 };
             }},
            {"Pool::defaultKey",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value p = a[0];
                 std::string n = text(a[1]);
                 return [=] { return keySummary(makePool(p)->defaultKey(n)); };
             }},
            {"Pool::getTime",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 json::value p = a[0], sets = a[2];
                 std::string n = text(a[1]);
                 return [=] {
                     ext::shared_ptr<Pool> pool = makePool(p);
                     for (const json::value& s : sets.as_array())
                         pool->setTime(std::string(s.as_array()[0].as_string()), real(s.as_array()[1]));
                     return json::value(pool->getTime(n));
                 };
             }},
        });

    }

}
