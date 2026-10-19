// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "instruments/bondspec.hpp"

#include <ql/cashflows/cashflowvectors.hpp>
#include <ql/cashflows/fixedratecoupon.hpp>
#include <ql/cashflows/iborcoupon.hpp>
#include <ql/cashflows/simplecashflow.hpp>
#include <ql/instruments/bonds/amortizingfixedratebond.hpp>
#include <ql/instruments/bonds/fixedratebond.hpp>
#include <ql/instruments/bonds/floatingratebond.hpp>
#include <ql/instruments/bonds/zerocouponbond.hpp>
#include <ql/pricingengines/bond/discountingbondengine.hpp>
#include <ql/utilities/null.hpp>

#include <initializer_list>

using namespace QuantLib;

namespace harness::bonds {

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

        Natural natural(const json::value& v) {
            Integer n = integer(v);
            if (n == Null<Integer>() || n < 0)
                throw InvalidParams("expected a Natural, got " + json::serialize(v));
            return static_cast<Natural>(n);
        }

        // A list of reals in which a null is Null<Real>(), as the caps' and floors' lists hold them.
        std::vector<Real> nullableReals(const json::value& v) {
            std::vector<Real> out;
            for (const json::value& x : array(v, "a list of reals"))
                out.push_back(real(x));
            return out;
        }

        ExCouponSpec exCoupon(const json::value& v) {
            ExCouponSpec s;
            if (v.is_null())
                return s;
            const std::string what = "an ex-coupon period";
            const json::object& o = object(v, what);
            onlyKeys(o, {"period", "calendar", "convention", "endOfMonth"}, what);
            const json::array& period = array(key(o, "period", what), "a period as [length, units]");
            if (period.size() != 2)
                throw InvalidParams("expected a period as [length, units]");
            s.given = true;
            s.length = integer(period[0]);
            s.units = enumeration<TimeUnit>(period[1]);
            s.calendar = optionalCalendar(key(o, "calendar", what));
            s.convention = enumeration<BusinessDayConvention>(key(o, "convention", what));
            s.endOfMonth = boolean(key(o, "endOfMonth", what));
            return s;
        }

        FixedLegSpec fixedLeg(const json::value& v) {
            const std::string what = "a fixed-rate leg";
            const json::object& o = object(v, what);
            onlyKeys(o, {"schedule", "notionals", "couponRates", "dayCounter", "compounding", "frequency",
                         "firstPeriodDayCounter", "paymentCalendar", "paymentLag", "paymentAdjustment",
                         "exCouponPeriod"},
                     what);
            FixedLegSpec s;
            s.schedule = schedule(key(o, "schedule", what));
            s.notionals = reals(key(o, "notionals", what));
            s.couponRates = reals(key(o, "couponRates", what));
            s.dayCounter = dayCounter(key(o, "dayCounter", what));
            s.compounding = enumeration<Compounding>(key(o, "compounding", what));
            s.frequency = enumeration<Frequency>(key(o, "frequency", what));
            s.firstPeriodDayCounter = optionalDayCounter(key(o, "firstPeriodDayCounter", what));
            s.paymentCalendar = optionalCalendar(key(o, "paymentCalendar", what));
            s.paymentLag = integer(key(o, "paymentLag", what));
            s.paymentAdjustment = enumeration<BusinessDayConvention>(key(o, "paymentAdjustment", what));
            s.exCoupon = exCoupon(key(o, "exCouponPeriod", what));
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

        Leg makeFixedLeg(const FixedLegSpec& f) {
            Schedule sch = makeSchedule(f.schedule);
            // the wheel's FixedRateLeg helper's settings, in its order (SWIG/cashflows.i)
            FixedRateLeg builder = FixedRateLeg(sch)
                                       .withNotionals(f.notionals)
                                       .withPaymentAdjustment(f.paymentAdjustment)
                                       .withPaymentCalendar(f.paymentCalendar ? makeCalendar(f.paymentCalendar)
                                                                              : sch.calendar())
                                       .withPaymentLag(f.paymentLag)
                                       .withFirstPeriodDayCounter(makeDayCounter(f.firstPeriodDayCounter));
            if (f.exCoupon.given)
                builder = builder.withExCouponPeriod(Period(f.exCoupon.length, f.exCoupon.units),
                                                     makeCalendar(f.exCoupon.calendar), f.exCoupon.convention,
                                                     f.exCoupon.endOfMonth);
            builder = builder.withCouponRates(f.couponRates, harness::makeDayCounter(f.dayCounter), f.compounding,
                                              f.frequency);
            return builder;
        }

        Period exCouponPeriod(const ExCouponSpec& e) { return e.given ? Period(e.length, e.units) : Period(); }

    }

