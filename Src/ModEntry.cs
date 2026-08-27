using System;
using System.Reflection;
using System.Collections.Generic;
using MelonLoader;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Newtonsoft.Json;

[assembly: MelonInfo(
    typeof(KogamaOfflinePatch.KogamaOfflinePatch),
    "KogamaOfflinePatch",
    "3.5.3",
    "Marshal")]

[assembly: MelonGame("Multiverse ApS", "KoGaMa")]

namespace KogamaOfflinePatch
{
    public class KogamaOfflinePatch : MelonMod
    {
        private bool _il2cppPatchesApplied = false;
        private int _attemptCount = 0;
        private const int MaxPatchAttempts = 30;
        private bool _startGameScheduled = false;
        private bool _renderersEnabled = false;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Kogama Offline Patch v0.1.0");
            MelonLogger.Msg("Photon redirect (Well...Umm, yeah) 127.0.0.1:5055 (local Photon Server)");
            MelonLogger.Msg("Map loader (Well yeah) http://127.0.0.1:8080");
            MelonLogger.Msg("IL2CPP patches will be applied on first scene load...");
            System.AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;
        }
        private void OnAssemblyLoaded(object sender, System.AssemblyLoadEventArgs args)
        {
            try
            {
                var asmName = args.LoadedAssembly.GetName().Name ?? "";
                if (asmName.IndexOf("Assembly-CSharp", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MelonLogger.Msg($"[KogamaOfflinePatch] {asmName} loaded applying Assembly-CSharp patches NOW (before any Awake).");
                    RegionConfigPatch.TryApply(HarmonyInstance);
                    BypassMVGameControllerInit.Apply(HarmonyInstance);
                }
            }
            catch { }
        }
        private void ProbeAvatarFieldsFixed(UnityEngine.GameObject avatarGO)
        {
            MelonLogger.Msg("--- PROBE: AvatarLocal Runtime Type Test ---");

            try
            {
                var comps = avatarGO.GetComponentsInChildren<UnityEngine.Component>(true);

                foreach (var c in comps)
                {
                    if (c == null)
                        continue;

                    if (!(c is Il2CppObjectBase il2cppObj))
                        continue;
                    var namePtr = IL2CPP.il2cpp_class_get_name(il2cppObj.ObjectClass);

                    if (namePtr == IntPtr.Zero)
                        continue;

                    var nativeName = Marshal.PtrToStringAnsi(namePtr);

                    if (nativeName != "AvatarLocal")
                        continue;

                    MelonLogger.Msg(
                        $"[Probe] Found AvatarLocal natively. " +
                        $"Wrapper was: {c.GetType().FullName}"
                    );
                    System.Type trueType =
                        FindTypeInAnyAssembly("Il2Cpp.AvatarLocal") ??
                        FindTypeInAnyAssembly("AvatarLocal");

                    if (trueType == null)
                    {
                        MelonLogger.Warning(
                            "[Probe] Could not resolve managed System.Type for AvatarLocal."
                        );
                        continue;
                    }

                    MelonLogger.Msg(
                        $"[Probe] Resolved System.Type: {trueType.FullName}"
                    );

                    var ptrProp = il2cppObj.GetType().GetProperty("Pointer", BindingFlags.Public | BindingFlags.Instance);
                    if (ptrProp == null) return;
                    var nativePtr = (IntPtr)ptrProp.GetValue(il2cppObj);

                    var ptrCtor = trueType.GetConstructor(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null, new[] { typeof(IntPtr) }, null);

                    if (ptrCtor == null)
                    {
                        MelonLogger.Warning("[Probe] Could not find IntPtr constructor for AvatarLocal.");
                        continue;
                    }

                    object trueAvatarLocalInstance = ptrCtor.Invoke(new object[] { nativePtr });
                    MelonLogger.Msg("[Probe] Successfully created true AvatarLocal wrapper via IntPtr.");

                    var mvAvatarProp = trueType.GetProperty("mvAvatar", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mvAvatarProp == null)
                    {
                        MelonLogger.Warning("[Probe] mvAvatar property was not found.");
                        continue;
                    }

                    object mvAvatarValue = mvAvatarProp.GetValue(trueAvatarLocalInstance);
                    if (mvAvatarValue == null)
                    {
                        MelonLogger.Msg("[Probe] RESULT: mvAvatar is NULL.");
                        continue;
                    }

                    MelonLogger.Msg($"[Probe] RESULT: mvAvatar is NON-NULL! Type: {mvAvatarValue.GetType().FullName}");

                    MelonLogger.Msg("[Probe] --- Dumping mvAvatar Fields ---");
                    var mvFields = mvAvatarValue.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var f in mvFields)
                    {
                        try
                        {
                            object val = f.GetValue(mvAvatarValue);
                            string valType = val != null ? val.GetType().FullName : "NULL";
                            MelonLogger.Msg($"  [Field] {f.FieldType.Name} {f.Name} = {valType}");
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Msg($"  [Field] {f.FieldType.Name} {f.Name} (Error: {ex.Message})");
                        }
                    }

                    MelonLogger.Msg("[Probe] --- Dumping mvAvatar Properties ---");
                    var mvProps = mvAvatarValue.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var p in mvProps)
                    {
                        if (!p.CanRead) continue;
                        try
                        {
                            object val = p.GetValue(mvAvatarValue);
                            string valType = val != null ? val.GetType().FullName : "NULL";
                            MelonLogger.Msg($"  [Prop]  {p.PropertyType.Name} {p.Name} = {valType}");
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Msg($"  [Prop]  {p.PropertyType.Name} {p.Name} (Error: {ex.Message})");
                        }
                    }

                    break;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error(
                    $"[Probe] AvatarLocal runtime inspection failed: {ex}"
                );
            }

            MelonLogger.Msg("--- END PROBE ---");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName != "DesktopBase")
            {
                BypassMVGameControllerInit._startGameInvoked = false;
            }

