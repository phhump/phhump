using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace StellarIndustries.Core.Util
{
    /// <summary>
    /// Reflection-based mapper between plain C# objects (public instance fields) and the
    /// generic JSON tree produced by <see cref="Json"/>. Fields marked [NonSerialized] are skipped.
    /// Missing fields keep their default values, unknown fields are ignored, which makes
    /// save data tolerant to additive schema changes.
    /// </summary>
    public static class JsonMapper
    {
        public static object ToTree(object obj)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            if (obj is string || obj is bool) return obj;
            if (t.IsEnum) return obj.ToString();
            if (t.IsPrimitive || obj is decimal) return Convert.ToDouble(obj, CultureInfo.InvariantCulture);
            if (obj is IDictionary dict)
            {
                var d = new Dictionary<string, object>();
                foreach (DictionaryEntry e in dict) d[Convert.ToString(e.Key, CultureInfo.InvariantCulture)] = ToTree(e.Value);
                return d;
            }
            if (obj is IList list)
            {
                var l = new List<object>(list.Count);
                foreach (var item in list) l.Add(ToTree(item));
                return l;
            }
            var result = new Dictionary<string, object>();
            foreach (var f in Fields(t)) result[f.Name] = ToTree(f.GetValue(obj));
            return result;
        }

        public static T FromTree<T>(object tree) => (T)FromTree(typeof(T), tree);

        public static object FromTree(Type t, object tree)
        {
            if (tree == null) return t.IsValueType ? Activator.CreateInstance(t) : null;
            if (t == typeof(string)) return Convert.ToString(tree, CultureInfo.InvariantCulture);
            if (t == typeof(bool)) return tree is bool b ? b : Convert.ToDouble(tree, CultureInfo.InvariantCulture) != 0;
            if (t.IsEnum)
            {
                var s = Convert.ToString(tree, CultureInfo.InvariantCulture);
                try { return Enum.Parse(t, s, true); } catch { return Activator.CreateInstance(t); }
            }
            if (t.IsPrimitive) return Convert.ChangeType(Convert.ToDouble(tree, CultureInfo.InvariantCulture), t, CultureInfo.InvariantCulture);

            if (t.IsArray)
            {
                var src = tree as IList ?? new List<object>();
                var et = t.GetElementType();
                var arr = Array.CreateInstance(et, src.Count);
                for (int i = 0; i < src.Count; i++) arr.SetValue(FromTree(et, src[i]), i);
                return arr;
            }
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                var et = t.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(t);
                if (tree is IList src) foreach (var item in src) list.Add(FromTree(et, item));
                return list;
            }
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var args = t.GetGenericArguments();
                if (args[0] != typeof(string)) throw new NotSupportedException("Only string-keyed dictionaries are supported");
                var dict = (IDictionary)Activator.CreateInstance(t);
                if (tree is IDictionary src) foreach (DictionaryEntry e in src) dict[e.Key] = FromTree(args[1], e.Value);
                return dict;
            }

            var obj = Activator.CreateInstance(t);
            if (tree is IDictionary<string, object> map)
            {
                foreach (var f in Fields(t))
                {
                    if (!map.TryGetValue(f.Name, out var v)) continue;
                    try { f.SetValue(obj, FromTree(f.FieldType, v)); }
                    catch (Exception) { /* keep default value for malformed fields */ }
                }
            }
            return obj;
        }

        static readonly Dictionary<Type, FieldInfo[]> cache = new Dictionary<Type, FieldInfo[]>();

        static FieldInfo[] Fields(Type t)
        {
            lock (cache)
            {
                if (cache.TryGetValue(t, out var fs)) return fs;
                var all = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
                var keep = new List<FieldInfo>();
                foreach (var f in all) if (!f.IsNotSerialized && !f.IsInitOnly) keep.Add(f);
                fs = keep.ToArray();
                cache[t] = fs;
                return fs;
            }
        }
    }
}