    std::optional<indexes::CalendarSpec> optionalCalendar(const json::value& v) {
        if (v.is_null())
            return std::nullopt;
        return indexes::calendar(v);
    }

    std::optional<DayCounterSpec> optionalDayCounter(const json::value& v) {
        if (v.is_null())
            return std::nullopt;
        return dayCounter(v);
    }

    Calendar makeCalendar(const std::optional<indexes::CalendarSpec>& s) {
        return s ? indexes::makeCalendar(*s) : Calendar();
    }

    DayCounter makeDayCounter(const std::optional<DayCounterSpec>& s) {
        return s ? harness::makeDayCounter(*s) : DayCounter();
    }

    ScheduleSpec schedule(const json::value& v) {
        const std::string what = "a schedule";
        const json::object& o = object(v, what);
        ScheduleSpec s;
        if (o.contains("dates")) {
            onlyKeys(o, {"dates", "calendar", "convention"}, what);
            s.fromDates = true;
            for (const json::value& d : array(key(o, "dates", what), "a list of dates"))
                s.dates.push_back(date(d));
            s.calendar = indexes::calendar(key(o, "calendar", what));
            s.convention = enumeration<BusinessDayConvention>(key(o, "convention", what));
            return s;
        }
        s.rule = cashflows::schedule(v);
        return s;
    }

    Schedule makeSchedule(const ScheduleSpec& s) {
        if (!s.fromDates)
            return cashflows::makeSchedule(s.rule);
        std::vector<Date> dates;
        for (const DateSpec& d : s.dates)
            dates.push_back(makeDate(d));
        return Schedule(dates, indexes::makeCalendar(s.calendar), s.convention);
    }

    LegSpec leg(const json::value& v) {
        const std::string what = "a leg";
        const json::object& o = object(v, what);
        onlyKeys(o, {"fixedRateLeg", "coupons", "extraFlows"}, what);
        LegSpec s;
        if (const json::value& f = key(o, "fixedRateLeg", what); !f.is_null())
            s.fixedRateLeg = fixedLeg(f);
        if (const json::value* c = o.if_contains("coupons"))
            for (const json::value& x : array(*c, "a list of coupons"))
                s.coupons.push_back(coupon(x));
        for (const json::value& x : array(key(o, "extraFlows", what), "a list of flows")) {
            const std::string flow = "a flow";
            const json::object& fo = object(x, flow);
            onlyKeys(fo, {"type", "amount", "date"}, flow);
            std::string type = text(key(fo, "type", flow));
            if (type != "Redemption" && type != "AmortizingPayment")
                throw InvalidParams("unknown flow type " + type);
            s.extraFlows.push_back({type == "Redemption", real(key(fo, "amount", flow)), date(key(fo, "date", flow))});
        }
        return s;
    }

    Leg makeLeg(const LegSpec& s) {
        Leg out;
        if (s.fixedRateLeg)
            out = makeFixedLeg(*s.fixedRateLeg);
        for (const CouponSpec& c : s.coupons)
            out.push_back(ext::make_shared<FixedRateCoupon>(
                makeDate(c.paymentDate), c.nominal, c.rate, harness::makeDayCounter(c.dayCounter),
                makeDate(c.accrualStartDate), makeDate(c.accrualEndDate), makeDate(c.refPeriodStart),
                makeDate(c.refPeriodEnd), makeDate(c.exCouponDate)));
        for (const FlowSpec& f : s.extraFlows) {
            if (f.redemption)
                out.push_back(ext::make_shared<Redemption>(f.amount, makeDate(f.date)));
            else
                out.push_back(ext::make_shared<AmortizingPayment>(f.amount, makeDate(f.date)));
        }
        return out;
    }

