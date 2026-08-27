using System;
using System.IO;
using System.Reflection;
using MelonLoader;
using HarmonyLib;

namespace KogamaOfflinePatch
{
    public static class MapLoaderPatch
    {
        private const string LocalApiUrl            = "http://127.0.0.1:8080";
        private const string LocalStreamingAssetsUrl = "http://127.0.0.1:8080/static";

        private static string LocalMapsFolder =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KogamaMaps");

        private const string UrlsTypeName = "MV.Common.Urls";

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            var urlsType = FindTypeInAnyAssembly("Il2Cpp" + UrlsTypeName)
                        ?? FindTypeInAnyAssembly(UrlsTypeName);
            if (urlsType == null)
            {
                MelonLogger.Error($"[MapLoaderPatch] Type '{UrlsTypeName}' not found. If MVCommon is named differently, search your dump for it.");
                return;
            }

            MelonLogger.Msg($"[MapLoaderPatch] Found {urlsType.FullName} in assembly {urlsType.Assembly.GetName().Name}");

            foreach (var method in urlsType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "Init") continue;
                var ps = method.GetParameters();
                if (ps.Length != 2) continue;
                if (ps[0].ParameterType != typeof(string)) continue;
                if (ps[1].ParameterType != typeof(string)) continue;

                harmony.Patch(method, prefix: new HarmonyMethod(typeof(MapLoaderPatch), nameof(Prefix_Urls_Init)));
                MelonLogger.Msg($"[MapLoaderPatch] Hooked MV.Common.Urls.Init.");
            }
            PatchProperty(harmony, urlsType, "API",            nameof(Getter_API));
            PatchProperty(harmony, urlsType, "StreamingAssets", nameof(Getter_StreamingAssets));

            MelonLogger.Msg($"[MapLoaderPatch] Drop .kgm files into: {LocalMapsFolder}");
            MelonLogger.Msg($"[MapLoaderPatch] Routing API → {LocalApiUrl}");
            MelonLogger.Msg($"[MapLoaderPatch] Routing StreamingAssets → {LocalStreamingAssetsUrl}");
        }

        private static void PatchProperty(HarmonyLib.Harmony harmony, Type urlsType, string propName, string getterName)
        {
            var prop = urlsType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (prop == null)
            {
                MelonLogger.Warning($"[MapLoaderPatch] Property '{propName}' not found on {urlsType.FullName}.");
                return;
            }
            var getter = prop.GetGetMethod(nonPublic: true);
            if (getter == null)
            {
                MelonLogger.Warning($"[MapLoaderPatch] Getter for '{propName}' not found.");
                return;
            }
            MelonLogger.Msg($"[MapLoaderPatch] Patching {urlsType.FullName}.{propName} getter");
            harmony.Patch(getter, prefix: new HarmonyMethod(typeof(MapLoaderPatch), getterName)); // Careful who you call ugly in Highschool when they can write code like this (AI would never code this, cope with it you fuckass vibe coder)
        }
        public static void Prefix_Urls_Init(ref string apiUrl, ref string streamingAssetsUrl)
        {
            MelonLogger.Msg($"[MapLoaderPatch] Urls.Init called with api='{apiUrl}' streamingAssets='{streamingAssetsUrl}'");
            apiUrl             = LocalApiUrl;
            streamingAssetsUrl = LocalStreamingAssetsUrl;
            MelonLogger.Msg($"[MapLoaderPatch] Urls.Init rewritten → api='{LocalApiUrl}' streamingAssets='{LocalStreamingAssetsUrl}'");
        }
        public static bool Getter_API(ref string __result)
        {
            __result = LocalApiUrl + "/";
            return false;
        }
        public static bool Getter_StreamingAssets(ref string __result)
        {
            __result = LocalStreamingAssetsUrl + "/";
            return false;
        }
        private static Type FindTypeInAnyAssembly(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName, throwOnError: false); }
                catch { } // ignore shit inside this catch xddddddddd (Professional code btw, PROFESSIONAL)
                if (t != null) return t;
            }
            return null;
        }
    }
}
