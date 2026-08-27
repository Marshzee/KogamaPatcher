using System;
using System.Reflection;
using MelonLoader;
using HarmonyLib;

namespace KogamaOfflinePatch
{
    public static class UnityWebRequestSpy
    {
        private const string UnityWebRequestTypeName =
            "UnityEngine.Networking.UnityWebRequest";
        private const int MaxLogs = 50;
        private static int _logCount = 0;
        private static bool _applied = false;

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            if (_applied) return;
            _applied = true;

            var uwrType =
                FindTypeInAnyAssembly("Il2Cpp" + UnityWebRequestTypeName) ??
                FindTypeInAnyAssembly(UnityWebRequestTypeName);
            if (uwrType == null)
            {
                MelonLogger.Warning(
                    $"[UnityWebRequestSpy] Type '{UnityWebRequestTypeName}' not found. " +
                    $"Module not loaded yet? Will rely on other patches.");
                return;
            }

            MelonLogger.Msg($"[UnityWebRequestSpy] Found {uwrType.FullName} in assembly {uwrType.Assembly.GetName().Name}");

            int hooked = 0;
            var urlProp = uwrType.GetProperty(
                "url",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (urlProp != null)
            {
                var getter = urlProp.GetGetMethod(nonPublic: true);
                if (getter != null)
                {
                    harmony.Patch(
                        getter,
                        prefix: new HarmonyMethod(typeof(UnityWebRequestSpy), nameof(Prefix_Get_URL)));
                    hooked++;
                    MelonLogger.Msg($"[UnityWebRequestSpy] Hooked UnityWebRequest.url getter");
                }
            }

            foreach (var method in uwrType.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (method.Name == "Get")
                {
                    var ps = method.GetParameters();
                    if (ps.Length >= 1 && ps[0].ParameterType == typeof(string))
                    {
                        harmony.Patch(
                            method,
                            prefix: new HarmonyMethod(typeof(UnityWebRequestSpy), nameof(Prefix_Factory_Get)));
                        hooked++;
                        MelonLogger.Msg($"[UnityWebRequestSpy] Hooked UnityWebRequest.Get({string.Join(", ", System.Linq.Enumerable.Select(ps, p => p.ParameterType.Name))})");
                    }
                }
                else if (method.Name == "Post")
                {
                    var ps = method.GetParameters();
                    if (ps.Length >= 1 && ps[0].ParameterType == typeof(string))
                    {
                        harmony.Patch(
                            method,
                            prefix: new HarmonyMethod(typeof(UnityWebRequestSpy), nameof(Prefix_Factory_Post)));
                        hooked++;
                        MelonLogger.Msg($"[UnityWebRequestSpy] Hooked UnityWebRequest.Post({string.Join(", ", System.Linq.Enumerable.Select(ps, p => p.ParameterType.Name))})");
                    }
                }
            }

            MelonLogger.Msg($"[UnityWebRequestSpy] {hooked} UnityWebRequest method(s) hooked (spy mode — no rewriting).");
        }
        public static void Prefix_Get_URL(object __instance, ref string __result)
        {
            if (_logCount >= MaxLogs) return;
            try
            {
                var fld = __instance.GetType().GetField(
                    "m_Url",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                string url = fld?.GetValue(__instance) as string;
                if (!string.IsNullOrEmpty(url))
                {
                    MelonLogger.Msg($"[UnityWebRequestSpy] UWR.url read: '{url}'");
                    _logCount++;
                }
            }
            catch { }
        }
        public static void Prefix_Factory_Get(object[] __args)
        {
            if (__args == null || __args.Length < 1) return;
            string url = __args[0] as string;
            if (string.IsNullOrEmpty(url)) return;
            if (_logCount < MaxLogs)
            {
                MelonLogger.Msg($"[UnityWebRequestSpy] UnityWebRequest.Get('{url}')");
                _logCount++;
            }
            if (url.IndexOf("kogama.com", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                url.IndexOf("kogama.", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string newUrl = "http://127.0.0.1:8080" + ExtractPathAndQuery(url);
                MelonLogger.Msg($"[UnityWebRequestSpy]   → redirected to '{newUrl}'");
                __args[0] = newUrl;
            }
        }
        public static void Prefix_Factory_Post(object[] __args)
        {
            if (__args == null || __args.Length < 1) return;
            string url = __args[0] as string;
            if (string.IsNullOrEmpty(url)) return;
            if (_logCount < MaxLogs)
            {
                MelonLogger.Msg($"[UnityWebRequestSpy] UnityWebRequest.Post('{url}')");
                _logCount++;
            }
            if (url.IndexOf("kogama.com", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                url.IndexOf("kogama.", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string newUrl = "http://127.0.0.1:8080" + ExtractPathAndQuery(url);
                MelonLogger.Msg($"[UnityWebRequestSpy]   → redirected to '{newUrl}'");
                __args[0] = newUrl;
            }
        }
        private static string ExtractPathAndQuery(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.PathAndQuery;
            }
            catch
            {
                int schemeEnd = url.IndexOf("://", System.StringComparison.Ordinal);
                if (schemeEnd < 0) return url;
                int pathStart = url.IndexOf('/', schemeEnd + 3);
                if (pathStart < 0) return "/";
                return url.Substring(pathStart);
            }
        }
        private static Type FindTypeInAnyAssembly(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName, throwOnError: false); }
                catch { }
                if (t != null) return t;
            }
            return null;
        }
    }
}
