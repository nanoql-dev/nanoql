// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/cashflows.hpp: the CashFlows functions the wheel doesn't bind (isExpired, the coupon rates, the
// nominal, the accrual and reference dates, yieldValueBasisPoint in both forms, and yield on a Brent solver), and the
// positions previousCashFlow and nextCashFlow return; and ql/cashflow.hpp's earlier_than<CashFlow>.
//
// A leg is {"leadingFlows", "fixedRateLeg", "coupons", "extraFlows"}, its flows in that order: Redemption and
// AmortizingPayment flows ({"type", "amount", "date"}; an optional key), the coupons of a FixedRateLeg (null for none;
// its spec is tools/vectors/gen/cashflows/cashflows.py's, a rule-based schedule of cashflows/couponspec.hpp and every
// builder setting), fixed-rate coupons given one by one ({"paymentDate", "nominal", "rate", "dayCounter",
// "accrualStartDate", "accrualEndDate", "refPeriodStart", "refPeriodEnd", "exCouponDate"}, a simple annual rate; an
// optional key), and more flows of the first kind. An InterestRate is {"rate",
// "dayCounter", "compounding", "frequency"}. A position is an index into the leg: previousCashFlow's rend() is -1 and
// nextCashFlow's end() the leg's size.

#include "cashflows/couponspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/cashflow.hpp>
#include <ql/cashflows/cashflows.hpp>
#include <ql/cashflows/cashflowvectors.hpp>
#include <ql/cashflows/fixedratecoupon.hpp>
#include <ql/cashflows/simplecashflow.hpp>
#include <ql/math/solvers1d/brent.hpp>

