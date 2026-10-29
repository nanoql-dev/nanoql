// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The specs the commodities' methods share (ql/experimental/commodities/), with the vectors' input names
// (tools/vectors/gen/cxx/experimental/commodities/commodityspec.py), and the globals each method resets.
//
// A unit of measure is its code, one of QuantLib's presets: "BBL", "MT", "MB", "GAL", "l", "kl", "KL_tk", "Lot". A
// commodity type is {"name", "code"}, or "<NULL>" for NullCommodityType. A currency is its code, "USD" or "EUR". A
// quantity is {"commodityType", "unit", "amount"}; a conversion {"commodityType", "source", "target", "factor"}. A
// commodity curve is {"name", "commodityType", "currency", "unit", "calendar", "dates", "prices", "dayCounter"}, the
// constructor from dates, with an optional "basis", another curve set as its basis curve; an index {"name",
// "commodityType", "currency", "unit", "calendar", "lotQuantity", "curve", "contracts", "nearbyOffset"}, the curve a
// curve spec or null and the contracts null or [{"code", "expirationDate", "underlyingStartDate",
// "underlyingEndDate"}, ...]. A unit cost is {"amount", "currency", "unit"}; secondary costs are {name: {"unitCost": a
// unit cost} or {"money": {"amount", "currency"}}}; a pricing period {"startDate", "endDate", "paymentDate",
// "quantity"}. A calendar is indexes::calendar's, a yield curve indexes::curve's.
//
// Each method resets the globals the commodities read before it builds anything: the unit-conversion manager cleared
// to QuantLib's known factors, the commodity settings to USD and barrels, and Quantity's statics to no conversion and
// no base unit; a method that takes other settings sets them after.

#ifndef NANOQL_CXX_EXPERIMENTAL_COMMODITIES_COMMODITYSPEC_HPP
#define NANOQL_CXX_EXPERIMENTAL_COMMODITIES_COMMODITYSPEC_HPP

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/currencies/america.hpp>
#include <ql/currencies/europe.hpp>
#include <ql/experimental/commodities/commodity.hpp>
#include <ql/experimental/commodities/commoditycurve.hpp>
#include <ql/experimental/commodities/commodityindex.hpp>
#include <ql/experimental/commodities/commoditysettings.hpp>
#include <ql/experimental/commodities/commodityunitcost.hpp>
#include <ql/experimental/commodities/energycommodity.hpp>
#include <ql/experimental/commodities/exchangecontract.hpp>
#include <ql/experimental/commodities/petroleumunitsofmeasure.hpp>
#include <ql/experimental/commodities/pricingperiod.hpp>
#include <ql/experimental/commodities/quantity.hpp>
#include <ql/experimental/commodities/unitofmeasureconversionmanager.hpp>

#include <any>
#include <map>
#include <optional>
#include <string>
#include <vector>

namespace harness::commodities {

    inline const std::vector<std::string>& unitCodes() {
        static const std::vector<std::string> codes = {"BBL", "MT", "MB", "GAL", "l", "kl", "KL_tk", "Lot"};
        return codes;
    }

    inline std::string unit(const json::value& v) {
        const std::string code = text(v);
        for (const auto& c : unitCodes())
            if (c == code)
                return code;
        throw InvalidParams("unknown unit of measure " + code);
    }

    inline QuantLib::UnitOfMeasure makeUnit(const std::string& code) {
        using namespace QuantLib;
        if (code == "BBL")
            return BarrelUnitOfMeasure();
        if (code == "MT")
            return MTUnitOfMeasure();
        if (code == "MB")
            return MBUnitOfMeasure();
        if (code == "GAL")
            return GallonUnitOfMeasure();
        if (code == "l")
            return LitreUnitOfMeasure();
        if (code == "kl")
            return KilolitreUnitOfMeasure();
        if (code == "KL_tk")
            return TokyoKilolitreUnitOfMeasure();
        return LotUnitOfMeasure();
    }

    struct CommodityTypeSpec {
        bool null = true;
        std::string name, code;
    };

    inline CommodityTypeSpec commodityType(const json::value& v) {
        if (v.is_string() && v.as_string() == "<NULL>")
            return {};
        if (!v.is_object() || v.as_object().size() != 2)
            throw InvalidParams("expected a commodity type {name, code} or \"<NULL>\", got " + json::serialize(v));
        return {false, text(v.at("name")), text(v.at("code"))};
    }

