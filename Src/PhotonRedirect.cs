// FUCK YOU PHOTON - From Marshze <3

using System;
using System.Reflection;
using MelonLoader;
using HarmonyLib;

namespace KogamaOfflinePatch
{
    public static class PhotonRedirect
    {

        private const string LocalServer = "127.0.0.1";
        private const string LocalAppId = "any";
        private const string PhotonPeerTypeName = "ExitGames.Client.Photon.PhotonPeer";

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            var peerType =
                FindTypeInAnyAssembly("Il2Cpp" + PhotonPeerTypeName) ??
                FindTypeInAnyAssembly(PhotonPeerTypeName);
            if (peerType == null)
            {
                MelonLogger.Error($"[PhotonRedirect] Type '{PhotonPeerTypeName}' (or Il2Cpp-prefixed) not found. Photon3Unity3D version mismatch?");
                return;
            }

            MelonLogger.Msg($"[PhotonRedirect] Found {peerType.FullName} in assembly {peerType.Assembly.GetName().Name}");

            int hookedCount = 0;
            foreach (var method in peerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.Name != "Connect") continue;
                var ps = method.GetParameters();
                if (ps.Length < 2 || ps.Length > 3) continue;
                if (ps[0].ParameterType != typeof(string)) continue;
                if (ps[1].ParameterType != typeof(string)) continue;

                var prefix = new HarmonyMethod(typeof(PhotonRedirect), nameof(Prefix_PhotonPeer_Connect)); harmony.Patch(method, prefix: prefix);
                hookedCount++;
                MelonLogger.Msg($"[PhotonRedirect] Hooked PhotonPeer.Connect({string.Join(", ", System.Linq.Enumerable.Select(ps, p => p.ParameterType.Name))})");
            }

            if (hookedCount == 0)
            {
                MelonLogger.Error("[PhotonRedirect] No PhotonPeer.Connect overloads were patched. Aborting redirect.");
                return;
            }
        }
        public static void Prefix_PhotonPeer_Connect(object[] __args)
        {
            MelonLogger.Msg("[PhotonRedirect] PhotonPeer.Connect called.");
            if (__args == null || __args.Length < 2) return;

            string originalAddress = __args[0] as string;
            string originalAppId   = __args[1] as string;

            MelonLogger.Msg($"[PhotonRedirect] PhotonPeer.Connect original args: address='{originalAddress}', appId='{originalAppId}'");

            __args[0] = "127.0.0.1";
            __args[1] = "any";
            MelonLogger.Msg("[PhotonRedirect] Forced arguments to address='127.0.0.1', appId='any'");
        }

        public static void Postfix_PhotonPeer_Connect(Exception __exception)
        {
            if (__exception != null)
            {
                MelonLogger.Warning($"[PhotonRedirect] PhotonPeer.Connect threw exception: {__exception}");
            }
            else
            {
                MelonLogger.Msg("[PhotonRedirect] PhotonPeer.Connect returned successfully.");
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