#include <cstdint>
#include <initializer_list>
#include <optional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const json::object& object(const json::value& v, const std::string& what) {
            if (!v.is_object())
                throw InvalidParams("expected " + what + " spec, got " + json::serialize(v));
            return v.get_object();
        }

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
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

        const json::array& array(const json::value& v, const std::string& what) {
            if (!v.is_array())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            return v.get_array();
        }

        struct ExCouponSpec {
            Integer length = 0;
            TimeUnit units = Days;
            indexes::CalendarSpec calendar;
            BusinessDayConvention convention = Unadjusted;
            bool endOfMonth = false;
        };

        struct FixedLegSpec {
            cashflows::ScheduleSpec schedule;
            std::vector<Real> notionals, couponRates;
            DayCounterSpec dayCounter;
            Compounding compounding = Simple;
            Frequency frequency = Annual;
            std::optional<DayCounterSpec> firstPeriodDayCounter;
            std::optional<indexes::CalendarSpec> paymentCalendar;
            Integer paymentLag = 0;
            BusinessDayConvention paymentAdjustment = Following;
            std::optional<ExCouponSpec> exCoupon;
        };

        struct CouponSpec {
            DateSpec paymentDate, accrualStartDate, accrualEndDate, refPeriodStart, refPeriodEnd, exCouponDate;
            Real nominal, rate;
            DayCounterSpec dayCounter;
        };

        struct FlowSpec {
            bool redemption;
            Real amount;
            DateSpec date;
        };

        struct FlowLegSpec {
            std::vector<FlowSpec> leadingFlows;
            std::optional<FixedLegSpec> fixedRateLeg;
            std::vector<CouponSpec> coupons;
            std::vector<FlowSpec> extraFlows;
        };

        struct RateSpec {
            Real rate;
            DayCounterSpec dayCounter;
            Compounding compounding;
            Frequency frequency;
        };

        FixedLegSpec fixedLeg(const json::value& v) {
            const std::string what = "a fixed-rate leg";
            const json::object& o = object(v, what);
            onlyKeys(o, {"schedule", "notionals", "couponRates", "dayCounter", "compounding", "frequency",
                         "firstPeriodDayCounter", "paymentCalendar", "paymentLag", "paymentAdjustment",
                         "exCouponPeriod"},
                     what);
            FixedLegSpec s;
            s.schedule = cashflows::schedule(key(o, "schedule", what));
            s.notionals = reals(key(o, "notionals", what));
            s.couponRates = reals(key(o, "couponRates", what));
            s.dayCounter = dayCounter(key(o, "dayCounter", what));
            s.compounding = enumeration<Compounding>(key(o, "compounding", what));
            s.frequency = enumeration<Frequency>(key(o, "frequency", what));
            if (const json::value& f = key(o, "firstPeriodDayCounter", what); !f.is_null())
                s.firstPeriodDayCounter = dayCounter(f);
            if (const json::value& c = key(o, "paymentCalendar", what); !c.is_null())
                s.paymentCalendar = indexes::calendar(c);
            s.paymentLag = integer(key(o, "paymentLag", what));
            s.paymentAdjustment = enumeration<BusinessDayConvention>(key(o, "paymentAdjustment", what));
            if (const json::value& e = key(o, "exCouponPeriod", what); !e.is_null()) {
                const std::string ex = "an ex-coupon period";
                const json::object& eo = object(e, ex);
                onlyKeys(eo, {"period", "calendar", "convention", "endOfMonth"}, ex);
                const json::array& period = array(key(eo, "period", ex), "a period as [length, units]");
                if (period.size() != 2)
                    throw InvalidParams("expected a period as [length, units]");
                ExCouponSpec x;
                x.length = integer(period[0]);
                x.units = enumeration<TimeUnit>(period[1]);
                x.calendar = indexes::calendar(key(eo, "calendar", ex));
                x.convention = enumeration<BusinessDayConvention>(key(eo, "convention", ex));
                x.endOfMonth = boolean(key(eo, "endOfMonth", ex));
                s.exCoupon = x;
            }
            return s;
        }

        CouponSpec coupon(const json::value& v) {
            const std::string what = "a fixed-rate coupon";
            const json::object& o = object(v, what);
            onlyKeys(o, {"paymentDate", "nominal", "rate", "dayCounter", "accrualStartDate", "accrualEndDate",
                         "refPeriodStart", "refPeriodEnd", "exCouponDate"},
                     what);
            return {date(key(o, "paymentDate", what)),    date(key(o, "accrualStartDate", what)),
                    date(key(o, "accrualEndDate", what)), date(key(o, "refPeriodStart", what)),
                    date(key(o, "refPeriodEnd", what)),   date(key(o, "exCouponDate", what)),
                    real(key(o, "nominal", what)),        real(key(o, "rate", what)),
                    dayCounter(key(o, "dayCounter", what))};
        }

        std::vector<FlowSpec> flows(const json::value& v) {
            std::vector<FlowSpec> out;
            for (const json::value& x : array(v, "a list of flows")) {
                const std::string flow = "a flow";
                const json::object& fo = object(x, flow);
                onlyKeys(fo, {"type", "amount", "date"}, flow);
                std::string type = text(key(fo, "type", flow));
                if (type != "Redemption" && type != "AmortizingPayment")
                    throw InvalidParams("unknown flow type " + type);
                out.push_back({type == "Redemption", real(key(fo, "amount", flow)), date(key(fo, "date", flow))});
            }
            return out;
        }

        FlowLegSpec flowLeg(const json::value& v) {
            const std::string what = "a leg";
            const json::object& o = object(v, what);
            onlyKeys(o, {"leadingFlows", "fixedRateLeg", "coupons", "extraFlows"}, what);
            FlowLegSpec s;
            if (const json::value* l = o.if_contains("leadingFlows"))
                s.leadingFlows = flows(*l);
            if (const json::value& f = key(o, "fixedRateLeg", what); !f.is_null())
                s.fixedRateLeg = fixedLeg(f);
            if (const json::value* c = o.if_contains("coupons"))
                for (const json::value& x : array(*c, "a list of coupons"))
                    s.coupons.push_back(coupon(x));
            s.extraFlows = flows(key(o, "extraFlows", what));
            return s;
        }

        void addFlows(Leg& leg, const std::vector<FlowSpec>& flows) {
            for (const FlowSpec& f : flows) {
                if (f.redemption)
                    leg.push_back(ext::make_shared<Redemption>(f.amount, makeDate(f.date)));
                else
                    leg.push_back(ext::make_shared<AmortizingPayment>(f.amount, makeDate(f.date)));
            }
        }

        Leg makeLeg(const FlowLegSpec& s) {
            Leg leg;
            addFlows(leg, s.leadingFlows);
            if (s.fixedRateLeg) {
                const FixedLegSpec& f = *s.fixedRateLeg;
                Schedule schedule = cashflows::makeSchedule(f.schedule);
                // the wheel's FixedRateLeg helper's settings, in its order (SWIG/cashflows.i)
                FixedRateLeg builder = FixedRateLeg(schedule)
                                           .withNotionals(f.notionals)
                                           .withPaymentAdjustment(f.paymentAdjustment)
                                           .withPaymentCalendar(f.paymentCalendar
                                                                    ? indexes::makeCalendar(*f.paymentCalendar)
                                                                    : schedule.calendar())
                                           .withPaymentLag(f.paymentLag)
                                           .withFirstPeriodDayCounter(f.firstPeriodDayCounter
                                                                          ? makeDayCounter(*f.firstPeriodDayCounter)
                                                                          : DayCounter());
                if (f.exCoupon)
                    builder = builder.withExCouponPeriod(Period(f.exCoupon->length, f.exCoupon->units),
                                                         indexes::makeCalendar(f.exCoupon->calendar),
                                                         f.exCoupon->convention, f.exCoupon->endOfMonth);
                builder =
                    builder.withCouponRates(f.couponRates, makeDayCounter(f.dayCounter), f.compounding, f.frequency);
                Leg coupons = builder;
                leg.insert(leg.end(), coupons.begin(), coupons.end());
            }
            for (const CouponSpec& c : s.coupons)
                leg.push_back(ext::make_shared<FixedRateCoupon>(
                    makeDate(c.paymentDate), c.nominal, c.rate, makeDayCounter(c.dayCounter),
                    makeDate(c.accrualStartDate), makeDate(c.accrualEndDate), makeDate(c.refPeriodStart),
                    makeDate(c.refPeriodEnd), makeDate(c.exCouponDate)));
            addFlows(leg, s.extraFlows);
            return leg;
        }

        RateSpec rateSpec(const json::value& v) {
            const std::string what = "an InterestRate";
            const json::object& o = object(v, what);
            onlyKeys(o, {"rate", "dayCounter", "compounding", "frequency"}, what);
            return {real(key(o, "rate", what)), dayCounter(key(o, "dayCounter", what)),
                    enumeration<Compounding>(key(o, "compounding", what)),
                    enumeration<Frequency>(key(o, "frequency", what))};
        }

        // A function of (leg, includeSettlementDateFlows, settlementDate), its include null for std::nullopt.
        template <class F>
        Call atSettlement(const Args& a, F f) {
            arity(a, 3);
            FlowLegSpec s = flowLeg(a[0]);
            std::optional<bool> include = optionalBoolean(a[1]);
            DateSpec settlement = date(a[2]);
            return [=] { return f(makeLeg(s), include, makeDate(settlement)); };
        }

        const Register unit({
            {"CashFlows::isExpired",
             [](const Args& a) {
                 return atSettlement(a, [](const Leg& leg, const std::optional<bool>& include, const Date& d) {
                     return json::value(CashFlows::isExpired(leg, include, d));
                 });
             }},
            {"CashFlows::previousCashFlow",
             [](const Args& a) {
                 return atSettlement(a, [](const Leg& leg, const std::optional<bool>& include, const Date& d) {
                     auto i = CashFlows::previousCashFlow(leg, include, d);
                     return json::value(static_cast<std::int64_t>(leg.size()) - 1 -
                                        static_cast<std::int64_t>(i - leg.rbegin()));
                 });
             }},
            {"CashFlows::nextCashFlow",
             [](const Args& a) {
                 return atSettlement(a, [](const Leg& leg, const std::optional<bool>& include, const Date& d) {
                     auto i = CashFlows::nextCashFlow(leg, include, d);
                     return json::value(static_cast<std::int64_t>(i - leg.begin()));
                 });
             }},
            {"CashFlows::previousCouponRate",
             [](const Args& a) {
                 return atSettlement(a, [](const Leg& leg, const std::optional<bool>& include, const Date& d) {
                     return json::value(CashFlows::previousCouponRate(leg, include, d));
                 });
             }},
            {"CashFlows::nextCouponRate",
             [](const Args& a) {
                 return atSettlement(a, [](const Leg& leg, const std::optional<bool>& include, const Date& d) {
                     return json::value(CashFlows::nextCouponRate(leg, include, d));
                 });
             }},
            {"CashFlows::nominal",
             [](const Args& a) {
                 return atSettlement(a, [](const Leg& leg, const std::optional<bool>& include, const Date& d) {
                     return json::value(CashFlows::nominal(leg, include, d));
                 });
             }},
            {"CashFlows::accrualStartDate",
             [](const Args& a) {
                 return atSettlement(a, [](const Leg& leg, const std::optional<bool>& include, const Date& d) {
                     return result(CashFlows::accrualStartDate(leg, include, d));
                 });
             }},
            {"CashFlows::accrualEndDate",
             [](const Args& a) {
                 return atSettlement(a, [](const Leg& leg, const std::optional<bool>& include, const Date& d) {
                     return result(CashFlows::accrualEndDate(leg, include, d));
                 });
             }},
            {"CashFlows::referencePeriodStart",
             [](const Args& a) {
                 return atSettlement(a, [](const Leg& leg, const std::optional<bool>& include, const Date& d) {
                     return result(CashFlows::referencePeriodStart(leg, include, d));
                 });
             }},
            {"CashFlows::referencePeriodEnd",
             [](const Args& a) {
                 return atSettlement(a, [](const Leg& leg, const std::optional<bool>& include, const Date& d) {
                     return result(CashFlows::referencePeriodEnd(leg, include, d));
                 });
             }},
            // yieldValueBasisPoint(leg, rate, includeSettlementDateFlows, settlementDate, npvDate)
            {"CashFlows::yieldValueBasisPoint(Leg,InterestRate)",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 FlowLegSpec s = flowLeg(a[0]);
                 RateSpec r = rateSpec(a[1]);
                 std::optional<bool> include = optionalBoolean(a[2]);
                 DateSpec settlement = date(a[3]), npvDate = date(a[4]);
                 return [=] {
                     InterestRate y(r.rate, makeDayCounter(r.dayCounter), r.compounding, r.frequency);
                     return json::value(CashFlows::yieldValueBasisPoint(makeLeg(s), y, include, makeDate(settlement),
                                                                        makeDate(npvDate)));
                 };
             }},
            // yieldValueBasisPoint(leg, yield, dayCounter, compounding, frequency, includeSettlementDateFlows,
            //                      settlementDate, npvDate)
            {"CashFlows::yieldValueBasisPoint(Leg,Rate,DayCounter,Compounding,Frequency)",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 FlowLegSpec s = flowLeg(a[0]);
                 Real yield = real(a[1]);
                 DayCounterSpec dc = dayCounter(a[2]);
                 auto comp = enumeration<Compounding>(a[3]);
                 auto freq = enumeration<Frequency>(a[4]);
                 std::optional<bool> include = optionalBoolean(a[5]);
                 DateSpec settlement = date(a[6]), npvDate = date(a[7]);
                 return [=] {
                     return json::value(CashFlows::yieldValueBasisPoint(makeLeg(s), yield, makeDayCounter(dc), comp,
                                                                        freq, include, makeDate(settlement),
                                                                        makeDate(npvDate)));
                 };
             }},
            // yield<Brent>(solver with maxEvaluations, leg, npv, dayCounter, compounding, frequency,
            //              includeSettlementDateFlows, settlementDate, npvDate, accuracy, guess): its arguments are
            //              (leg, npv, dayCounter, compounding, frequency, include, settlementDate, npvDate, accuracy,
            //              maxEvaluations, guess)
            {"CashFlows::yield<Brent>",
             [](const Args& a) -> Call {
                 arity(a, 11);
                 FlowLegSpec s = flowLeg(a[0]);
                 Real npv = real(a[1]);
                 DayCounterSpec dc = dayCounter(a[2]);
                 auto comp = enumeration<Compounding>(a[3]);
                 auto freq = enumeration<Frequency>(a[4]);
                 std::optional<bool> include = optionalBoolean(a[5]);
                 DateSpec settlement = date(a[6]), npvDate = date(a[7]);
                 Real accuracy = real(a[8]);
                 Integer maxEvaluations = integer(a[9]);
                 if (maxEvaluations < 0)
                     throw InvalidParams("expected a Size, got " + json::serialize(a[9]));
                 Real guess = real(a[10]);
                 return [=] {
                     Brent solver;
                     solver.setMaxEvaluations(static_cast<Size>(maxEvaluations));
                     return json::value(CashFlows::yield<Brent>(solver, makeLeg(s), npv, makeDayCounter(dc), comp,
                                                                freq, include, makeDate(settlement), makeDate(npvDate),
                                                                accuracy, guess));
                 };
             }},
            // earlier_than<CashFlow>()(*leg[i], *leg[j])
            {"earlier_than<CashFlow>",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 FlowLegSpec s = flowLeg(a[0]);
                 Integer i = integer(a[1]), j = integer(a[2]);
                 return [=] {
                     Leg leg = makeLeg(s);
                     if (i < 0 || j < 0 || static_cast<Size>(i) >= leg.size() || static_cast<Size>(j) >= leg.size())
                         throw InvalidParams("a position outside the leg");
                     return json::value(
                         earlier_than<CashFlow>()(*leg[static_cast<Size>(i)], *leg[static_cast<Size>(j)]));
                 };
             }},
        });

    }

}
