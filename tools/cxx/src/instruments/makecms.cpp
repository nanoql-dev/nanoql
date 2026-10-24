// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/makecms.hpp, .cpp, which the wheel doesn't bind.
//
//     MakeCms::operator ext::shared_ptr<Swap>   (swapTenor, swapIndex, iborIndex, setters, pricer): the swap's legs
//                                               and, with a pricer, its legs' NPVs and its NPV on the engine the
//                                               builder attaches
//
// The swap index is a spec of cashflows/cmsspec.hpp; the Ibor index null for the swap index's own, or an Ibor index
// spec of indexes/indexspec.hpp; the pricer null for none, or a CMS pricer spec of cashflows/cmsspec.hpp, set with
// withCmsCouponPricer. The setters are [name, value] pairs called in order: receiveCms, withAtmSpread and the end-of-
// month flags a boolean; withNominal and withIborSpread a real; withForwardStart and the legs' tenors [length, units];
// withEffectiveDate and the legs' first and next-to-last dates a serial; the legs' calendars a calendar spec and their
// day counts a day counter spec; the conventions and rules QuantLib's integers; withDiscountingTermStructure a curve
// spec. Each leg's output is, per cash flow, [its date, its accrual start, its accrual end, its nominal, whether it is
// a CMS coupon, its spread, its fixing date].

#include "cashflows/cmsspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "termstructures/volatility/optionlet/optionletvolspec.hpp"

#include <ql/cashflows/cmscoupon.hpp>
#include <ql/cashflows/floatingratecoupon.hpp>
#include <ql/instruments/makecms.hpp>
#include <ql/instruments/swap.hpp>