    inline QuantLib::CommodityType makeCommodityType(const CommodityTypeSpec& s) {
        if (s.null)
            return QuantLib::NullCommodityType();
        return {s.name, s.code};
    }

    inline std::string currency(const json::value& v) {
        const std::string code = text(v);
        if (code != "USD" && code != "EUR")
            throw InvalidParams("unknown currency " + code);
        return code;
    }

    inline QuantLib::Currency makeCurrency(const std::string& code) {
        if (code == "USD")
            return QuantLib::USDCurrency();
        return QuantLib::EURCurrency();
    }

    struct QuantitySpec {
        CommodityTypeSpec commodityType;
        std::string unit;
        QuantLib::Real amount;
    };

    inline QuantitySpec quantity(const json::value& v) {
        if (!v.is_object() || v.as_object().size() != 3)
            throw InvalidParams("expected a quantity {commodityType, unit, amount}, got " + json::serialize(v));
        return {commodityType(v.at("commodityType")), unit(v.at("unit")), real(v.at("amount"))};
    }

    inline QuantLib::Quantity makeQuantity(const QuantitySpec& s) {
        return {makeCommodityType(s.commodityType), makeUnit(s.unit), s.amount};
    }

    struct ConversionSpec {
        CommodityTypeSpec commodityType;
        std::string source, target;
        QuantLib::Real factor;
    };

    inline ConversionSpec conversion(const json::value& v) {
        if (!v.is_object() || v.as_object().size() != 4)
            throw InvalidParams("expected a conversion {commodityType, source, target, factor}, got "
                                + json::serialize(v));
        return {commodityType(v.at("commodityType")), unit(v.at("source")), unit(v.at("target")),
                real(v.at("factor"))};
    }

    inline QuantLib::UnitOfMeasureConversion makeConversion(const ConversionSpec& s) {
        return {makeCommodityType(s.commodityType), makeUnit(s.source), makeUnit(s.target), s.factor};
    }

    struct CurveSpec {
        std::string name;
        CommodityTypeSpec commodityType;
        std::string currency, unit;
        indexes::CalendarSpec calendar;
        std::vector<DateSpec> dates;
        std::vector<QuantLib::Real> prices;
        DayCounterSpec dayCounter;
        std::vector<CurveSpec> basis;  // none, or one
    };

    inline CurveSpec curve(const json::value& v) {
        if (!v.is_object())
            throw InvalidParams("expected a commodity curve, got " + json::serialize(v));
        const json::object& o = v.as_object();
        if (o.size() != 8 && !(o.size() == 9 && o.contains("basis")))
            throw InvalidParams("a commodity curve is {name, commodityType, currency, unit, calendar, dates, prices, "
                                "dayCounter[, basis]}");
        CurveSpec s{text(o.at("name")), commodityType(o.at("commodityType")), currency(o.at("currency")),
                    unit(o.at("unit")), indexes::calendar(o.at("calendar")), {}, reals(o.at("prices")),
                    harness::dayCounter(o.at("dayCounter")), {}};
        if (!o.at("dates").is_array())
            throw InvalidParams("expected the curve's dates as serials");
        for (const auto& d : o.at("dates").as_array())
            s.dates.push_back(date(d));
        if (o.contains("basis"))
            s.basis.push_back(curve(o.at("basis")));
        return s;
    }

    inline QuantLib::ext::shared_ptr<QuantLib::CommodityCurve> makeCurve(const CurveSpec& s) {
        using namespace QuantLib;
        std::vector<Date> dates;
        for (const auto& d : s.dates)
            dates.push_back(makeDate(d));
        auto c = ext::make_shared<CommodityCurve>(s.name, makeCommodityType(s.commodityType), makeCurrency(s.currency),
                                                  makeUnit(s.unit), indexes::makeCalendar(s.calendar), dates,
                                                  s.prices, makeDayCounter(s.dayCounter));
        if (!s.basis.empty())
            c->setBasisOfCurve(makeCurve(s.basis.front()));
        return c;
    }

    struct ContractSpec {
        std::string code;
        DateSpec expirationDate, underlyingStartDate, underlyingEndDate;
    };

    inline std::optional<std::vector<ContractSpec>> contracts(const json::value& v) {
        if (v.is_null())
            return std::nullopt;
        if (!v.is_array())
            throw InvalidParams("expected exchange contracts, got " + json::serialize(v));
        std::vector<ContractSpec> out;
        for (const auto& c : v.as_array())
            out.push_back({text(c.at("code")), date(c.at("expirationDate")), date(c.at("underlyingStartDate")),
                           date(c.at("underlyingEndDate"))});
        return out;
    }

