using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mafi;
using Mafi.Core;

namespace UtilitiesPP
{
    public static class BlueprintBuildersPatch
    {
        private static object s_constructionMgr;
        private static FieldInfo s_ongoingConstructionsField;
        private static FieldInfo s_ongoingDeconstructionsField;
        private static MethodInfo s_markConstructedMethod;
        private static MethodInfo s_markDeconstructedMethod;
        private static PropertyInfo s_kvpKeyProp;

        private static object s_instaBuildMgr;
        private static MethodInfo s_setInstaBuildMethod;
        private static PropertyInfo s_isInstaBuildEnabledProp;

        private static object s_simLoopEvents;
        private static object s_updateAfterCmdProcEvent;
        private static MethodInfo s_eventAddMethod;

        public sealed class BlueprintBuildersSubscriber { }
        private static readonly BlueprintBuildersSubscriber s_subscriptionOwner = new BlueprintBuildersSubscriber();
        private static Action s_handlerDelegate;

        private static bool s_initialized;

        public static void LateInit(DependencyResolver resolver)
        {
            s_constructionMgr = null;
            s_ongoingConstructionsField = null;
            s_ongoingDeconstructionsField = null;
            s_markConstructedMethod = null;
            s_markDeconstructedMethod = null;
            s_kvpKeyProp = null;
            s_instaBuildMgr = null;
            s_setInstaBuildMethod = null;
            s_isInstaBuildEnabledProp = null;
            s_simLoopEvents = null;
            s_updateAfterCmdProcEvent = null;
            s_eventAddMethod = null;
            s_initialized = false;

            try
            {
                var cmType = UtilitiesReflection.GetTypeOrWarn(
                    "Mafi.Core.Entities.Static.ConstructionManager, Mafi.Core",
                    "Utilities++ blueprint builders (construction manager)");
                if (cmType != null)
                {
                    var opt = resolver.TryResolve(cmType);
                    if (opt.HasValue)
                    {
                        s_constructionMgr = opt.Value;
                        s_ongoingConstructionsField = UtilitiesReflection.GetFieldOrWarn(
                            cmType, "m_ongoingConstructions",
                            BindingFlags.Instance | BindingFlags.NonPublic,
                            "Utilities++ blueprint builders (ongoing constructions field)");
                        s_ongoingDeconstructionsField = UtilitiesReflection.GetFieldOrWarn(
                            cmType, "m_ongoingDeconstructions",
                            BindingFlags.Instance | BindingFlags.NonPublic,
                            "Utilities++ blueprint builders (ongoing deconstructions field)");
                        s_markConstructedMethod = UtilitiesReflection.GetMethodOrWarn(
                            cmType, "MarkConstructed",
                            BindingFlags.Instance | BindingFlags.Public,
                            "Utilities++ blueprint builders (MarkConstructed)");
                        s_markDeconstructedMethod = UtilitiesReflection.GetMethodOrWarn(
                            cmType, "MarkDeconstructed",
                            BindingFlags.Instance | BindingFlags.Public,
                            "Utilities++ blueprint builders (MarkDeconstructed)");
                    }
                }

                var ibmType = ResolveInstaBuildManagerType();
                if (ibmType != null)
                {
                    var opt = resolver.TryResolve(ibmType);
                    if (opt.HasValue)
                    {
                        s_instaBuildMgr = opt.Value;
                        s_setInstaBuildMethod = ibmType.GetMethod("SetInstaBuild",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        s_isInstaBuildEnabledProp = ibmType.GetProperty("IsInstaBuildEnabled",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                }

                var sleType = UtilitiesReflection.GetTypeOrWarn(
                    "Mafi.Core.Simulation.ISimLoopEvents, Mafi.Core",
                    "Utilities++ blueprint builders (sim loop events interface)");
                if (sleType != null)
                {
                    var opt = resolver.TryResolve(sleType);
                    if (opt.HasValue)
                    {
                        s_simLoopEvents = opt.Value;
                        var updateAfterCmdProcProp = sleType.GetProperty("UpdateAfterCmdProc",
                            BindingFlags.Instance | BindingFlags.Public);
                        if (updateAfterCmdProcProp != null)
                        {
                            s_updateAfterCmdProcEvent = updateAfterCmdProcProp.GetValue(s_simLoopEvents);
                            if (s_updateAfterCmdProcEvent != null)
                            {
                                var eventType = s_updateAfterCmdProcEvent.GetType();
                                MethodInfo openAdd = null;
                                MethodInfo openAddNonSaveable = null;
                                MethodInfo closedAdd = null;
                                MethodInfo closedAddNonSaveable = null;
                                foreach (var m in eventType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
                                {
                                    if (m.Name != "Add" && m.Name != "AddNonSaveable") continue;
                                    var pars = m.GetParameters();
                                    if (pars.Length != 2) continue;
                                    if (pars[1].ParameterType != typeof(Action)) continue;
                                    if (m.IsGenericMethodDefinition)
                                    {
                                        if (m.Name == "AddNonSaveable") openAddNonSaveable = m;
                                        else openAdd = m;
                                    }
                                    else
                                    {
                                        if (m.Name == "AddNonSaveable") closedAddNonSaveable = m;
                                        else closedAdd = m;
                                    }
                                }
                                MethodInfo picked = closedAddNonSaveable ?? closedAdd;
                                if (picked == null && openAddNonSaveable != null)
                                {
                                    try { picked = openAddNonSaveable.MakeGenericMethod(typeof(BlueprintBuildersSubscriber)); }
                                    catch { }
                                }
                                if (picked == null && openAdd != null)
                                {
                                    try { picked = openAdd.MakeGenericMethod(typeof(BlueprintBuildersSubscriber)); }
                                    catch { }
                                }
                                s_eventAddMethod = picked;
                            }
                        }
                    }
                }

                if (s_simLoopEvents != null && s_updateAfterCmdProcEvent != null && s_eventAddMethod != null)
                {
                    s_handlerDelegate = OnUpdateAfterCmdProc;
                    try
                    {
                        s_eventAddMethod.Invoke(s_updateAfterCmdProcEvent,
                            new object[] { s_subscriptionOwner, s_handlerDelegate });
                    }
                    catch
                    {
                    }
                }

                BlueprintBuildersState.OnBlueprintModeChanged -= OnBlueprintModeChanged;
                BlueprintBuildersState.OnBlueprintModeChanged += OnBlueprintModeChanged;
                if (BlueprintBuildersState.BlueprintMode)
                    OnBlueprintModeChanged(true);

                s_initialized = true;
            }
            catch
            {
            }
        }

        private static Type ResolveInstaBuildManagerType()
        {
            var t = UtilitiesReflection.GetTypeOrWarn(
                "Mafi.Core.Utils.InstaBuildManager, Mafi.Core",
                "Utilities++ blueprint builders (insta build manager)");
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in asm.GetTypes())
                        if (type.Name == "InstaBuildManager") return type;
                }
                catch { }
            }
            return null;
        }

        private static void OnBlueprintModeChanged(bool enabled)
        {
            if (!enabled) return;
            ForceDisableInstaBuild();
        }

        private static void ForceDisableInstaBuild()
        {
            try
            {
                if (s_instaBuildMgr == null || s_setInstaBuildMethod == null) return;
                bool currently = false;
                if (s_isInstaBuildEnabledProp != null)
                {
                    try { currently = (bool)s_isInstaBuildEnabledProp.GetValue(s_instaBuildMgr); }
                    catch { }
                }
                if (currently)
                    s_setInstaBuildMethod.Invoke(s_instaBuildMgr, new object[] { false });
            }
            catch
            {
            }
        }

        private static void OnUpdateAfterCmdProc()
        {
            if (!s_initialized) return;
            if (!BlueprintBuildersState.BlueprintMode) return;
            if (s_constructionMgr == null) return;

            DrainConstructions();
            DrainDeconstructions();
        }

        private static void DrainConstructions()
        {
            try
            {
                if (s_ongoingConstructionsField == null || s_markConstructedMethod == null) return;
                var dict = s_ongoingConstructionsField.GetValue(s_constructionMgr) as IEnumerable;
                if (dict == null) return;

                List<object> snapshot = null;
                foreach (var entry in dict)
                {
                    if (entry == null) continue;
                    if (s_kvpKeyProp == null)
                    {
                        var et = entry.GetType();
                        s_kvpKeyProp = et.GetProperty("Key");
                        if (s_kvpKeyProp == null) return;
                    }
                    var ent = s_kvpKeyProp.GetValue(entry);
                    if (ent == null) continue;
                    if (snapshot == null) snapshot = new List<object>();
                    snapshot.Add(ent);
                }

                if (snapshot == null) return;

                for (int i = 0; i < snapshot.Count; i++)
                {
                    try
                    {
                        s_markConstructedMethod.Invoke(s_constructionMgr,
                            BindingFlags.OptionalParamBinding | BindingFlags.InvokeMethod, null,
                            new object[] { snapshot[i], Type.Missing, Type.Missing }, null);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static void DrainDeconstructions()
        {
            try
            {
                if (s_ongoingDeconstructionsField == null || s_markDeconstructedMethod == null) return;
                var dict = s_ongoingDeconstructionsField.GetValue(s_constructionMgr) as IEnumerable;
                if (dict == null) return;

                List<object> snapshot = null;
                foreach (var entry in dict)
                {
                    if (entry == null) continue;
                    if (s_kvpKeyProp == null)
                    {
                        var et = entry.GetType();
                        s_kvpKeyProp = et.GetProperty("Key");
                        if (s_kvpKeyProp == null) return;
                    }
                    var ent = s_kvpKeyProp.GetValue(entry);
                    if (ent == null) continue;
                    if (snapshot == null) snapshot = new List<object>();
                    snapshot.Add(ent);
                }

                if (snapshot == null) return;

                for (int i = 0; i < snapshot.Count; i++)
                {
                    try
                    {
                        s_markDeconstructedMethod.Invoke(s_constructionMgr,
                            BindingFlags.OptionalParamBinding | BindingFlags.InvokeMethod, null,
                            new object[] { snapshot[i], Type.Missing, Type.Missing, Type.Missing }, null);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }
}