    BondSpec bond(const json::value& v) {
        const std::string what = "a bond";
        const json::object& o = object(v, what);
        BondSpec s;
        s.type = text(key(o, "type", what));
        if (s.type == "Bond") {
            onlyKeys(o, {"type", "settlementDays", "calendar", "issueDate", "coupons"}, s.type);
            s.calendar = optionalCalendar(key(o, "calendar", s.type));
            s.issueDate = date(key(o, "issueDate", s.type));
            s.leg = leg(key(o, "coupons", s.type));
        } else if (s.type == "BondFaceAmount") {
            onlyKeys(o, {"type", "settlementDays", "calendar", "faceAmount", "maturityDate", "issueDate", "cashflows"},
                     s.type);
            s.calendar = optionalCalendar(key(o, "calendar", s.type));
            s.faceAmount = real(key(o, "faceAmount", s.type));
            s.maturityDate = date(key(o, "maturityDate", s.type));
            s.issueDate = date(key(o, "issueDate", s.type));
            s.leg = leg(key(o, "cashflows", s.type));
        } else if (s.type == "FixedRateBond") {
            onlyKeys(o, {"type", "settlementDays", "faceAmount", "schedule", "coupons", "accrualDayCounter",
                         "paymentConvention", "redemption", "issueDate", "paymentCalendar", "exCouponPeriod",
                         "firstPeriodDayCounter"},
                     s.type);
            s.faceAmount = real(key(o, "faceAmount", s.type));
            s.schedule = schedule(key(o, "schedule", s.type));
            s.coupons = reals(key(o, "coupons", s.type));
            s.accrualDayCounter = optionalDayCounter(key(o, "accrualDayCounter", s.type));
            s.paymentConvention = enumeration<BusinessDayConvention>(key(o, "paymentConvention", s.type));
            s.redemption = real(key(o, "redemption", s.type));
            s.issueDate = date(key(o, "issueDate", s.type));
            s.paymentCalendar = optionalCalendar(key(o, "paymentCalendar", s.type));
            s.exCoupon = exCoupon(key(o, "exCouponPeriod", s.type));
            s.firstPeriodDayCounter = optionalDayCounter(key(o, "firstPeriodDayCounter", s.type));
        } else if (s.type == "FloatingRateBond") {
            onlyKeys(o, {"type", "settlementDays", "faceAmount", "schedule", "index", "accrualDayCounter",
                         "paymentConvention", "fixingDays", "gearings", "spreads", "caps", "floors", "inArrears",
                         "redemption", "issueDate", "exCouponPeriod", "fixingConvention"},
                     s.type);
            s.faceAmount = real(key(o, "faceAmount", s.type));
            s.schedule = schedule(key(o, "schedule", s.type));
            s.index = indexes::iborIndex(key(o, "index", s.type));
            s.accrualDayCounter = optionalDayCounter(key(o, "accrualDayCounter", s.type));
            s.paymentConvention = enumeration<BusinessDayConvention>(key(o, "paymentConvention", s.type));
            const json::value& days = key(o, "fixingDays", s.type);
            s.fixingDays = days.is_null() ? Null<Natural>() : natural(days);
            s.gearings = reals(key(o, "gearings", s.type));
            s.spreads = reals(key(o, "spreads", s.type));
            s.caps = nullableReals(key(o, "caps", s.type));
            s.floors = nullableReals(key(o, "floors", s.type));
            s.inArrears = boolean(key(o, "inArrears", s.type));
            s.redemption = real(key(o, "redemption", s.type));
            s.issueDate = date(key(o, "issueDate", s.type));
            s.exCoupon = exCoupon(key(o, "exCouponPeriod", s.type));
            s.fixingConvention = enumeration<BusinessDayConvention>(key(o, "fixingConvention", s.type));
        } else if (s.type == "ZeroCouponBond") {
            onlyKeys(o, {"type", "settlementDays", "calendar", "faceAmount", "maturityDate", "paymentConvention",
                         "redemption", "issueDate"},
                     s.type);
            s.calendar = optionalCalendar(key(o, "calendar", s.type));
            s.faceAmount = real(key(o, "faceAmount", s.type));
            s.maturityDate = date(key(o, "maturityDate", s.type));
            s.paymentConvention = enumeration<BusinessDayConvention>(key(o, "paymentConvention", s.type));
            s.redemption = real(key(o, "redemption", s.type));
            s.issueDate = date(key(o, "issueDate", s.type));
        } else if (s.type == "AmortizingFixedRateBond") {
            onlyKeys(o, {"type", "settlementDays", "notionals", "schedule", "coupons", "accrualDayCounter",
                         "paymentConvention", "issueDate", "exCouponPeriod", "redemptions", "paymentLag"},
                     s.type);
            s.notionals = reals(key(o, "notionals", s.type));
            s.schedule = schedule(key(o, "schedule", s.type));
            s.coupons = reals(key(o, "coupons", s.type));
            s.accrualDayCounter = optionalDayCounter(key(o, "accrualDayCounter", s.type));
            s.paymentConvention = enumeration<BusinessDayConvention>(key(o, "paymentConvention", s.type));
            s.issueDate = date(key(o, "issueDate", s.type));
            s.exCoupon = exCoupon(key(o, "exCouponPeriod", s.type));
            s.redemptions = reals(key(o, "redemptions", s.type));
            s.paymentLag = integer(key(o, "paymentLag", s.type));
        } else {
            throw InvalidParams("unknown bond " + s.type);
        }
        s.settlementDays = natural(key(o, "settlementDays", s.type));
        return s;
    }