    inline QuantLib::ext::shared_ptr<QuantLib::ExchangeContracts>
    makeContracts(const std::optional<std::vector<ContractSpec>>& s) {
        using namespace QuantLib;
        if (!s)
            return nullptr;
        auto out = ext::make_shared<ExchangeContracts>();
        for (const auto& c : *s)
            out->emplace(makeDate(c.expirationDate),
                         ExchangeContract(c.code, makeDate(c.expirationDate), makeDate(c.underlyingStartDate),
                                          makeDate(c.underlyingEndDate)));
        return out;
    }

    struct IndexSpec {
        std::string name;
        CommodityTypeSpec commodityType;
        std::string currency, unit;
        indexes::CalendarSpec calendar;
        QuantLib::Real lotQuantity;
        std::vector<CurveSpec> curve;  // none, or one
        std::optional<std::vector<ContractSpec>> contracts;
        QuantLib::Integer nearbyOffset;
    };

    inline IndexSpec index(const json::value& v) {
        if (!v.is_object() || v.as_object().size() != 9)
            throw InvalidParams("expected a commodity index {name, commodityType, currency, unit, calendar, "
                                "lotQuantity, curve, contracts, nearbyOffset}, got " + json::serialize(v));
        const json::object& o = v.as_object();
        IndexSpec s{text(o.at("name")), commodityType(o.at("commodityType")), currency(o.at("currency")),
                    unit(o.at("unit")), indexes::calendar(o.at("calendar")), real(o.at("lotQuantity")), {},
                    contracts(o.at("contracts")), integer(o.at("nearbyOffset"))};
        if (!o.at("curve").is_null())
            s.curve.push_back(curve(o.at("curve")));
        return s;
    }

    inline QuantLib::ext::shared_ptr<QuantLib::CommodityIndex> makeIndex(const IndexSpec& s) {
        using namespace QuantLib;
        return ext::make_shared<CommodityIndex>(
            s.name, makeCommodityType(s.commodityType), makeCurrency(s.currency), makeUnit(s.unit),
            indexes::makeCalendar(s.calendar), s.lotQuantity, s.curve.empty() ? nullptr : makeCurve(s.curve.front()),
            makeContracts(s.contracts), s.nearbyOffset);
    }

    struct UnitCostSpec {
        QuantLib::Real amount;
        std::string currency, unit;
    };

    inline UnitCostSpec unitCost(const json::value& v) {
        if (!v.is_object() || v.as_object().size() != 3)
            throw InvalidParams("expected a unit cost {amount, currency, unit}, got " + json::serialize(v));
        return {real(v.at("amount")), currency(v.at("currency")), unit(v.at("unit"))};
    }

    inline QuantLib::CommodityUnitCost makeUnitCost(const UnitCostSpec& s) {
        return {QuantLib::Money(makeCurrency(s.currency), s.amount), makeUnit(s.unit)};
    }

    struct SecondaryCostSpec {
        std::optional<UnitCostSpec> unitCost;
        QuantLib::Real amount = 0.0;
        std::string currency;
    };

    // null for none, or {name: {"unitCost": …} or {"money": {"amount", "currency"}}}.
    inline std::optional<std::map<std::string, SecondaryCostSpec>> secondaryCosts(const json::value& v) {
        if (v.is_null())
            return std::nullopt;
        if (!v.is_object())
            throw InvalidParams("expected secondary costs, got " + json::serialize(v));
        std::map<std::string, SecondaryCostSpec> out;
        for (const auto& kv : v.as_object()) {
            const json::value& c = kv.value();
            if (c.is_object() && c.as_object().contains("unitCost"))
                out[std::string(kv.key())] = {unitCost(c.at("unitCost")), 0.0, ""};
            else if (c.is_object() && c.as_object().contains("money"))
                out[std::string(kv.key())] = {std::nullopt, real(c.at("money").at("amount")),
                                              currency(c.at("money").at("currency"))};
            else
                throw InvalidParams("a secondary cost is {unitCost} or {money}");
        }
        return out;
    }

    inline QuantLib::ext::shared_ptr<QuantLib::SecondaryCosts>
    makeSecondaryCosts(const std::optional<std::map<std::string, SecondaryCostSpec>>& s) {
        using namespace QuantLib;
        if (!s)
            return nullptr;
        auto out = ext::make_shared<SecondaryCosts>();
        for (const auto& kv : *s) {
            if (kv.second.unitCost)
                (*out)[kv.first] = makeUnitCost(*kv.second.unitCost);
            else
                (*out)[kv.first] = Money(makeCurrency(kv.second.currency), kv.second.amount);
        }
        return out;
    }

