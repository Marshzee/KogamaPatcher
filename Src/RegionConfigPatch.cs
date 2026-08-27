using System;
using System.Reflection;
using MelonLoader;
using HarmonyLib;

namespace KogamaOfflinePatch
{
    public static class RegionConfigPatch
    {
        private const string EnumTypeName     = "RegionConfigType";
        private const string EnumTypeNestedName = "RegionConfigNamePair+RegionConfigType";
        private const string ManagerTypeName  = "RegionConfigManager";
        private const string NamePairTypeName = "RegionConfigNamePair";
        private static int? _localOrdinal;
        private static bool _applied = false;
        private static bool _didAssemblyScan = false;

        public static void TryApply(HarmonyLib.Harmony harmony)
        {
            if (_applied) return;

            var enumType = FindTypeInAnyAssembly("Il2Cpp." + EnumTypeNestedName)
                        ?? FindTypeInAnyAssembly(EnumTypeNestedName)
                        ?? FindTypeInAnyAssembly("Il2Cpp" + EnumTypeName)
                        ?? FindTypeInAnyAssembly(EnumTypeName);
            if (!_didAssemblyScan)
            {
                _didAssemblyScan = true;
                MelonLogger.Msg("[RegionConfigPatch] Scanning loaded assemblies for Region/Config types...");
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    System.Type[] types;
                    try { types = asm.GetTypes(); }
                    catch { continue; }
                    foreach (var t in types)
                    {
                        if (t.FullName != null &&
                            (t.FullName.IndexOf("Region", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                             t.FullName.IndexOf("Config", System.StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            MelonLogger.Msg($"  - {t.FullName} (in {asm.GetName().Name})");
                        }
                    }
                }

                var mgrType = FindTypeInAnyAssembly("Il2Cpp." + ManagerTypeName);
                if (mgrType != null)
                {
                    MelonLogger.Msg($"[RegionConfigPatch] === {mgrType.FullName} surface ===");
                    foreach (var prop in mgrType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        MelonLogger.Msg($"    prop {prop.PropertyType.Name} {prop.Name}");
                    }
                    foreach (var fld in mgrType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        MelonLogger.Msg($"    field {fld.FieldType.Name} {fld.Name}");
                    }
                }
                var npType = FindTypeInAnyAssembly("Il2Cpp." + NamePairTypeName);
                if (npType != null)
                {
                    MelonLogger.Msg($"[RegionConfigPatch] === {npType.FullName} surface ===");
                    foreach (var prop in npType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        MelonLogger.Msg($"    prop {prop.PropertyType.Name} {prop.Name}");
                    }
                    foreach (var fld in npType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        MelonLogger.Msg($"    field {fld.FieldType.Name} {fld.Name}");
                    }
                }
            }

            if (enumType == null)
            {
                MelonLogger.Msg("[RegionConfigPatch] RegionConfigType enum not yet visible.");
                return;
            }

            if (_localOrdinal == null)
            {
                foreach (var f in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (string.Equals(f.Name, "local", StringComparison.OrdinalIgnoreCase))
                    {
                        _localOrdinal = (int)f.GetRawConstantValue();
                        MelonLogger.Msg($"[RegionConfigPatch] Found RegionConfigType.local = {_localOrdinal}");
                        break;
                    }
                }
                if (_localOrdinal == null)
                {
                    MelonLogger.Warning("[RegionConfigPatch] Could not find RegionConfigType.local enum value.");
                    return;
                }
            }

            var managerType = FindTypeInAnyAssembly("Il2Cpp" + ManagerTypeName)
                           ?? FindTypeInAnyAssembly(ManagerTypeName);
            if (managerType != null)
            {
                MelonLogger.Msg($"[RegionConfigPatch] Found {managerType.FullName} in assembly {managerType.Assembly.GetName().Name}");
                PatchManager(harmony, managerType, enumType);
            }

            var namePairType = FindTypeInAnyAssembly("Il2Cpp" + NamePairTypeName)
                            ?? FindTypeInAnyAssembly(NamePairTypeName);
            if (namePairType != null)
            {
                MelonLogger.Msg($"[RegionConfigPatch] Found {namePairType.FullName}");
                PatchNamePair(harmony, namePairType, enumType);
            }
            if (managerType != null)
            {
                PatchDetectRegion(harmony, managerType, enumType);
            }
            if (namePairType != null)
            {
                PatchRegionNameToType(harmony, namePairType, enumType);
            }

            _applied = true;
        }
        private static Type FindTypeInAnyAssembly(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName, throwOnError: false); }
                catch { /* skip broken assemblies */ }
                if (t != null) return t;
            }
            return null;
        }
        private static void PatchManager(HarmonyLib.Harmony harmony, Type managerType, Type enumType)
        {
            int patched = 0;
            foreach (var prop in managerType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                if (prop.PropertyType == enumType && prop.CanRead && prop.GetGetMethod(true) != null)
                {
                    var getter = prop.GetGetMethod(true);
                    var prefix = typeof(RegionConfigPatch).GetMethod(nameof(Prefix_EnumReturn),
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(getter, prefix: new HarmonyMethod(prefix));
                    patched++;
                    MelonLogger.Msg($"[RegionConfigPatch] Patched {managerType.FullName}.{prop.Name} (enum return)");
                }
                else if (prop.PropertyType == typeof(string) &&
                         (prop.Name.IndexOf("region", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          prop.Name.IndexOf("config", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var getter = prop.GetGetMethod(true);
                    var prefix = typeof(RegionConfigPatch).GetMethod(nameof(Prefix_StringReturn),
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(getter, prefix: new HarmonyMethod(prefix));
                    patched++;
                    MelonLogger.Msg($"[RegionConfigPatch] Patched {managerType.FullName}.{prop.Name} (string return)");
                }
            }
            MelonLogger.Msg($"[RegionConfigPatch] {patched} RegionConfigManager properties patched.");
        }
        private static void PatchNamePair(HarmonyLib.Harmony harmony, Type namePairType, Type enumType)
        {
            int patched = 0;
            foreach (var prop in namePairType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (prop.PropertyType == enumType && prop.CanRead && prop.GetGetMethod(true) != null)
                {
                    var getter = prop.GetGetMethod(true);
                    var prefix = typeof(RegionConfigPatch).GetMethod(nameof(Prefix_EnumReturn),
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(getter, prefix: new HarmonyMethod(prefix));
                    patched++;
                    MelonLogger.Msg($"[RegionConfigPatch] Patched {namePairType.FullName}.{prop.Name} (enum)");
                }
                else if (prop.PropertyType == typeof(string) && prop.CanRead)
                {
                    var getter = prop.GetGetMethod(true);
                    var prefix = typeof(RegionConfigPatch).GetMethod(nameof(Prefix_StringReturn),
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(getter, prefix: new HarmonyMethod(prefix));
                    patched++;
                }
            }
            MelonLogger.Msg($"[RegionConfigPatch] {patched} RegionConfigNamePair properties patched.");
        }
                public static bool Prefix_EnumReturn(ref object __result, MethodBase __originalMethod)
        {
            if (_localOrdinal == null) return true; 

            Type enumType =
                __result != null ? __result.GetType() :
                __originalMethod is MethodInfo mi ? mi.ReturnType :
                null;

            if (enumType == null || !enumType.IsEnum) return true;

            __result = System.Enum.ToObject(enumType, _localOrdinal.Value);
            return false;
        }
        public static bool Prefix_StringReturn(ref string __result)
        {
            __result = "local";
            return false;
        }
        private static void PatchDetectRegion(HarmonyLib.Harmony harmony, Type managerType, Type enumType)
        {
            foreach (var method in managerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (method.Name != "DetectRegionFromEnvironment") continue;
                var rt = method.ReturnType;
                if (rt != enumType) continue;

                var prefix = typeof(RegionConfigPatch).GetMethod(nameof(Prefix_EnumReturn),
                    BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                MelonLogger.Msg($"[RegionConfigPatch] Patched {managerType.FullName}.DetectRegionFromEnvironment");
                return;
            }
            MelonLogger.Msg($"[RegionConfigPatch] DetectRegionFromEnvironment not found (already removed or renamed).");
        }
        private static void PatchRegionNameToType(HarmonyLib.Harmony harmony, Type namePairType, Type enumType)
        {
            foreach (var method in namePairType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (method.Name != "RegionNameToType") continue;
                if (method.ReturnType != enumType) continue;
                var ps = method.GetParameters();
                if (ps.Length != 1 || ps[0].ParameterType != typeof(string)) continue;

                var prefix = typeof(RegionConfigPatch).GetMethod(nameof(Prefix_EnumReturn),
                    BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                MelonLogger.Msg($"[RegionConfigPatch] Patched {namePairType.FullName}.RegionNameToType(String)");
                return;
            }
            MelonLogger.Msg($"[RegionConfigPatch] RegionNameToType not found.");
        }
    }
}