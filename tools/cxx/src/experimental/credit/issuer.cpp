// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/issuer.hpp, .cpp, which the wheel doesn't expose, on experimental/credit/portfoliospec.hpp's
// specs, at the evaluation date the settings set:
//
//     Issuer::defaultProbability (issuer, key, dates): the key's curve's default probabilities at the dates
//     Issuer::defaultedBetween   (issuer, start, end, key, includeRefDate): the event's date, or null for none
//     Issuer::defaultsBetween    (issuer, start, end, key, includeRefDate): the events' dates
//     Issuer::events             (issuer): the event set's dates, in its order

#include "experimental/credit/portfoliospec.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        using credit::portfolio::makeIssuer;
        using credit::portfolio::makeKey;

        const Register unit({
            {"Issuer::defaultProbability",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 json::value i = a[0], k = a[1], ds = a[2];
                 return [=] {
                     Issuer issuer = makeIssuer(i);
                     const Handle<DefaultProbabilityTermStructure>& c = issuer.defaultProbability(makeKey(k));
                     json::array out;
                     for (const json::value& d : ds.as_array())
                         out.emplace_back(c->defaultProbability(makeDate(date(d))));
                     return json::value(std::move(out));
                 };
             }},
            {"Issuer::defaultedBetween",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 json::value i = a[0], k = a[3];
                 DateSpec s = date(a[1]), e = date(a[2]);
                 bool include = boolean(a[4]);
                 return [=] {
                     ext::shared_ptr<DefaultEvent> x =
                         makeIssuer(i).defaultedBetween(makeDate(s), makeDate(e), makeKey(k), include);
                     return x ? result(x->date()) : json::value(nullptr);
                 };
             }},
            {"Issuer::defaultsBetween",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 json::value i = a[0], k = a[3];
                 DateSpec s = date(a[1]), e = date(a[2]);
                 bool include = boolean(a[4]);
                 return [=] {
                     json::array out;
                     for (const auto& x : makeIssuer(i).defaultsBetween(makeDate(s), makeDate(e), makeKey(k), include))
                         out.push_back(result(x->date()));
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