    struct PeriodSpec {
        DateSpec startDate, endDate, paymentDate;
        QuantitySpec quantity;
    };

    inline std::vector<PeriodSpec> periods(const json::value& v) {
        if (!v.is_array())
            throw InvalidParams("expected pricing periods, got " + json::serialize(v));
        std::vector<PeriodSpec> out;
        for (const auto& p : v.as_array())
            out.push_back({date(p.at("startDate")), date(p.at("endDate")), date(p.at("paymentDate")),
                           quantity(p.at("quantity"))});
        return out;
    }

    inline QuantLib::PricingPeriods makePeriods(const std::vector<PeriodSpec>& s) {
        using namespace QuantLib;
        PricingPeriods out;
        for (const auto& p : s)
            out.push_back(ext::make_shared<PricingPeriod>(makeDate(p.startDate), makeDate(p.endDate),
                                                          makeDate(p.paymentDate), makeQuantity(p.quantity)));
        return out;
    }

    // The globals the commodities read, back to QuantLib's defaults.
    inline void resetGlobals() {
        using namespace QuantLib;
        UnitOfMeasureConversionManager::instance().clear();
        CommoditySettings::instance().currency() = USDCurrency();
        CommoditySettings::instance().unitOfMeasure() = BarrelUnitOfMeasure();
        Quantity::conversionType = Quantity::NoConversion;
        Quantity::baseUnitOfMeasure = UnitOfMeasure();
    }

    // A quantity, [unit, amount].
    inline json::value quantityResult(const QuantLib::Quantity& q) {
        return json::array{json::value(q.unitOfMeasure().code()), json::value(q.amount())};
    }

    // A calculation's pricing errors, each [level, error, detail, text], the text as QuantLib's operator<< writes it.
    inline json::value errorsResult(const QuantLib::PricingErrors& errors) {
        json::array out;
        for (const auto& e : errors)
            out.emplace_back(json::array{static_cast<int>(e.errorLevel), e.error, e.detail, streamed(e)});
        return json::value(out);
    }

    // The secondary cost amounts, [[name, amount], ...] in the map's order.
    inline json::value costsResult(const QuantLib::SecondaryCostAmounts& costs) {
        json::array out;
        for (const auto& kv : costs)
            out.emplace_back(json::array{json::value(kv.first), json::value(kv.second.value())});
        return json::value(out);
    }

    // An energy swap's results after its NPV: {"NPV", "isExpired", "errors", "costs", "positions", "cashFlows",
    // "quantity"}, a position
    // [date, payLegPrice, receiveLegPrice, quantityAmount, riskDelta, unrealized] and a cash flow [date, currency,
    // discountedAmount, undiscountedAmount, discountedPaymentAmount, undiscountedPaymentAmount, paymentCurrency,
    // discountFactor, paymentDiscountFactor, finalized], the amounts' values (the base amounts in the base currency).
    template <class Swap>
    json::value swapResult(const Swap& swap) {
        json::object o;
        o["NPV"] = swap.NPV();
        o["isExpired"] = swap.isExpired();
        o["errors"] = errorsResult(swap.pricingErrors());
        o["costs"] = costsResult(swap.secondaryCostAmounts());
        json::array positions, flows;
        for (const auto& kv : swap.dailyPositions()) {
            const QuantLib::EnergyDailyPosition& p = kv.second;
            positions.emplace_back(json::array{result(kv.first), p.payLegPrice, p.receiveLegPrice, p.quantityAmount,
                                               p.riskDelta, p.unrealized});
        }
        for (const auto& kv : swap.paymentCashFlows()) {
            const QuantLib::CommodityCashFlow& c = *kv.second;
            flows.emplace_back(json::array{result(kv.first), c.currency().code(), c.discountedAmount().value(),
                                           c.undiscountedAmount().value(), c.discountedPaymentAmount().value(),
                                           c.undiscountedPaymentAmount().value(),
                                           c.discountedPaymentAmount().currency().code(), c.discountFactor(),
                                           c.paymentDiscountFactor(), c.finalized()});
        }
        o["positions"] = positions;
        o["cashFlows"] = flows;
        o["quantity"] = quantityResult(swap.quantity());
        return json::value(o);
    }

}

#endif
