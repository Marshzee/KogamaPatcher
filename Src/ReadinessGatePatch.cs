using System;
using System.Reflection;
using MelonLoader;
using HarmonyLib;

namespace KogamaOfflinePatch
{
    public static class ReadinessGatePatch
    {
        private const string TargetTypeName   = "GameNamespace.GameSession";
        private const string TargetMethodName = "WaitForGameStart"; // or "IsGameReady", etc... MA

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            var t = Type.GetType(TargetTypeName);
            if (t == null)
            {
                MelonLogger.Error($"[ReadinessGatePatch] Type '{TargetTypeName}' not found.");
                return;
            }

            var method = t.GetMethod(TargetMethodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                MelonLogger.Error($"[ReadinessGatePatch] Method '{TargetMethodName}' not found on {TargetTypeName}.");
                return;
            }

            MelonLogger.Msg($"[ReadinessGatePatch] Hooked {t.FullName}.{method.Name}.");
        }

        public static bool Prefix_AlwaysTrue(ref bool __result)
        {
            __result = true;
            return false;
        }

        public static bool Prefix_Skip()
        {
            return false;
        }
    }
}