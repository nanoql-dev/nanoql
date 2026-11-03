// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The C++ replayer's runtime (docs/bindings.md, "The vector suite through each binding"): a trace line's JSON, the
// objects the trace names by id, readers that make each argument as its C++ member's parameter, and checks that compare
// each result with the recorded one: doubles by their bits, NaN as NaN, objects by their ids and a new one by its
// class, collections element by element, refusals by their codes. nanoql-bind's generated dispatch calls them, one
// handler per C function, each calling its member through nanoql.hpp.
#ifndef NANOQL_REPLAY_HPP
#define NANOQL_REPLAY_HPP

#include <cstdint>
#include <cstring>
#include <exception>
#include <optional>
#include <string>
#include <tuple>
#include <type_traits>
#include <utility>
#include <vector>

#include "nanoql.hpp"

namespace replay {

enum class jt { null, boolean, number, string, array, object };

// A JSON value: a number keeps its text; an array's or object's children follow it (an object's as key, value).
struct jv {
    jt type;
    const char* text;
    int len;
    int count;
    jv** items;
};

const jv* at(const jv* v, int i);
const jv* member(const jv* o, const char* key);
double hexdouble(const jv* v);
int64_t num(const jv* v);
std::string unescape(const jv* v);
bool same_double(double a, double b);
std::string hex(double d);

// A type's name without each file-local class's compiler prefix (<File>F<hash>__, the hash of its source file's path).
std::string type_key(const std::string& name);

template <class T>
struct is_optional : std::false_type {};
template <class T>
struct is_optional<std::optional<T>> : std::true_type {};
template <class T>
struct is_vector : std::false_type {};
template <class T>
struct is_vector<std::vector<T>> : std::true_type {};
template <class T>
struct is_pair : std::false_type {};
template <class A, class B>
struct is_pair<std::pair<A, B>> : std::true_type {};
template <class T>
struct is_tuple : std::false_type {};
template <class... T>
struct is_tuple<std::tuple<T...>> : std::true_type {};
template <class T>
inline constexpr bool always_false = false;

// One replay: its objects by id, its counts, the line in hand.
class rp {
public:
    bool stop = false;
    long long line = 0, mismatches = 0, refusals = 0;
    const jv* after_values = nullptr;
    const char* fn = "";
    std::vector<nanoql::Object> objs;

    void mismatch(const std::string& what, const std::string& want, const std::string& got);

    // Argument i: an argument the trace couldn't write ({"x": ...}) stops the fact.
    const jv* arg(const jv* a, int i);

    // An element in its C++ form; an object the replay didn't make stops the fact.
    template <class T>
    T el(const jv* v);

    template <class T>
    T value(const jv* a, int i) { return el<T>(arg(a, i)); }

    template <class C>
    C obj(const jv* a, int i) { return object<C>(arg(a, i)); }

    nanoql::Date date(const jv* a, int i) { return value<nanoql::Date>(a, i); }
    nanoql::Period period(const jv* a, int i) { return value<nanoql::Period>(a, i); }

    // A matrix, which the trace writes as {"r", "c", "v"}; null stops the fact where the parameter takes no null.
    std::optional<nanoql::Matrix> opt_matrix(const jv* a, int i);
    nanoql::Matrix matrix(const jv* a, int i) { return required(opt_matrix(a, i)); }

    // A collection: null, or its elements.
    template <class T>
    std::optional<std::vector<T>> vec(const jv* a, int i)
    {
        const jv* v = arg(a, i);
        if (!v || v->type != jt::array) return std::nullopt;
        std::vector<T> out;
        out.reserve(v->count);
        for (int k = 0; k < v->count; k++) out.push_back(el<T>(v->items[k]));
        return out;
    }

    // A value the parameter takes no null for: null stops the fact.
    template <class T>
    T required(std::optional<T>&& v)
    {
        if (!v) {
            stop = true;
            return T{};
        }
        return std::move(*v);
    }

    // The status: true when the call succeeded as recorded and its result is to be checked.
    bool st(int got, int recorded);

