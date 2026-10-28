// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/catbonds/catrisk.hpp and riskynotional.hpp, which the wheel doesn't expose:
//
//     EventSet::paths (events, events start, events end, start, end): every path of the event set's simulation until
//         it runs out, each a list of [date serial, loss]
//     BetaRisk::paths (maxLoss, years, mean, stdDev, start, end, n): the first n paths of the beta risk's simulation,
//         drawn from MSVC's streams as built here, the harness's own toolchain
//     BetaRiskSimulation::betas (start, end, maxLoss, lambda, alpha, beta, n): n losses drawn by the simulation's
//         generateBeta, its gamma draws at the shapes given, on MSVC's streams
//     NotionalRisk::path (risk, events, dates): the notional path a digital risk ["Digital", threshold] or a
//         proportional one ["Proportional", attachment, exhaustion], with no payment offset, writes from the events:
//         its notional rate at each date and its loss
//
// BetaRisk's events arrive by fresh draws where nanoql cumulates them (the S-xcr plan's XR8 (xlii)), so its generator
// reads the paths up to the first where the two part.

#include "convert.hpp"

#include <ql/experimental/catbonds/catrisk.hpp>
#include <ql/experimental/catbonds/riskynotional.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        typedef std::vector<std::pair<Date, Real>> Path;

        Path events(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected the events as an array");
            Path out;
            for (const json::value& e : v.get_array()) {
                std::vector<Real> p = reals(e);
                if (p.size() != 2)
                    throw InvalidParams("expected an event as [date serial, loss]");
                out.emplace_back(Date(static_cast<Date::serial_type>(p[0])), p[1]);
            }
            return out;
        }

        json::value encoded(const Path& p) {
            json::array out;
            for (const auto& e : p)
                out.push_back(json::array{result(e.first), json::value(e.second)});
            return out;
        }

        const Register unit({
            {"EventSet::paths",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Path es = events(a[0]);
                 DateSpec eventsStart = date(a[1]), eventsEnd = date(a[2]), start = date(a[3]), end = date(a[4]);
                 return [=] {
                     EventSet set(ext::make_shared<Path>(es), makeDate(eventsStart), makeDate(eventsEnd));
                     ext::shared_ptr<CatSimulation> s = set.newSimulation(makeDate(start), makeDate(end));
                     json::array out;
                     Path p;
                     while (s->nextPath(p))
                         out.push_back(encoded(p));
                     return json::value(std::move(out));
                 };
             }},
            {"BetaRisk::paths",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 Real maxLoss = real(a[0]), years = real(a[1]), mean = real(a[2]), stdDev = real(a[3]);
                 DateSpec start = date(a[4]), end = date(a[5]);
                 Integer n = integer(a[6]);
                 return [=] {
                     BetaRisk risk(maxLoss, years, mean, stdDev);
                     ext::shared_ptr<CatSimulation> s = risk.newSimulation(makeDate(start), makeDate(end));
                     json::array out;
                     Path p;
                     for (Integer i = 0; i < n; ++i) {
                         s->nextPath(p);
                         out.push_back(encoded(p));
                     }
                     return json::value(std::move(out));
                 };
             }},
            {"BetaRiskSimulation::betas",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 DateSpec start = date(a[0]), end = date(a[1]);
                 Real maxLoss = real(a[2]), lambda = real(a[3]), alpha = real(a[4]), beta = real(a[5]);
                 Integer n = integer(a[6]);
                 return [=] {
                     BetaRiskSimulation s(makeDate(start), makeDate(end), maxLoss, lambda, alpha, beta);
                     json::array out;
                     for (Integer i = 0; i < n; ++i)
                         out.push_back(json::value(s.generateBeta()));
                     return json::value(std::move(out));
                 };
             }},
            {"NotionalRisk::path",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 if (!a[0].is_array() || a[0].get_array().empty())
                     throw InvalidParams("expected a notional risk as [type, parameters]");
                 std::string type = text(a[0].get_array()[0]);
                 std::vector<Real> params;
                 for (std::size_t i = 1; i < a[0].get_array().size(); ++i)
                     params.push_back(real(a[0].get_array()[i]));
                 if (!((type == "Digital" && params.size() == 1) || (type == "Proportional" && params.size() == 2)))
                     throw InvalidParams("unknown notional risk " + type);
                 Path es = events(a[1]);
                 std::vector<DateSpec> dates;
                 if (!a[2].is_array())
                     throw InvalidParams("expected the dates as an array");
                 for (const json::value& d : a[2].get_array())
                     dates.push_back(date(d));
                 return [=] {
                     auto offset = ext::make_shared<NoOffset>();
                     ext::shared_ptr<NotionalRisk> risk =
                         type == "Digital" ?
                             ext::shared_ptr<NotionalRisk>(ext::make_shared<DigitalNotionalRisk>(offset, params[0])) :
                             ext::shared_ptr<NotionalRisk>(
                                 ext::make_shared<ProportionalNotionalRisk>(offset, params[0], params[1]));
                     NotionalPath path;
                     risk->updatePath(es, path);
                     json::array rates;
                     for (const DateSpec& d : dates)
                         rates.push_back(json::value(path.notionalRate(makeDate(d))));
                     return json::value(json::object{{"notionalRate", rates}, {"loss", path.loss()}});
                 };
             }},
        });

    }

}
