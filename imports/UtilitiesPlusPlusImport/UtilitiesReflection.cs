using System;
using System.Collections.Generic;
using System.Reflection;

namespace UtilitiesPP
{
    public static class UtilitiesReflection
    {
        private const string TAG = "[U++]";
        private static readonly HashSet<string> s_warned = new HashSet<string>();

        public static Type GetTypeOrWarn(string assemblyQualifiedOrFull, string featureLabel)
        {
            try
            {
                Type t = Type.GetType(assemblyQualifiedOrFull, false);
                if (t != null) return t;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType(assemblyQualifiedOrFull);
                    if (t != null) return t;
                }
            }
            catch (Exception ex) { WarnOnce("type:" + assemblyQualifiedOrFull, featureLabel, ex.Message); return null; }
            WarnOnce("type:" + assemblyQualifiedOrFull, featureLabel, "type not found");
            return null;
        }

        public static MethodInfo GetMethodOrWarn(Type t, string name, BindingFlags flags, string featureLabel, Type[] paramTypes = null)
        {
            if (t == null) { WarnOnce("method:?." + name, featureLabel, "owner type is null"); return null; }
            try
            {
                var m = paramTypes != null ? t.GetMethod(name, flags, null, paramTypes, null) : t.GetMethod(name, flags);
                if (m != null) return m;
            }
            catch (Exception ex) { WarnOnce("method:" + t.FullName + "." + name, featureLabel, ex.Message); return null; }
            WarnOnce("method:" + t.FullName + "." + name, featureLabel, "method not found");
            return null;
        }

        public static FieldInfo GetFieldOrWarn(Type t, string name, BindingFlags flags, string featureLabel)
        {
            if (t == null) { WarnOnce("field:?." + name, featureLabel, "owner type is null"); return null; }
            try
            {
                var f = t.GetField(name, flags);
                if (f != null) return f;
            }
            catch (Exception ex) { WarnOnce("field:" + t.FullName + "." + name, featureLabel, ex.Message); return null; }
            WarnOnce("field:" + t.FullName + "." + name, featureLabel, "field not found");
            return null;
        }

        public static PropertyInfo GetPropertyOrWarn(Type t, string name, BindingFlags flags, string featureLabel)
        {
            if (t == null) { WarnOnce("prop:?." + name, featureLabel, "owner type is null"); return null; }
            try
            {
                var p = t.GetProperty(name, flags);
                if (p != null) return p;
            }
            catch (Exception ex) { WarnOnce("prop:" + t.FullName + "." + name, featureLabel, ex.Message); return null; }
            WarnOnce("prop:" + t.FullName + "." + name, featureLabel, "property not found");
            return null;
        }

        public static MethodInfo PickMethodOrWarn(Type t, Func<MethodInfo, bool> predicate, BindingFlags flags, string featureLabel, string searchHint)
        {
            if (t == null) { WarnOnce("pick:?." + searchHint, featureLabel, "owner type is null"); return null; }
            try
            {
                foreach (var m in t.GetMethods(flags))
                {
                    if (predicate(m)) return m;
                }
            }
            catch (Exception ex) { WarnOnce("pick:" + t.FullName + "." + searchHint, featureLabel, ex.Message); return null; }
            WarnOnce("pick:" + t.FullName + "." + searchHint, featureLabel, "no method matched predicate");
            return null;
        }

        private static void WarnOnce(string key, string feature, string detail)
        {
            if (!s_warned.Add(key)) return;
        }
    }
}