    template <class F, class C>
    int call(int t, F f, C check)
    {
        try {
            auto v = f();
            if (st(NQL_OK, t)) check(v);
        } catch (const nanoql::error& e) {
            st(e.code(), t);
        } catch (const std::exception& e) {
            mismatch("call", "status " + std::to_string(t), std::string("an exception: ") + e.what());
        }
        return 1;
    }

    template <class F, class C>
    int call_void(int t, F f, C check)
    {
        try {
            f();
            if (st(NQL_OK, t)) check();
        } catch (const nanoql::error& e) {
            st(e.code(), t);
        } catch (const std::exception& e) {
            mismatch("call", "status " + std::to_string(t), std::string("an exception: ") + e.what());
        }
        return 1;
    }

    // A span's or an in-out argument's values after the call: the recorded "w" list k.
    void after(int k, const std::vector<double>& v);
    void after(int k, const std::vector<int32_t>& v);

    // A result object: a new one registered under its id (its class checked), a known one compared by equality.
    void check_object(const jv* res, const nanoql::Object& o);

private:
    template <class T, std::size_t... I>
    T el_tuple(const jv* v, std::index_sequence<I...>)
    {
        return T(el<std::tuple_element_t<I, T>>(at(v, static_cast<int>(I)))...);
    }

    template <class C>
    C object(const jv* v)
    {
        if (!v || v->type == jt::null) return C(nullptr);
        int64_t id = v->type == jt::number ? num(v) : 0;
        if (id <= 0 || id >= static_cast<int64_t>(objs.size()) || !objs[static_cast<std::size_t>(id)]) {
            stop = true;  // an object made by a call the replay didn't make
            return C(nullptr);
        }
        return nanoql::object_cast<C>(objs[static_cast<std::size_t>(id)]);
    }
};

template <class T>
T rp::el(const jv* v)
{
    using namespace nanoql;
    if constexpr (std::is_same_v<T, double>) {
        return v && v->type == jt::string ? hexdouble(v) : 0.0;
    } else if constexpr (std::is_same_v<T, bool>) {
        return v && v->type == jt::boolean && v->len;
    } else if constexpr (std::is_enum_v<T> || std::is_integral_v<T>) {
        return static_cast<T>(num(v));
    } else if constexpr (std::is_same_v<T, std::string>) {
        return v && v->type == jt::string ? unescape(v) : std::string();
    } else if constexpr (std::is_same_v<T, Date>) {
        return Date(detail::adopt, static_cast<nql_date>(num(v)));
    } else if constexpr (std::is_same_v<T, Period>) {
        return Period(detail::adopt, nql_period{static_cast<int32_t>(num(at(v, 0))), static_cast<int32_t>(num(at(v, 1)))});
    } else if constexpr (std::is_same_v<T, std::complex<double>>) {
        return {hexdouble(at(v, 0)), hexdouble(at(v, 1))};
    } else if constexpr (is_optional<T>::value) {
        if (!v || v->type == jt::null) return std::nullopt;
        return el<typename T::value_type>(v);
    } else if constexpr (std::is_base_of_v<Object, T>) {
        return object<T>(v);
    } else if constexpr (is_pair<T>::value) {
        return T(el<typename T::first_type>(at(v, 0)), el<typename T::second_type>(at(v, 1)));
    } else if constexpr (is_tuple<T>::value) {
        // A tuple argument, or a collection's tuple: its JSON array, each element in its own form.
        return el_tuple<T>(v, std::make_index_sequence<std::tuple_size_v<T>>{});
    } else if constexpr (is_vector<T>::value) {
        T out;
        for (int k = 0; v && v->type == jt::array && k < v->count; k++) out.push_back(el<typename T::value_type>(v->items[k]));
        return out;
    } else {
        static_assert(always_false<T>, "no reader for the type");
    }
}

// A result against the recorded one.
template <class T>
void check(rp& r, const jv* res, const T& v)
{
    using namespace nanoql;
    if constexpr (std::is_same_v<T, double>) {
        double want = res && res->type == jt::string ? hexdouble(res) : 0.0;
        if (!same_double(want, v)) r.mismatch("double", hex(want), hex(v));
    } else if constexpr (std::is_same_v<T, bool>) {
        bool want = res && res->type == jt::boolean && res->len;
        if (want != v) r.mismatch("bool", want ? "true" : "false", v ? "true" : "false");
    } else if constexpr (std::is_enum_v<T> || std::is_integral_v<T>) {
        int64_t got = static_cast<int64_t>(v);
        if (num(res) != got) r.mismatch("integer", std::to_string(num(res)), std::to_string(got));
    } else if constexpr (std::is_same_v<T, std::string>) {
        // A null string comes back empty (docs/bindings.md, "C++").
        std::string want = res && res->type == jt::string ? unescape(res) : std::string();
        if (want != v) r.mismatch("string", want, v);
    } else if constexpr (std::is_same_v<T, Date>) {
        if (num(res) != v.serial()) r.mismatch("date", std::to_string(num(res)), std::to_string(v.serial()));
    } else if constexpr (std::is_same_v<T, Period>) {
        nql_period p = v.raw();
        if (num(at(res, 0)) != p.length || num(at(res, 1)) != p.units) r.mismatch("period", "", "");
    } else if constexpr (std::is_same_v<T, std::complex<double>>) {
        if (!same_double(hexdouble(at(res, 0)), v.real()) || !same_double(hexdouble(at(res, 1)), v.imag())) r.mismatch("complex", "", "");
    } else if constexpr (is_optional<T>::value) {
        bool null = !res || res->type == jt::null;
        if (null != !v.has_value()) r.mismatch("has-value", null ? "none" : "a value", v ? "a value" : "none");
        else if (v) check(r, res, *v);
    } else if constexpr (std::is_base_of_v<Object, T>) {
        r.check_object(res, v);
    } else if constexpr (std::is_same_v<T, Matrix>) {
        if (!res || res->type != jt::object) {
            r.mismatch("matrix", "null", "a matrix");
        } else if (num(member(res, "r")) != v.rows() || num(member(res, "c")) != v.columns()) {
            r.mismatch("matrix", "its shape", "another shape");
        } else {
            check(r, member(res, "v"), v.values());
        }
    } else if constexpr (std::is_same_v<T, Distribution>) {
        const std::vector<double>* parts[6] = {&v.x, &v.dx, &v.density, &v.cumulative, &v.excess, &v.average};
        for (int k = 0; k < 6; k++) check(r, at(res, k), *parts[k]);
    } else if constexpr (is_vector<T>::value) {
        // A null collection comes back empty.
        if (!res || res->type != jt::array) {
            if (!v.empty()) r.mismatch("array", "null", "an array");
            return;
        }
        if (res->count != static_cast<int>(v.size())) {
            r.mismatch("array", std::to_string(res->count) + " elements", std::to_string(v.size()) + " elements");
            return;
        }
        for (int k = 0; k < res->count; k++) {
            typename T::value_type x = v[static_cast<std::size_t>(k)];
            check(r, res->items[k], x);
        }
    } else if constexpr (is_pair<T>::value) {
        check(r, at(res, 0), v.first);
        check(r, at(res, 1), v.second);
    } else if constexpr (is_tuple<T>::value) {
        std::apply([&](const auto&... e) {
            int k = 0;
            (check(r, at(res, k++), e), ...);
        }, v);
    } else {
        static_assert(always_false<T>, "no check for the type");
    }
}

// A sequence generator's draws: values by rows, weights, the dimension, the next snapshot.
template <class G>
void check_sequences(rp& r, const jv* res, const std::tuple<nanoql::Matrix, std::vector<double>, G>& v)
{
    check(r, at(res, 0), std::get<0>(v).values());
    check(r, at(res, 1), std::get<1>(v));
    if (num(at(res, 2)) != std::get<0>(v).columns()) r.mismatch("dimension", std::to_string(num(at(res, 2))), std::to_string(std::get<0>(v).columns()));
    r.check_object(at(res, 3), std::get<2>(v));
}

typedef int (*handler)(rp& r, const jv* args, const jv* result, int status);

struct entry {
    const char* name;
    handler run;
};

// The generated table's parts, each ending with a null entry.
extern const entry* const tables[];

}  // namespace replay

#endif
