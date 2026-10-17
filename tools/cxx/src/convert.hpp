// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// Methods, arguments and results (docs/porting-playbook.md §7.1).
//
// A method runs in two phases. Its check reads every argument the harness itself reads: each argument's JSON kind,
// and for a spec its type among the names the harness knows, the keys that type needs, and no key it doesn't take.
// It yields plain values and spec structs, and a failure raises InvalidParams before QuantLib is called. It then
// returns the call, which builds every QuantLib value from them (a Date from its serial, a day counter from its
// spec) and calls QuantLib, so that a constructor's refusal of an argument is QuantLib's, as in the wheel.
//
// Arguments follow the vectors' encoding: dates as serials, Date() and Null<T>() as null, enums as integers, and
// objects as spec objects with the vectors' input names. Results go back the same way, a date as its serial or null.

#ifndef NANOQL_CXX_CONVERT_HPP
#define NANOQL_CXX_CONVERT_HPP

#include <ql/time/date.hpp>
#include <ql/time/daycounter.hpp>
#include <ql/types.hpp>

#include <boost/json.hpp>

#include <climits>
#include <cstddef>
#include <functional>
#include <initializer_list>
#include <map>
#include <optional>
#include <sstream>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

namespace harness {

    namespace json = boost::json;
    using Args = json::array;

    // The harness's own failure: an argument it can't read, or a failure of its own logic inside the call. It is
    // answered with JSON-RPC's invalid-params code, never as an exception from QuantLib.
    struct InvalidParams : std::runtime_error {
        using std::runtime_error::runtime_error;
    };

    // The call: builds QuantLib values from the checked arguments and calls QuantLib.
    using Call = std::function<json::value()>;
    // The check: reads the arguments and returns the call.
    using Method = std::function<Call(const Args&)>;

    // Each unit's file registers its methods with a static Register; a name registered twice stops the harness.
    struct Register {
        Register(std::initializer_list<std::pair<const char*, Method>> methods);
    };

    const std::map<std::string, Method>& methods();

    // The check.
    void arity(const Args& a, std::size_t n);
    QuantLib::Real real(const json::value& v);         // a number, or null for Null<Real>()
    QuantLib::Integer integer(const json::value& v);   // an integer in Integer's range, or null for Null<Integer>()
    long long int64(const json::value& v);             // a 64-bit integer, or null for Null<long long>()
    bool boolean(const json::value& v);
    std::optional<bool> optionalBoolean(const json::value& v);  // a boolean, or null for none
    std::string text(const json::value& v);
    std::vector<QuantLib::Real> reals(const json::value& v);    // an array of reals
    std::vector<QuantLib::Integer> integers(const json::value& v);  // an array of Integers

    template <class E>
    E enumeration(const json::value& v) {
        // QuantLib decides whether the value is one of the enum's: the harness only reads an integer.
        if (!v.is_int64() || v.as_int64() < INT_MIN || v.as_int64() > INT_MAX)
            throw InvalidParams("expected an enum's integer, got " + json::serialize(v));
        return static_cast<E>(static_cast<int>(v.as_int64()));
    }

    struct DateSpec {
        bool null;
        QuantLib::Date::serial_type serial;
    };
    DateSpec date(const json::value& v);  // a serial, or null for Date()

    struct DayCounterSpec {
        std::string type;
        int convention;
    };
    DayCounterSpec dayCounter(const json::value& v);

    // The call.
    QuantLib::Date makeDate(const DateSpec& d);
    QuantLib::DayCounter makeDayCounter(const DayCounterSpec& d);

    // Results.
    json::value result(const QuantLib::Date& d);  // its serial, or null for Date()

    template <class T>
    std::string streamed(const T& x) {
        std::ostringstream s;
        s << x;
        return s.str();
    }

}

#endif