#include <optional>
#include <string>
#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct SetterSpec {
            std::string name;
            bool flag = false;
            Real number = 0.0;
            Integer integer = 0;
            termstructures::PeriodSpec period{0, Days};
            DateSpec date{true, 0};
            indexes::CalendarSpec calendar;
            DayCounterSpec dayCounter;
            indexes::CurveSpec curve;
        };

        SetterSpec setter(const json::value& v) {
            if (!v.is_array() || v.as_array().size() != 2 || !v.as_array()[0].is_string())
                throw InvalidParams("expected a [name, value] setter, got " + json::serialize(v));
            const json::array& p = v.as_array();
            SetterSpec s;
            s.name = text(p[0]);
            const json::value& x = p[1];
            const std::string& n = s.name;
            if (n == "receiveCms" || n == "withAtmSpread" || n == "withCmsLegEndOfMonth" ||
                n == "withFloatingLegEndOfMonth")
                s.flag = boolean(x);
            else if (n == "withNominal" || n == "withIborSpread")
                s.number = real(x);
            else if (n == "withForwardStart" || n == "withCmsLegTenor" || n == "withFloatingLegTenor")
                s.period = termstructures::tenor(x);
            else if (n == "withEffectiveDate" || n == "withCmsLegFirstDate" || n == "withCmsLegNextToLastDate" ||
                     n == "withFloatingLegFirstDate" || n == "withFloatingLegNextToLastDate")
                s.date = date(x);
            else if (n == "withCmsLegCalendar" || n == "withFloatingLegCalendar")
                s.calendar = indexes::calendar(x);
            else if (n == "withCmsLegDayCount" || n == "withFloatingLegDayCount")
                s.dayCounter = dayCounter(x);
            else if (n == "withCmsLegConvention" || n == "withCmsLegTerminationDateConvention" ||
                     n == "withCmsLegRule" || n == "withFloatingLegConvention" ||
                     n == "withFloatingLegTerminationDateConvention" || n == "withFloatingLegRule")
                s.integer = integer(x);
            else if (n == "withDiscountingTermStructure")
                s.curve = indexes::curve(x);
            else
                throw InvalidParams("unknown setter " + n);
            return s;
        }

        void apply(MakeCms& m, const SetterSpec& s) {
            const std::string& n = s.name;
            if (n == "receiveCms")
                m.receiveCms(s.flag);
            else if (n == "withAtmSpread")
                m.withAtmSpread(s.flag);
            else if (n == "withCmsLegEndOfMonth")
                m.withCmsLegEndOfMonth(s.flag);
            else if (n == "withFloatingLegEndOfMonth")
                m.withFloatingLegEndOfMonth(s.flag);
            else if (n == "withNominal")
                m.withNominal(s.number);
            else if (n == "withIborSpread")
                m.withIborSpread(s.number);
            else if (n == "withForwardStart")
                m.withForwardStart(termstructures::makePeriod(s.period));
            else if (n == "withCmsLegTenor")
                m.withCmsLegTenor(termstructures::makePeriod(s.period));
            else if (n == "withFloatingLegTenor")
                m.withFloatingLegTenor(termstructures::makePeriod(s.period));
            else if (n == "withEffectiveDate")
                m.withEffectiveDate(makeDate(s.date));
            else if (n == "withCmsLegFirstDate")
                m.withCmsLegFirstDate(makeDate(s.date));
            else if (n == "withCmsLegNextToLastDate")
                m.withCmsLegNextToLastDate(makeDate(s.date));
            else if (n == "withFloatingLegFirstDate")
                m.withFloatingLegFirstDate(makeDate(s.date));
            else if (n == "withFloatingLegNextToLastDate")
                m.withFloatingLegNextToLastDate(makeDate(s.date));
            else if (n == "withCmsLegCalendar")
                m.withCmsLegCalendar(indexes::makeCalendar(s.calendar));
            else if (n == "withFloatingLegCalendar")
                m.withFloatingLegCalendar(indexes::makeCalendar(s.calendar));
            else if (n == "withCmsLegDayCount")
                m.withCmsLegDayCount(makeDayCounter(s.dayCounter));
            else if (n == "withFloatingLegDayCount")
                m.withFloatingLegDayCount(makeDayCounter(s.dayCounter));
            else if (n == "withCmsLegConvention")
                m.withCmsLegConvention(static_cast<BusinessDayConvention>(s.integer));
            else if (n == "withCmsLegTerminationDateConvention")
                m.withCmsLegTerminationDateConvention(static_cast<BusinessDayConvention>(s.integer));
            else if (n == "withCmsLegRule")
                m.withCmsLegRule(static_cast<DateGeneration::Rule>(s.integer));
            else if (n == "withFloatingLegConvention")
                m.withFloatingLegConvention(static_cast<BusinessDayConvention>(s.integer));
            else if (n == "withFloatingLegTerminationDateConvention")
                m.withFloatingLegTerminationDateConvention(static_cast<BusinessDayConvention>(s.integer));
            else if (n == "withFloatingLegRule")
                m.withFloatingLegRule(static_cast<DateGeneration::Rule>(s.integer));
            else
                m.withDiscountingTermStructure(indexes::makeCurve(s.curve));
        }

        json::value legOf(const Leg& leg) {
            json::array out;
            for (const auto& cf : leg) {
                json::array row;
                row.emplace_back(result(cf->date()));
                auto c = ext::dynamic_pointer_cast<Coupon>(cf);
                auto f = ext::dynamic_pointer_cast<FloatingRateCoupon>(cf);
                row.emplace_back(c ? result(c->accrualStartDate()) : json::value(nullptr));
                row.emplace_back(c ? result(c->accrualEndDate()) : json::value(nullptr));
                row.emplace_back(c ? json::value(c->nominal()) : json::value(nullptr));
                row.emplace_back(ext::dynamic_pointer_cast<CmsCoupon>(cf) != nullptr);
                row.emplace_back(f ? json::value(f->spread()) : json::value(nullptr));
                row.emplace_back(f ? result(f->fixingDate()) : json::value(nullptr));
                out.emplace_back(std::move(row));
            }
            return out;
        }

        const Register unit({
            {"MakeCms::operator ext::shared_ptr<Swap>",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 termstructures::PeriodSpec swapTenor = termstructures::tenor(a[0]);
                 cms::SwapIndexSpec swapIndex = cms::swapIndex(a[1]);
                 std::optional<indexes::IndexSpec> ibor;
                 if (!a[2].is_null())
                     ibor = indexes::iborIndex(a[2]);
                 if (!a[3].is_array())
                     throw InvalidParams("expected a list of setters, got " + json::serialize(a[3]));
                 std::vector<SetterSpec> setters;
                 for (const auto& x : a[3].as_array())
                     setters.push_back(setter(x));
                 std::optional<cms::PricerSpec> pricer;
                 if (!a[4].is_null())
                     pricer = cms::pricer(a[4]);
                 return [=] {
                     ext::shared_ptr<SwapIndex> index = cms::makeSwapIndex(swapIndex);
                     ext::shared_ptr<IborIndex> iborIndex = ibor ? indexes::makeIborIndex(*ibor) : nullptr;
                     MakeCms m(termstructures::makePeriod(swapTenor), index, iborIndex);
                     if (pricer)
                         m.withCmsCouponPricer(cms::makePricer(*pricer));
                     for (const auto& s : setters)
                         apply(m, s);
                     ext::shared_ptr<Swap> swap = m;
                     json::object out;
                     json::array legs;
                     for (Size j = 0; j < swap->numberOfLegs(); ++j)
                         legs.emplace_back(legOf(swap->leg(j)));
                     out["legs"] = std::move(legs);
                     if (pricer) {
                         json::array npv{swap->legNPV(0), swap->legNPV(1), swap->NPV()};
                         out["npv"] = std::move(npv);
                     } else {
                         out["npv"] = nullptr;
                     }
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