    ext::shared_ptr<Bond> makeBond(const BondSpec& s) {
        if (s.type == "Bond")
            return ext::make_shared<Bond>(s.settlementDays, makeCalendar(s.calendar), makeDate(s.issueDate),
                                          makeLeg(s.leg));
        if (s.type == "BondFaceAmount")
            return ext::make_shared<Bond>(s.settlementDays, makeCalendar(s.calendar), s.faceAmount,
                                          makeDate(s.maturityDate), makeDate(s.issueDate), makeLeg(s.leg));
        if (s.type == "FixedRateBond")
            return ext::make_shared<FixedRateBond>(
                s.settlementDays, s.faceAmount, makeSchedule(s.schedule), s.coupons,
                makeDayCounter(s.accrualDayCounter), s.paymentConvention, s.redemption, makeDate(s.issueDate),
                makeCalendar(s.paymentCalendar),
                exCouponPeriod(s.exCoupon), makeCalendar(s.exCoupon.calendar), s.exCoupon.convention,
                s.exCoupon.endOfMonth, makeDayCounter(s.firstPeriodDayCounter));
        if (s.type == "FloatingRateBond")
            return ext::make_shared<FloatingRateBond>(
                s.settlementDays, s.faceAmount, makeSchedule(s.schedule), indexes::makeIborIndex(s.index),
                makeDayCounter(s.accrualDayCounter), s.paymentConvention, s.fixingDays, s.gearings, s.spreads, s.caps,
                s.floors, s.inArrears, s.redemption, makeDate(s.issueDate), exCouponPeriod(s.exCoupon),
                makeCalendar(s.exCoupon.calendar), s.exCoupon.convention, s.exCoupon.endOfMonth, s.fixingConvention);
        if (s.type == "ZeroCouponBond")
            return ext::make_shared<ZeroCouponBond>(s.settlementDays, makeCalendar(s.calendar), s.faceAmount,
                                                    makeDate(s.maturityDate), s.paymentConvention, s.redemption,
                                                    makeDate(s.issueDate));
        if (s.type == "AmortizingFixedRateBond")
            return ext::make_shared<AmortizingFixedRateBond>(
                s.settlementDays, s.notionals, makeSchedule(s.schedule), s.coupons,
                makeDayCounter(s.accrualDayCounter), s.paymentConvention, makeDate(s.issueDate),
                exCouponPeriod(s.exCoupon), makeCalendar(s.exCoupon.calendar), s.exCoupon.convention,
                s.exCoupon.endOfMonth, s.redemptions, s.paymentLag);
        throw InvalidParams("the bond " + s.type + " was not checked");
    }

    ext::shared_ptr<Bond> makePricedBond(const BondSpec& s, const indexes::CurveSpec& curve,
                                         const std::optional<bool>& includeSettlementDateFlows) {
        auto bond = makeBond(s);
        bond->setPricingEngine(
            ext::make_shared<DiscountingBondEngine>(indexes::makeCurve(curve), includeSettlementDateFlows));
        return bond;
    }

    RateSpec rate(const json::value& v) {
        const std::string what = "an InterestRate";
        const json::object& o = object(v, what);
        onlyKeys(o, {"rate", "dayCounter", "compounding", "frequency"}, what);
        return {real(key(o, "rate", what)), dayCounter(key(o, "dayCounter", what)),
                enumeration<Compounding>(key(o, "compounding", what)),
                enumeration<Frequency>(key(o, "frequency", what))};
    }

    InterestRate makeRate(const RateSpec& s) {
        return {s.rate, harness::makeDayCounter(s.dayCounter), s.compounding, s.frequency};
    }

    PriceSpec price(const json::value& v) {
        const std::string what = "a bond price";
        const json::object& o = object(v, what);
        onlyKeys(o, {"amount", "type"}, what);
        return {real(key(o, "amount", what)), enumeration<Bond::Price::Type>(key(o, "type", what))};
    }

    Bond::Price makePrice(const PriceSpec& s) { return {s.amount, s.type}; }

    json::value realArray(const std::vector<Real>& x) {
        json::array out;
        for (Real r : x)
            out.emplace_back(r);
        return out;
    }

}
