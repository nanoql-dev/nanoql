// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/catbonds/catbond.hpp and montecarlocatbondengine.hpp, which the wheel doesn't expose: a floating
// cat bond on QuantLib's test's USD LIBOR 6M (its forecast curve flat at a rate, Actual/360) and Black coupon pricer
// without volatility, priced by the Monte Carlo engine:
//
//     FloatingCatBond::values (settlementDays, faceAmount, schedule, riskFreeRate, accrual day counter, notional risk,
//         fixingDays, spreads, redemption, issue date, cat risk, discount): the bond's cash flows' dates and amounts,
//         and the engine's NPV, settlement value, clean price, loss probability, exhaustion probability and expected
//         loss, each {"throws": true} where QuantLib refuses it. The schedule is termstructures/credit/creditspec.hpp's,
//         the notional risk catrisk.cpp's, the cat risk ["EventSet", events, start, end] or ["BetaRisk", maxLoss,
//         years, mean, stdDev], the discount a creditspec discount.

#include "termstructures/credit/creditspec.hpp"

#include <ql/cashflows/couponpricer.hpp>
#include <ql/experimental/catbonds/catbond.hpp>
#include <ql/experimental/catbonds/catrisk.hpp>
#include <ql/experimental/catbonds/montecarlocatbondengine.hpp>
#include <ql/indexes/ibor/usdlibor.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/daycounters/actual360.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        template <class F>
        json::value attempted(F f) {
            try {
                return json::value(f());
            } catch (const Error&) {
                return json::object{{"throws", true}};
            }
        }

        struct BondSpec {
            Integer settlementDays = 0, fixingDays = 0;
            Real faceAmount = 0.0, riskFreeRate = 0.0, redemption = 100.0;
            credit::ScheduleSpec schedule;
            credit::DayCounterSpec dayCounter;
            std::string notionalType, riskType;
            std::vector<Real> notionalParams, riskParams, spreads;
            std::vector<std::pair<Real, Real>> events;
            DateSpec issueDate{true, 0}, eventsStart{true, 0}, eventsEnd{true, 0};
            credit::DiscountSpec discount;
        };

        std::vector<Real> tail(const json::value& v, std::string& type) {
            if (!v.is_array() || v.get_array().empty())
                throw InvalidParams("expected [type, parameters]");
            type = text(v.get_array()[0]);
            std::vector<Real> out;
            for (std::size_t i = 1; i < v.get_array().size(); ++i)
                if (v.get_array()[i].is_number())
                    out.push_back(real(v.get_array()[i]));
            return out;
        }

        BondSpec bond(const Args& a) {
            BondSpec s;
            s.settlementDays = integer(a[0]);
            s.faceAmount = real(a[1]);
            s.schedule = credit::schedule(a[2]);
            s.riskFreeRate = real(a[3]);
            s.dayCounter = credit::dayCounter(a[4]);
            s.notionalParams = tail(a[5], s.notionalType);
            if (!((s.notionalType == "Digital" && s.notionalParams.size() == 1) ||
                  (s.notionalType == "Proportional" && s.notionalParams.size() == 2)))
                throw InvalidParams("unknown notional risk " + s.notionalType);
            s.fixingDays = integer(a[6]);
            s.spreads = reals(a[7]);
            s.redemption = real(a[8]);
            s.issueDate = date(a[9]);
            const json::array& risk = a[10].get_array();
            s.riskType = text(risk[0]);
            if (s.riskType == "EventSet") {
                for (const json::value& e : risk[1].get_array()) {
                    std::vector<Real> p = reals(e);
                    s.events.emplace_back(p[0], p[1]);
                }
                s.eventsStart = date(risk[2]);
                s.eventsEnd = date(risk[3]);
            } else if (s.riskType == "BetaRisk") {
                for (std::size_t i = 1; i < 5; ++i)
                    s.riskParams.push_back(real(risk[i]));
            } else {
                throw InvalidParams("unknown cat risk " + s.riskType);
            }
            s.discount = credit::discount(a[11]);
            return s;
        }

        json::value values(const BondSpec& s) {
            Handle<YieldTermStructure> riskFree(ext::make_shared<FlatForward>(
                Settings::instance().evaluationDate(), s.riskFreeRate, Actual360()));
            auto index = ext::make_shared<USDLibor>(6 * Months, riskFree);
            auto offset = ext::make_shared<NoOffset>();
            ext::shared_ptr<NotionalRisk> notional =
                s.notionalType == "Digital" ?
                    ext::shared_ptr<NotionalRisk>(ext::make_shared<DigitalNotionalRisk>(offset, s.notionalParams[0])) :
                    ext::shared_ptr<NotionalRisk>(ext::make_shared<ProportionalNotionalRisk>(
                        offset, s.notionalParams[0], s.notionalParams[1]));
            FloatingCatBond b(s.settlementDays, s.faceAmount, credit::makeSchedule(s.schedule), index,
                              credit::makeDayCounter(s.dayCounter), notional, ModifiedFollowing, s.fixingDays,
                              std::vector<Real>(), s.spreads, std::vector<Rate>(), std::vector<Rate>(), false,
                              s.redemption, makeDate(s.issueDate));
            setCouponPricer(b.cashflows(), ext::make_shared<BlackIborCouponPricer>(Handle<OptionletVolatilityStructure>()));
            ext::shared_ptr<CatRisk> risk;
            if (s.riskType == "EventSet") {
                auto es = ext::make_shared<std::vector<std::pair<Date, Real>>>();
                for (const auto& e : s.events)
                    es->emplace_back(Date(static_cast<Date::serial_type>(e.first)), e.second);
                risk = ext::make_shared<EventSet>(es, makeDate(s.eventsStart), makeDate(s.eventsEnd));
            } else {
                risk = ext::make_shared<BetaRisk>(s.riskParams[0], s.riskParams[1], s.riskParams[2], s.riskParams[3]);
            }
            b.setPricingEngine(ext::make_shared<MonteCarloCatBondEngine>(risk, credit::makeDiscount(s.discount)));
            json::array dates, amounts;
            for (const ext::shared_ptr<CashFlow>& cf : b.cashflows()) {
                dates.push_back(result(cf->date()));
                amounts.push_back(json::value(cf->amount()));
            }
            return json::object{{"cashflowDates", dates},
                                {"cashflowAmounts", amounts},
                                {"NPV", attempted([&] { return b.NPV(); })},
                                {"settlementValue", attempted([&] { return b.settlementValue(); })},
                                {"cleanPrice", attempted([&] { return b.cleanPrice(); })},
                                {"lossProbability", attempted([&] { return b.lossProbability(); })},
                                {"exhaustionProbability", attempted([&] { return b.exhaustionProbability(); })},
                                {"expectedLoss", attempted([&] { return b.expectedLoss(); })}};
        }

        const Register unit({
            {"FloatingCatBond::values",
             [](const Args& a) -> Call {
                 arity(a, 12);
                 BondSpec s = bond(a);
                 return [=] { return values(s); };
             }},
        });

    }

}