            MelonLogger.Msg($"[KogamaOfflinePatch] OnSceneWasLoaded fired: '{sceneName}' (buildIndex={buildIndex}, patchesApplied={_il2cppPatchesApplied})");

            if (_il2cppPatchesApplied) return;

            _attemptCount++;
            MelonLogger.Msg($"[KogamaOfflinePatch] Scene loaded: '{sceneName}' (buildIndex={buildIndex}, attempt {_attemptCount}/{MaxPatchAttempts})");

            System.Type peerType =
                FindTypeInAnyAssembly("Il2CppExitGames.Client.Photon.PhotonPeer") ??
                FindTypeInAnyAssembly("ExitGames.Client.Photon.PhotonPeer");

            if (peerType == null)
            {
                MelonLogger.Msg($"[KogamaOfflinePatch] PhotonPeer still not registered will retry on next scene.");
                return;
            }
            MelonLogger.Msg($"[KogamaOfflinePatch] PhotonPeer found: {peerType.FullName}");

            RegionConfigPatch.TryApply(HarmonyInstance);
            BypassMVGameControllerInit.Apply(HarmonyInstance);

            MelonLogger.Msg("[KogamaOfflinePatch] PhotonPeer type is registered applying patches...");

            PhotonRedirect.Apply(HarmonyInstance);
            PhotonInProcessStub.Apply(HarmonyInstance);
            MapLoaderPatch.Apply(HarmonyInstance);
            UnityWebRequestSpy.Apply(HarmonyInstance);

