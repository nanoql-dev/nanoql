// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/settings.hpp, and the other globals a vector case sets (docs/porting-playbook.md §7.2): the four Settings values,
// the at-par switch and the stored fixings, as tools/vectors/vectors.py's settings() sets them.

#include "convert.hpp"

#include <ql/cashflows/iborcoupon.hpp>
#include <ql/index.hpp>
#include <ql/indexes/indexmanager.hpp>
#include <ql/settings.hpp>
#include <ql/time/calendars/nullcalendar.hpp>

#include <utility>

using namespace QuantLib;

namespace harness {

    namespace {

        // An index named by a key of a case's fixings, which accepts every date. QuantLib keys stored histories by
        // name, so the case's own index of that name finds them.
        class FixingsIndex : public Index {
          public:
            explicit FixingsIndex(std::string name) : name_(std::move(name)) {}
            std::string name() const override { return name_; }
            Calendar fixingCalendar() const override { return NullCalendar(); }
            bool isValidFixingDate(const Date&) const override { return true; }
            Real fixing(const Date&, bool) const override {
                QL_FAIL("the index of a case's fixings forecasts nothing");
            }

          private:
            std::string name_;
        };

        struct FixingsSpec {
            std::string name;
            std::vector<DateSpec> dates;
            std::vector<Real> values;
        };

        // {name: [[serial, value], ...]}, or null for none.
        std::vector<FixingsSpec> fixings(const json::value& v) {
            const std::string expected = "expected fixings as {name: [[serial, value], ...]}, got ";
            std::vector<FixingsSpec> out;
            if (v.is_null())
                return out;
            if (!v.is_object())
                throw InvalidParams(expected + json::serialize(v));
            for (const auto& kv : v.as_object()) {
                FixingsSpec f{std::string(kv.key()), {}, {}};
                if (!kv.value().is_array())
                    throw InvalidParams(expected + json::serialize(v));
                for (const auto& pair : kv.value().as_array()) {
                    if (!pair.is_array() || pair.as_array().size() != 2)
                        throw InvalidParams(expected + json::serialize(v));
                    f.dates.push_back(date(pair.as_array()[0]));
                    f.values.push_back(real(pair.as_array()[1]));
                }
                out.push_back(std::move(f));
            }
            return out;
        }

        const Register unit({
            // Settings(evaluationDate, includeReferenceDateEvents, includeTodaysCashFlows,
            //          enforcesTodaysHistoricFixings[, useAtParCoupons, fixings])
            {"Settings",
             [](const Args& a) -> Call {
                 if (a.size() != 4)
                     arity(a, 6);
                 DateSpec today = date(a[0]);
                 bool referenceDateEvents = boolean(a[1]);
                 std::optional<bool> todaysCashFlows = optionalBoolean(a[2]);
                 bool historicFixings = boolean(a[3]);
                 std::optional<bool> atParCoupons = a.size() == 6 ? optionalBoolean(a[4]) : std::nullopt;
                 std::vector<FixingsSpec> stored = a.size() == 6 ? fixings(a[5]) : std::vector<FixingsSpec>();
                 return [=] {
                     Settings& s = Settings::instance();
                     s.evaluationDate() = makeDate(today);
                     s.includeReferenceDateEvents() = referenceDateEvents;
                     s.includeTodaysCashFlows() = todaysCashFlows;
                     s.enforcesTodaysHistoricFixings() = historicFixings;
                     if (atParCoupons) {
                         if (*atParCoupons)
                             IborCoupon::Settings::instance().createAtParCoupons();
                         else
                             IborCoupon::Settings::instance().createIndexedCoupons();
                     }
                     for (const auto& f : stored) {
                         std::vector<Date> dates;
                         for (const auto& d : f.dates)
                             dates.push_back(makeDate(d));
                         FixingsIndex(f.name).addFixings(dates.begin(), dates.end(), f.values.begin());
                     }
                     return json::value(nullptr);
                 };
             }},
            // After a case: the at-par switch back to the wheel's default, and no stored fixings.
            {"Settings::clearCaseGlobals",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] {
                     IborCoupon::Settings::instance().createAtParCoupons();
                     IndexManager::instance().clearHistories();
                     return json::value(nullptr);
                 };
             }},
            {"IndexManager::histories",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] {
                     json::array names;
                     for (const auto& n : IndexManager::instance().histories())
                         names.emplace_back(n);
                     return json::value(std::move(names));
                 };
             }},
            {"IborCoupon::Settings::usingAtParCoupons",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] { return json::value(IborCoupon::Settings::instance().usingAtParCoupons()); };
             }},
        });

    }

}