            _il2cppPatchesApplied = true;
            MelonLogger.Msg("[KogamaOfflinePatch] Patches applied.");
        }

      private void ProbeChunkInstances()
        {
            MelonLogger.Msg("--- PROBE: MVCubeModelInstance.chunkInstances ---");
            try
            {
                var cubeModelType = FindTypeInAnyAssembly("Il2Cpp.MVCubeModelInstance") ?? FindTypeInAnyAssembly("MVCubeModelInstance");
                if (cubeModelType == null)
                {
                    MelonLogger.Warning("[Probe] MVCubeModelInstance type not found.");
                    return;
                }

                var probeGO = new UnityEngine.GameObject("Probe_MVCubeModelInstance");
                
                var il2cppType = Il2CppType.From(cubeModelType);
                var component = probeGO.AddComponent(il2cppType);
                
                if (component == null)
                {
                    MelonLogger.Warning("[Probe] Failed to add MVCubeModelInstance component.");
                    return;
                }

                MelonLogger.Msg($"[Probe] Successfully added component: {component.GetType().Name}");

                System.Reflection.FieldInfo chunkField = null;
                for (System.Type t = component.GetType(); t != null; t = t.BaseType)
                {
                    chunkField = t.GetField("chunkInstances", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (chunkField != null)
                    {
                        MelonLogger.Msg($"[Probe] Found 'chunkInstances' field on {t.FullName}");
                        break;
                    }
                }

                if (chunkField == null)
                {
                    MelonLogger.Warning("[Probe] 'chunkInstances' field not found in type hierarchy.");
                    return;
                }
                object chunkInstancesValue = chunkField.GetValue(component);

                if (chunkInstancesValue == null)
                {
                    MelonLogger.Msg("[Probe] RESULT: chunkInstances is NULL.");
                }
                else
                {
                    MelonLogger.Msg("[Probe] RESULT: chunkInstances is NON-NULL.");
                    MelonLogger.Msg($"[Probe] C# Type: {chunkInstancesValue.GetType().FullName}");
                                        if (chunkInstancesValue is Il2CppObjectBase il2cppObj)
                    {
                        try 
                        {
                            var classPtr = il2cppObj.ObjectClass;
                            var namePtr = IL2CPP.il2cpp_class_get_name(classPtr);
                            var nativeName = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(namePtr);
                            MelonLogger.Msg($"[Probe] Native IL2CPP Class: {nativeName} (Ptr: {classPtr})");
                        }
                        catch (System.Exception ex)
                        {
                            MelonLogger.Warning($"[Probe] Failed to get native IL2CPP class info: {ex.Message}");
                        }
                    }
                    else
                    {
                        MelonLogger.Msg("[Probe] Value does not inherit from Il2CppObjectBase.");
                    }
                }
                UnityEngine.Object.Destroy(probeGO);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[Probe] FAILED: {ex.Message}");
            }
            MelonLogger.Msg("--- END PROBE ---");
        }

        public override void OnUpdate()
        {
            if (_il2cppPatchesApplied && !BypassMVGameControllerInit._startGameInvoked && !_startGameScheduled)
            {
                var desktopType = BypassMVGameControllerInit.FindTypeInAnyAssembly("Il2Cpp.MVGameControllerDesktop") ?? BypassMVGameControllerInit.FindTypeInAnyAssembly("MVGameControllerDesktop");
                if (desktopType != null)
                {
                    var instProp = desktopType.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (instProp != null)
                    {
                        var inst = instProp.GetValue(null, null);
                        if (inst != null)
                        {
                            MelonLogger.Msg("[KogamaOfflinePatch] OnUpdate detected StartGame() was missed. Scheduling invocation for next frame.");
                            _startGameScheduled = true;
                            MelonCoroutines.Start(DelayedForceStartGame(inst));
                        }
                    }
                }
            }

            if (_il2cppPatchesApplied && BypassMVGameControllerInit._cachedSpawnerPosition != UnityEngine.Vector3.zero)
            {
                try
                {
                    var controllerType = BypassMVGameControllerInit.FindTypeInAnyAssembly("Il2Cpp.MVGameControllerBase");
                    if (controllerType != null)
                    {
                        var localPlayerProp = controllerType.GetProperty("LocalPlayer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (localPlayerProp != null)
                        {
                            var localPlayer = localPlayerProp.GetValue(null, null);
                            if (localPlayer != null)
                            {
                                var posProp = localPlayer.GetType().GetProperty("Position");
                                if (posProp != null && posProp.CanWrite)
                                {
                                    posProp.SetValue(localPlayer, BypassMVGameControllerInit._cachedSpawnerPosition);
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            if (_il2cppPatchesApplied && BypassMVGameControllerInit._cachedSpawnerPosition != UnityEngine.Vector3.zero && !_renderersEnabled)
            {
                _renderersEnabled = true;
                MelonLogger.Msg("[KogamaOfflinePatch] Spawner found enabling renderers...");
                MelonCoroutines.Start(EnableRenderersDelayed());
            }

            if (_il2cppPatchesApplied)
            {
                BypassMVGameControllerInit.DriveJoinStateForward();
            }
        }
        private System.Collections.IEnumerator DelayedForceStartGame(object instance)
        {
            yield return null;
            BypassMVGameControllerInit.ForceStartGame(instance);
        }
        private System.Collections.IEnumerator EnableRenderersDelayed()
        {
            yield return new UnityEngine.WaitForSeconds(1f);            
            try
            {
                UnityEngine.RenderSettings.fog = false;
                MelonLogger.Msg("[KogamaOfflinePatch] Disabled fog.");
                UnityEngine.RenderSettings.ambientLight = new UnityEngine.Color(0.5f, 0.6f, 0.8f, 1f);
                
                var mainCam = UnityEngine.Camera.main;
                if (mainCam != null)
                {
                    mainCam.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
                    mainCam.backgroundColor = new UnityEngine.Color(0.4f, 0.7f, 1.0f, 1f);
                }
            }
            catch { }

            for (int pass = 0; pass < 5; pass++)
            {
                try
                {
                    var renderers = UnityEngine.Object.FindObjectsOfType<UnityEngine.MeshRenderer>();
                    int enabled = 0;
                    foreach (var r in renderers)
                    {
                        if (r != null && !r.enabled) { r.enabled = true; enabled++; }
                    }
                    MelonLogger.Msg($"[KogamaOfflinePatch] Pass {pass + 1}: Enabled {enabled} renderers.");
                }
                catch { }
                yield return new UnityEngine.WaitForSeconds(2f);
            }
        }

        private void TryCreateAvatar()
        {
            MelonLogger.Msg("[KogamaOfflinePatch] TryCreateAvatar() no-op (avatar handled by TryCloneAndActivateAvatar).");
        }

                private void ProbeAvatarFields(UnityEngine.GameObject avatarGO)
        {
            MelonLogger.Msg("--- PROBE: Avatar Field Inspection ---");
            var comps = avatarGO.GetComponentsInChildren<UnityEngine.Component>(true);
            
            foreach (var c in comps)
            {
                if (c == null) continue;
                
                bool isRelevant = false;
                for (System.Type t = c.GetType(); t != null; t = t.BaseType)
                {
                    string tName = t.FullName ?? "";
                    if (tName.Contains("Avatar") || tName.Contains("WorldObject") || tName.Contains("CubeModel") || tName.Contains("Body"))
                    {
                        isRelevant = true;
                        break;
                    }
                }

                if (!isRelevant) continue;

                MelonLogger.Msg($"[Probe] Inspecting Component: {c.GetType().Name} on GameObject: {c.gameObject.name}");

                var fields = c.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var f in fields)
                {
                    string fTypeFullName = f.FieldType.FullName ?? "";
                    if (fTypeFullName.Contains("Body") || fTypeFullName.Contains("CubeModel") || fTypeFullName.Contains("Avatar") || fTypeFullName.Contains("WorldObject"))
                    {
                        try
                        {
                            object val = f.GetValue(c);
                            string valType = val != null ? val.GetType().FullName : "NULL";
                            MelonLogger.Msg($"  [Field] {f.FieldType.Name} {f.Name} = {valType}");
                        }
                        catch (System.Exception ex)
                        {
                            MelonLogger.Msg($"  [Field] {f.FieldType.Name} {f.Name} (Error reading: {ex.Message})");
                        }
                    }
                }

                var props = c.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var p in props)
                {
                    string pTypeFullName = p.PropertyType.FullName ?? "";
                    if (pTypeFullName.Contains("Body") || pTypeFullName.Contains("CubeModel") || pTypeFullName.Contains("Avatar") || pTypeFullName.Contains("WorldObject"))
                    {
                        if (!p.CanRead) continue;
                        try
                        {
                            object val = p.GetValue(c);
                            string valType = val != null ? val.GetType().FullName : "NULL";
                            MelonLogger.Msg($"  [Prop]  {p.PropertyType.Name} {p.Name} = {valType}");
                        }
                        catch (System.Exception ex)
                        {
                            MelonLogger.Msg($"  [Prop]  {p.PropertyType.Name} {p.Name} (Error reading: {ex.Message})");
                        }
                    }
                }
            }
            MelonLogger.Msg("--- END PROBE ---");
        }

        private UnityEngine.Color32 GetColor(int mat)
        {
            switch (mat)
            {
                case 0: return new UnityEngine.Color32(255, 128, 128, 255);
                case 1: return new UnityEngine.Color32(255, 0, 0, 255);
                case 2: return new UnityEngine.Color32(128, 0, 0, 255);
                case 3: return new UnityEngine.Color32(204, 128, 77, 255);
                case 4: return new UnityEngine.Color32(178, 128, 230, 255);
                case 5: return new UnityEngine.Color32(128, 128, 255, 255);
                case 6: return new UnityEngine.Color32(0, 0, 255, 255);
                case 7: return new UnityEngine.Color32(0, 0, 128, 255);
                case 8: return new UnityEngine.Color32(128, 64, 0, 255);
                case 9: return new UnityEngine.Color32(128, 0, 128, 255);
                case 10: return new UnityEngine.Color32(128, 255, 128, 255);
                case 11: return new UnityEngine.Color32(0, 255, 0, 255);
                case 12: return new UnityEngine.Color32(0, 128, 0, 255);
                case 15: return new UnityEngine.Color32(255, 153, 51, 255);
                case 16: return new UnityEngine.Color32(255, 128, 0, 255);
                case 18: return new UnityEngine.Color32(230, 204, 128, 255);
                case 19: return new UnityEngine.Color32(204, 178, 102, 255);
                case 20: return new UnityEngine.Color32(178, 178, 178, 255);
                case 21: return new UnityEngine.Color32(128, 128, 128, 255);
                case 22: return new UnityEngine.Color32(77, 77, 77, 255); 
                case 23: return new UnityEngine.Color32(0, 0, 0, 255);
                default: return new UnityEngine.Color32(255, 255, 255, 255);
            }
        }
        private static System.Type FindTypeInAnyAssembly(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type t = null;
                try { t = asm.GetType(fullName, throwOnError: false); }
                catch { }
                if (t != null) return t;
            }
            return null;
        }
    }

    public class BodyPartData
    {
        public List<VoxelData> voxels { get; set; }
    }

    public class VoxelData
    {
        public int x { get; set; }
        public int y { get; set; }
        public int z { get; set; }
        public int mat { get; set; }
        public List<int> corners { get; set; }
    }
}
