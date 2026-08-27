
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MelonLoader;
using HarmonyLib;

namespace KogamaOfflinePatch
{
    public static class BypassMVGameControllerInit
    {
        public static UnityEngine.Vector3 _cachedSpawnerPosition = UnityEngine.Vector3.zero;
        private const string ControllerTypeName  = "MVGameControllerBase";
        private const string InitMethodName      = "Initialize";
        private const string AwakeMethodName     = "Awake";
        public static object _cachedLocalPlayerInstance = null;
        private const string StartMethodName     = "Start";
        public static int _cachedSpawnerWoId = -1;
        public static int _cachedAvatarWoId = -1;
        public static object _cachedSpawnerInstance = null;
        private const string StartGameMethodName = "StartGame";
        private const string UpdateMethodName    = "Update";
        private const string DesktopTypeName     = "MVGameControllerDesktop";

        private static readonly string[] StateMachineTypeNames = new[]
        {
            "MVGameControllerBase+_InitRegionDependent_d__162",
            "MVGameControllerBase+<InitRegionDependent>d__162",
            "Il2Cpp.MVGameControllerBase+_InitRegionDependent_d__162",
            "Il2Cpp.MVGameControllerBase+<InitRegionDependent>d__162",

            "LoadingScreenBackground+_WaitForSessionDataCoroutine_d__5",
            "LoadingScreenBackground+<WaitForSessionDataCoroutine>d__5",
            "Il2Cpp.LoadingScreenBackground+_WaitForSessionDataCoroutine_d__5",
            "Il2Cpp.LoadingScreenBackground+<WaitForSessionDataCoroutine>d__5",

            "LoadingScreenHandler+_LoadingBarAnimation_d__23",
            "LoadingScreenHandler+_FadeInAnimation_d__24",
            "LoadingScreenHandler+<LoadingBarAnimation>d__23",
            "LoadingScreenHandler+<FadeInAnimation>d__24",
            "Il2Cpp.LoadingScreenHandler+_LoadingBarAnimation_d__23",
            "Il2Cpp.LoadingScreenHandler+_FadeInAnimation_d__24",
        };
        private const string MoveNextMethodName  = "MoveNext";

        private static bool _applied = false;

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            if (_applied) return;

            var controllerType =
                FindTypeInAnyAssembly("Il2Cpp." + ControllerTypeName) ??
                FindTypeInAnyAssembly(ControllerTypeName);
            if (controllerType == null)
            {
                MelonLogger.Warning($"[KoGaMaPatch] {ControllerTypeName} not loaded yet  will retry.");
                return;
            }

            MelonLogger.Msg($"[KoGaMaPatch] Found {controllerType.FullName}");



            var desktopType =
                FindTypeInAnyAssembly("Il2Cpp." + DesktopTypeName) ??
                FindTypeInAnyAssembly(DesktopTypeName);

            if (desktopType != null)
            {
                var desktopInit = desktopType.GetMethod(
                    InitMethodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null,
                    types: System.Type.EmptyTypes,
                    modifiers: null);
                if (desktopInit != null && desktopInit.DeclaringType == desktopType)
                {
                    harmony.Patch(desktopInit, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), "Postfix_Initialize"));
                    MelonLogger.Msg($"[KoGaMaPatch] Hooked {DesktopTypeName}.{InitMethodName}");
                }
                else
                {
                    MelonLogger.Msg($"[KoGaMaPatch] {DesktopTypeName}.{InitMethodName} not overridden here  will fall back to base hook.");
                    var baseInit = controllerType.GetMethod(
                        InitMethodName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (baseInit != null)
                    {
                        harmony.Patch(baseInit, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_Initialize)));
                        MelonLogger.Msg($"[KoGaMaPatch] Hooked {ControllerTypeName}.{InitMethodName} (base only, no override)");
                    }
                }
            }
            else
            {
                var initMethod = controllerType.GetMethod(
                    InitMethodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (initMethod != null)
                {
                    harmony.Patch(initMethod, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_Initialize)));
                    MelonLogger.Msg($"[KoGaMaPatch] Hooked {ControllerTypeName}.{InitMethodName}");
                }

var mvLocalObjectControllerType = FindTypeInAnyAssembly("Il2Cpp.MVLocalObjectController") ?? FindTypeInAnyAssembly("MVLocalObjectController");
if (mvLocalObjectControllerType != null)
{
    foreach (var m in mvLocalObjectControllerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
    {
        if (m.Name == "Update" || m.Name == "OnPhotonEvent" || m.Name == "HandleAttachWorldObjectToSeat")
        {
            try { harmony.Patch(m, finalizer: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Finalizer_SwallowException))); } catch { }
        }
    }

        var lobbyStateControllerType = FindTypeInAnyAssembly("Il2Cpp.LobbyStatePlayModeController") ?? FindTypeInAnyAssembly("LobbyStatePlayModeController");
        if (lobbyStateControllerType != null)
        {
            var lobbyUpdateMethod = lobbyStateControllerType.GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
            if (lobbyUpdateMethod != null)
            {
                harmony.Patch(lobbyUpdateMethod, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_ForceLobbyMenuOpen)));
                MelonLogger.Msg("[KoGaMaPatch] Hooked LobbyStatePlayModeController.Update.");
            }
        }
        var eventHandlingType = FindTypeInAnyAssembly("Il2Cpp.MVNetworkGame+EventHandling") ?? FindTypeInAnyAssembly("MVNetworkGame+EventHandling");
if (eventHandlingType != null)
{
    var handleEventMethod = eventHandlingType.GetMethod("HandleEvent", BindingFlags.NonPublic | BindingFlags.Instance);
    if (handleEventMethod != null)
    {
        harmony.Patch(handleEventMethod, finalizer: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Finalizer_SwallowException)));
        MelonLogger.Msg("[KoGaMaPatch] Hooked MVNetworkGame.EventHandling.HandleEvent (swallow).");
    }
}
}
            }
            System.Reflection.MethodInfo targetAwake = null;
            if (desktopType != null)
            {
                var desktopAwake = desktopType.GetMethod(
                    AwakeMethodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null,
                    types: System.Type.EmptyTypes,
                    modifiers: null);
                if (desktopAwake != null && desktopAwake.DeclaringType == desktopType)
                {
                    targetAwake = desktopAwake;
                    MelonLogger.Msg($"[KoGaMaPatch] {DesktopTypeName}.{AwakeMethodName} is overridden  hooking it.");
                }
            }
            if (targetAwake == null)
            {

                var awakeMethod = controllerType.GetMethod(
                    AwakeMethodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (awakeMethod != null)
                {
                    targetAwake = awakeMethod;
                    MelonLogger.Msg($"[KoGaMaPatch] {AwakeMethodName} is on base  hooking base.");
                }
            }
            if (targetAwake != null)
            {

                harmony.Patch(targetAwake,
                    prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_Awake)),
                    postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_Awake_InvokeStartGame)));
                MelonLogger.Msg($"[KoGaMaPatch] Hooked {targetAwake.DeclaringType?.Name}.{AwakeMethodName} (prefix: synthesize data, postfix: invoke StartGame)");
            }

            if (desktopType != null)
            {
                MelonLogger.Msg($"[KoGaMaPatch] Found {desktopType.FullName}");
                foreach (var mname in new[] { StartMethodName, StartGameMethodName, UpdateMethodName })
                {

                    var m = desktopType.GetMethod(
                        mname,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        binder: null,
                        types: System.Type.EmptyTypes,
                        modifiers: null);

                    var allOverloads = desktopType.GetMethods(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var candidate in allOverloads)
                    {
                        if (candidate.Name != mname) continue;
                        if (candidate.DeclaringType != desktopType)
                        {
                            MelonLogger.Msg($"[KoGaMaPatch] Skipped {candidate.DeclaringType?.Name}.{mname} (not on most-derived)");
                            continue;
                        }

                        if (mname == StartMethodName)
                        {
                            harmony.Patch(candidate, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_Start_InvokeStartGameIfMissing)));
                            MelonLogger.Msg($"[KoGaMaPatch] Hooked {DesktopTypeName}.{mname} (Trace + StartGameIfMissing)");
                        }
                        else
                        {
                            harmony.Patch(candidate, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_Trace)));
                            MelonLogger.Msg($"[KoGaMaPatch] Hooked {DesktopTypeName}.{mname}");
                        }
                    }
                }
            }
            else
            {

                MelonLogger.Warning($"[KoGaMaPatch] No {DesktopTypeName} found  falling back to base hooks (may be wrong).");
                foreach (var mname in new[] { StartMethodName, StartGameMethodName, UpdateMethodName })
                {
                    var m = controllerType.GetMethod(
                        mname,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m != null)
                    {
                        if (mname == StartMethodName)
                        {
                            harmony.Patch(m, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_Start_InvokeStartGameIfMissing)));
                            MelonLogger.Msg($"[KoGaMaPatch] Hooked {ControllerTypeName}.{mname} (Trace + StartGameIfMissing)");
                        }
                        else
                        {
                            harmony.Patch(m, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_Trace)));
                            MelonLogger.Msg($"[KoGaMaPatch] Hooked {ControllerTypeName}.{mname}");
                        }
                    }
                }
            }

            Type smType = null;
            string smTypeNameFound = null;
            foreach (var name in StateMachineTypeNames)
            {
                smType = FindTypeInAnyAssembly(name);
                if (smType != null) { smTypeNameFound = name; break; }
            }
            if (smType != null)
            {
                MelonLogger.Msg($"[KoGaMaPatch] Found state machine: {smType.FullName} (searched as '{smTypeNameFound}')");
                var moveNext = smType.GetMethod(
                    MoveNextMethodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (moveNext != null)
                {
                    harmony.Patch(moveNext, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_MoveNext_Prepare)));
                    MelonLogger.Msg($"[KoGaMaPatch] Hooked MoveNext  coroutine will end on first tick.");
                }
            }
            else
            {
                MelonLogger.Warning($"[KoGaMaPatch] State machine type not found. Searched: {string.Join(", ", StateMachineTypeNames)}");

                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "Assembly-CSharp") continue;
                    System.Type[] types;
                    try { types = asm.GetTypes(); } catch { continue; }
                    foreach (var t in types)
                    {
                        if (t.FullName != null && t.FullName.Contains("d__"))
                            MelonLogger.Msg($"[KoGaMaPatch]   (state machine candidate) {t.FullName}");
                    }
                }
            }
            TryHookWaitUntil(harmony);
            TrySynthesizeGameSessionData();
            TryHookLoadingScreenHandlers(harmony);

var mvNetworkGameType = FindTypeInAnyAssembly("Il2Cpp.MVNetworkGame") ?? FindTypeInAnyAssembly("MVNetworkGame");
if (mvNetworkGameType != null)
{

    var onOpResponseMethod = mvNetworkGameType.GetMethod("OnOperationResponse", BindingFlags.Public | BindingFlags.Instance);
    if (onOpResponseMethod != null)
    {
        harmony.Patch(onOpResponseMethod, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_OnOperationResponse)));
        MelonLogger.Msg("[KoGaMaPatch] Hooked MVNetworkGame.OnOperationResponse.");
    }
    var worldProp = mvNetworkGameType.GetProperty("World");
    if (worldProp != null && worldProp.CanRead)
    {
        var getter = worldProp.GetGetMethod();
        if (getter != null)
        {
            harmony.Patch(getter, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_Get_World)));
            MelonLogger.Msg("[KoGaMaPatch] Hooked MVNetworkGame.World getter.");
        }
    }

    var wocmProp = mvNetworkGameType.GetProperty("WorldObjectClientManager");
    if (wocmProp != null && wocmProp.CanRead)
    {
        var getter = wocmProp.GetGetMethod();
        if (getter != null)
        {
            harmony.Patch(getter, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_Get_WOCM)));
            MelonLogger.Msg("[KoGaMaPatch] Hooked MVNetworkGame.WorldObjectClientManager getter.");
        }
    }
}
            TryHookAdvancedDiagnostics(harmony);

            try
            {
                var sceneMgrType = typeof(UnityEngine.SceneManagement.SceneManager);
                var sceneMgrMethods = sceneMgrType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                int hookedCount = 0;
                foreach (var m in sceneMgrMethods)
                {
                    if (m.Name != "LoadScene" && m.Name != "LoadSceneAsync") continue;
                    
                    var ps = m.GetParameters();
                    if (ps.Length >= 1 && ps[0].ParameterType == typeof(string))
                    {
                        harmony.Patch(m, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), "Prefix_LoadScene_BlockMenu_String"));
                        hookedCount++;
                    }
                    else if (ps.Length >= 1 && ps[0].ParameterType == typeof(int))
                    {
                        harmony.Patch(m, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), "Prefix_LoadScene_BlockMenu_Int"));
                        hookedCount++;
                    }
                }
                MelonLogger.Msg($"[KoGaMaPatch] Hooked {hookedCount} SceneManager.LoadScene/LoadSceneAsync overloads to prevent menu fallback.");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[KoGaMaPatch] Failed to hook SceneManager.LoadScene: {ex.Message}");
            }

            var pcType = FindTypeInAnyAssembly("Il2Cpp.MVPlayerContainer") ?? FindTypeInAnyAssembly("MVPlayerContainer");
            if (pcType != null)
            {
    var getPlayerMethod = pcType.GetMethod("GetPlayerUnsafe", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    if (getPlayerMethod != null)
    {
        harmony.Patch(getPlayerMethod, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_GetPlayerUnsafe)));
        MelonLogger.Msg("[KoGaMaPatch] Hooked MVPlayerContainer.GetPlayerUnsafe.");
    }

var mvPlayerContainerType = FindTypeInAnyAssembly("Il2Cpp.MVPlayerContainer") ?? FindTypeInAnyAssembly("MVPlayerContainer");
if (mvPlayerContainerType != null)
{
    var getItemMethod = mvPlayerContainerType.GetMethod("get_Item", new[] { typeof(int) });
    if (getItemMethod != null)
    {
                harmony.Patch(getItemMethod, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_Get_Item)));
        MelonLogger.Msg("[KoGaMaPatch] Hooked MVPlayerContainer.get_Item (safe indexer).");
    }

        var lobbyStateControllerType = FindTypeInAnyAssembly("Il2Cpp.LobbyStatePlayModeController") ?? FindTypeInAnyAssembly("LobbyStatePlayModeController");
        if (lobbyStateControllerType != null)
        {
            var lobbyInitMethod = lobbyStateControllerType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
            if (lobbyInitMethod != null)
            {
                harmony.Patch(lobbyInitMethod, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_LobbyStateController_Initialize)));
                MelonLogger.Msg("[KoGaMaPatch] Hooked LobbyStatePlayModeController.Initialize.");
            }
        }
}
var pkgClientType = FindTypeInAnyAssembly("Il2Cpp.KoGaMaPackageClient") ?? FindTypeInAnyAssembly("KoGaMaPackageClient");
if (pkgClientType != null)
{
    foreach (var m in pkgClientType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
    {
        if (m.Name == "WorldObjectFactory")
        {
            harmony.Patch(
                m,
                postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_WorldObjectFactory_CacheSpawner)),
                finalizer: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Finalizer_SwallowException_WithRetryQueue))
            );
            MelonLogger.Msg($"[KoGaMaPatch] Hooked KoGaMaPackageClient.WorldObjectFactory (postfix + finalizer).");
        }
        else if (m.Name == "AddWorldObject" || m.Name == "AddPrototype" || m.Name == "AddLink" || m.Name == "AddObjectLink" || m.Name == "HandleDeserializedData")
        {
            harmony.Patch(m, finalizer: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Finalizer_SwallowException)));
            MelonLogger.Msg($"[KoGaMaPatch] Hooked KoGaMaPackageClient.{m.Name} (swallow).");
        }
    }
}

string[] typesToPatch = {
    "MVWorldObjectClient",
    "MVCubeModelBase",
    "MVVehicleBase",
    "MVBody",
    "MVBlueprintBase",
    "MVAvatar",
    "MVLocalPlayer"
};
string[] methodsToSwallow = {
    "CreateWorldObject", "ApplyData", "InstantiatePrefab", 
    "SetupBusinessLogic", "GetTransformData", "InitializeCommon", "AttachCubes", "Initialize",
    "SuspendCurrentSpawnRole", "UnSuspendCurrentSpawnRole", "CreateSpawnRoleFailed", "CreateSpawnRole"
};

foreach (var typeName in typesToPatch)
{
    var t = FindTypeInAnyAssembly("Il2Cpp." + typeName) ?? FindTypeInAnyAssembly(typeName);
    if (t == null) continue;
    
    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
    {
        if (methodsToSwallow.Contains(m.Name) && m.DeclaringType == t)
        {
            try
            {
                harmony.Patch(m, finalizer: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Finalizer_SwallowException)));
                MelonLogger.Msg($"[KoGaMaPatch] Hooked {typeName}.{m.Name} (swallow).");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[KoGaMaPatch] Failed to hook {typeName}.{m.Name}: {ex.Message}");
            }
        }
    }
}

var mvAvatarTypeForCtor = FindTypeInAnyAssembly("Il2Cpp.MVAvatar") ?? FindTypeInAnyAssembly("MVAvatar");
if (mvAvatarTypeForCtor != null)
{
    foreach (var ctor in mvAvatarTypeForCtor.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
    {
        try
        {
            harmony.Patch(ctor, finalizer: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Finalizer_SwallowException)));
            MelonLogger.Msg("[KoGaMaPatch] Hooked MVAvatar constructor with exception-swallowing finalizer.");
        }
        catch { }
    }
}
var cubeModelBaseType = FindTypeInAnyAssembly("Il2Cpp.MVCubeModelBase") ?? FindTypeInAnyAssembly("MVCubeModelBase");
if (cubeModelBaseType != null)
{
    foreach (var ctor in cubeModelBaseType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
    {
        harmony.Patch(ctor, finalizer: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Finalizer_SwallowException)));
    }
    MelonLogger.Msg("[KoGaMaPatch] Hooked MVCubeModelBase constructors with exception-swallowing finalizer.");
}


var worldNetType = FindTypeInAnyAssembly("Il2Cpp.WorldNetwork") ?? FindTypeInAnyAssembly("WorldNetwork");
if (worldNetType != null)
{
    var wocmProp = worldNetType.GetProperty("WorldObjectClientManagerNetwork");
    if (wocmProp != null)
    {
        var getter = wocmProp.GetGetMethod();
        if (getter != null)
        {
            harmony.Patch(getter, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_Get_WorldObjectClientManagerNetwork)));
            MelonLogger.Msg("[KoGaMaPatch] Hooked WorldNetwork.WorldObjectClientManagerNetwork getter.");
        }
    }

    var remnProp = worldNetType.GetProperty("RuntimeEventManagerNetwork");
    if (remnProp != null)
    {
        var getter = remnProp.GetGetMethod();
        if (getter != null)
        {
            harmony.Patch(getter, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_Get_RuntimeEventManagerNetwork)));
            MelonLogger.Msg("[KoGaMaPatch] Hooked WorldNetwork.RuntimeEventManagerNetwork getter.");
        }
    }
}
    try
{
    var mgrType = FindTypeInAnyAssembly("Il2Cpp.MVWorldObjectClientManagerNetwork") ?? FindTypeInAnyAssembly("MVWorldObjectClientManagerNetwork");
    if (mgrType != null)
    {
        var addMethod = mgrType.GetMethod("AddToWorldObjects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (addMethod != null)
        {
            harmony.Patch(addMethod, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_AddToWorldObjects_SkipNull)));
            MelonLogger.Msg("[KoGaMaPatch] Hooked MVWorldObjectClientManagerNetwork.AddToWorldObjects (null-skip).");
        }
        else
        {
            MelonLogger.Warning("[KoGaMaPatch] AddToWorldObjects method not found.");
        }
    }
    else
    {
        MelonLogger.Warning("[KoGaMaPatch] MVWorldObjectClientManagerNetwork type not found.");
    }
}
catch (Exception ex)
{
    MelonLogger.Warning($"[KoGaMaPatch] Failed to hook AddToWorldObjects: {ex.Message}");
}
try
{
    var groupType = FindTypeInAnyAssembly("Il2Cpp.MVGroup") ?? FindTypeInAnyAssembly("MVGroup");
    if (groupType != null)
    {
        var posChangedNotify = groupType.GetMethod("PositionChangedNotify", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (posChangedNotify != null)
        {
            harmony.Patch(posChangedNotify, finalizer: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Finalizer_SwallowException)));
            MelonLogger.Msg("[KoGaMaPatch] Hooked MVGroup.PositionChangedNotify with exception-swallowing finalizer.");
        }
        else
        {
            MelonLogger.Warning("[KoGaMaPatch] PositionChangedNotify method not found on MVGroup.");
        }
    }
}
catch (Exception ex)
{
    MelonLogger.Warning($"[KoGaMaPatch] Failed to hook MVGroup.PositionChangedNotify: {ex.Message}");
}
            var spawnerType = FindTypeInAnyAssembly("Il2Cpp.MVAvatarSpawnRoleCreator") ?? FindTypeInAnyAssembly("MVAvatarSpawnRoleCreator");
            if (spawnerType != null)
            {
                var initMethod = spawnerType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (initMethod != null)
                {
                    harmony.Patch(initMethod, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_Spawner_Initialize)));
                    MelonLogger.Msg("[KoGaMaPatch] Hooked MVAvatarSpawnRoleCreator.Initialize to cache Spawner ID.");
                }
            }
            try
            {

                var mvAvatarType = FindTypeInAnyAssembly("Il2Cpp.MVAvatar") ?? FindTypeInAnyAssembly("MVAvatar");
                if (mvAvatarType != null)
                {
                    var initMethod = mvAvatarType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
                    if (initMethod != null) harmony.Patch(initMethod, finalizer: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Finalizer_SwallowException)));
                }

                var mvLocalPlayerType = FindTypeInAnyAssembly("Il2Cpp.MVLocalPlayer") ?? FindTypeInAnyAssembly("MVLocalPlayer");
                if (mvLocalPlayerType != null)
                {
                    foreach (var m in mvLocalPlayerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (m.Name == "CreateSpawnRole" || m.Name == "CreateSpawnRoleFailed" || m.Name == "SuspendCurrentSpawnRole" || m.Name == "UnSuspendCurrentSpawnRole")
                        {
                            try { harmony.Patch(m, finalizer: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Finalizer_SwallowException))); } catch { }
                        }
                    }
                }

                if (mvAvatarType != null)
                {
                    foreach (var ctor in mvAvatarType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        try
                        {
                            harmony.Patch(ctor, finalizer: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Finalizer_SwallowException)));
                        }
                        catch { }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error("[KoGaMaPatch] Error during final Harmony patches: " + ex.Message);
            }
try
{
    var worldNetworkType = FindTypeInAnyAssembly("Il2Cpp.WorldNetwork") ?? FindTypeInAnyAssembly("WorldNetwork");
    if (worldNetworkType != null)
    {
        foreach (var m in worldNetworkType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (m.Name == "AddLink" || m.Name == "AddObjectLink")
            {
                harmony.Patch(m, finalizer: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Finalizer_SwallowException)));
                MelonLogger.Msg($"[KoGaMaPatch] Hooked WorldNetwork.{m.Name} with exception-swallowing finalizer.");
            }
        }
    }
    else
    {
        MelonLogger.Warning("[KoGaMaPatch] WorldNetwork type not found  can't make AddLink resilient.");
    }
}
catch (Exception ex)
{
    MelonLogger.Warning($"[KoGaMaPatch] Failed to hook WorldNetwork.AddLink/AddObjectLink: {ex.Message}");
}

System.Type mediatorType = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    System.Type[] types;
    try { types = asm.GetTypes(); }
    catch { continue; }
    foreach (var t in types)
    {
        if (t.Name == "SpawnRoleDataMediator")
        {
            mediatorType = t;
            break;
        }
    }
    if (mediatorType != null) break;
}

if (mediatorType != null)
{
    try
    {
        _dummyMediator = System.Activator.CreateInstance(mediatorType);
        MelonLogger.Msg($"[KoGaMaPatch] Created dummy SpawnRoleDataMediator.");
    }
    catch
    {
        _dummyMediator = CreateUninitializedIl2CppObject(mediatorType);
        MelonLogger.Msg($"[KoGaMaPatch] Created dummy SpawnRoleDataMediator (uninitialized).");
    }


    var mediatorProp = controllerType.GetProperty("SpawnRoleDataMediatorLocal", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
    if (mediatorProp != null)
    {
        var mediatorGetter = mediatorProp.GetGetMethod(true);
        if (mediatorGetter != null)
        {
            harmony.Patch(mediatorGetter, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_GetMediatorLocal)));
            MelonLogger.Msg($"[KoGaMaPatch] Hooked MVGameControllerBase.get_SpawnRoleDataMediatorLocal.");
        }
    }
}
            try
            {
                var dictType = typeof(Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Object, Il2CppSystem.Object>);
                var types = new[] { dictType, dictType };
                
                MethodInfo dchMethod = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.Name == "HashtableFunctions")
                            {

                                var m = t.GetMethod("DeepCopyHashTable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, types, null);
                                if (m != null) { dchMethod = m; break; }
                            }
                        }
                    }
                    catch { }
                    if (dchMethod != null) break;
                }

                if (dchMethod != null)
                {
                    harmony.Patch(dchMethod, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_DeepCopyHashTable)));
                    MelonLogger.Msg($"[KoGaMaPatch] Patched DeepCopyHashTable on {dchMethod.DeclaringType.FullName}.");
                }
                else
                    MelonLogger.Warning("[KoGaMaPatch] DeepCopyHashTable(Dictionary, Dictionary) method not found.");
            }
            catch (System.Exception ex) { MelonLogger.Warning($"[KoGaMaPatch] DeepCopyHashTable patch: {ex.Message}"); }
try
{
    var mcmType = FindTypeInAnyAssembly("Il2Cpp.MainCameraManager")
               ?? FindTypeInAnyAssembly("MainCameraManager");
    if (mcmType != null)
    {
        var updateCamMethod = mcmType.GetMethod("UpdateCamera",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (updateCamMethod != null)
        {
            harmony.Patch(updateCamMethod,
                prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit),
                    nameof(Prefix_SkipUpdateCamera)));
            MelonLogger.Msg("[KoGaMaPatch] Hooked MainCameraManager.UpdateCamera.");
        }
    }
}
catch (System.Exception ex)
{
    MelonLogger.Warning($"[KoGaMaPatch] Failed to hook MainCameraManager.UpdateCamera: {ex.Message}");
}

            try
            {
                System.Type otcType = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { foreach (var t in asm.GetTypes()) { if (t.Name == "ObscuredTypesConverter") { otcType = t; break; } } }
                    catch { }
                    if (otcType != null) break;
                }
                if (otcType != null)
                {
                    MelonLogger.Msg($"[KoGaMaPatch] Found ObscuredTypesConverter: {otcType.FullName} in {otcType.Assembly.GetName().Name}");
                    var cuvMethod = otcType.GetMethod("CreateUnObscuredValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (cuvMethod != null)
                    {
                        harmony.Patch(cuvMethod, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_BypassObscuredConverter)));
                        MelonLogger.Msg("[KoGaMaPatch] Patched CreateUnObscuredValue (bypass).");
                    }
                    else
                    {
                        var methods = otcType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        MelonLogger.Warning($"[KoGaMaPatch] CreateUnObscuredValue not found. Static methods: {string.Join(", ", System.Linq.Enumerable.Select(methods, m => m.Name))}");
                    }
                }
                else
                    MelonLogger.Warning("[KoGaMaPatch] ObscuredTypesConverter type not found in any assembly.");
            }
            catch (System.Exception ex) { MelonLogger.Warning($"[KoGaMaPatch] Obscured patch failed: {ex.Message}"); }
            try
            {
                bool patchedDCH = false;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    System.Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                    catch { continue; }
                    foreach (var t in types)
                    {
                        if (t == null) continue;
                        var m = t.GetMethod("DeepCopyHashTable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (m != null)
                        {
                            harmony.Patch(m, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_DeepCopyHashTable)));
                            MelonLogger.Msg($"[KoGaMaPatch] Patched DeepCopyHashTable on {t.FullName} in {t.Assembly.GetName().Name}.");
                            patchedDCH = true;
                            break;
                        }
                    }
                    if (patchedDCH) break;
                }
                if (!patchedDCH)
                    MelonLogger.Warning("[KoGaMaPatch] DeepCopyHashTable method not found on any type.");
            }
            catch (System.Exception ex) { MelonLogger.Warning($"[KoGaMaPatch] DeepCopyHashTable search: {ex.Message}"); }


            try
            {
                var clientType = FindTypeInAnyAssembly("Il2Cpp.MVWorldObjectClient") ?? FindTypeInAnyAssembly("MVWorldObjectClient");
                if (clientType != null)
                {
                    var dvdpMethod = clientType.GetMethod("DeepCopyWorldObjectDataParameters", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                    if (dvdpMethod != null)
                    {
                        harmony.Patch(dvdpMethod, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_DeepCopyWODP)));
                        MelonLogger.Msg("[KoGaMaPatch] Patched DeepCopyWorldObjectDataParameters (shallow copy).");
                    }
                    else
                        MelonLogger.Warning("[KoGaMaPatch] DeepCopyWorldObjectDataParameters not found even with FlattenHierarchy.");
                }
            }
            catch (System.Exception ex) { MelonLogger.Warning($"[KoGaMaPatch] DeepCopyWODP patch failed: {ex.Message}"); }


            try
            {
                System.Type otcType = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { foreach (var t in asm.GetTypes()) { if (t.Name == "ObscuredTypesConverter") { otcType = t; break; } } }
                    catch { }
                    if (otcType != null) break;
                }
                if (otcType != null)
                {
                    var cuvMethod = otcType.GetMethod("CreateUnObscuredValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (cuvMethod != null)
                    {
                        harmony.Patch(cuvMethod, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_CreateUnObscuredValue)));
                        MelonLogger.Msg("[KoGaMaPatch] Patched CreateUnObscuredValue (decrypt).");
                    }
                }
            }
            catch (System.Exception ex) { MelonLogger.Warning($"[KoGaMaPatch] Obscured patch failed: {ex.Message}"); }

    _applied = true;
  
        }
    }
            public static bool Prefix_DeepCopyWODP(ref object __result, object __instance)
        {
            try
            {
                var newDict = new Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Object, Il2CppSystem.Object>();
                var il2cppObjType = FindTypeInAnyAssembly("Il2CppSystem.Object");
                var il2cppRuntime = FindTypeInAnyAssembly("Il2CppInterop.Runtime.IL2CPP");
                MethodInfo opImpInt = null;
                foreach (var m in il2cppObjType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name == "op_Implicit" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(int))
                    { opImpInt = m; break; }
                }
                var wopType = FindTypeInAnyAssembly("Il2CppMV.WorldObject.WorldObjectDataParameters") ?? FindTypeInAnyAssembly("MV.WorldObject.WorldObjectDataParameters");
                var classStoreType = FindTypeInAnyAssembly("Il2CppInterop.Runtime.Il2CppClassPointerStore`1");
                var wopClassPtr = (IntPtr)classStoreType.MakeGenericType(wopType).GetField("NativeClassPtr", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                var boxM = il2cppRuntime.GetMethod("il2cpp_value_box", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var objCtor = il2cppObjType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(IntPtr) }, null);

                System.Func<int, Il2CppSystem.Object> boxEnum = (intVal) =>
                {
                    IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
                    System.Runtime.InteropServices.Marshal.WriteInt32(ptr, intVal);
                    IntPtr boxedPtr = (IntPtr)boxM.Invoke(null, new object[] { wopClassPtr, ptr });
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
                    return (Il2CppSystem.Object)objCtor.Invoke(new object[] { boxedPtr });
                };
                int type = 0, id = 0, owner = 0, groupId = 0, itemId = 0, previewOwner = 0;
                for (Type t = __instance.GetType(); t != null; t = t.BaseType)
                {
                    try
                    {
                        var p = t.GetProperty("WorldObjectType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p != null) type = (int)p.GetValue(__instance);
                        p = t.GetProperty("Id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p != null) id = (int)p.GetValue(__instance);
                        p = t.GetProperty("OwnerActorNr", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p != null) owner = (int)p.GetValue(__instance);
                        p = t.GetProperty("GroupId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p != null) groupId = (int)p.GetValue(__instance);
                        p = t.GetProperty("ItemId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p != null) itemId = (int)p.GetValue(__instance);
                        p = t.GetProperty("PreviewOwnerProfileId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p != null) previewOwner = (int)p.GetValue(__instance);
                    }
                    catch { }
                }
                newDict[boxEnum(3)] = (Il2CppSystem.Object)opImpInt.Invoke(null, new object[] { type });
                newDict[boxEnum(1)] = (Il2CppSystem.Object)opImpInt.Invoke(null, new object[] { id });
                newDict[boxEnum(9)] = (Il2CppSystem.Object)opImpInt.Invoke(null, new object[] { owner });
                newDict[boxEnum(6)] = (Il2CppSystem.Object)opImpInt.Invoke(null, new object[] { groupId });
                newDict[boxEnum(5)] = (Il2CppSystem.Object)opImpInt.Invoke(null, new object[] { itemId });
                newDict[boxEnum(4)] = (Il2CppSystem.Object)opImpInt.Invoke(null, new object[] { previewOwner });

                __result = newDict;
                return false;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[KoGaMaPatch] Prefix_DeepCopyWODP failed: {ex.Message}");
                return true;
            }
        }
                public static bool Prefix_Clone(object __instance, object[] __args, ref object __result)
        {
            try
            {
                int ownerActorNumber = (int)__args[0];
                int cloneGroupId = (int)__args[1];
                object cloneBookkeeping = __args[2];
                object worldObjects = __args[3];
                object prototypes = __args[4];
                IntPtr cbPtr = IntPtr.Zero;
                for (Type t = cloneBookkeeping.GetType(); t != null; t = t.BaseType)
                {
                    var pp = t.GetProperty("Pointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (pp != null) { cbPtr = (IntPtr)pp.GetValue(cloneBookkeeping); break; }
                }
                if (cbPtr == IntPtr.Zero) { __result = null; return false; }
                int cloneId = System.Runtime.InteropServices.Marshal.ReadInt32(cbPtr, 0x10);
                try
                {
                    var ctrlType = FindTypeInAnyAssembly("Il2Cpp.MVGameControllerBase") ?? FindTypeInAnyAssembly("MVGameControllerBase");
                    var gameProp = ctrlType.GetProperty("Game", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    var gameSetter = gameProp.GetSetMethod(nonPublic: true);
                    if (_cachedPhotonListener != null) gameSetter.Invoke(null, new object[] { _cachedPhotonListener });
                }
                catch { }
                object dataDict = null;
                for (Type t = __instance.GetType(); t != null && dataDict == null; t = t.BaseType)
                {
                    var p = t.GetProperty("Data", BindingFlags.Public | BindingFlags.Instance);
                    if (p != null && p.DeclaringType == t)
                    {
                        dataDict = p.GetValue(__instance);
                        break;
                    }
                }
                if (dataDict == null) { __result = null; return false; }
                var il2cppDict = dataDict as Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Object, Il2CppSystem.Object>;
                if (il2cppDict == null) { __result = null; return false; }
                var newDict = new Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Object, Il2CppSystem.Object>();
                int oldId = 0, oldOwnerActorNr = 0, oldGroudId = 0;
                for (Type t = __instance.GetType(); t != null; t = t.BaseType)
                {
                    var idP = t.GetProperty("Id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (idP != null && oldId == 0) oldId = (int)idP.GetValue(__instance);
                    var oanP = t.GetProperty("OwnerActorNr", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (oanP != null && oldOwnerActorNr == 0) oldOwnerActorNr = (int)oanP.GetValue(__instance);
                    var gidP = t.GetProperty("GroupId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (gidP != null && oldGroudId == 0) oldGroudId = (int)gidP.GetValue(__instance);
                }

                int copyCount = 0;
                foreach (var entry in il2cppDict)
                {
                    object key = entry.Key;
                    object val = entry.Value;
                    object newVal = val;
                    try
                    {

                        if (val is int intVal)
                        {
                            if (intVal == oldId) newVal = cloneId;
                            else if (intVal == oldOwnerActorNr) newVal = ownerActorNumber;
                            else if (intVal == oldGroudId) newVal = cloneGroupId;
                        }
                    }
                    catch { }

                    newDict[(Il2CppSystem.Object)key] = (Il2CppSystem.Object)newVal;
                    copyCount++;
                }

                MelonLogger.Msg($"[KoGaMaPatch] Prefix_Clone: Copied {copyCount} entries from Data. newId={cloneId}");


                var pkgType = FindTypeInAnyAssembly("Il2Cpp.KoGaMaPackageClient") ?? FindTypeInAnyAssembly("KoGaMaPackageClient");
                var factoryMethod = pkgType.GetMethod("WorldObjectFactory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                
                try
                {
                    __result = factoryMethod.Invoke(null, new object[] { newDict, worldObjects, prototypes });
                    if (__result == null) MelonLogger.Error("[KoGaMaPatch] WorldObjectFactory returned NULL (no exception) in Prefix_Clone!");
                }
                catch (System.Exception ex)
                {
                    var inner = ex.InnerException ?? ex;
                    MelonLogger.Error($"[KoGaMaPatch] WorldObjectFactory threw: {inner.GetType().Name}: {inner.Message}");
                }
                System.Runtime.InteropServices.Marshal.WriteInt32(cbPtr, 0x10, cloneId + 1);
                try
                {
                    var mapsField = cloneBookkeeping.GetType().GetField("worldObjectIdsMaps", BindingFlags.Public | BindingFlags.Instance);
                    if (mapsField != null)
                    {
                        object mapsDict = mapsField.GetValue(cloneBookkeeping);
                        if (mapsDict != null)
                        {
                            var dictAddM = mapsDict.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
                            dictAddM.Invoke(mapsDict, new object[] { cloneId - 1, cloneId });
                        }
                    }
                }
                catch { }

                return false;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[KoGaMaPatch] Prefix_Clone failed: {ex.Message}");
                __result = null;
                return false;
            }
        }
                public static bool Prefix_BypassObscuredConverter(object value, ref object __result)
        {
            __result = value;
            return false;
        }

        public static bool Prefix_DeepCopyHashTable(object from, object to)
        {
            try
            {
                var fromDict = from as Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Object, Il2CppSystem.Object>;
                var toDict = to as Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Object, Il2CppSystem.Object>;
                if (fromDict == null || toDict == null) return true;

                foreach (var entry in fromDict)
                {
                    object key = entry.Key;
                    object val = entry.Value;
                    object newVal = val;

                    try
                    {
                        if (val != null)
                        {

                            var fakeActiveField = val.GetType().GetField("fakeValueActive", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (fakeActiveField != null && (bool)fakeActiveField.GetValue(val))
                            {
                                var fakeValueField = val.GetType().GetField("fakeValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (fakeValueField != null)
                                {
                                    newVal = fakeValueField.GetValue(val);
                                }
                            }
                            else
                            {

                                var getDecryptedM = val.GetType().GetMethod("GetDecrypted", Type.EmptyTypes);
                                if (getDecryptedM != null)
                                {
                                    newVal = getDecryptedM.Invoke(val, null);
                                }
                            }
                        }
                    }
                    catch { }

                    toDict[(Il2CppSystem.Object)key] = (Il2CppSystem.Object)newVal;
                }
            }
            catch { }
            return false;
        }

        public static bool Prefix_CreateUnObscuredValue(object obscuredValue, ref object __result)
        {
            try
            {
                if (obscuredValue == null) { __result = null; return false; }


                var getDecryptedM = obscuredValue.GetType().GetMethod("GetDecrypted", Type.EmptyTypes);
                if (getDecryptedM != null)
                {
                    object decrypted = getDecryptedM.Invoke(obscuredValue, null);
                    if (decrypted != null)
                    {


                        __result = decrypted;
                        return false;
                    }
                }
                

                __result = obscuredValue;
                return false;
            }
            catch { }
            __result = obscuredValue;
            return false;
        }
public static object _dummyMediator = null;

public static bool Prefix_SkipUpdateCamera()
{


    return false;
}

public static bool Prefix_GetMediatorLocal(ref object __result)
{
    if (__result == null)
    {
        __result = _dummyMediator;
    }
    return false;
}
private static readonly List<object[]> _retryQueue = new List<object[]>();
private static int _retryPassCount = 0;
private const int MaxRetryPasses = 5;
private static int _totalSwallowedCount = 0;
private static readonly System.Collections.Generic.Dictionary<string, int> _swallowedCounts = new System.Collections.Generic.Dictionary<string, int>();

public static bool Prefix_AddToWorldObjects_SkipNull(object wo)
{
    if (wo == null)
    {
        DriveLog("AddToWorldObjects skipped  wo was null (upstream WorldObjectFactory swallow).");
        return false;
    }

    if (_cachedSpawnerWoId != -1) return true;
    try
    {
        string typeName = wo.GetType().Name;
        if (typeName == "MVAvatarSpawnRoleCreator" || typeName.Contains("SpawnRoleCreator"))
        {

            System.Reflection.PropertyInfo idProp = null;
            for (System.Type t = wo.GetType(); t != null && idProp == null; t = t.BaseType)
            {
                idProp = t.GetProperty("Id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (idProp != null)
            {
                _cachedSpawnerWoId = (int)idProp.GetValue(wo);
                MelonLogger.Msg($"[KoGaMaPatch] *** CACHED SPAWNER ID FROM ADDTOWORLD: {_cachedSpawnerWoId} ***");
            }
        }
    }
    catch (System.Exception ex)
    {
        MelonLogger.Warning($"[KoGaMaPatch] Spawner scan in AddToWorldObjects failed: {ex.Message}");
    }

    return true;
}

public static bool Prefix_Get_Item(ref object __result, object[] __args)
{
    int actorNumber = (int)__args[0];
    if (_cachedLocalPlayerInstance != null)
    {
        __result = _cachedLocalPlayerInstance;
        return false;
    }
    return true;
}

public static void Postfix_LobbyStateController_Initialize(object __instance)
{
    try
    {
        var shouldOpenField = __instance.GetType().GetField("shouldOpenLobbyMenu", BindingFlags.NonPublic | BindingFlags.Instance);
        if (shouldOpenField != null)
        {
            shouldOpenField.SetValue(__instance, true);
        }
    }
    catch { }
}

public static void Prefix_ForceLobbyMenuOpen(object __instance)
{
    try
    {
        var shouldOpenField = __instance.GetType().GetField("shouldOpenLobbyMenu", BindingFlags.NonPublic | BindingFlags.Instance);
        if (shouldOpenField != null)
        {
            shouldOpenField.SetValue(__instance, true);
        }
    }
    catch { }
}


private static void FindSpawnerIdFromWorld()
{
    try
    {
        if (_cachedWorldObjectsDict == null)
        {
            MelonLogger.Warning("[KoGaMaPatch] Cached worldObjects dict is null.");
            return;
        }

        var getEnumeratorM = _cachedWorldObjectsDict.GetType().GetMethod("GetEnumerator");
        var enumerator = getEnumeratorM?.Invoke(_cachedWorldObjectsDict, null);
        if (enumerator == null) return;

        var moveNextM = enumerator.GetType().GetMethod("MoveNext");
        var currentProp = enumerator.GetType().GetProperty("Current");

        while ((bool)moveNextM.Invoke(enumerator, null))
        {
            var entry = currentProp.GetValue(enumerator);
            var woProp = entry.GetType().GetProperty("Value");
            object wo = woProp.GetValue(entry);

            if (wo != null)
            {
                string typeName = wo.GetType().Name;
                if (typeName == "MVAvatarSpawnRoleCreator" || typeName.Contains("SpawnRoleCreator"))
                {
                    System.Reflection.PropertyInfo idProp = null;
                    for (System.Type t = wo.GetType(); t != null && idProp == null; t = t.BaseType)
                    {
                        idProp = t.GetProperty("Id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    }

                    if (idProp != null)
                    {
                        _cachedSpawnerWoId = (int)idProp.GetValue(wo);
                        MelonLogger.Msg($"[KoGaMaPatch] Found Spawner ID via cached dict: {_cachedSpawnerWoId}");
                        return;
                    }
                }
            }
        }
    }
    catch (System.Exception ex)
    {
        MelonLogger.Warning($"[KoGaMaPatch] FindSpawnerIdFromWorld failed: {ex.Message}");
    }
}
public static object _cachedWorldObjectsDict = null;
private static int _loggedFactoryData = 0;
private static bool _loggedFactoryError = false;
public static void Prefix_WorldNetwork_AddWorldObject(Dictionary<object, object> data)
{
    if (data == null) return;
    if (_cachedSpawnerWoId != -1) return;

    try
    {
        int currentId = -1;
        bool isSpawner = false;


        foreach (System.Collections.Generic.KeyValuePair<object, object> entry in data)
        {
            string keyStr = entry.Key == null ? "" : entry.Key.ToString();
            string valStr = entry.Value == null ? "" : entry.Value.ToString();

            if (keyStr == "1" || keyStr.Equals("id", System.StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(valStr, out int id))
                {
                    currentId = id;
                }
            }
            

            if (valStr.Contains("SpawnRoleCreator") || valStr.Contains("AvatarSpawnRoleCreator"))
            {
                isSpawner = true;
            }
        }

        if (isSpawner && currentId != -1)
        {
            _cachedSpawnerWoId = currentId;
            MelonLogger.Msg($"[KoGaMaPatch] *** SPAWNER DETECTED IN WorldNetwork.AddWorldObject *** Cached ID: {_cachedSpawnerWoId}");
        }
    }
    catch { }
}

private static object _cachedPrototypesDict = null;
public static void Prefix_KoGaMaPackageClient_AddWorldObject(Dictionary<object, object> data)
{
    try
    {
        if (_cachedSpawnerWoId != -1) return;

        int currentId = -1;
        bool isSpawner = false;

        foreach (System.Collections.Generic.KeyValuePair<object, object> entry in data)
        {
            string keyStr = entry.Key == null ? "" : entry.Key.ToString();
            string valStr = entry.Value == null ? "" : entry.Value.ToString();


            if (_totalSwallowedCount < 5)
            {
                MelonLogger.Msg($"[KoGaMaPatch] AddWorldObject Data: Key='{keyStr}', Value='{valStr}'");
            }

            if (keyStr == "1" || keyStr.ToLowerInvariant() == "id")
            {
                int.TryParse(valStr, out currentId);
            }
            

            if (valStr.Contains("SpawnRoleCreator") || valStr.Contains("AvatarSpawnRoleCreator"))
            {
                isSpawner = true;
            }
        }

        if (currentId != -1 && isSpawner)
        {
            _cachedSpawnerWoId = currentId;
            MelonLogger.Msg($"[KoGaMaPatch] Found Spawner ID during map parse: {_cachedSpawnerWoId}");
        }

        _totalSwallowedCount++;
    }
    catch { }
}

        public static void Postfix_WorldObjectFactory_CacheSpawner(object __result, object[] __args)
        {

            if (__args != null && __args.Length > 1 && _cachedWorldObjectsDict == null)
            {
                _cachedWorldObjectsDict = __args[1];
                MelonLogger.Msg("[KoGaMaPatch] Cached worldObjects dictionary from WorldObjectFactory.");
            }

            if (__args != null && __args.Length > 2 && _cachedPrototypesDict == null)
            {
                _cachedPrototypesDict = __args[2];
                MelonLogger.Msg("[KoGaMaPatch] Cached prototypes dictionary from WorldObjectFactory.");
            }

            if (__result == null) return;

    if (__result == null) return;
    if (_cachedSpawnerWoId >= 0) return;

    try
    {
        var goProp = __result.GetType().GetProperty("GameObject");
        if (goProp != null)
        {
            var go = goProp.GetValue(__result);
            if (go != null)
            {
                var nameProp = go.GetType().GetProperty("name");
                if (nameProp != null)
                {
                    string goName = nameProp.GetValue(go) as string;
                    if (goName != null && (goName.Contains("Spawn") || goName.Contains("Avatar")))
                    {
                        PropertyInfo idProp = null;
                        for (var t = __result.GetType(); t != null && idProp == null; t = t.BaseType)
                            idProp = t.GetProperty("Id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (idProp != null)
                        {
                            _cachedSpawnerWoId = (int)idProp.GetValue(__result);
                            _cachedSpawnerInstance = __result;
                            MelonLogger.Msg($"[KoGaMaPatch] *** Found AvatarSpawner via GameObject name! ID: {_cachedSpawnerWoId} ***");
                            

                            var transformProp = go.GetType().GetProperty("transform");
                            if (transformProp != null)
                            {
                                var transform = transformProp.GetValue(go);
                                var positionProp = transform.GetType().GetProperty("position");
                                if (positionProp != null)
{
    _cachedSpawnerPosition = (UnityEngine.Vector3)positionProp.GetValue(transform);
    MelonLogger.Msg($"[KoGaMaPatch] *** Spawner Position: {_cachedSpawnerPosition} ***");


var avatarRootProp = __result.GetType().GetProperty(
    "AvatarRuntimePrototypeRoot",
    BindingFlags.NonPublic | BindingFlags.Instance);
if (avatarRootProp != null)
{
    _cachedAvatarWoId = (int)avatarRootProp.GetValue(__result);
    MelonLogger.Msg($"[KoGaMaPatch] *** Avatar woId from spawner: {_cachedAvatarWoId} ***");
}
else
{

    try
    {
        var childrenProp = __result.GetType().GetProperty("Children",
            BindingFlags.Public | BindingFlags.Instance);
        if (childrenProp != null)
        {
            var children = childrenProp.GetValue(__result) as System.Collections.IList;
            if (children != null)
            {
                foreach (var child in children)
                {
                    var wotProp = child.GetType().GetProperty("WorldObjectType");
                    if (wotProp != null)
                    {
                        var wot = wotProp.GetValue(child);
                        if (wot != null && wot.ToString().Contains("PlayModeAvatar"))
                        {
                            var childIdProp = child.GetType().GetProperty("Id");
                            if (childIdProp != null)
                            {
                                _cachedAvatarWoId = (int)childIdProp.GetValue(child);
                                MelonLogger.Msg($"[KoGaMaPatch] *** Avatar woId from children scan: {_cachedAvatarWoId} ***");
                            }
                        }
                    }
                }
            }
        }
    }
    catch { }
}

    if (_spawnRoleScheduled)
    {
        _spawnRoleScheduled = false;
        MelonLogger.Msg("[KoGaMaPatch] Spawner discovered firing deferred avatar clone now.");
        TryCloneAndActivateAvatar();
    }
}
                            }
                        }
                    }
                }
            }
        }
    }
    catch (System.Exception ex)
    {
        MelonLogger.Warning($"[KoGaMaPatch] Spawner scan failed: {ex.Message}");
    }
    
}

public static void Finalizer_SwallowException_WithRetryQueue(ref System.Exception __exception, MethodBase __originalMethod, object[] __args)
{
    if (__exception == null) return;

    bool isForwardRefFailure = __exception is System.Collections.Generic.KeyNotFoundException || 
                               __exception.Message.Contains("KeyNotFoundException");
    
    if (isForwardRefFailure && __args != null && __args.Length >= 3)
    {
        _retryQueue.Add(new object[] { __args[0], __args[1], __args[2] });
    }
    
    __exception = null;
}

public static void RetryQueuedWorldObjects(MethodInfo worldObjectFactoryMethod)
{
    if (_retryQueue.Count == 0 || _retryPassCount >= MaxRetryPasses) return;
    _retryPassCount++;

    var stillFailing = new System.Collections.Generic.List<object[]>();
    int succeeded = 0;
    foreach (var entry in _retryQueue)
    {
        try 
        { 
            worldObjectFactoryMethod.Invoke(null, entry); 
            succeeded++; 
        }
        catch (System.Exception)
        {
            stillFailing.Add(entry);
        }
    }

    MelonLogger.Msg($"[KoGaMaPatch] Retry pass #{_retryPassCount}: {succeeded} succeeded, {stillFailing.Count} still failing.");
    _retryQueue.Clear();
    _retryQueue.AddRange(stillFailing);
    
    if (succeeded > 0 && _retryQueue.Count > 0)
    {
        RetryQueuedWorldObjects(worldObjectFactoryMethod);
    }
}

public static void Postfix_Spawner_Initialize(object __instance)
{
    try
    {

        System.Reflection.PropertyInfo idProp = null;
        for (System.Type t = __instance.GetType(); t != null && idProp == null; t = t.BaseType)
        {
            idProp = t.GetProperty("Id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        if (idProp != null)
        {
            _cachedSpawnerWoId = (int)idProp.GetValue(__instance);
            MelonLogger.Msg($"[KoGaMaPatch] Cached Spawner ID: {_cachedSpawnerWoId}");
        }
    }
    catch { }
}

public static void Postfix_MVWorldObjectClient_ctor(object __instance)
{
    try
    {
        string typeName = __instance.GetType().Name;

        if (typeName == "MVAvatarSpawnRoleCreator" || typeName.Contains("SpawnRoleCreator"))
        {
            var idProp = __instance.GetType().GetProperty("Id");
            if (idProp != null)
            {
                _cachedSpawnerWoId = (int)idProp.GetValue(__instance);
                MelonLogger.Msg($"[KoGaMaPatch] Cached Spawner ID: {_cachedSpawnerWoId}");
            }
        }
    }
    catch { }
}

public static void Postfix_KoGaMaPackageClient_ctor(object __instance)
{
    try
    {
        var t = __instance.GetType();
        

        var prototypesField = t.GetField("prototypes", BindingFlags.Public | BindingFlags.Instance);
        if (prototypesField != null && prototypesField.GetValue(__instance) == null)
        {
            var dict = System.Activator.CreateInstance(prototypesField.FieldType);
            prototypesField.SetValue(__instance, dict);
            MelonLogger.Msg("[KoGaMaPatch] Initialized null prototypes dictionary on KoGaMaPackageClient!");
        }
        

        var worldObjectsField = t.GetField("worldObjects", BindingFlags.Public | BindingFlags.Instance);
        if (worldObjectsField != null && worldObjectsField.GetValue(__instance) == null)
        {
            var dict = System.Activator.CreateInstance(worldObjectsField.FieldType);
            worldObjectsField.SetValue(__instance, dict);
        }
        

        var linksField = t.GetField("links", BindingFlags.Public | BindingFlags.Instance);
        if (linksField != null && linksField.GetValue(__instance) == null)
        {
            var dict = System.Activator.CreateInstance(linksField.FieldType);
            linksField.SetValue(__instance, dict);
        }
        

        var objectLinksField = t.GetField("objectLinks", BindingFlags.Public | BindingFlags.Instance);
        if (objectLinksField != null && objectLinksField.GetValue(__instance) == null)
        {
            var dict = System.Activator.CreateInstance(objectLinksField.FieldType);
            objectLinksField.SetValue(__instance, dict);
        }
    }
    catch (System.Exception ex)
    {
        MelonLogger.Warning($"[KoGaMaPatch] Postfix_KoGaMaPackageClient_ctor error: {ex.Message}");
    }
}

        private static void TryHookLoadingScreenHandlers(HarmonyLib.Harmony harmony)
        {
            try
            {
                System.Type lshType =
                    FindTypeInAnyAssembly("Il2Cpp.LoadingScreenHandler") ??
                    FindTypeInAnyAssembly("LoadingScreenHandler");
                if (lshType == null)
                {
                    MelonLogger.Msg("[KoGaMaPatch] LoadingScreenHandler not found (no spinner patching).");
                }
                else
                {
                    MelonLogger.Msg($"[KoGaMaPatch] Found {lshType.FullName}");
                    var onJoinState = lshType.GetMethod(
                        "OnJoinStateChanged",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (onJoinState != null)
                    {
                        harmony.Patch(onJoinState, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_OnJoinStateChanged)));
                        MelonLogger.Msg($"[KoGaMaPatch] Hooked LoadingScreenHandler.OnJoinStateChanged");
                    }
                    else
                    {
                        MelonLogger.Msg($"[KoGaMaPatch] LoadingScreenHandler.OnJoinStateChanged NOT found.");
                    }
                    var startM = lshType.GetMethod(
                        "Start",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (startM != null && startM.DeclaringType == lshType)
                    {
                        harmony.Patch(startM, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_Trace)));
                        MelonLogger.Msg($"[KoGaMaPatch] Hooked LoadingScreenHandler.Start");
                    }
                }
                System.Type lsbType =
                    FindTypeInAnyAssembly("Il2Cpp.LoadingScreenBackground") ??
                    FindTypeInAnyAssembly("LoadingScreenBackground");
                if (lsbType != null)
                {
                    MelonLogger.Msg($"[KoGaMaPatch] Found {lsbType.FullName}");
                    var startM = lsbType.GetMethod(
                        "Start",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (startM != null && startM.DeclaringType == lsbType)
                    {
                        harmony.Patch(startM, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_Trace)));
                        MelonLogger.Msg($"[KoGaMaPatch] Hooked LoadingScreenBackground.Start");
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[KoGaMaPatch] TryHookLoadingScreenHandlers error: {ex.Message}");
            }

            TryHookStaticJoinState(harmony);
        }
        private static void TryHookStaticJoinState(HarmonyLib.Harmony harmony)
        {
            try
            {
                var controllerType =
                    FindTypeInAnyAssembly("Il2Cpp.MVGameControllerBase") ??
                    FindTypeInAnyAssembly("MVGameControllerBase");
                if (controllerType == null) return;


                var joinStateProp = controllerType.GetProperty(
                    "JoinState",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (joinStateProp != null)
                {
                    var setter = joinStateProp.GetSetMethod(nonPublic: true);
                    if (setter != null)
                    {
                        harmony.Patch(setter, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_SetJoinState)));
                        MelonLogger.Msg("[KoGaMaPatch] Hooked static MVGameControllerBase.set_JoinState");





                        _cachedSetJoinStateMethod = setter;
                        DriveLog($"Cached set_JoinState at apply time: {setter}");
                    }
                    var getter = joinStateProp.GetGetMethod(nonPublic: true);
                    if (getter != null)
                    {
                        harmony.Patch(getter, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_GetJoinState)));
                        MelonLogger.Msg("[KoGaMaPatch] Hooked static MVGameControllerBase.get_JoinState");
                    }
                }




                var onJoinProp = controllerType.GetProperty(
                    "OnJoinStateChanged",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (onJoinProp != null)
                {
                    var setter = onJoinProp.GetSetMethod(nonPublic: true);
                    if (setter != null)
                    {
                        harmony.Patch(setter, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_SetOnJoinStateChanged)));
                        MelonLogger.Msg("[KoGaMaPatch] Hooked static MVGameControllerBase.set_OnJoinStateChanged");
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[KoGaMaPatch] TryHookStaticJoinState error: {ex.Message}");
            }
        }
        private static void TryHookWaitUntil(HarmonyLib.Harmony harmony)
        {
            try
            {
                System.Type waitUntilType =
                    System.Type.GetType("UnityEngine.WaitUntil, UnityEngine.CoreModule") ??
                    System.Type.GetType("UnityEngine.WaitUntil");
                if (waitUntilType == null)
                {
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        System.Type[] types;
                        try { types = asm.GetTypes(); }
                        catch (System.Reflection.ReflectionTypeLoadException ex)
                        {
                            types = ex.Types.Where(t => t != null).ToArray();
                        }
                        catch { continue; }
                        foreach (var t in types)
                        {
                            if (t == null) continue;
                            if (t.Name != "WaitUntil") continue;
                            waitUntilType = t;
                            break;
                        }
                        if (waitUntilType != null) break;
                    }
                }
                if (waitUntilType == null)
                {
                    MelonLogger.Warning("[KoGaMaPatch] WaitUntil type not found  coroutines that yield on WaitUntil will still block.");
                    int dumped = 0;
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        System.Type[] types;
                        try { types = asm.GetTypes(); }
                        catch (System.Reflection.ReflectionTypeLoadException ex)
                        {
                            types = ex.Types.Where(t => t != null).ToArray();
                        }
                        catch { continue; }
                        foreach (var t in types)
                        {
                            if (t == null) continue;
                            if (t.FullName == null) continue;
                            if (t.FullName.Contains("WaitUntil"))
                            {
                                MelonLogger.Msg($"[KoGaMaPatch]   WaitUntil candidate: {t.FullName} (in {asm.GetName().Name})");
                                dumped++;
                            }
                        }
                    }
                    MelonLogger.Msg($"[KoGaMaPatch] WaitUntil diagnostic: {dumped} type(s) shown.");

                    return;
                }

                MelonLogger.Msg($"[KoGaMaPatch] Found WaitUntil: {waitUntilType.FullName}");

                System.Reflection.MethodInfo getKeepWaiting = null;
                for (System.Type bt = waitUntilType; bt != null && getKeepWaiting == null; bt = bt.BaseType)
                {
                    getKeepWaiting = bt.GetMethod(
                        "get_keepWaiting",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (getKeepWaiting == null)
                {
                    System.Reflection.PropertyInfo prop = null;
                    for (System.Type bt = waitUntilType; bt != null && prop == null; bt = bt.BaseType)
                    {
                        prop = bt.GetProperty(
                            "keepWaiting",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                    if (prop != null) getKeepWaiting = prop.GetGetMethod();
                }

                if (getKeepWaiting == null)
                {
                    MelonLogger.Warning("[KoGaMaPatch] WaitUntil.get_keepWaiting not found.");
                    return;
                }



                MelonLogger.Msg($"[KoGaMaPatch]   keepWaiting method metadata: declaringType={getKeepWaiting.DeclaringType?.FullName}, isVirtual={getKeepWaiting.IsVirtual}, isPublic={getKeepWaiting.IsPublic}");

                harmony.Patch(getKeepWaiting, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_GetKeepWaiting_ReturnFalse)));
                MelonLogger.Msg("[KoGaMaPatch] Hooked WaitUntil.get_keepWaiting  predicate will always return false (skip).");




                try
                {
                    System.Type customYI =
                        System.Type.GetType("UnityEngine.CustomYieldInstruction, UnityEngine.CoreModule") ??
                        System.Type.GetType("UnityEngine.CustomYieldInstruction");
                    if (customYI != null)
                    {
                        var baseGetKeepWaiting = customYI.GetMethod("get_keepWaiting", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (baseGetKeepWaiting != null && baseGetKeepWaiting != getKeepWaiting)
                        {
                            harmony.Patch(baseGetKeepWaiting, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_GetKeepWaiting_ReturnFalse)));
                            MelonLogger.Msg("[KoGaMaPatch] Also hooked CustomYieldInstruction.get_keepWaiting (base abstract).");
                        }
                    }
                }
                catch (System.Exception ex2)
                {
                    DriveLog($"TryHookWaitUntil: base patch attempt: {ex2.Message}");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[KoGaMaPatch] TryHookWaitUntil error: {ex.Message}");
            }
        }



        private static int _keepWaitingHookCalls = 0;
        public static bool Prefix_GetKeepWaiting_ReturnFalse(ref bool __result)
        {


            __result = false;
            int n = System.Threading.Interlocked.Increment(ref _keepWaitingHookCalls);
            if (n == 1 || (n & 0xFFF) == 0)
            {
                MelonLogger.Msg($"[KoGaMaPatch] Prefix_GetKeepWaiting fired: call #{n}");
            }
            return false;
        }
        private static object _cachedOnJoinStateChangedDelegate = null;
        private static System.Reflection.MethodInfo _cachedSetJoinStateMethod = null;
        public static void Prefix_SetOnJoinStateChanged(object[] __args)
        {
            try
            {
                if (__args != null && __args.Length > 0 && __args[0] != null)
                {
                    _cachedOnJoinStateChangedDelegate = __args[0];
                    MelonLogger.Msg($"[KoGaMaPatch] Captured OnJoinStateChanged delegate: type={_cachedOnJoinStateChangedDelegate.GetType().FullName}");
                    DriveLog($"Captured OnJoinStateChanged delegate: type={_cachedOnJoinStateChangedDelegate.GetType().FullName}");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[KoGaMaPatch] Prefix_SetOnJoinStateChanged error: {ex.Message}");
            }
        }


        public static void Postfix_GetJoinState(ref object __result)
        {
            try
            {
                if (__result != null)
                {
                    int stateValue = (int)__result;
                    DriveLog($"get_JoinState returned {stateValue}");
                }
            }
            catch {}
        }


        public static void Postfix_SetJoinState(object[] __args, MethodBase __originalMethod)
        {
            try
            {
                if (_cachedSetJoinStateMethod == null)
                {
                    _cachedSetJoinStateMethod = (System.Reflection.MethodInfo)__originalMethod;
                    DriveLog("Cached set_JoinState method");
                }
                if (__args != null && __args.Length > 0 && __args[0] != null)
                {
                    int stateValue = (int)__args[0];
                    MelonLogger.Msg($"[KoGaMaPatch] set_JoinState({stateValue})");
                    DriveLog($"set_JoinState({stateValue})");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[KoGaMaPatch] Postfix_SetJoinState error: {ex.Message}");
            }
        }
        public static void Postfix_OnJoinStateChanged(object[] __args, object __instance)
        {
            try
            {
                if (__args != null && __args.Length > 0 && __args[0] != null)
                {
                    int stateValue = (int)__args[0];
                    MelonLogger.Msg($"[KoGaMaPatch] LoadingScreenHandler.OnJoinStateChanged(state={stateValue})");
                    DriveLog($"OnJoinStateChanged state={stateValue}");




                    if (_cachedLoadingScreenHandler == null && __instance != null)
                    {
                        _cachedLoadingScreenHandler = __instance;
                        DriveLog($"Cached LoadingScreenHandler via OnJoinStateChanged postfix: {__instance.GetType().FullName}");
                    }



                    DumpLoadingScreenState(__instance, stateValue);
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[KoGaMaPatch] Postfix_OnJoinStateChanged error: {ex.Message}");
            }
        }
        private static void DumpLoadingScreenState(object handler, int newState)
        {
            if (handler == null) return;
            try
            {
                var t = handler.GetType();
                int curEvt    = ReadFieldAny<int>(t, handler, "currentEventCount") ?? -1;
                int evtCount  = ReadFieldAny<int>(t, handler, "eventsCount") ?? -1;
                float? tgtProg = ReadFieldAny<float>(t, handler, "targetProgress");
                bool hasSession = ReadFieldAny<bool>(t, handler, "hasCapturedSessionData") ?? false;



                int? targetProgInt = ReadFieldAny<int>(t, handler, "targetProgress");
                string progStr = tgtProg.HasValue ? tgtProg.Value.ToString("0.00")
                    : (targetProgInt.HasValue ? targetProgInt.Value.ToString() + "/(as int)" : "n/a");

                DriveLog($"LSH: state={newState}, currentEventCount={curEvt}/{evtCount}, targetProgress={progStr}, hasCapturedSessionData={hasSession}");

                try
                {
                    var lookupF = FindFieldByCandidates(t,
                        "eventCountLookup");
                    if (lookupF != null)
                    {
                        var dict = lookupF.GetValue(handler);
                        if (dict != null)
                        {

                            string entries = "";
                            try
                            {
                                var dictType = dict.GetType();
                                var keysProp = dictType.GetProperty("Keys");
                                if (keysProp != null)
                                {
                                    var keys = keysProp.GetValue(dict) as System.Collections.IEnumerable;
                                    if (keys != null)
                                    {
                                        foreach (var k in keys)
                                        {
                                            var valProp = dictType.GetProperty("Item");
                                            if (valProp != null)
                                            {
                                                var v = valProp.GetValue(dict, new object[] { k });
                                                entries += $"[{k}→{v}] ";
                                            }
                                        }
                                    }
                                }
                            }
                            catch (System.Exception ex2)
                            {
                                entries = $"<dict iteration failed: {ex2.Message}>";
                            }
                            DriveLog($"LSH: eventCountLookup: {entries}");
                        }
                        else
                        {
                            DriveLog($"LSH: eventCountLookup is null");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    DriveLog($"DumpLoadingScreenState: eventCountLookup access failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"DumpLoadingScreenState error: {ex.GetType().Name}: {ex.Message}");
            }
        }





        private static System.Reflection.FieldInfo FindFieldByCandidates(System.Type t, string baseName)
        {
            string[] variants = {
                baseName,
                "m_" + baseName,
                "_" + baseName,
                "<" + baseName + ">k__BackingField",
                baseName.Substring(0, 1).ToUpperInvariant() + baseName.Substring(1),
            };
            foreach (var n in variants)
            {
                for (var cur = t; cur != null; cur = cur.BaseType)
                {
                    try
                    {
                        var f = cur.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f != null) return f;
                    }
                    catch {}
                }
            }
            return null;
        }




        private static T? ReadFieldAny<T>(System.Type t, object instance, string baseName) where T : struct
        {

            var f = FindFieldByCandidates(t, baseName);
            if (f != null && f.FieldType == typeof(T))
            {
                try { return (T)f.GetValue(instance); } catch { }
            }

            string[] variants = {
                baseName,
                "m_" + baseName,
                "_" + baseName,
                baseName.Substring(0, 1).ToUpperInvariant() + baseName.Substring(1),
            };
            foreach (var n in variants)
            {
                for (var cur = t; cur != null; cur = cur.BaseType)
                {
                    try
                    {
                        var p = cur.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p != null && p.PropertyType == typeof(T) && p.CanRead)
                            return (T)p.GetValue(instance);
                    }
                    catch {}
                }
            }
            return null;
        }

        private static System.IO.StreamWriter _driveLogWriter = null;
        private static readonly object _driveLogLock = new object();
        public static void DriveLog(string msg)
        {
            try
            {
                lock (_driveLogLock)
                {
                    if (_driveLogWriter == null)
                    {
                        string dir = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "KogamaOfflinePatch");
                        System.IO.Directory.CreateDirectory(dir);
                        _driveLogWriter = new System.IO.StreamWriter(
                            System.IO.Path.Combine(dir, "drive.log"),
                            append: true) { AutoFlush = true };
                    }
                    _driveLogWriter.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
                }
            }
            catch {}
        }
        private static object _cachedLoadingScreenHandler = null;
        private static System.Reflection.MethodInfo _cachedOnJoinStateChangedMethod = null;
        private static object _cachedPhotonListener = null;
        private static System.Reflection.MethodInfo _cachedOnStatusChangedMethod = null;
        private static int _cachedPhotonListenerStrategy = 0;
        private static int _driveState = 0;
        private static int _driveTickCounter = 0;
        private const int DriveIntervalTicks = 30;
        private static bool _driveStarted = false;
        private static bool _photonStatusFired = false;
        private static int _globalTick = 0;
        private static int _pendingEncryptionFire = -1;
        private const int PhotonStatus_Connect = 1024;
        private const int PhotonStatus_EncryptionEstablished = 1048;
        public static void DriveJoinStateForward()
        {
            DriveLog($"DriveJoinStateForward tick (state={_driveState}, ticks={_driveTickCounter}, hasDelegate={_cachedOnJoinStateChangedDelegate != null}, hasListener={_cachedPhotonListener != null})");

            if (!_applied) { DriveLog("DriveJoinStateForward: _applied=false, skipping"); return; }
            _driveTickCounter++;
            _globalTick++;
            while (_pendingReinvokes.Count > 0)
            {
                var head = _pendingReinvokes.Peek();
                if (head.FireAtTick > _globalTick) break;
                _pendingReinvokes.Dequeue();
                if (head.Target == null) { DriveLog($"PendingReinvoke #{head.Ticket}: target null, skipping"); continue; }
                if (_startGameInvoked)   { DriveLog($"PendingReinvoke #{head.Ticket}: _startGameInvoked=true, skipping"); continue; }
                try
                {

                    System.Reflection.MethodInfo startGameMethod = null;
                    for (System.Type t = head.Target.GetType(); t != null && startGameMethod == null; t = t.BaseType)
                    {
                        var candidates = t.GetMethods(
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (var m in candidates)
                        {
                            if (m.Name != StartGameMethodName) continue;
                            if (m.GetParameters().Length != 0) continue;
                            if (m.ReturnType != typeof(void)) continue;
                            startGameMethod = m;
                            break;
                        }
                    }
                    if (startGameMethod == null)
                    {
                        DriveLog($"PendingReinvoke #{head.Ticket}: StartGame() not found on {head.Target.GetType().FullName}");
                        continue;
                    }
                    _startGameInvoked = true;
                    startGameMethod.Invoke(head.Target, null);
                    DriveLog($"PendingReinvoke #{head.Ticket}: StartGame() invoked on {head.Target.GetType().Name}");
                    MelonLogger.Msg($"[KoGaMaPatch] PendingReinvoke: StartGame() invoked on {head.Target.GetType().Name}");
                }
                catch (System.Exception ex)
                {
                    var inner = ex.InnerException ?? ex;
                    DriveLog($"PendingReinvoke #{head.Ticket}: invoke FAILED {inner.GetType().Name}: {inner.Message}");
                    _startGameInvoked = false;
                }
            }
            if (_driveState >= 4)
            {
                if (_stuckAfterState4StartTick < 0)
                {
                    _stuckAfterState4StartTick = _globalTick;
                    DriveLog($"Started stuck-timer at globalTick={_globalTick} (state={_driveState})");
                }
                MaybeAttemptSceneLoadFallback();
            }
            if (_sceneLoadFallbackFiredTick > 0 && !_mapLoaded)
            {
                int ticksSinceFallback = _globalTick - _sceneLoadFallbackFiredTick;

                if (ticksSinceFallback > 60)
                {
                    try { ForceLoadKgmMapFromDisk(); }
                    catch (System.Exception ex) { DriveLog($"ForceLoadKgmMapFromDisk threw: {ex.GetType().Name}: {ex.Message}"); }
                }
            }

if (_sceneLoadFallbackFiredTick > 0 && !_mapLoaded)
{
    int ticksSinceFallback = _globalTick - _sceneLoadFallbackFiredTick;

    if (ticksSinceFallback > 60)
    {
        try { ForceLoadKgmMapFromDisk(); }
        catch (System.Exception ex) { DriveLog($"ForceLoadKgmMapFromDisk threw: {ex.GetType().Name}: {ex.Message}"); }
    }
}

            if (_driveTickCounter < DriveIntervalTicks) return;
            _driveTickCounter = 0;
            if (_driveState > 6) { DriveLog($"DriveJoinStateForward: state={_driveState} > 6, stopping"); return; }

            try
            {
                int nextState = _driveState;
                bool invoked = false;

                if (_cachedSetJoinStateMethod != null)
                {
                    try
                    {
                        _cachedSetJoinStateMethod.Invoke(null, new object[] { nextState });
                        DriveLog($"DriveSpinner: set_JoinState({nextState}) invoked");
                        invoked = true;
                    }
                    catch (System.Exception ex)
                    {
                        var inner = ex.InnerException ?? ex;
                        DriveLog($"DriveSpinner: set_JoinState({nextState}) FAILED: {inner.GetType().Name}: {inner.Message}");
                    }
                }

                if (!invoked && _cachedOnJoinStateChangedDelegate != null)
                {
                    try
                    {
                        _cachedOnJoinStateChangedDelegate.GetType().GetMethod("Invoke").Invoke(_cachedOnJoinStateChangedDelegate, new object[] { nextState });
                        DriveLog($"DriveSpinner: invoked static delegate for state={nextState}");
                        invoked = true;
                    }
                    catch (System.Exception ex)
                    {
                        var inner = ex.InnerException ?? ex;
                        DriveLog($"DriveSpinner: static delegate invoke FAILED state={nextState}: {inner.GetType().Name}: {inner.Message}");
                    }
                }

                if (!invoked && _cachedLoadingScreenHandler == null)
                {
                    _cachedLoadingScreenHandler = FindLoadingScreenHandlerInstance();
                    if (_cachedLoadingScreenHandler != null)
                    {
                        MelonLogger.Msg($"[KoGaMaPatch] Cached LoadingScreenHandler instance: {_cachedLoadingScreenHandler.GetType().FullName}");
                        DriveLog($"Cached LoadingScreenHandler instance: {_cachedLoadingScreenHandler.GetType().FullName}");
                    }
                    else
                    {
                        DriveLog("FindLoadingScreenHandlerInstance returned null this tick");
                    }
                }

                if (!invoked && _cachedLoadingScreenHandler == null) { DriveLog("cached handler still null, bailing"); return; }
                if (!invoked && _cachedOnJoinStateChangedMethod == null)
                {
                    _cachedOnJoinStateChangedMethod = _cachedLoadingScreenHandler.GetType().GetMethod(
                        "OnJoinStateChanged",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (_cachedOnJoinStateChangedMethod == null)
                        DriveLog($"OnJoinStateChanged method not found on {_cachedLoadingScreenHandler.GetType().FullName}");
                }
                if (!invoked && _cachedOnJoinStateChangedMethod == null) { DriveLog("cached OnJoinStateChangedMethod null, bailing"); return; }


                if (!invoked)
                {
                    MelonLogger.Msg($"[KoGaMaPatch] DriveSpinner: invoking OnJoinStateChanged(state={nextState}) on {_cachedLoadingScreenHandler.GetType().Name}");
                    DriveLog($"DriveSpinner invoking OnJoinStateChanged(state={nextState})");
                    try
                    {
                        _cachedOnJoinStateChangedMethod.Invoke(_cachedLoadingScreenHandler, new object[] { nextState });
                        DriveLog($"DriveSpinner invoke OK for state={nextState}");
                    }
                    catch (System.Exception ex)
                    {
                        var inner = ex.InnerException ?? ex;
                        MelonLogger.Warning($"[KoGaMaPatch] DriveSpinner: invoke failed: {inner.Message}");
                        DriveLog($"DriveSpinner invoke FAILED for state={nextState}: {inner.GetType().Name}: {inner.Message}");
                    }
                }
                _driveState++;

                if (_cachedPhotonListener == null)
                {
                    _cachedPhotonListener = FindPhotonListener();

                    if (_cachedPhotonListener != null && _cachedPhotonListenerStrategy == 0)
                        _cachedPhotonListenerStrategy = 1;
                    if (_cachedPhotonListener != null)
                    {
                        MelonLogger.Msg($"[KoGaMaPatch] Cached PhotonListener: {_cachedPhotonListener.GetType().FullName} (strategy={_cachedPhotonListenerStrategy})");
                        DriveLog($"Cached PhotonListener: {_cachedPhotonListener.GetType().FullName} (strategy={_cachedPhotonListenerStrategy})");

                        var onStatusCandidates = _cachedPhotonListener.GetType().GetMethods(
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (var m in onStatusCandidates)
                        {
                            if (m.Name != "OnStatusChanged") continue;
                            var ps = m.GetParameters();
                            if (ps.Length != 1) continue;


                            if (m.ReturnType != typeof(void)) continue;
                            _cachedOnStatusChangedMethod = m;
                            MelonLogger.Msg($"[KoGaMaPatch] Cached OnStatusChanged ({m.GetParameters()[0].ParameterType.FullName}).");
                            DriveLog($"Cached OnStatusChanged ({m.GetParameters()[0].ParameterType.FullName}).");
                            break;
                        }
                        if (_cachedOnStatusChangedMethod == null)
                        {

                            foreach (var m in onStatusCandidates)
                            {
                                if (m.Name != "OnStatusChanged") continue;
                                _cachedOnStatusChangedMethod = m;
                                MelonLogger.Msg($"[KoGaMaPatch] Cached OnStatusChanged (fallback, arity={m.GetParameters().Length}, returns {m.ReturnType.FullName}).");
                                DriveLog($"Cached OnStatusChanged (fallback, arity={m.GetParameters().Length}).");
                                break;
                            }
                        }
                    }
                    else
                    {
                        DriveLog($"FindPhotonListener returned null at state={nextState}");
                    }
                }

                if (_cachedPhotonListener != null && _cachedOnStatusChangedMethod != null
    && _cachedPhotonListenerStrategy > 0 && _cachedPhotonListenerStrategy < 3)
{
    if (!_photonStatusFired)
    {
        _photonStatusFired = true;
        try
        {
            var statusEnumType = _cachedOnStatusChangedMethod.GetParameters()[0].ParameterType;

            object connectVal = System.Enum.ToObject(statusEnumType, PhotonStatus_Connect);
            _cachedOnStatusChangedMethod.Invoke(_cachedPhotonListener, new object[] { connectVal });
            MelonLogger.Msg("[KoGaMaPatch] OnStatusChanged fired: Connect");

            object encVal = System.Enum.ToObject(statusEnumType, PhotonStatus_EncryptionEstablished);
            _cachedOnStatusChangedMethod.Invoke(_cachedPhotonListener, new object[] { encVal });
            MelonLogger.Msg("[KoGaMaPatch] OnStatusChanged fired: EncryptionEstablished");

            var opRequestsProp = _cachedPhotonListener.GetType().GetProperty("OperationRequestSender");
            if (opRequestsProp != null)
            {
                object opRequests = opRequestsProp.GetValue(_cachedPhotonListener);
                if (opRequests != null)
                {
                    var joinGameMethod = opRequests.GetType().GetMethod("JoinGame", new[] { typeof(string) });
                    if (joinGameMethod != null)
                    {
                        joinGameMethod.Invoke(opRequests, new object[] { "" });
                                            if (joinGameMethod != null)
                    {
                        joinGameMethod.Invoke(opRequests, new object[] { "" });
                        MelonLogger.Msg("[KoGaMaPatch] Forced OperationRequests.JoinGame() invocation.");
                        try
                        {
                            var controllerType = FindTypeInAnyAssembly("Il2Cpp.MVGameControllerBase");
                            var sessionDataProp = controllerType.GetProperty("GameSessionData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                            if (sessionDataProp != null)
                            {
                                var sessionData = sessionDataProp.GetValue(null, null);
                                if (sessionData != null)
                                {
                                    var planetIdField = sessionData.GetType().GetField("planetID", BindingFlags.Public | BindingFlags.Instance);
                                    if (planetIdField != null) planetIdField.SetValue(sessionData, 1);
                                    
                                    var gameModeField = sessionData.GetType().GetField("gameMode", BindingFlags.Public | BindingFlags.Instance);
                                    if (gameModeField != null) gameModeField.SetValue(sessionData, 1);
                                    
                                    MelonLogger.Msg("[KoGaMaPatch] Forced GameSessionData.planetID = 1 and gameMode = Play");
                                }
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        MelonLogger.Warning("[KoGaMaPatch] JoinGame method not found!");
                    }
                        
                    }
                    else
                    {
                        MelonLogger.Warning("[KoGaMaPatch] JoinGame method not found!");
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            MelonLogger.Warning($"[KoGaMaPatch] Failed to force join pipeline: {inner.GetType().Name}: {inner.Message}");
        }
    }

    else if (nextState >= 1 && _photonStatusFired && _pendingEncryptionFire > 0 && _globalTick >= _pendingEncryptionFire)
{
    try
    {
        var statusEnumType = _cachedOnStatusChangedMethod.GetParameters()[0].ParameterType;
        object encVal = System.Enum.ToObject(statusEnumType, PhotonStatus_EncryptionEstablished);
        _cachedOnStatusChangedMethod.Invoke(_cachedPhotonListener, new object[] { encVal });
        MelonLogger.Msg("[KoGaMaPatch] OnStatusChanged fired: EncryptionEstablished");
        DriveLog("OnStatusChanged fired: EncryptionEstablished");

        try
        {
            var peerProp = _cachedPhotonListener.GetType().GetProperty("Peer");
            if (peerProp != null)
            {
                object peer = peerProp.GetValue(_cachedPhotonListener);
                if (peer != null)
                {
                    var peerBaseField = peer.GetType().GetField("peerBase", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (peerBaseField != null)
                    {
                        object peerBase = peerBaseField.GetValue(peer);
                        if (peerBase != null)
                        {
                            var stateField = peerBase.GetType().GetField("peerConnectionState", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (stateField != null)
                            {

                                stateField.SetValue(peerBase, System.Enum.ToObject(stateField.FieldType, 3));
                                MelonLogger.Msg("[KoGaMaPatch] Forced PhotonPeer.peerBase.peerConnectionState = Connected (3)");
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception ex) { MelonLogger.Warning($"Failed to force peer state: {ex.Message}"); }

        var joinMethod = _cachedPhotonListener.GetType().GetMethod("Join", BindingFlags.Public | BindingFlags.Instance);
        if (joinMethod != null)
        {
            joinMethod.Invoke(_cachedPhotonListener, null);
            MelonLogger.Msg("[KoGaMaPatch] Forced MVNetworkGame.Join() invocation.");
            DriveLog("Forced MVNetworkGame.Join() invocation.");
        }
        else
        {
            MelonLogger.Warning("[KoGaMaPatch] MVNetworkGame.Join() method not found!");
        }

        _pendingEncryptionFire = -1;
    }
    catch (System.Exception ex)
    {
        var inner = ex.InnerException ?? ex;
        MelonLogger.Warning($"[KoGaMaPatch] OnStatusChanged(EncryptionEstablished) failed: {inner.GetType().Name}: {inner.Message}");
        DriveLog($"OnStatusChanged(EncryptionEstablished) failed: {inner.GetType().Name}: {inner.Message}");
    }
}
                    {
                        try
                        {
                            var statusEnumType = _cachedOnStatusChangedMethod.GetParameters()[0].ParameterType;
                            object encVal = System.Enum.ToObject(statusEnumType, PhotonStatus_EncryptionEstablished);
                            _cachedOnStatusChangedMethod.Invoke(_cachedPhotonListener, new object[] { encVal });
                            MelonLogger.Msg("[KoGaMaPatch] OnStatusChanged fired: EncryptionEstablished");
                            DriveLog("OnStatusChanged fired: EncryptionEstablished");

                            _pendingEncryptionFire = -1;
                        }
                        catch (System.Exception ex)
                        {
                            var inner = ex.InnerException ?? ex;
                            MelonLogger.Warning($"[KoGaMaPatch] OnStatusChanged(EncryptionEstablished) failed: {inner.GetType().Name}: {inner.Message}");
                            DriveLog($"OnStatusChanged(EncryptionEstablished) failed: {inner.GetType().Name}: {inner.Message}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[KoGaMaPatch] DriveJoinStateForward error: {ex.Message}");
                DriveLog($"DriveJoinStateForward OUTER error: {ex.GetType().Name}: {ex.Message}");
            }
        }
    public static bool _mapLoaded = false;
    public static bool _avatarCloneAttempted = false;

        public static void TryCloneAndActivateAvatar()
        {
            if (_avatarCloneAttempted) return;
            if (_cachedSpawnerWoId == -1) return;
            if (_cachedSpawnerPosition == UnityEngine.Vector3.zero) return;
            _avatarCloneAttempted = true;
            try
            {
                MelonLogger.Msg("[KoGaMaPatch] *** Starting avatar creation via Clone() ***");

                var spawnerType = FindTypeInAnyAssembly("Il2Cpp.MVAvatarSpawnRoleCreator") ?? FindTypeInAnyAssembly("MVAvatarSpawnRoleCreator");
                if (spawnerType == null) { MelonLogger.Error("[KoGaMaPatch] MVAvatarSpawnRoleCreator type not found."); return; }

                IntPtr spawnerPtr = IntPtr.Zero;
                for (Type t = _cachedSpawnerInstance.GetType(); t != null; t = t.BaseType)
                {
                    var pp = t.GetProperty("Pointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (pp != null && pp.PropertyType == typeof(IntPtr)) { spawnerPtr = (IntPtr)pp.GetValue(_cachedSpawnerInstance); break; }
                }
                if (spawnerPtr == IntPtr.Zero) { MelonLogger.Error("[KoGaMaPatch] Failed to get spawner native pointer."); return; }

                var spawnerCtor = spawnerType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(IntPtr) }, null);
                if (spawnerCtor == null) { MelonLogger.Error("[KoGaMaPatch] MVAvatarSpawnRoleCreator(IntPtr) ctor not found."); return; }

                object trueSpawner = spawnerCtor.Invoke(new object[] { spawnerPtr });
                MelonLogger.Msg($"[KoGaMaPatch] Re-wrapped spawner as {trueSpawner.GetType().Name}");


                var avRootProp = spawnerType.GetProperty("AvatarRuntimePrototypeRoot", BindingFlags.NonPublic | BindingFlags.Instance);
                if (avRootProp == null) { MelonLogger.Error("[KoGaMaPatch] AvatarRuntimePrototypeRoot property not found."); return; }

                int previewAvatarWoId = (int)avRootProp.GetValue(trueSpawner);
                MelonLogger.Msg($"[KoGaMaPatch] Spawner.AvatarRuntimePrototypeRoot = {previewAvatarWoId}");


                var tryGetValueM = _cachedWorldObjectsDict.GetType().GetMethod("TryGetValue");
                object[] tryGetArgs = new object[] { previewAvatarWoId, null };
                bool foundAvatar = (bool)tryGetValueM.Invoke(_cachedWorldObjectsDict, tryGetArgs);
                
                if (!foundAvatar || tryGetArgs[1] == null)
                {
                    MelonLogger.Error("[KoGaMaPatch] MVPreviewAvatar not found in worldObjects dict.");
                    return;
                }
                object previewAvatar = tryGetArgs[1];
                MelonLogger.Msg($"[KoGaMaPatch] Found PlayModeAvatar via woId! Type={previewAvatar.GetType().Name}");


                var cloneBookkeepingType = FindTypeInAnyAssembly("Il2Cpp.CloneBookkeeping") ?? FindTypeInAnyAssembly("CloneBookkeeping");
                if (cloneBookkeepingType == null) { MelonLogger.Error("[KoGaMaPatch] CloneBookkeeping type not found."); return; }

                object cloneBookkeeping = System.Activator.CreateInstance(cloneBookkeepingType);
                IntPtr cbPtr = IntPtr.Zero;
                for (Type t = cloneBookkeepingType; t != null; t = t.BaseType)
                {
                    var pp = t.GetProperty("Pointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (pp != null && pp.PropertyType == typeof(IntPtr)) { cbPtr = (IntPtr)pp.GetValue(cloneBookkeeping); break; }
                }
                if (cbPtr != IntPtr.Zero)
                    System.Runtime.InteropServices.Marshal.WriteInt32(cbPtr, 0x10, 1000000);


                MethodInfo cloneMethod = null;
                for (Type t = previewAvatar.GetType(); t != null && cloneMethod == null; t = t.BaseType)
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                        if (m.Name == "Clone" && m.GetParameters().Length == 5) { cloneMethod = m; break; }

                if (cloneMethod == null) { MelonLogger.Error("[KoGaMaPatch] Clone method not found."); return; }

                object clone = null;
                try
                {
                    clone = cloneMethod.Invoke(previewAvatar, new object[] { 1, _cachedSpawnerWoId, cloneBookkeeping, _cachedWorldObjectsDict, _cachedPrototypesDict });
                    MelonLogger.Msg($"[KoGaMaPatch] Clone() returned: {(clone != null ? clone.GetType().Name : "NULL")}");
                }
                catch (System.Exception ex)
                { var inner = ex.InnerException ?? ex; MelonLogger.Error($"[KoGaMaPatch] Clone() failed: {inner.Message}"); return; }
                if (clone == null) { MelonLogger.Error("[KoGaMaPatch] Clone() returned null."); return; }

                try
                {
                    object wocm = _cachedPhotonListener.GetType().GetProperty("WorldObjectClientManager")?.GetValue(_cachedPhotonListener);
                    var addWO = wocm?.GetType().GetMethod("AddToWorldObjects", BindingFlags.Public | BindingFlags.Instance);
                    addWO?.Invoke(wocm, new object[] { clone });
                    MelonLogger.Msg("[KoGaMaPatch] Added clone to WOCM.");
                }
                catch (System.Exception ex) { MelonLogger.Warning($"[KoGaMaPatch] AddToWorldObjects: {ex.InnerException?.Message ?? ex.Message}"); }


                var mvAvatarLocalType = FindTypeInAnyAssembly("Il2Cpp.MVAvatarLocal") ?? FindTypeInAnyAssembly("MVAvatarLocal");
                if (mvAvatarLocalType == null) { MelonLogger.Error("[KoGaMaPatch] MVAvatarLocal type not found."); return; }

                IntPtr clonePtr = IntPtr.Zero;
                for (Type t = clone.GetType(); t != null; t = t.BaseType)
                {
                    var pp = t.GetProperty("Pointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (pp != null && pp.PropertyType == typeof(IntPtr)) { clonePtr = (IntPtr)pp.GetValue(clone); break; }
                }
                var intPtrCtor = mvAvatarLocalType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(IntPtr) }, null);
                if (intPtrCtor == null || clonePtr == IntPtr.Zero) { MelonLogger.Error("[KoGaMaPatch] Can't re-wrap as MVAvatarLocal."); return; }
                
                object avatarLocal = intPtrCtor.Invoke(new object[] { clonePtr });
                MelonLogger.Msg("[KoGaMaPatch] Re-wrapped as MVAvatarLocal.");


                try
                {
                    var sp = mvAvatarLocalType.GetProperty("SpawnId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (sp != null && sp.CanWrite) sp.SetValue(avatarLocal, _cachedSpawnerWoId);
                    else { var sf = mvAvatarLocalType.GetField("spawnWorldObjectId", BindingFlags.NonPublic | BindingFlags.Instance); sf?.SetValue(avatarLocal, _cachedSpawnerWoId); }
                }
                catch { }


                MethodInfo initMethod = null;
                for (Type t = mvAvatarLocalType; t != null && initMethod == null; t = t.BaseType)
                    initMethod = t.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
                
                if (initMethod != null)
                {
                    try { initMethod.Invoke(avatarLocal, null); MelonLogger.Msg("[KoGaMaPatch] Initialize() completed."); }
                    catch (System.Exception ex) { MelonLogger.Warning($"[KoGaMaPatch] Initialize threw: {ex.InnerException?.Message ?? ex.Message}"); }
                }

                var ctrlType2 = FindTypeInAnyAssembly("Il2Cpp.MVGameControllerBase") ?? FindTypeInAnyAssembly("MVGameControllerBase");
                var lpProp = ctrlType2.GetProperty("LocalPlayer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var lp = lpProp?.GetValue(null, null);
                if (lp == null) { MelonLogger.Error("[KoGaMaPatch] LocalPlayer null."); return; }
                
                var srmProp = lp.GetType().GetProperty("SpawnRolesManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var srm = srmProp?.GetValue(lp, null);
                if (srm == null) { MelonLogger.Error("[KoGaMaPatch] SpawnRolesManager null."); return; }

                var addM = srm.GetType().GetMethod("AddSpawnRole", BindingFlags.Public | BindingFlags.Instance);
                if (addM != null) { try { addM.Invoke(srm, new object[] { 1000000 }); } catch { } }

                var actM = srm.GetType().GetMethod("ActivateSpawnRole", BindingFlags.Public | BindingFlags.Instance);
                if (actM != null)
                {
                    try
                    {
                        actM.Invoke(srm, new object[] { 1000000, _cachedSpawnerPosition, UnityEngine.Quaternion.identity });
                        MelonLogger.Msg($"[KoGaMaPatch] *** ActivateSpawnRole(1000000) avatar should be active! ***");
                    }
                    catch (System.Exception ex)
                    { MelonLogger.Error($"[KoGaMaPatch] ActivateSpawnRole failed: {ex.InnerException?.Message ?? ex.Message}"); }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[KoGaMaPatch] TryCloneAndActivateAvatar OUTER: {ex.Message}");
                _avatarCloneAttempted = false;
            }
        }

private static void ForceLoadKgmMapFromDisk()
{
    if (_mapLoaded) return;
    _mapLoaded = true;

    if (_cachedPhotonListener == null)
    {
        MelonLogger.Warning("[KoGaMaPatch] Cannot load map: MVNetworkGame instance is null.");
        return;
    }
    try
    {
        if (_staticWorldNetwork == null)
        {
            MelonLogger.Msg("[KoGaMaPatch] Constructing WorldNetwork manually...");
            var worldNetworkType = FindTypeInAnyAssembly("Il2Cpp.WorldNetwork") ?? FindTypeInAnyAssembly("WorldNetwork");
            if (worldNetworkType != null)
            {
                try 
                { 
                    _staticWorldNetwork = System.Activator.CreateInstance(worldNetworkType); 
                    

                    for (var t = _staticWorldNetwork.GetType(); t != null; t = t.BaseType)
                    {
                        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition().Name.StartsWith("Dictionary`2"))
                            {
                                if (f.GetValue(_staticWorldNetwork) == null)
                                {
                                    var newDict = System.Activator.CreateInstance(f.FieldType);
                                    f.SetValue(_staticWorldNetwork, newDict);
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[KoGaMaPatch] Failed to construct WorldNetwork: {ex.Message}");
                    return;
                }
            }
        }
        object worldNetwork = _staticWorldNetwork;
        var playerControllerProp = _cachedPhotonListener.GetType().GetProperty("PlayerController", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (playerControllerProp != null && playerControllerProp.GetValue(_cachedPhotonListener, null) == null)
        {
            var mvLocalObjectControllerType = FindTypeInAnyAssembly("Il2Cpp.MVLocalObjectController") ?? FindTypeInAnyAssembly("MVLocalObjectController");
            if (mvLocalObjectControllerType != null)
            {
                try
                {
                    var wocm = _staticWorldNetwork.GetType().GetProperty("WorldObjectClientManagerNetwork")?.GetValue(_staticWorldNetwork);
                    if (wocm != null)
                    {
                        object controller = System.Activator.CreateInstance(mvLocalObjectControllerType, new object[] { wocm });
                        playerControllerProp.SetValue(_cachedPhotonListener, controller, null);
                        MelonLogger.Msg("[KoGaMaPatch] Manually constructed PlayerController.");
                    }
                }
                catch (System.Exception ex) { MelonLogger.Warning($"[KoGaMaPatch] Failed to construct PlayerController: {ex.Message}"); }
            }
        }

        string mapPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KogamaMaps", "1.kgm");

        if (!System.IO.File.Exists(mapPath))
        {
            MelonLogger.Warning($"[KoGaMaPatch] Map file not found: {mapPath}");
            return;
        }

        MelonLogger.Msg($"[KoGaMaPatch] Reading map from disk: {mapPath}");
        byte[] mapData = System.IO.File.ReadAllBytes(mapPath);


        var bytePackerType = FindTypeInAnyAssembly("Il2CppMV.WorldObject.BytePacker") ?? FindTypeInAnyAssembly("MV.WorldObject.BytePacker") ?? FindTypeInAnyAssembly("BytePacker");
        if (bytePackerType == null)
        {
            MelonLogger.Error("[KoGaMaPatch] BytePacker type not found!");
            return;
        }

        object bytePacker = null;
        try
        {
            var il2cppArrayType = FindTypeInAnyAssembly("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1")?.MakeGenericType(typeof(byte));
            if (il2cppArrayType != null)
            {
                var arrCtor = il2cppArrayType.GetConstructor(new[] { typeof(byte[]) });
                if (arrCtor != null)
                {
                    var il2cppArr = arrCtor.Invoke(new object[] { mapData });
                    var ctor = bytePackerType.GetConstructor(new[] { il2cppArrayType });
                    if (ctor != null) bytePacker = ctor.Invoke(new object[] { il2cppArr });
                }
            }
        }
        catch (System.Exception ex)
        {
            MelonLogger.Error($"[KoGaMaPatch] Failed to create BytePacker: {ex.Message}");
            return;
        }

        if (bytePacker == null)
        {
            MelonLogger.Error("[KoGaMaPatch] Failed to create BytePacker instance entirely!");
            return;
        }


        var createMethod = worldNetwork.GetType().GetMethod("CreateGameWorldFromQueryData", BindingFlags.Public | BindingFlags.Instance);
        if (createMethod == null)
        {
            MelonLogger.Error("[KoGaMaPatch] CreateGameWorldFromQueryData method not found!");
            return;
        }

        MelonLogger.Msg("[KoGaMaPatch] Forcing map deserialization...");
        try
        {
            createMethod.Invoke(worldNetwork, new object[] { bytePacker, 1 });
            MelonLogger.Msg("[KoGaMaPatch] Map loaded successfully!");
            

            FindSpawnerIdFromWorld();

        }
        catch (System.Reflection.TargetInvocationException tex)
        {
            var inner = tex.InnerException ?? tex;
            MelonLogger.Error($"[KoGaMaPatch] CreateGameWorldFromQueryData threw: {inner.GetType().Name}: {inner.Message}");
        }

        try
        {
            var containerProp = _cachedPhotonListener.GetType().GetProperty("MVPlayerContainer");
            object container = containerProp?.GetValue(_cachedPhotonListener);
            object localPlayer = container?.GetType().GetProperty("LocalPlayer")?.GetValue(container);


            if (localPlayer == null && container != null)
            {
                var localPlayerField = container.GetType().GetField("localPlayer", BindingFlags.NonPublic | BindingFlags.Instance);
                if (localPlayerField != null) localPlayer = localPlayerField.GetValue(container);
            }


            if (localPlayer == null && container != null)
            {
                var getItemMethod = container.GetType().GetMethod("get_Item", new[] { typeof(int) });
                if (getItemMethod != null)
                {
                    try { localPlayer = getItemMethod.Invoke(container, new object[] { 1 }); } catch { }
                }
            }

            if (localPlayer == null)
            {
                MelonLogger.Warning("[KoGaMaPatch] LocalPlayer is null. Creating it on the spot...");
                var userProfileType = FindTypeInAnyAssembly("Il2CppMV.WorldObject.MetaData.UserProfileData") ?? FindTypeInAnyAssembly("MV.WorldObject.MetaData.UserProfileData");
                var localPlayerType = FindTypeInAnyAssembly("Il2Cpp.MVLocalPlayerRegistered") ?? FindTypeInAnyAssembly("MVLocalPlayerRegistered");
                var intListType = FindTypeInAnyAssembly("Il2CppSystem.Collections.Generic.List`1")?.MakeGenericType(typeof(int));
                
                if (userProfileType != null && localPlayerType != null && intListType != null)
                {
                    object dummyProfile = System.Activator.CreateInstance(userProfileType);
                    object intList = System.Activator.CreateInstance(intListType);
                    var playerCtor = localPlayerType.GetConstructor(new[] { typeof(int), typeof(int), typeof(string), typeof(int), intListType, userProfileType });
                    if (playerCtor != null)
                    {
                        var harmony = new HarmonyLib.Harmony("KogamaOfflinePatch.PlayerCtorBypass2");
                        harmony.Patch(playerCtor, finalizer: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Finalizer_SwallowException)));
                        try
                        {
                            localPlayer = playerCtor.Invoke(new object[] { 1, 1, "local", 1, intList, dummyProfile });
                            if (localPlayer != null && container != null)
                            {
                                var playerType = FindTypeInAnyAssembly("Il2Cpp.MVPlayer") ?? FindTypeInAnyAssembly("MVPlayer");
                                var addMethod = container.GetType().GetMethod("Add", new[] { playerType });
                                addMethod?.Invoke(container, new object[] { localPlayer });
                                var setLocalMethod = container.GetType().GetMethod("SetLocalPlayer");
                                setLocalMethod?.Invoke(container, new object[] { 1 });
                            }
                        }
                        catch (System.Exception ex) { MelonLogger.Error($"[KoGaMaPatch] On-spot player creation failed: {ex.Message}"); }
                        finally { harmony.Unpatch(playerCtor, HarmonyPatchType.Finalizer); }
                    }
                }
            }

            if (localPlayer != null)
            {
                MelonLogger.Msg("[KoGaMaPatch] Found LocalPlayer. Setting up SpawnRoleManager...");

                System.Type spawnRolesRuntimeDataType = FindTypeInAnyAssembly("Il2CppMV.WorldObject.SpawnRoles.SpawnRolesRuntimeData") ?? FindTypeInAnyAssembly("MV.WorldObject.SpawnRoles.SpawnRolesRuntimeData");
                object runtimeData = System.Activator.CreateInstance(spawnRolesRuntimeDataType);


                var setupMethod = localPlayer.GetType().GetMethod("SetupPlayerWorldObjects", BindingFlags.Public | BindingFlags.Instance);
                if (setupMethod != null)
                {
                    try
                    {
                        setupMethod.Invoke(localPlayer, new object[] { 0, runtimeData });
                        MelonLogger.Msg("[KoGaMaPatch] SetupPlayerWorldObjects successful.");
                        TryCloneAndActivateAvatar();
                    }
                    catch (System.Exception ex)
                    {
                        MelonLogger.Error($"[KoGaMaPatch] Setup/CreateSpawnRole threw: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }
            else
            {
                MelonLogger.Error("[KoGaMaPatch] LocalPlayer is STILL null after all fallbacks. Cannot spawn avatar.");
            }
        }
        catch (System.Exception ex)
        {
            MelonLogger.Error($"[KoGaMaPatch] Force avatar spawn failed: {ex.Message}");
        }

        try
        {
            var listener = _cachedPhotonListener;
            var eventDataType = FindTypeInAnyAssembly("Il2CppExitGames.Client.Photon.EventData") ?? FindTypeInAnyAssembly("ExitGames.Client.Photon.EventData");
            if (listener != null && eventDataType != null)
            {
                var onEventMethod = listener.GetType().GetMethod("OnEvent", BindingFlags.Public | BindingFlags.Instance);
                var codeProp = eventDataType.GetProperty("Code");
                var paramsProp = eventDataType.GetProperty("Parameters", BindingFlags.Public | BindingFlags.Instance);
                
                if (onEventMethod != null && codeProp != null)
                {

                    var il2cppObjectType = FindTypeInAnyAssembly("Il2CppSystem.Object");
                    var dictType = FindTypeInAnyAssembly("Il2CppSystem.Collections.Generic.Dictionary`2")?.MakeGenericType(typeof(byte), il2cppObjectType);
                    object event62Dict = null;
                    if (dictType != null) { try { event62Dict = System.Activator.CreateInstance(dictType); } catch { } }


                    object evt62 = System.Activator.CreateInstance(eventDataType);
                    codeProp.SetValue(evt62, (byte)62);
                    if (paramsProp != null && event62Dict != null) paramsProp.SetValue(evt62, event62Dict);
                    onEventMethod.Invoke(listener, new object[] { evt62 });
                    MelonLogger.Msg("[KoGaMaPatch] Faked SetupUserPlayMode (62) event sent.");
                }
            }
        }
        catch (System.Exception ex)
        {
            MelonLogger.Warning($"[KoGaMaPatch] Failed to send post-load events: {ex.Message}");
        }
    }
    catch (System.Exception ex)
    {
        MelonLogger.Error($"[KoGaMaPatch] Failed to load map: {ex.Message}");
    }

        try
        {
            var controllerType = FindTypeInAnyAssembly("Il2Cpp.MVGameControllerBase") ?? FindTypeInAnyAssembly("MVGameControllerBase");
            if (controllerType != null)
            {

                var playModeUIProp = controllerType.GetProperty("PlayModeUI", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (playModeUIProp != null)
                {
                    object playModeUI = playModeUIProp.GetValue(null, null);
                    if (playModeUI != null)
                    {
                        var inLobbyStateProp = playModeUI.GetType().GetProperty("InLobbyState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (inLobbyStateProp != null)
                        {
                            inLobbyStateProp.SetValue(playModeUI, true);
                            MelonLogger.Msg("[KoGaMaPatch] Forced PlayModeUI.InLobbyState = true.");
                        }
                    }
                }


                var desktopType = FindTypeInAnyAssembly("Il2Cpp.MVGameControllerDesktop") ?? FindTypeInAnyAssembly("MVGameControllerDesktop");
                if (desktopType != null)
                {
                    var lockCursorManagerProp = desktopType.GetProperty("LockCursorManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (lockCursorManagerProp != null)
                    {
                        object lockCursorManager = lockCursorManagerProp.GetValue(null, null);
                        if (lockCursorManager != null)
                        {
                            var cursorLockProp = lockCursorManager.GetType().GetProperty("CursorLock", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (cursorLockProp != null && cursorLockProp.CanWrite)
                            {
                                cursorLockProp.SetValue(lockCursorManager, false);
                                MelonLogger.Msg("[KoGaMaPatch] Forced LockCursorManager.CursorLock = false.");

        try
        {
            var lobbyStateControllerType = FindTypeInAnyAssembly("Il2Cpp.LobbyStatePlayModeController") ?? FindTypeInAnyAssembly("LobbyStatePlayModeController");
            if (lobbyStateControllerType != null)
            {
                System.Reflection.MethodInfo findMethod = null;
                foreach (var m in typeof(UnityEngine.Resources).GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name == "FindObjectsOfTypeAll" && m.IsGenericMethod)
                    {
                        findMethod = m;
                        break;
                    }
                }
                
                if (findMethod != null)
                {
                    var generic = findMethod.MakeGenericMethod(lobbyStateControllerType);
                    var array = generic.Invoke(null, null) as System.Array;
                    if (array != null && array.Length > 0)
                    {
                        var controller = array.GetValue(0);
                        var shouldOpenField = controller.GetType().GetField("shouldOpenLobbyMenu", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (shouldOpenField != null)
                        {
                            shouldOpenField.SetValue(controller, true);
                            MelonLogger.Msg("[KoGaMaPatch] Forced LobbyStatePlayModeController.shouldOpenLobbyMenu = true.");
                        }
                    }
                }
            }
        }
        catch (System.Exception ex) { MelonLogger.Warning($"[KoGaMaPatch] Failed to force shouldOpenLobbyMenu: {ex.Message}"); }
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception ex) { MelonLogger.Warning($"[KoGaMaPatch] Failed to force lobby UI: {ex.Message}"); }
        
}

private static bool _spawnRoleScheduled = false;

private static object _staticWorldNetwork = null;


public static bool Prefix_GetPlayerUnsafe(ref object __result, int actorNr)
{
    if (_cachedLocalPlayerInstance != null)
    {
        __result = _cachedLocalPlayerInstance;
    }

    return false; 
}

public static bool Prefix_Get_World(ref object __result)
{
    if (_staticWorldNetwork != null)
    {
        __result = _staticWorldNetwork;
        return false;
    }
    return true;
}

public static bool Prefix_Get_WOCM(ref object __result)
{
    if (_staticWorldNetwork != null)
    {
        var wocm = _staticWorldNetwork.GetType().GetProperty("WorldObjectClientManagerNetwork")?.GetValue(_staticWorldNetwork);
        if (wocm != null)
        {
            __result = wocm;
            return false;
        }
    }
    return true;
}

private static bool _joinInitialized = false;

public static bool Prefix_OnOperationResponse(object __instance, object operationResponse)
{
    if (_joinInitialized) return true;

    try
    {
        if (operationResponse == null) return true;
        var codeProp = operationResponse.GetType().GetProperty("OperationCode");
        if (codeProp == null) return true;
        byte opCode = (byte)codeProp.GetValue(operationResponse);

        if (opCode == 255)
        {
            MelonLogger.Msg("[KoGaMaPatch] Intercepted Join response (Op 255). Performing safe initialization.");
            

            var connStateField = __instance.GetType().GetField("connState", BindingFlags.NonPublic | BindingFlags.Instance);
            if (connStateField != null)
            {
                var mvConnStateType = FindTypeInAnyAssembly("Il2Cpp.MVConnState") ?? FindTypeInAnyAssembly("MVConnState");
                if (mvConnStateType != null) connStateField.SetValue(__instance, System.Enum.ToObject(mvConnStateType, 1));
            }


            var controllerType = FindTypeInAnyAssembly("Il2Cpp.MVGameControllerBase") ?? FindTypeInAnyAssembly("MVGameControllerBase");
            var localPlayerProp = controllerType.GetProperty("LocalPlayer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            object localPlayer = localPlayerProp?.GetValue(null, null);
            
            if (localPlayer == null)
            {
                MelonLogger.Msg("[KoGaMaPatch] Creating MVLocalPlayerRegistered...");
                var userProfileType = FindTypeInAnyAssembly("Il2CppMV.WorldObject.MetaData.UserProfileData") ?? FindTypeInAnyAssembly("MV.WorldObject.MetaData.UserProfileData");
                var localPlayerType = FindTypeInAnyAssembly("Il2Cpp.MVLocalPlayerRegistered") ?? FindTypeInAnyAssembly("MVLocalPlayerRegistered");
                var intListType = FindTypeInAnyAssembly("Il2CppSystem.Collections.Generic.List`1")?.MakeGenericType(typeof(int));
                
                if (userProfileType != null && localPlayerType != null && intListType != null)
                {
                    object dummyProfile = System.Activator.CreateInstance(userProfileType);
                    object intList = System.Activator.CreateInstance(intListType);
                    
                    var playerCtor = localPlayerType.GetConstructor(new[] { typeof(int), typeof(int), typeof(string), typeof(int), intListType, userProfileType });
                    if (playerCtor != null)
                    {
                        var harmony = new HarmonyLib.Harmony("KogamaOfflinePatch.PlayerCtorBypass");
                        harmony.Patch(playerCtor, finalizer: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Finalizer_SwallowException)));
                        
                        try
                        {
                            object newPlayer = playerCtor.Invoke(new object[] { 1, 1, "local", 1, intList, dummyProfile });
                            if (newPlayer != null)
                            {

                                _cachedLocalPlayerInstance = newPlayer;
                                
                                var containerProp = __instance.GetType().GetProperty("MVPlayerContainer");
                                object container = containerProp?.GetValue(__instance);
                                if (container != null)
                                {
                                    var playerType = FindTypeInAnyAssembly("Il2Cpp.MVPlayer") ?? FindTypeInAnyAssembly("MVPlayer");
                                    var addMethod = container.GetType().GetMethod("Add", new[] { playerType });
                                    addMethod?.Invoke(container, new object[] { newPlayer });
                                    
                                    var setLocalMethod = container.GetType().GetMethod("SetLocalPlayer");
                                    setLocalMethod?.Invoke(container, new object[] { 1 });
                                    MelonLogger.Msg("[KoGaMaPatch] LocalPlayer injected successfully.");
                                }
                            }
                        }
                        catch (System.Exception ex) { MelonLogger.Error($"[KoGaMaPatch] Player creation failed: {ex.Message}"); }
                        finally { harmony.Unpatch(playerCtor, HarmonyPatchType.Finalizer); }
                    }
                }
            }

            var joinStateProp = controllerType.GetProperty("JoinState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (joinStateProp != null)
            {
                var mvJoinStateType = FindTypeInAnyAssembly("Il2Cpp.MVJoinState") ?? FindTypeInAnyAssembly("MVJoinState");
                if (mvJoinStateType != null)
                {
                    object loadGuiVal = System.Enum.Parse(mvJoinStateType, "LoadGUI", true);
                    if (loadGuiVal != null) joinStateProp.SetValue(null, loadGuiVal);
                }
            }


            var loadModeGuiMethod = __instance.GetType().GetMethod("LoadModeGui", BindingFlags.NonPublic | BindingFlags.Instance);
            if (loadModeGuiMethod != null)
            {
                MelonLogger.Msg("[KoGaMaPatch] Calling LoadModeGui()...");
                loadModeGuiMethod.Invoke(__instance, null);
            }

            var operationRequestsProp = __instance.GetType().GetProperty("OperationRequestSender");
            object opRequests = operationRequestsProp?.GetValue(__instance);
            if (opRequests != null)
            {
                var tryRemoveMethod = opRequests.GetType().GetMethod("TryRemovePendingOperation");
                if (tryRemoveMethod != null)
                {
                    var mvOperationCodesType = FindTypeInAnyAssembly("Il2Cpp.MVOperationCodes") ?? FindTypeInAnyAssembly("MVOperationCodes");
                    if (mvOperationCodesType != null)
                    {
                        object joinCode = System.Enum.Parse(mvOperationCodesType, "Join", true);
                        if (joinCode != null) tryRemoveMethod.Invoke(opRequests, new object[] { joinCode });
                    }
                }
            }

            _joinInitialized = true;
            return false;
        }
    }
    catch (System.Exception ex)
    {
        MelonLogger.Error($"[KoGaMaPatch] Prefix_OnOperationResponse error: {ex.Message}");
    }
    return true;
}
        private static bool _avatarSpawned = false;

        private static void ForceAvatarSpawn()
        {
            if (_avatarSpawned) return;
            _avatarSpawned = true;

            try
            {
                MelonLogger.Msg("[KoGaMaPatch] Attempting to synthesize MVAvatarLocal locally...");


                var pkgType = FindTypeInAnyAssembly("Il2Cpp.KoGaMaPackageClient") ?? FindTypeInAnyAssembly("KoGaMaPackageClient");
                if (pkgType == null) return;

                var factoryMethod = pkgType.GetMethod("WorldObjectFactory", BindingFlags.Public | BindingFlags.Static);
                if (factoryMethod == null) return;


                var dictType = typeof(Il2CppSystem.Collections.Generic.Dictionary<,>).MakeGenericType(typeof(Il2CppSystem.Object), typeof(Il2CppSystem.Object));
                object dict = System.Activator.CreateInstance(dictType);


                var wocm = _cachedPhotonListener.GetType().GetProperty("WorldObjectClientManager").GetValue(_cachedPhotonListener);
                object worldObjectsDict = wocm.GetType().GetField("worldObjects", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(wocm);
                if (worldObjectsDict == null)
                {
                    var emptyDictType = typeof(Il2CppSystem.Collections.Generic.Dictionary<,>).MakeGenericType(typeof(int), FindTypeInAnyAssembly("Il2Cpp.MVWorldObjectClient"));
                    worldObjectsDict = System.Activator.CreateInstance(emptyDictType);
                }

                object prototypesDict = _cachedWorldObjectsDict;
                if (prototypesDict == null)
                {
                    var emptyProtoDictType = typeof(Il2CppSystem.Collections.Generic.Dictionary<,>).MakeGenericType(typeof(int), FindTypeInAnyAssembly("Il2Cpp.RuntimePrototypeCubeModel"));
                    prototypesDict = System.Activator.CreateInstance(emptyProtoDictType);
                }


                object avatarInstance = factoryMethod.Invoke(null, new object[] { dict, worldObjectsDict, prototypesDict });
                if (avatarInstance != null)
                {
                    MelonLogger.Msg($"[KoGaMaPatch] Created MVAvatarLocal instance via Factory: {avatarInstance.GetType().Name}");


                    var addToWorldMethod = wocm.GetType().GetMethod("AddToWorldObjects", BindingFlags.Public | BindingFlags.Instance);
                    if (addToWorldMethod != null)
                    {
                        addToWorldMethod.Invoke(wocm, new object[] { avatarInstance });
                        MelonLogger.Msg("[KoGaMaPatch] Added avatar to WorldObjectClientManager.");
                    }


                    var initMethod = avatarInstance.GetType().GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
                    if (initMethod != null)
                    {
                        try { initMethod.Invoke(avatarInstance, null); } catch { }
                        MelonLogger.Msg("[KoGaMaPatch] Called Initialize() on avatar.");
                    }


                    try
                    {
                        var goProp = avatarInstance.GetType().GetProperty("GameObject");
                        if (goProp != null)
                        {
                            var go = goProp.GetValue(avatarInstance);
                            if (go != null)
                            {
                                var transformProp = go.GetType().GetProperty("transform");
                                var transform = transformProp.GetValue(go);
                                var positionProp = transform.GetType().GetProperty("position");
                                if (positionProp != null && positionProp.CanWrite)
                                {
                                    positionProp.SetValue(transform, _cachedSpawnerPosition);
                                }
                            }
                        }
                    }
                    catch { }


                    var activateMethod = avatarInstance.GetType().GetMethod("Activate", BindingFlags.Public | BindingFlags.Instance);
                    if (activateMethod != null)
                    {
                        var localPlayerType = FindTypeInAnyAssembly("Il2Cpp.MVLocalPlayer") ?? FindTypeInAnyAssembly("MVLocalPlayer");
                        var localPlayerProp = FindTypeInAnyAssembly("Il2Cpp.MVGameControllerBase")?.GetProperty("LocalPlayer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        object newPlayer = localPlayerProp?.GetValue(null, null);

                        object spawnRoleDataReceiver = null;
                        if (newPlayer != null)
                        {
                            var receiverField = localPlayerType.GetField("spawnRoleDataReceiver", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (receiverField != null) spawnRoleDataReceiver = receiverField.GetValue(newPlayer);
                        }

                        if (spawnRoleDataReceiver != null)
                        {
                            activateMethod.Invoke(avatarInstance, new object[] { _cachedSpawnerWoId, spawnRoleDataReceiver, _cachedSpawnerPosition, UnityEngine.Quaternion.identity });
                            MelonLogger.Msg("[KoGaMaPatch] *** Called MVAvatarLocal.Activate  Avatar 3D model should appear now! ***");
                        }
                        else
                        {
                            MelonLogger.Warning("[KoGaMaPatch] spawnRoleDataReceiver is null!");
                        }
                    }
                }
                else
                {
                    MelonLogger.Warning("[KoGaMaPatch] WorldObjectFactory returned null!");
                }
            }
            catch (System.Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                MelonLogger.Error("[KoGaMaPatch] Force Avatar Factory threw: " + inner.GetType().Name + " - " + inner.Message + "\n" + inner.StackTrace);
            }
        }

private static object _cachedWocm = null;
private static object _cachedRemn = null;

public static bool Prefix_Get_WorldObjectClientManagerNetwork(ref object __result)
{
    if (_cachedWocm == null)
    {
        var wocmType = FindTypeInAnyAssembly("Il2Cpp.MVWorldObjectClientManagerNetwork") ?? FindTypeInAnyAssembly("MVWorldObjectClientManagerNetwork");
        if (wocmType != null)
        {
            try
            {
                _cachedWocm = CreateUninitializedIl2CppObject(wocmType);
                if (_cachedWocm != null)
                {
                    MelonLogger.Msg("[KoGaMaPatch] Manually created MVWorldObjectClientManagerNetwork instance (uninitialized).");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[KoGaMaPatch] Failed to create MVWorldObjectClientManagerNetwork: {ex.Message}");
            }
        }
    }
    if (_cachedWocm != null)
    {
        __result = _cachedWocm;
        return false;
    }
    return true;
}

public static bool Prefix_Get_RuntimeEventManagerNetwork(ref object __result)
{
    if (_cachedRemn == null)
    {
        var remnType = FindTypeInAnyAssembly("Il2Cpp.RuntimeEventManagerNetwork") ?? FindTypeInAnyAssembly("RuntimeEventManagerNetwork");
        if (remnType != null)
        {
            try
            {
                _cachedRemn = CreateUninitializedIl2CppObject(remnType);
                if (_cachedRemn != null)
                {
                    MelonLogger.Msg("[KoGaMaPatch] Manually created RuntimeEventManagerNetwork instance (uninitialized).");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[KoGaMaPatch] Failed to create RuntimeEventManagerNetwork: {ex.Message}");
            }
        }
    }
    if (_cachedRemn != null)
    {
        __result = _cachedRemn;
        return false;
    }
    return true;
}

private static object CreateUninitializedIl2CppObject(System.Type targetType)
{
    try
    {
        var il2cppType = FindTypeInAnyAssembly("Il2CppInterop.Runtime.IL2CPP");
        if (il2cppType == null) return null;

        var newObjM = il2cppType.GetMethod("il2cpp_object_new", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (newObjM == null) return null;


        var classPtrProp = targetType.GetProperty("Class", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (classPtrProp == null) return null;
        
        var classPtr = classPtrProp.GetValue(null);
        if (classPtr == null) return null;


        var ptr = (System.IntPtr)newObjM.Invoke(null, new object[] { classPtr });


        var intPtrCtor = targetType.GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(System.IntPtr) },
            modifiers: null);

        if (intPtrCtor != null)
        {
            return intPtrCtor.Invoke(new object[] { ptr });
        }
    }
    catch { }


    return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(targetType);
}
public static void Finalizer_SwallowException(ref Exception __exception)
{
    if (__exception != null)
    {
        __exception = null;
    }
}
        private static object FindLoadingScreenHandlerInstance()
        {
            try
            {
                var t = FindTypeInAnyAssembly("Il2Cpp.LoadingScreenHandler") ??
                        FindTypeInAnyAssembly("LoadingScreenHandler");
                if (t == null)
                {
                    DriveLog("FindLoadingScreenHandlerInstance: LoadingScreenHandler type not found");
                    return null;
                }
                DriveLog($"FindLoadingScreenHandlerInstance: type={t.FullName}");


                var resourcesType = typeof(UnityEngine.Resources);
                var resourcesMethods = resourcesType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (var m in resourcesMethods)
                {
                    if (m.Name != "FindObjectsOfTypeAll") continue;
                    if (!m.IsGenericMethodDefinition) continue;
                    if (m.GetParameters().Length != 0) continue;
                    try
                    {
                        var generic = m.MakeGenericMethod(t);
                        var arr = generic.Invoke(null, null) as System.Array;
                        DriveLog($"Resources.FindObjectsOfTypeAll<{t.Name}>: returned {arr?.Length ?? 0} items");
                        if (arr != null && arr.Length > 0)
                        {


                            object fallback = null;
                            for (int i = 0; i < arr.Length; i++)
                            {
                                var item = arr.GetValue(i);
                                if (item == null) continue;
                                var enabledProp = item.GetType().GetProperty("enabled",
                                    BindingFlags.Public | BindingFlags.Instance);
                                if (enabledProp != null)
                                {
                                    bool enabled = (bool)enabledProp.GetValue(item);
                                    if (enabled) return item;
                                    if (fallback == null) fallback = item;
                                }
                                else
                                {
                                    return item;
                                }
                            }
                            if (fallback != null) return fallback;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        DriveLog($"Resources.FindObjectsOfTypeAll<{t.Name}>: threw {ex.GetType().Name}: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }


                var objectMethods = typeof(UnityEngine.Object)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (var m in objectMethods)
                {
                    if (m.Name != "FindObjectsOfType") continue;
                    if (!m.IsGenericMethodDefinition) continue;
                    if (m.GetParameters().Length > 1) continue;
                    try
                    {
                        var generic = m.MakeGenericMethod(t);
                        object[] args = m.GetParameters().Length == 1 ? new object[] { true } : null;
                        var arr = generic.Invoke(null, args) as System.Array;
                        DriveLog($"Object.FindObjectsOfType<{t.Name}>: returned {arr?.Length ?? 0} items");
                        if (arr != null && arr.Length > 0) return arr.GetValue(0);
                    }
                    catch (System.Exception ex)
                    {
                        DriveLog($"Object.FindObjectsOfType<{t.Name}>: threw {ex.GetType().Name}: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }

                try
                {
                    var monoType = typeof(UnityEngine.MonoBehaviour);
                    var monoFindMethod = monoType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m =>
                            m.Name == "FindObjectsOfType" &&
                            m.IsGenericMethodDefinition &&
                            m.GetParameters().Length == 0);
                    if (monoFindMethod != null)
                    {
                        var monoGeneric = monoFindMethod.MakeGenericMethod(monoType);
                        var monoArr = monoGeneric.Invoke(null, null) as System.Array;
                        DriveLog($"MonoBehaviour.FindObjectsOfType: returned {monoArr?.Length ?? 0} items");
                        if (monoArr != null)
                        {
                            for (int i = 0; i < monoArr.Length; i++)
                            {
                                var item = monoArr.GetValue(i);
                                if (item == null) continue;
                                var itemType = item.GetType();
                                if (itemType.Name.Contains("LoadingScreenHandler"))
                                {
                                    DriveLog($"Found LoadingScreenHandler instance via MonoBehaviour scan: {itemType.FullName}");
                                    return item;
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    DriveLog($"MonoBehaviour scan threw: {ex.Message}");
                }

                DriveLog("FindLoadingScreenHandlerInstance: all paths exhausted, returning null");
                return null;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[KoGaMaPatch] FindLoadingScreenHandlerInstance error: {ex.Message}");
                DriveLog($"FindLoadingScreenHandlerInstance OUTER error: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private static object WrapIl2CppPointer(System.Type il2cppObjBaseType, System.Type targetType, System.IntPtr ptr)
        {
            try
            {
                var intPtrCtor = targetType.GetConstructor(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null,
                    types: new[] { typeof(System.IntPtr) },
                    modifiers: null);
                if (intPtrCtor != null)
                {
                    DriveLog($"WrapIl2CppPointer: using {targetType.Name}(IntPtr) ctor");
                    return intPtrCtor.Invoke(new object[] { ptr });
                }



                if (il2cppObjBaseType != null)
                {
                    var baseCtor = il2cppObjBaseType.GetConstructor(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        binder: null,
                        types: new[] { typeof(System.IntPtr) },
                        modifiers: null);
                    if (baseCtor != null)
                    {
                        var il2cppObj = baseCtor.Invoke(new object[] { ptr });
                        DriveLog($"WrapIl2CppPointer: constructed Il2CppObjectBase(ptr), now TryCast<{targetType.Name}>");

                        foreach (var m in il2cppObjBaseType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (m.Name != "TryCast") continue;
                            if (!m.IsGenericMethodDefinition) continue;
                            if (m.GetParameters().Length != 0) continue;
                            try
                            {
                                var generic = m.MakeGenericMethod(targetType);
                                var result = generic.Invoke(il2cppObj, null);
                                if (result != null)
                                {
                                    DriveLog($"WrapIl2CppPointer: TryCast<{targetType.Name}> succeeded");
                                    return result;
                                }
                                DriveLog($"WrapIl2CppPointer: TryCast<{targetType.Name}> returned null  type mismatch?");
                            }
                            catch (System.Exception ex)
                            {
                                DriveLog($"WrapIl2CppPointer: TryCast<{targetType.Name}> threw: {ex.InnerException?.Message ?? ex.Message}");
                            }
                        }
                    }
                }




                if (il2cppObjBaseType != null)
                {
                    try
                    {
                        var getUninitObj = typeof(System.Runtime.CompilerServices.RuntimeHelpers)
                            .GetMethod("GetUninitializedObject", BindingFlags.Public | BindingFlags.Static);
                        if (getUninitObj != null)
                        {
                            var uninitialized = getUninitObj.Invoke(null, new object[] { targetType });

                            var createGh = il2cppObjBaseType.GetMethod(
                                "CreateGCHandle",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (createGh != null)
                            {
                                createGh.Invoke(uninitialized, new object[] { ptr });
                                DriveLog($"WrapIl2CppPointer: created via RuntimeHelpers + CreateGCHandle");
                                return uninitialized;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        DriveLog($"WrapIl2CppPointer: RuntimeHelpers path threw: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"WrapIl2CppPointer OUTER error: {ex.GetType().Name}: {ex.Message}");
            }
            return null;
        }
        private static object FindPhotonListener()
        {
            try
            {
                var controllerType =
                    FindTypeInAnyAssembly("Il2Cpp.MVGameControllerBase") ??
                    FindTypeInAnyAssembly("MVGameControllerBase");
                if (controllerType == null)
                {
                    DriveLog("FindPhotonListener: MVGameControllerBase not found");
                    return null;
                }
                try
                {
                    var gameProp = controllerType.GetProperty(
                        "Game",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (gameProp != null)
                    {
                        var getter = gameProp.GetGetMethod(nonPublic: true) ?? gameProp.GetGetMethod();
                        if (getter != null)
                        {
                            try
                            {
                                var live = getter.Invoke(null, null);
                                if (live != null)
                                {
                                    DriveLog($"FindPhotonListener: MVGameControllerBase.get_Game() → {live.GetType().FullName} (live wrapper)");
                                    _cachedPhotonListenerStrategy = 1;
                                    return live;
                                }
                                DriveLog("FindPhotonListener: MVGameControllerBase.get_Game() returned null");
                            }
                            catch (System.Exception ex)
                            {
                                DriveLog($"FindPhotonListener: get_Game() threw: {ex.InnerException?.Message ?? ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        DriveLog("FindPhotonListener: MVGameControllerBase.Game property not found");
                    }
                }
                catch (System.Exception ex)
                {
                    DriveLog($"FindPhotonListener: strategy 1 outer error: {ex.Message}");
                }




                var desktopType =
                    FindTypeInAnyAssembly("Il2Cpp.MVGameControllerDesktop") ??
                    FindTypeInAnyAssembly("MVGameControllerDesktop");
                object inst = null;
                if (desktopType != null)
                {
                    var instProp = desktopType.GetProperty(
                        "Instance",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (instProp != null)
                    {
                        var getter = instProp.GetGetMethod(nonPublic: true) ?? instProp.GetGetMethod();
                        if (getter != null)
                        {
                            try
                            {
                                inst = getter.Invoke(null, null);
                                DriveLog($"FindPhotonListener: MVGameControllerDesktop.Instance → {(inst != null ? inst.GetType().FullName : "null")}");
                            }
                            catch (System.Exception ex)
                            {
                                DriveLog($"FindPhotonListener: MVGameControllerDesktop.Instance getter threw: {ex.InnerException?.Message ?? ex.Message}");
                            }
                        }
                    }
                }

                if (inst != null)
                {

                    for (System.Type t = inst.GetType(); t != null; t = t.BaseType)
                    {
                        var gameField = t.GetField(
                            "game",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (gameField != null)
                        {
                            try
                            {
                                var game = gameField.GetValue(inst);
                                if (game != null)
                                {
                                    DriveLog($"FindPhotonListener: inst.game (on {t.Name}) → {game.GetType().FullName}");
                                    _cachedPhotonListenerStrategy = 2;
                                    return game;
                                }
                                DriveLog($"FindPhotonListener: inst.game (on {t.Name}) returned null");
                            }
                            catch (System.Exception ex)
                            {
                                DriveLog($"FindPhotonListener: read inst.game (on {t.Name}) threw: {ex.InnerException?.Message ?? ex.Message}");
                            }
                        }
                    }
                    DriveLog("FindPhotonListener: no `game` instance field found on any base type");
                }
                else
                {
                    DriveLog("FindPhotonListener: no controller instance available for strategy 2");
                }



                DriveLog("FindPhotonListener: WARNING  falling back to unsafe static NativeFieldInfoPtr_game dereference");
                System.Type mvNetworkGameType =
                    FindTypeInAnyAssembly("Il2Cpp.MVNetworkGame") ??
                    FindTypeInAnyAssembly("MVNetworkGame");
                if (mvNetworkGameType == null)
                {
                    DriveLog("FindPhotonListener: MVNetworkGame type not found");
                    return null;
                }
                System.Type il2cppObjBaseType =
                    FindTypeInAnyAssembly("Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase") ??
                    FindTypeInAnyAssembly("Il2CppObjectBase");
                var staticGameField = controllerType.GetField(
                    "NativeFieldInfoPtr_game",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (staticGameField != null && il2cppObjBaseType != null)
                {
                    try
                    {
                        var raw = staticGameField.GetValue(null);
                        if (raw is System.IntPtr ptr && ptr != System.IntPtr.Zero)
                        {
                            var managed = WrapIl2CppPointer(il2cppObjBaseType, mvNetworkGameType, ptr);
                            if (managed != null)
                            {
                                DriveLog($"FindPhotonListener: UNSAFE dereferenced NativeFieldInfoPtr_game → {managed.GetType().FullName}");
                                _cachedPhotonListenerStrategy = 3;
                                return managed;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        DriveLog($"FindPhotonListener: unsafe deref threw: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"FindPhotonListener OUTER error: {ex.GetType().Name}: {ex.Message}");
            }
            return null;
        }





        public static void Postfix_Initialize(object __instance)
        {
            try
            {
                MelonLogger.Msg($"[KoGaMaPatch] MVGameControllerBase.Initialize() called on {__instance?.GetType().Name ?? "null"}");
            }
            catch {}
        }




        public static bool Prefix_OnStatusChanged_SuppressDisconnect(object[] __args)
        {
            try
            {
                if (__args != null && __args.Length > 0 && __args[0] != null)
                {
                    int statusCode = (int)__args[0];

                    if (statusCode == 1025 || statusCode == 1040 || statusCode == 1041)
                    {
                        MelonLogger.Msg($"[KoGaMaPatch] Suppressed OnStatusChanged({statusCode}) to prevent disconnect!");
                        return false;
                    }
                }
            }
            catch { }
            return true;
        }



        public static bool Prefix_LoadScene_BlockMenu_String(string sceneName)
{
    MelonLogger.Warning($"[KoGaMaPatch] SceneManager.LoadScene('{sceneName}') called!");
    if (sceneName == "DesktopBase")
    {
        MelonLogger.Warning($"[KoGaMaPatch] Blocked SceneManager.LoadScene('{sceneName}') to stay in play mode!");
        return false;
    }
    return true;
}



        public static bool Prefix_LoadScene_BlockMenu_Int(int sceneBuildIndex)
        {
            if (sceneBuildIndex == 0)
            {
                MelonLogger.Warning($"[KoGaMaPatch] Blocked SceneManager.LoadScene({sceneBuildIndex}) to stay in play mode!");
                return false;
            }
            return true;
        }
        public static void Prefix_Awake(object __instance)
        {
            try
            {

                DriveLog($"Prefix_Awake ENTERED on {__instance?.GetType().FullName ?? "null"} (sceneLoadCount={_sceneLoadCount})");
                MelonLogger.Msg($"[KoGaMaPatch] Awake prefix on {__instance?.GetType().Name ?? "null"}  synthesizing GameSessionData NOW (sceneLoadCount={_sceneLoadCount}).");

                if (!_synthesized)
                {
                    _synthesized = true;
                    TrySynthesizeGameSessionData();
                }

                if (_sceneLoadCount > 0)
                {
                    ResetPerSceneState();
                }
                _sceneLoadCount++;
                int newId = ++_pendingReinvokeTicket;
                _pendingReinvokes.Enqueue(new PendingReinvoke {
                    Ticket    = newId,
                    Target    = __instance,
                    FireAtTick = _globalTick + 1,
                });
                DriveLog($"Prefix_Awake: scheduled StartGame re-invoke ticket={newId} for tick={_globalTick + 1}");
            }
            catch (System.Exception ex)
            {
                DriveLog($"Prefix_Awake EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                MelonLogger.Warning($"[KoGaMaPatch] Prefix_Awake error: {ex.Message}");
            }
        }

        private static int _sceneLoadCount = 0;
        private static System.Collections.Generic.Queue<PendingReinvoke> _pendingReinvokes
            = new System.Collections.Generic.Queue<PendingReinvoke>();
        private static int _pendingReinvokeTicket = 0;

        private class PendingReinvoke
        {
            public int Ticket;
            public object Target;
            public int FireAtTick;
        }

        private static void ResetPerSceneState()
        {
            DriveLog("ResetPerSceneState: clearing all per-scene flags + cached references");
            _startGameInvoked = false;
            _driveStarted = false;
            _driveState = 0;
            _driveTickCounter = 0;
            _photonStatusFired = false;
            _pendingEncryptionFire = -1;
            _stuckAfterState4StartTick = -1;
            _sceneLoadFallbackAttempted = false;
            _sceneLoadFallbackFiredTick = -1;
            _bruteForceHideAttemptCount = 0;
            _bruteForceHideLastRunTick = -1;
            _cachedLoadingScreenHandler = null;
            _cachedOnJoinStateChangedMethod = null;
            _cachedPhotonListener = null;
            _cachedOnStatusChangedMethod = null;
            _cachedPhotonListenerStrategy = 0;
            _cachedLevelLoader = null;

            _pendingReinvokes.Clear();

        }
        public static bool _startGameInvoked = false;
        public static void Postfix_Awake_InvokeStartGame(object __instance)
        {
            try
            {
                DriveLog($"Postfix_Awake_InvokeStartGame ENTERED on {__instance?.GetType().FullName ?? "null"}");
                MelonLogger.Msg($"[KoGaMaPatch] Awake postfix on {__instance?.GetType().Name ?? "null"}  synthesizing GameSessionData NOW.");

                if (!_synthesized)
                {
                    _synthesized = true;
                    TrySynthesizeGameSessionData();
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"Prefix_Awake EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                MelonLogger.Warning($"[KoGaMaPatch] Prefix_Awake error: {ex.Message}");
            }
        }

        public static void Postfix_Start_InvokeStartGameIfMissing(object __instance, MethodBase __originalMethod)
        {
            try
            {
                string typeName = __instance?.GetType().Name ?? "null";
                MelonLogger.Msg($"[KoGaMaPatch] {typeName}.{__originalMethod.Name}() called (postfix: invoke StartGame if missing)");
                DriveLog($"Postfix_Start_InvokeStartGameIfMissing: ENTERED on {__instance?.GetType().FullName ?? "null"}");



            }
            catch (System.Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                DriveLog($"Postfix_Start_InvokeStartGameIfMissing: error {inner.GetType().Name}: {inner.Message}");
                MelonLogger.Warning($"[KoGaMaPatch] Postfix_Start_InvokeStartGameIfMissing error: {inner.GetType().Name}: {inner.Message}");
            }
        }

                public static void ForceStartGame(object instance)
        {
            if (_startGameInvoked) return;
            if (instance == null) return;

            try
            {
                System.Reflection.MethodInfo startGameMethod = null;
                for (System.Type t = instance.GetType(); t != null && startGameMethod == null; t = t.BaseType)
                {
                    var candidates = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var m in candidates)
                    {
                        if (m.Name == "StartGame" && m.GetParameters().Length == 0 && m.ReturnType == typeof(void))
                        {
                            startGameMethod = m;
                            break;
                        }
                    }
                }
                if (startGameMethod != null)
                {
                    _startGameInvoked = true;
                    startGameMethod.Invoke(instance, null);
                    MelonLogger.Msg($"[KoGaMaPatch] ForceStartGame: StartGame() invoked successfully on {instance.GetType().Name}");
                }
            }
            catch (System.Exception ex)
            {
                _startGameInvoked = false;
            }
        }
        private static int _traceTick = 0;
        private static string _lastTraceKey = "";
        public static void Postfix_Trace(object __instance, MethodBase __originalMethod)
        {
            try
            {
                string name = __originalMethod.Name;
                string typeName = __instance?.GetType().Name ?? "null";
                if (name == UpdateMethodName)
                {

                    string key = typeName + "." + name;
                    if (key == _lastTraceKey)
                    {
                        if ((_traceTick++ & 63) != 0) return;
                    }
                    else
                    {
                        _lastTraceKey = key;
                        _traceTick = 0;
                    }
                }
                MelonLogger.Msg($"[KoGaMaPatch] {typeName}.{name}() called");
            }
            catch {}
        }
private static readonly System.Collections.Generic.HashSet<int> _tamperedStateMachines = new System.Collections.Generic.HashSet<int>();

private const int SentinelTerminalState = 0x7FFFFFFF;

public static bool Prefix_MoveNext_Prepare(ref object __instance)
        {


            TrySynthesizeGameSessionData();

            if (__instance == null)
            {
                MelonLogger.Msg("[KoGaMaPatch] MoveNext prefix: __instance null  letting original run.");
                return true;
            }

            var wt = __instance.GetType();
            string typeName = wt.FullName ?? wt.Name;



            bool isInitRegionDependent =
                typeName != null && (
                    typeName.Contains("InitRegionDependent") ||
                    typeName.Contains("InitRegion"));

            if (!isInitRegionDependent)
            {

                DriveLog($"MoveNext on {wt.Name}  not InitRegionDependent, letting original run.");
                return true;
            }

            try
            {
                IntPtr nativePtr = GetIl2CppPointer(__instance);
                if (nativePtr == IntPtr.Zero)
                {
                    DriveLog($"MoveNext on {wt.Name}  native Pointer is zero (wrapper not yet fully constructed?), letting original run.");
                    return true;
                }

                long instanceHash64 = nativePtr.ToInt64();
                int instanceHash = unchecked((int)(instanceHash64 ^ (instanceHash64 >> 32)));
                if (_tamperedStateMachines.Contains(instanceHash))
                {
                    return true;
                }

                if (!_initRegionStateOffsetProbed)
                {
                    _initRegionStateOffsetProbed = true;
                    DumpStateOffsetCandidates(nativePtr, wt.Name);
                }

                int state = Marshal.ReadInt32(nativePtr, _initRegionStateOffset);
                DriveLog($"{wt.Name}: MoveNext state={state}, sentinel-fixing to terminate coroutine.");
                _tamperedStateMachines.Add(instanceHash);

                Marshal.WriteInt32(nativePtr, _initRegionStateOffset, SentinelTerminalState);
                return true;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[KoGaMaPatch] MoveNext prefix error on {wt.Name}: {ex.Message}");
                DriveLog($"MoveNext prefix error on {wt.Name}: {ex.GetType().Name}: {ex.Message}");
                return true;
            }
        }

        private const int InitRegionStateOffset = 0x10;
        private static int _initRegionStateOffset = InitRegionStateOffset;
        private static bool _initRegionStateOffsetProbed = false;
        private static IntPtr GetIl2CppPointer(object instance)
        {
            try
            {
                for (System.Type t = instance.GetType(); t != null; t = t.BaseType)
                {
                    var p = t.GetProperty("Pointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (p != null && p.PropertyType == typeof(IntPtr))
                    {
                        return (IntPtr)p.GetValue(instance);
                    }
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"GetIl2CppPointer error: {ex.Message}");
            }
            return IntPtr.Zero;
        }
        private static void DumpStateOffsetCandidates(IntPtr nativePtr, string typeName)
        {
            try
            {
                DriveLog($"-- {typeName} native dump at {nativePtr.ToString("X")} --");
                int[] candidates = new int[] { 0x10, 0x14, 0x18, 0x1C, 0x20, 0x24, 0x28, 0x2C, 0x30, 0x34, 0x38, 0x3C, 0x40, 0x44, 0x48, 0x4C, 0x50, 0x54, 0x58, 0x5C };
                for (int i = 0; i < candidates.Length; i++)
                {
                    int off = candidates[i];
                    int val;
                    try { val = Marshal.ReadInt32(nativePtr, off); }
                    catch { val = unchecked((int)0xBADC0DE); }
                    DriveLog($"   +0x{off:X2}: {val} (0x{val:X8})");

                    if (_initRegionStateOffset < 0 && val == 0)
                    {
                        _initRegionStateOffset = off;
                        DriveLog($"   --> assuming +0x{off:X2} is <>1__state (first 0-valued int32 candidate after object header)");
                    }
                }
                if (_initRegionStateOffset < 0)
                {
                    _initRegionStateOffset = 0x10;
                    DriveLog($"   --> no 0-valued int32 candidate found; defaulting to offset 0x10");
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"DumpStateOffsetCandidates error: {ex.Message}");
            }
        }

        private static System.Reflection.FieldInfo FindStateMachineStateField(object instance)
        {

            string[] candidates = new[] {
                "<>1__state",
                "_1__state",
                "__state",
                "state",
                "<state>",
                "<>4__this",
            };
            for (System.Type t = instance.GetType(); t != null; t = t.BaseType)
            {
                foreach (var name in candidates)
                {
                    var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null && f.FieldType == typeof(int)) return f;
                }
            }
            return null;
        }
        private static void DumpStateMachineFields(object instance)
        {
            try
            {
                for (System.Type t = instance.GetType(); t != null; t = t.BaseType)
                {
                    DriveLog($"-- {t.FullName} fields --");
                    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        try { DriveLog($"   {f.FieldType.Name} {f.Name}"); } catch { }
                    }
                    if (t.BaseType == null || t.BaseType == typeof(object) || t.BaseType.Name.StartsWith("Il2CppObjectBase")) break;
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"DumpStateMachineFields error: {ex.Message}");
            }
        }

        private static bool _synthesized = false;
        private static System.Reflection.MethodInfo _cachedIsInitializedSetter = null;
        private static System.Reflection.MethodInfo _cachedOnPostGameInitGetter = null;
        private static System.Reflection.MethodInfo _cachedOnPostGameInitSetter = null;
        private static object _cachedOnPostGameInitDelegate = null;

        private static int _stuckAfterState4StartTick = -1;

        private static bool _sceneLoadFallbackAttempted = false;

        private static object _cachedLevelLoader = null;
        private static int _bruteForceHideAttemptCount = 0;
        private const int BruteForceHideMaxAttempts = 8;
        private static int[] _bruteForceHideSchedule = { 1, 5, 15, 30, 60, 120, 240, 480 };
        private static int _bruteForceHideLastRunTick = -1;
        private static int _sceneLoadFallbackFiredTick = -1;
        private static void TryHookAdvancedDiagnostics(HarmonyLib.Harmony harmony)
        {
            try
            {
                var controllerType =
                    FindTypeInAnyAssembly("Il2Cpp.MVGameControllerBase") ??
                    FindTypeInAnyAssembly("MVGameControllerBase");
                if (controllerType == null) return;
                var isInitProp = controllerType.GetProperty(
                    "IsInitialized",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (isInitProp != null)
                {
                    var setter = isInitProp.GetSetMethod(nonPublic: true);
                    if (setter != null)
                    {
                        _cachedIsInitializedSetter = setter;
                        harmony.Patch(setter, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_SetIsInitialized)));
                        MelonLogger.Msg("[KoGaMaPatch] Hooked static MVGameControllerBase.set_IsInitialized");
                    }
                }
                var onPostProp = controllerType.GetProperty(
                    "OnPostGameInit",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (onPostProp != null)
                {
                    var getter = onPostProp.GetGetMethod(nonPublic: true);
                    var setter = onPostProp.GetSetMethod(nonPublic: true);
                    if (getter != null) _cachedOnPostGameInitGetter = getter;
                    if (setter != null)
                    {
                        _cachedOnPostGameInitSetter = setter;
                        harmony.Patch(setter, prefix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Prefix_SetOnPostGameInit)));
                        MelonLogger.Msg("[KoGaMaPatch] Hooked static MVGameControllerBase.set_OnPostGameInit");
                    }
                }

                var privM = controllerType.GetMethod(
                    "OnJoinStateChanged",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (privM != null)
                {
                    harmony.Patch(privM, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_PrivateOnJoinStateChanged)));
                    MelonLogger.Msg("[KoGaMaPatch] Hooked private MVGameControllerBase.OnJoinStateChanged");
                }
                else
                {
                    MelonLogger.Warning("[KoGaMaPatch] Private MVGameControllerBase.OnJoinStateChanged NOT found.");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[KoGaMaPatch] TryHookAdvancedDiagnostics error: {ex.Message}");
            }
        }
        public static void Postfix_SetIsInitialized(object[] __args)
        {
            try
            {
                if (__args != null && __args.Length > 0)
                {
                    bool v = (bool)__args[0];
                    MelonLogger.Msg($"[KoGaMaPatch] MVGameControllerBase.set_IsInitialized({v})");
                    DriveLog($"set_IsInitialized({v})");
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"Postfix_SetIsInitialized error: {ex.Message}");
            }
        }
        public static void Prefix_SetOnPostGameInit(object[] __args)
        {
            try
            {
                if (__args != null && __args.Length > 0 && __args[0] != null)
                {
                    _cachedOnPostGameInitDelegate = __args[0];
                    MelonLogger.Msg($"[KoGaMaPatch] Captured OnPostGameInit delegate: type={_cachedOnPostGameInitDelegate.GetType().FullName}");
                    DriveLog($"Captured OnPostGameInit delegate: type={_cachedOnPostGameInitDelegate.GetType().FullName}");
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"Prefix_SetOnPostGameInit error: {ex.Message}");
            }
        }
        public static void Postfix_PrivateOnJoinStateChanged(object[] __args)
        {
            try
            {
                if (__args != null && __args.Length > 0 && __args[0] != null)
                {
                    int stateValue = (int)__args[0];
                    MelonLogger.Msg($"[KoGaMaPatch] MVGameControllerBase.OnJoinStateChanged(state={stateValue}) [private]");
                    DriveLog($"MVGameControllerBase.OnJoinStateChanged(state={stateValue}) [private]");
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"Postfix_PrivateOnJoinStateChanged error: {ex.Message}");
            }
        }
        private static void MaybeAttemptSceneLoadFallback()
        {
            if (_sceneLoadFallbackAttempted) return;
            if (_stuckAfterState4StartTick < 0) return;
            int ticksSinceStuck = _globalTick - _stuckAfterState4StartTick;
            const int STUCK_THRESHOLD_TICKS = 90;
            if (ticksSinceStuck < STUCK_THRESHOLD_TICKS) return;

            _sceneLoadFallbackAttempted = true;
            _sceneLoadFallbackFiredTick = _globalTick;
            MelonLogger.Msg("[KoGaMaPatch] Stuck at JoinState=4 for >1.5s  attempting scene-load fallback.");
            DriveLog($"SceneLoadFallback: triggered after {ticksSinceStuck} ticks.");

            bool loadScenesSucceeded = false;
            try
            {
                if (_cachedIsInitializedSetter != null)
                {
                    _cachedIsInitializedSetter.Invoke(null, new object[] { true });
                    MelonLogger.Msg("[KoGaMaPatch] SceneLoadFallback: set IsInitialized=true");
                    DriveLog("SceneLoadFallback: set IsInitialized=true");
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"SceneLoadFallback: set_IsInitialized threw: {ex.GetType().Name}: {ex.Message}");
            }

            try
            {
                if (_cachedOnPostGameInitDelegate != null)
                {
                    var delType = _cachedOnPostGameInitDelegate.GetType();
                    var invoke = delType.GetMethod("Invoke");
                    if (invoke != null)
                    {
                        invoke.Invoke(_cachedOnPostGameInitDelegate, null);
                        MelonLogger.Msg("[KoGaMaPatch] SceneLoadFallback: invoked OnPostGameInit");
                        DriveLog("SceneLoadFallback: invoked OnPostGameInit");
                    }
                }
            }
            catch (System.Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                DriveLog($"SceneLoadFallback: OnPostGameInit.Invoke threw: {inner.GetType().Name}: {inner.Message}");
            }

            try
            {
                System.Type controllerType =
                    FindTypeInAnyAssembly("Il2Cpp.MVGameControllerBase") ??
                    FindTypeInAnyAssembly("MVGameControllerBase");
                if (controllerType != null)
                {
                    var llProp = controllerType.GetProperty("LevelLoader", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (llProp != null)
                    {
                        var getter = llProp.GetGetMethod(nonPublic: true) ?? llProp.GetGetMethod();
                        if (getter != null)
                        {
                            _cachedLevelLoader = getter.Invoke(null, null);
                            if (_cachedLevelLoader != null)
                            {
                                var llType = _cachedLevelLoader.GetType();
                                DriveLog($"SceneLoadFallback: LevelLoader = {_cachedLevelLoader.GetType().FullName}");



                                try
                                {
                                    var allMethods = llType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    var sb = new System.Text.StringBuilder();
                                    sb.Append("LevelLoader methods:");
                                    foreach (var mm in allMethods)
                                    {
                                        sb.Append(' ');
                                        sb.Append(mm.ReturnType.Name);
                                        sb.Append(' ');
                                        sb.Append(mm.Name);
                                        sb.Append('(');
                                        var ps = mm.GetParameters();
                                        for (int i = 0; i < ps.Length; i++)
                                        {
                                            if (i > 0) sb.Append(',');
                                            sb.Append(ps[i].ParameterType.Name);
                                        }
                                        sb.Append(')');
                                    }
                                    DriveLog(sb.ToString());
                                }
                                catch (System.Exception dumpEx)
                                {
                                    DriveLog($"LevelLoader method dump failed: {dumpEx.GetType().Name}: {dumpEx.Message}");
                                }
                                System.Reflection.MethodInfo loadScenes = null;
                                var mvGameModeType =
                                    FindTypeInAnyAssembly("Il2CppMV.Common.MVGameMode") ??
                                    FindTypeInAnyAssembly("MV.Common.MVGameMode") ??
                                    FindTypeInAnyAssembly("Il2Cpp.MV.Common.MVGameMode") ??
                                    FindTypeInAnyAssembly("MVGameMode");
                                if (mvGameModeType == null)
                                {
                                    try
                                    {
                                        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                                        {
                                            Type[] tt;
                                            try { tt = asm.GetTypes(); }
                                            catch (System.Reflection.ReflectionTypeLoadException rtle) { tt = rtle.Types.Where(x => x != null).ToArray(); }
                                            catch { continue; }
                                            foreach (var t in tt)
                                            {
                                                if (t == null) continue;
                                                if (t.Name == "MVGameMode" && t.IsEnum)
                                                {
                                                    mvGameModeType = t;
                                                    break;
                                                }
                                            }
                                            if (mvGameModeType != null) break;
                                        }
                                    }
                                    catch (System.Exception scanEx)
                                    {
                                        DriveLog($"SceneLoadFallback: scan for MVGameMode failed: {scanEx.Message}");
                                    }
                                }
                                DriveLog($"SceneLoadFallback: MVGameMode type = {mvGameModeType?.FullName ?? "<not found>"}");
                                if (mvGameModeType != null)
                                {

                                    try
                                    {
                                        if (mvGameModeType.IsEnum)
                                        {
                                            var names = System.Enum.GetNames(mvGameModeType);
                                            var vals = System.Enum.GetValues(mvGameModeType);
                                            var sb = new System.Text.StringBuilder("MVGameMode values:");
                                            for (int i = 0; i < names.Length; i++)
                                                sb.Append($" {names[i]}={(int)vals.GetValue(i)}");
                                            DriveLog(sb.ToString());
                                        }
                                    }
                                    catch (System.Exception vex) { DriveLog($"MVGameMode dump: {vex.Message}"); }

                                    foreach (var m in llType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                                    {
                                        if (m.Name != "LoadScenes") continue;
                                        var ps = m.GetParameters();
                                        if (ps.Length != 4) continue;
                                        var p0t = ps[0].ParameterType;
                                        bool p0Ok = p0t != null && p0t.IsEnum &&
                                            (p0t.FullName == "MVGameMode" ||
                                             p0t.FullName == "MV.Common.MVGameMode" ||
                                             p0t.FullName == "Il2CppMV.Common.MVGameMode");
                                        if (!p0Ok) continue;
                                        if (ps[1].ParameterType.FullName != "System.Boolean") continue;
                                        if (ps[2].ParameterType.FullName != "System.Boolean") continue;
                                        var p3t = ps[3].ParameterType;
                                        bool p3Ok = p3t != null &&
                                            (p3t.FullName == "System.Action" ||
                                             p3t.FullName == "Il2CppSystem.Action");
                                        if (!p3Ok) continue;
                                        loadScenes = m;
                                        break;
                                    }
                                }
                                if (loadScenes != null)
                                {
                                    object chosenMode = null;
                                    try
                                    {
                                        if (mvGameModeType.IsEnum)
                                        {
                                            var names = System.Enum.GetNames(mvGameModeType);
                                            var vals = System.Enum.GetValues(mvGameModeType);

                                            for (int i = 0; i < names.Length; i++)
                                            {
                                                if (string.Equals(names[i], "Play", System.StringComparison.OrdinalIgnoreCase))
                                                {
                                                    chosenMode = vals.GetValue(i);
                                                    break;
                                                }
                                            }

                                            if (chosenMode == null)
                                            {
                                                for (int i = 0; i < names.Length; i++)
                                                {
                                                    if (names[i].IndexOf("Play", System.StringComparison.OrdinalIgnoreCase) >= 0)
                                                    {
                                                        chosenMode = vals.GetValue(i);
                                                        break;
                                                    }
                                                }
                                            }

                                            if (chosenMode == null)
                                            {
                                                for (int i = 0; i < names.Length; i++)
                                                {
                                                    if (!string.Equals(names[i], "None", System.StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        chosenMode = vals.GetValue(i);
                                                        break;
                                                    }
                                                }
                                            }

                                            if (chosenMode == null && names.Length > 0)
                                                chosenMode = vals.GetValue(0);
                                        }
                                    }
                                    catch (System.Exception pmodex)
                                    {
                                        DriveLog($"SceneLoadFallback: pick MVGameMode failed: {pmodex.Message}");
                                    }

                                    if (chosenMode != null)
                                    {
                                        DriveLog($"SceneLoadFallback: invoking LevelLoader.LoadScenes(mode={chosenMode}, tourist=false, useTouch=false, callback=null)");
                                        try
                                        {
                                            loadScenes.Invoke(_cachedLevelLoader, new object[] { chosenMode, false, false, null });
                                            MelonLogger.Msg($"[KoGaMaPatch] SceneLoadFallback: LevelLoader.LoadScenes(mode={chosenMode}, false, false, null) invoked");
                                            DriveLog($"SceneLoadFallback: LevelLoader.LoadScenes invoked successfully (mode={chosenMode})");
                                            loadScenesSucceeded = true;
                                        }
                                        catch (System.Exception invEx)
                                        {
                                            var inner = invEx.InnerException ?? invEx;
                                            DriveLog($"SceneLoadFallback: LoadScenes threw: {inner.GetType().Name}: {inner.Message}");
                                        }
                                    }
                                    else
                                    {
                                        DriveLog("SceneLoadFallback: could not resolve any MVGameMode value");
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        var sbDiag = new System.Text.StringBuilder();
                                        sbDiag.Append("SceneLoadFallback: LoadScenes overloads:");
                                        foreach (var m in llType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                                        {
                                            if (m.Name != "LoadScenes") continue;
                                            sbDiag.Append(' ');
                                            sbDiag.Append(m.ReturnType.Name);
                                            sbDiag.Append(' ');
                                            sbDiag.Append(m.Name);
                                            sbDiag.Append('(');
                                            var ps2 = m.GetParameters();
                                            for (int i = 0; i < ps2.Length; i++)
                                            {
                                                if (i > 0) sbDiag.Append(',');
                                                sbDiag.Append(ps2[i].ParameterType.FullName ?? ps2[i].ParameterType.Name);
                                            }
                                            sbDiag.Append(')');
                                        }
                                        DriveLog(sbDiag.ToString());
                                    }
                                    catch (System.Exception dEx)
                                    {
                                        DriveLog($"SceneLoadFallback: overload dump failed: {dEx.Message}");
                                    }
                                    DriveLog("SceneLoadFallback: LoadScenes(MVGameMode, bool, bool, Action) not found on LevelLoader");
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"SceneLoadFallback: LevelLoader section threw: {ex.GetType().Name}: {ex.Message}");
            }
            try
            {

                if (_cachedLoadingScreenHandler == null)
                {
                    _cachedLoadingScreenHandler = FindLoadingScreenHandlerInstance();
                    if (_cachedLoadingScreenHandler != null)
                    {
                        DriveLog($"SceneLoadFallback: LSH instance resolved on demand: {_cachedLoadingScreenHandler.GetType().FullName}");
                    }
                }
                if (_cachedLoadingScreenHandler != null)
                {
                    var lshT = _cachedLoadingScreenHandler.GetType();

                    string[] propNames = { "currentEventCount", "eventsCount", "targetProgress", "hasCapturedSessionData" };
                    System.Reflection.PropertyInfo curP = null, totP = null, tgtP = null, sessP = null;
                    foreach (var p in lshT.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (p.Name == "currentEventCount") curP = p;
                        else if (p.Name == "eventsCount") totP = p;
                        else if (p.Name == "targetProgress") tgtP = p;
                        else if (p.Name == "hasCapturedSessionData") sessP = p;
                    }
                    DriveLog($"SceneLoadFallback: LSH property lookup curP={(curP?.Name ?? "null")} totP={(totP?.Name ?? "null")} tgtP={(tgtP?.Name ?? "null")} sessP={(sessP?.Name ?? "null")}");

                    if (curP != null && curP.CanWrite && totP != null && totP.CanRead)
                    {
                        try
                        {
                            int total = (int)totP.GetValue(_cachedLoadingScreenHandler);
                            curP.SetValue(_cachedLoadingScreenHandler, total);
                            DriveLog($"SceneLoadFallback: LSH.currentEventCount = {total}/{total} (FORCED COMPLETE via property)");
                        }
                        catch (System.Exception setCurEx)
                        {
                            DriveLog($"SceneLoadFallback: set currentEventCount via property threw: {setCurEx.Message}");
                        }
                    }

                    if (tgtP != null && tgtP.CanWrite)
                    {
                        try
                        {
                            tgtP.SetValue(_cachedLoadingScreenHandler, 1.0f);
                            DriveLog("SceneLoadFallback: LSH.targetProgress forced to 1.0 via property");
                        }
                        catch (System.Exception tgtEx)
                        {
                            DriveLog($"SceneLoadFallback: set targetProgress via property threw: {tgtEx.Message}");
                        }
                    }


                    if (sessP != null && sessP.CanWrite)
                    {
                        try
                        {
                            sessP.SetValue(_cachedLoadingScreenHandler, true);
                        }
                        catch { }
                    }
                    try
                    {

                        var canvasGroupP = lshT.GetProperty("centerCanvasGroup",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (canvasGroupP != null && canvasGroupP.CanRead)
                        {
                            object cg = null;
                            try { cg = canvasGroupP.GetValue(_cachedLoadingScreenHandler); }
                            catch (System.Exception cgx) { DriveLog($"SceneLoadFallback: get centerCanvasGroup threw: {cgx.GetType().Name}: {cgx.Message}"); }
                            if (cg != null)
                            {
                                try
                                {
                                    var alphaP = cg.GetType().GetProperty("alpha",
                                        BindingFlags.Public | BindingFlags.Instance);
                                    if (alphaP != null && alphaP.CanWrite)
                                    {
                                        alphaP.SetValue(cg, 0.0f);
                                        DriveLog("SceneLoadFallback: LSH centerCanvasGroup.alpha forced to 0");
                                    }
                                    else
                                    {
                                        DriveLog($"SceneLoadFallback: CanvasGroup.alpha prop not writeable (alphaP={alphaP?.Name ?? "null"}, canWrite={alphaP?.CanWrite})");
                                    }
                                }
                                catch (System.Exception aex) { DriveLog($"SceneLoadFallback: set centerCanvasGroup.alpha threw: {aex.GetType().Name}: {aex.Message}"); }
                            }
                            else
                            {
                                DriveLog("SceneLoadFallback: centerCanvasGroup is null, skipping alpha hide");
                            }
                        }
                        else
                        {
                            DriveLog($"SceneLoadFallback: centerCanvasGroup prop not found (prop={canvasGroupP?.Name ?? "null"}, canRead={canvasGroupP?.CanRead})");
                        }
                    }
                    catch (System.Exception hideCgEx) { DriveLog($"SceneLoadFallback: outer hide centerCanvasGroup threw: {hideCgEx.GetType().Name}: {hideCgEx.Message}"); }

                    try
                    {

                        var barCoP = lshT.GetProperty("loadingBarCoroutine",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (barCoP != null && barCoP.CanRead && barCoP.CanWrite)
                        {
                            object co = null;
                            try { co = barCoP.GetValue(_cachedLoadingScreenHandler); }
                            catch (System.Exception bcx) { DriveLog($"SceneLoadFallback: get loadingBarCoroutine threw: {bcx.GetType().Name}: {bcx.Message}"); }
                            if (co != null)
                            {
                                try
                                {

                                    var stopCoM = lshT.GetMethod("StopCoroutine",
                                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                                        binder: null,
                                        types: new[] { co.GetType() },
                                        modifiers: null);
                                    if (stopCoM != null)
                                    {
                                        stopCoM.Invoke(_cachedLoadingScreenHandler, new object[] { co });
                                        DriveLog("SceneLoadFallback: LSH loadingBarCoroutine stopped");
                                    }
                                    else
                                    {
                                        DriveLog($"SceneLoadFallback: StopCoroutine({co.GetType().Name}) not found");
                                    }
                                }
                                catch (System.Exception stopEx)
                                {
                                    DriveLog($"SceneLoadFallback: stop coroutine failed: {stopEx.GetType().Name}: {stopEx.Message}");
                                }
                            }
                            else
                            {
                                DriveLog("SceneLoadFallback: loadingBarCoroutine is null, skipping stop");
                            }
                        }
                        else
                        {
                            DriveLog($"SceneLoadFallback: loadingBarCoroutine prop not found or not writeable (prop={barCoP?.Name ?? "null"})");
                        }
                    }
                    catch (System.Exception hideCoEx) { DriveLog($"SceneLoadFallback: outer hide coroutine threw: {hideCoEx.GetType().Name}: {hideCoEx.Message}"); }

                    try
                    {
                        var goP = lshT.GetProperty("gameObject",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (goP != null && goP.CanRead)
                        {
                            object go = null;
                            try { go = goP.GetValue(_cachedLoadingScreenHandler); }
                            catch (System.Exception gox) { DriveLog($"SceneLoadFallback: get gameObject threw: {gox.GetType().Name}: {gox.Message}"); }
                            if (go != null)
                            {
                                try
                                {
                                    var setActiveM = go.GetType().GetMethod("SetActive",
                                        BindingFlags.Public | BindingFlags.Instance,
                                        binder: null,
                                        types: new[] { typeof(bool) },
                                        modifiers: null);
                                    if (setActiveM != null)
                                    {
                                        setActiveM.Invoke(go, new object[] { false });
                                        DriveLog("SceneLoadFallback: LSH gameObject SetActive(false)  loading screen hidden");
                                    }
                                    else
                                    {
                                        DriveLog("SceneLoadFallback: GameObject.SetActive(bool) not found");
                                    }
                                }
                                catch (System.Exception goEx)
                                {
                                    DriveLog($"SceneLoadFallback: SetActive failed: {goEx.GetType().Name}: {goEx.Message}");
                                }
                            }
                            else
                            {
                                DriveLog("SceneLoadFallback: LSH gameObject is null, can't SetActive");
                            }
                        }
                        else
                        {
                            DriveLog($"SceneLoadFallback: LSH gameObject prop not found (prop={goP?.Name ?? "null"}, canRead={goP?.CanRead})");
                        }
                    }
                    catch (System.Exception hideGoEx) { DriveLog($"SceneLoadFallback: outer hide gameObject threw: {hideGoEx.GetType().Name}: {hideGoEx.Message}"); }
                    try
                    {
                        var onJoin = lshT.GetMethod("OnJoinStateChanged",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (onJoin != null)
                        {



                            var ps = onJoin.GetParameters();
                            if (ps.Length == 1)
                            {

                                object arg4 = System.Enum.ToObject(ps[0].ParameterType, 4);
                                onJoin.Invoke(_cachedLoadingScreenHandler, new object[] { arg4 });
                                DriveLog("SceneLoadFallback: re-fired LSH.OnJoinStateChanged(4) to trigger progress completion");
                            }
                        }
                    }
                    catch (System.Exception refireEx)
                    {
                        DriveLog($"SceneLoadFallback: re-fire OnJoinStateChanged failed: {refireEx.GetType().Name}: {refireEx.Message}");
                    }
                }
                else
                {
                    DriveLog("SceneLoadFallback: _cachedLoadingScreenHandler is null, can't force-complete LSH");
                }
            }
            catch (System.Exception lshEx)
            {
                DriveLog($"SceneLoadFallback: LSH force-complete threw: {lshEx.GetType().Name}: {lshEx.Message}");
            }
            if (loadScenesSucceeded)
            {
                DriveLog("SceneLoadFallback: skipping SceneManager.LoadScene  LevelLoader.LoadScenes already succeeded, letting its own async load finish naturally.");
            }
            else
            try
            {
                try
                {
                    var smType = typeof(UnityEngine.SceneManagement.SceneManager);
                    var sceneCount = smType.GetProperty("sceneCount", BindingFlags.Public | BindingFlags.Static);
                    var getSceneAt = smType.GetMethod("GetSceneAt", BindingFlags.Public | BindingFlags.Static);
                    if (sceneCount != null && getSceneAt != null)
                    {
                        int n = (int)sceneCount.GetValue(null);
                        var sb = new System.Text.StringBuilder();
                        sb.Append("Loaded scenes:");
                        for (int i = 0; i < n; i++)
                        {
                            var s = getSceneAt.Invoke(null, new object[] { i });
                            if (s == null) { sb.Append($" [#{i}=null]"); continue; }
                            var nameProp = s.GetType().GetProperty("name");
                            var pathProp = s.GetType().GetProperty("path");
                            string nm = nameProp != null ? (nameProp.GetValue(s) as string ?? "?") : "?";
                            string pa = pathProp != null ? (pathProp.GetValue(s) as string ?? "?") : "?";
                            sb.Append($" [#{i}={nm} ({pa})]");
                        }
                        DriveLog(sb.ToString());
                    }
                }
                catch (System.Exception ex)
                {
                    DriveLog($"SceneLoadFallback: scene enumeration failed: {ex.GetType().Name}: {ex.Message}");
                }

                var smType2 = typeof(UnityEngine.SceneManagement.SceneManager);
                var loadSceneMethod = smType2.GetMethod(
                    "LoadScene",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(string) },
                    modifiers: null);
                if (loadSceneMethod != null)
                {
                    string[] candidates = {
                        "DesktopPlayModeGUI",
                        "DesktopBase",
                        "MainMenu", "Menu", "Login", "Loader", "LobbyScene",
                        "Desktop", "BootstrapScene", "BootScene",
                        "GameScene", "Level", "World", "LoaderScene",
                        "MenuScene", "MainMenuScene", "StartupScene",
                    };
                    foreach (var name in candidates)
                    {
                        try
                        {
                            loadSceneMethod.Invoke(null, new object[] { name });
                            MelonLogger.Msg($"[KoGaMaPatch] SceneLoadFallback: SceneManager.LoadScene(\"{name}\") invoked");
                            DriveLog($"SceneLoadFallback: SceneManager.LoadScene(\"{name}\") invoked");
                            break;
                        }
                        catch (System.Exception)
                        {

                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"SceneLoadFallback: SceneManager.LoadScene threw: {ex.GetType().Name}: {ex.Message}");
            }
            _bruteForceHideAttemptCount = 0;
            _bruteForceHideLastRunTick = -1;
            try { TryBruteForceHideLoadingScreen("immediate"); } catch { }
            try { TryCreatePlaceholderLevel("scene-fallback"); } catch (System.Exception plex) { DriveLog($"TryCreatePlaceholderLevel threw: {plex.GetType().Name}: {plex.Message}"); }
        }
        private static void TryBruteForceHideLoadingScreen(string reason)
        {
            try
            {
                if (_bruteForceHideAttemptCount >= BruteForceHideMaxAttempts)
                {
                    return;
                }
                _bruteForceHideAttemptCount++;
                DriveLog($"BruteForceHide[{reason}] attempt #{_bruteForceHideAttemptCount}/{BruteForceHideMaxAttempts}");

                int disabled = 0;
                int scanned = 0;
                try
                {
                    var smType = typeof(UnityEngine.SceneManagement.SceneManager);
                    var sceneCount = smType.GetProperty("sceneCount", BindingFlags.Public | BindingFlags.Static);
                    var getSceneAt = smType.GetMethod("GetSceneAt", BindingFlags.Public | BindingFlags.Static);
                    if (sceneCount != null && getSceneAt != null)
                    {
                        int n = (int)sceneCount.GetValue(null);
                        DriveLog($"BruteForceHide[{reason}]: sceneCount={n}");
                        try
                        {
                            var getActiveSceneM = smType.GetMethod("GetActiveScene", BindingFlags.Public | BindingFlags.Static, null, System.Type.EmptyTypes, null);
                            if (getActiveSceneM != null)
                            {
                                var activeS = getActiveSceneM.Invoke(null, null);
                                if (activeS != null)
                                {
                                    var asT = activeS.GetType();
                                    var an = asT.GetProperty("name")?.GetValue(activeS) as string ?? "?";
                                    var ap = asT.GetProperty("path")?.GetValue(activeS) as string ?? "?";
                                    var ai = (int)(asT.GetProperty("buildIndex")?.GetValue(activeS) ?? -1);
                                    var al = (bool)(asT.GetProperty("isLoaded")?.GetValue(activeS) ?? false);
                                    DriveLog($"BruteForceHide[{reason}]: ACTIVE scene = '{an}' (buildIdx={ai}, isLoaded={al}, path='{ap}')");
                                }
                            }
                        }
                        catch (System.Exception asx)
                        {
                            DriveLog($"BruteForceHide[{reason}]: GetActiveScene threw: {asx.GetType().Name}: {asx.Message}");
                        }

                        for (int i = 0; i < n; i++)
                        {
                            var s = getSceneAt.Invoke(null, new object[] { i });
                            if (s == null) { DriveLog($"BruteForceHide[{reason}]: scene #{i} is null"); continue; }
                            var sType = s.GetType();
                            var nameP = sType.GetProperty("name");
                            var pathP = sType.GetProperty("path");
                            var loadedP = sType.GetProperty("isLoaded");
                            var buildIdxP = sType.GetProperty("buildIndex");
                            string sname = nameP != null ? (nameP.GetValue(s) as string ?? "?") : "?";
                            string spath = pathP != null ? (pathP.GetValue(s) as string ?? "?") : "?";
                            bool sIsLoaded = loadedP != null && (bool)loadedP.GetValue(s);
                            int sBuildIdx = buildIdxP != null ? (int)buildIdxP.GetValue(s) : -999;
                            DriveLog($"BruteForceHide[{reason}]: scene [{i}] '{sname}' (buildIdx={sBuildIdx}, isLoaded={sIsLoaded}, path='{spath}')");
                            var rootsMethod = sType.GetMethod("GetRootGameObjects", BindingFlags.Public | BindingFlags.Instance, null, System.Type.EmptyTypes, null);
                            if (rootsMethod == null)
                            {
                                DriveLog($"BruteForceHide[{reason}]: scene '{sname}' has no GetRootGameObjects() method");
                                continue;
                            }
                            object rootsObj = null;
                            try { rootsObj = rootsMethod.Invoke(s, null); }
                            catch (System.Exception rex)
                            {
                                DriveLog($"BruteForceHide[{reason}]: GetRootGameObjects() on '{sname}' threw: {rex.GetType().Name}: {rex.InnerException?.Message ?? rex.Message}");
                                continue;
                            }
                            if (rootsObj == null)
                            {
                                DriveLog($"BruteForceHide[{reason}]: scene '{sname}' roots is null");
                                continue;
                            }
                            int rootCount = GetArrayLength(rootsObj);
                            DriveLog($"BruteForceHide[{reason}]: scene '{sname}' has {rootCount} root GameObject(s) (type={rootsObj.GetType().FullName})");
                            for (int j = 0; j < rootCount; j++)
                            {
                                object go = GetArrayElement(rootsObj, j);
                                if (go == null) continue;
                                try
                                {
                                    var rnp = go.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                                    string rn = rnp != null ? (rnp.GetValue(go) as string ?? "?") : "?";
                                    var rap = go.GetType().GetProperty("activeSelf", BindingFlags.Public | BindingFlags.Instance);
                                    bool rootActive = rap != null && (bool)rap.GetValue(go);
                                    int rch = CountGameObjectChildren(go);
                                    DriveLog($"BruteForceHide[{reason}]:   ROOT [{j}] '{rn}' (active={rootActive}, children={rch})");
                                    if (rch >= 0 && rch <= 25)
                                    {
                                        LogGameObjectChildren(go, reason, depth: 0, maxDepth: 2);
                                    }
                                    TryForceActivatePlayModeUI(go, reason);
                                }
                                catch (System.Exception rnx)
                                {
                                    DriveLog($"BruteForceHide[{reason}]:   ROOT [{j}] name lookup failed: {rnx.GetType().Name}");
                                }
                                int found = ScanAndDisableLoadingTree(go, ref scanned);
                                disabled += found;
                            }
                        }
                    }
                }
                catch (System.Exception sx)
                {
                    DriveLog($"BruteForceHide[{reason}]: scene scan threw: {sx.GetType().Name}: {sx.Message}");
                }
                try
                {
                    var lshType = FindTypeInAnyAssembly("Il2Cpp.LoadingScreenHandler") ?? FindTypeInAnyAssembly("LoadingScreenHandler");
                    if (lshType != null)
                    {
                        var arr = FindObjectsOfTypeAllByReflection(lshType);
                        if (arr != null)
                        {
                            int len = GetArrayLength(arr);
                            DriveLog($"BruteForceHide[{reason}]: LSH scan found {len} candidates (type={arr.GetType().Name})");
                            for (int i = 0; i < len; i++)
                            {
                                var inst = GetArrayElement(arr, i);
                                if (inst == null) continue;
                                int found = DisableGameObjectViaComponent(inst, "LSH");
                                disabled += found;
                            }
                        }
                    }
                }
                catch (System.Exception lshx)
                {
                    DriveLog($"BruteForceHide[{reason}]: LSH scan threw: {lshx.GetType().Name}: {lshx.Message}");
                }

                try
                {
                    var lsbType = FindTypeInAnyAssembly("Il2Cpp.LoadingScreenBackground") ?? FindTypeInAnyAssembly("LoadingScreenBackground");
                    if (lsbType != null)
                    {
                        var arr = FindObjectsOfTypeAllByReflection(lsbType);
                        if (arr != null)
                        {
                            int len = GetArrayLength(arr);
                            DriveLog($"BruteForceHide[{reason}]: LSB scan found {len} candidates (type={arr.GetType().Name})");
                            for (int i = 0; i < len; i++)
                            {
                                var inst = GetArrayElement(arr, i);
                                if (inst == null) continue;
                                int found = DisableGameObjectViaComponent(inst, "LSB");
                                disabled += found;
                            }
                        }
                    }
                }
                catch (System.Exception lsbx)
                {
                    DriveLog($"BruteForceHide[{reason}]: LSB scan threw: {lsbx.GetType().Name}: {lsbx.Message}");
                }
                try
                {


                    System.Type canvasType = null;
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            var t = asm.GetType("UnityEngine.Canvas");
                            if (t != null) { canvasType = t; break; }
                        }
                        catch { }
                    }
                    if (canvasType != null)
                    {
                        var arr = FindObjectsOfTypeAllByReflection(canvasType);
                        if (arr != null)
                        {
                            int len = GetArrayLength(arr);
                            int canvasDisabled = 0;
                            for (int i = 0; i < len; i++)
                            {
                                var canvas = GetArrayElement(arr, i);
                                if (canvas == null) continue;
                                try
                                {
                                    var goP = canvas.GetType().GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
                                    object go = goP != null ? goP.GetValue(canvas) : null;
                                    if (go == null) continue;
                                    var nameP = go.GetType().GetProperty("name");
                                    string cname = nameP != null ? (nameP.GetValue(go) as string ?? "?") : "?";

                                    string cn = cname.ToLowerInvariant();
                                    bool matches = cn.Contains("loading") || cn.Contains("wait") || cn.Contains("spinner");
                                    if (!matches)
                                    {
                                        try
                                        {
                                            var getCompInChildrenM = go.GetType().GetMethod("GetComponentInChildren", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(System.Type) }, null);
                                            if (getCompInChildrenM != null)
                                            {
                                                var lshType = FindTypeInAnyAssembly("Il2Cpp.LoadingScreenHandler") ?? FindTypeInAnyAssembly("LoadingScreenHandler");
                                                if (lshType != null)
                                                {
                                                    var comp = getCompInChildrenM.Invoke(go, new object[] { lshType });
                                                    if (comp != null) matches = true;
                                                }
                                                if (!matches)
                                                {
                                                    var lsbType = FindTypeInAnyAssembly("Il2Cpp.LoadingScreenBackground") ?? FindTypeInAnyAssembly("LoadingScreenBackground");
                                                    if (lsbType != null)
                                                    {
                                                        var comp = getCompInChildrenM.Invoke(go, new object[] { lsbType });
                                                        if (comp != null) matches = true;
                                                    }
                                                }
                                            }
                                        }
                                        catch { }
                                    }

                                    if (matches)
                                    {
                                        if (DisableGameObject(go, $"canvas match (name='{cname}')"))
                                        {
                                            canvasDisabled++;
                                        }
                                    }
                                }
                                catch
                                {

                                }
                            }
                            DriveLog($"BruteForceHide[{reason}]: Canvas scan: {len} canvases, {canvasDisabled} disabled");
                            disabled += canvasDisabled;
                        }
                    }
                }
                catch (System.Exception cvsx)
                {
                    DriveLog($"BruteForceHide[{reason}]: Canvas scan threw: {cvsx.GetType().Name}: {cvsx.Message}");
                }

                DriveLog($"BruteForceHide[{reason}] done: scanned={scanned} disabled={disabled}");
            }
            catch (System.Exception ex)
            {
                DriveLog($"BruteForceHide[{reason}] OUTER error: {ex.GetType().Name}: {ex.Message}");
            }
        }
        private static int ScanAndDisableLoadingTree(object go, ref int scanned)
        {
            int disabled = 0;
            try
            {
                if (go == null) return 0;
                scanned++;

                string name = null;
                try
                {
                    var nameP = go.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                    if (nameP != null) name = nameP.GetValue(go) as string;
                }
                catch { }

                bool looksLikeLoading = false;
                if (!string.IsNullOrEmpty(name))
                {
                    string n = name.ToLowerInvariant();
                    if (n.Contains("loading") || n.Contains("wait") || n.Contains("spinner"))
                    {
                        looksLikeLoading = true;
                    }
                }
                if (!looksLikeLoading)
                {
                    try
                    {
                        var getComponentsMethod = go.GetType().GetMethod("GetComponents", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(System.Type) }, null);
                        if (getComponentsMethod != null)
                        {
                            var lshType = FindTypeInAnyAssembly("Il2Cpp.LoadingScreenHandler") ?? FindTypeInAnyAssembly("LoadingScreenHandler");
                            var lsbType = FindTypeInAnyAssembly("Il2Cpp.LoadingScreenBackground") ?? FindTypeInAnyAssembly("LoadingScreenBackground");
                            if (lshType != null)
                            {
                                var comps = getComponentsMethod.Invoke(go, new object[] { lshType }) as System.Array;
                                if (comps != null && comps.Length > 0) looksLikeLoading = true;
                            }
                            if (!looksLikeLoading && lsbType != null)
                            {
                                var comps = getComponentsMethod.Invoke(go, new object[] { lsbType }) as System.Array;
                                if (comps != null && comps.Length > 0) looksLikeLoading = true;
                            }
                        }
                    }
                    catch { }
                }
                if (looksLikeLoading)
                {

                    object root = go;
                    try
                    {
                        var transformP = go.GetType().GetProperty("transform", BindingFlags.Public | BindingFlags.Instance);
                        if (transformP != null)
                        {
                            object t = transformP.GetValue(go);
                            while (t != null)
                            {
                                var parentP = t.GetType().GetProperty("parent", BindingFlags.Public | BindingFlags.Instance);
                                object parent = parentP != null ? parentP.GetValue(t) : null;
                                if (parent == null) break;
                                var parentGoP = parent.GetType().GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
                                object parentGo = parentGoP != null ? parentGoP.GetValue(parent) : null;
                                if (parentGo == null) break;
                                root = parentGo;
                                t = parent;
                            }
                        }
                    }
                    catch { }

                    if (DisableGameObject(root, $"tree match (name='{name}')"))
                    {
                        disabled++;
                    }
                }
                try
                {
                    var transformP = go.GetType().GetProperty("transform", BindingFlags.Public | BindingFlags.Instance);
                    if (transformP != null)
                    {
                        object t = transformP.GetValue(go);
                        if (t != null)
                        {
                            var childCountP = t.GetType().GetProperty("childCount", BindingFlags.Public | BindingFlags.Instance);
                            var getChildM = t.GetType().GetMethod("GetChild", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                            if (childCountP != null && getChildM != null)
                            {
                                int cc = (int)childCountP.GetValue(t);
                                for (int c = 0; c < cc; c++)
                                {
                                    object child = getChildM.Invoke(t, new object[] { c });
                                    if (child == null) continue;
                                    var childGoP = child.GetType().GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
                                    object childGo = childGoP != null ? childGoP.GetValue(child) : null;
                                    if (childGo != null)
                                    {
                                        disabled += ScanAndDisableLoadingTree(childGo, ref scanned);
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            catch (System.Exception ex)
            {
                DriveLog($"ScanAndDisableLoadingTree: {ex.GetType().Name}: {ex.Message}");
            }
            return disabled;
        }
        private static int DisableGameObjectViaComponent(object component, string label)
        {
            try
            {
                if (component == null) return 0;
                var goP = component.GetType().GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
                if (goP == null) return 0;
                try
                {
                    object go = goP.GetValue(component);
                    if (go != null)
                    {
                        if (DisableGameObject(go, $"{label} component"))
                        {
                            return 1;
                        }
                    }
                }
                catch (System.Exception gex)
                {
                    DriveLog($"DisableGameObjectViaComponent[{label}]: gameObject getter threw: {gex.InnerException?.Message ?? gex.Message}");
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"DisableGameObjectViaComponent[{label}] OUTER: {ex.GetType().Name}: {ex.Message}");
            }
            return 0;
        }
        private static bool DisableGameObject(object go, string label)
        {
            try
            {
                if (go == null) return false;
                var setActiveM = go.GetType().GetMethod("SetActive", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
                if (setActiveM == null)
                {
                    DriveLog($"DisableGameObject[{label}]: SetActive(bool) not found");
                    return false;
                }
                setActiveM.Invoke(go, new object[] { false });
                DriveLog($"DisableGameObject[{label}]: SetActive(false) OK");
                return true;
            }
            catch (System.Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                DriveLog($"DisableGameObject[{label}]: SetActive threw: {inner.GetType().Name}: {inner.Message}");
                return false;
            }
        }
        private static System.Array FindObjectsOfTypeAllByReflection(System.Type t)
        {
            try
            {
                if (t == null) return null;
                var resourcesType = typeof(UnityEngine.Resources);
                var methods = resourcesType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (var m in methods)
                {
                    if (m.Name != "FindObjectsOfTypeAll") continue;
                    if (!m.IsGenericMethodDefinition) continue;
                    if (m.GetParameters().Length != 0) continue;
                    try
                    {
                        var generic = m.MakeGenericMethod(t);
                        return generic.Invoke(null, null) as System.Array;
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }
        private static int GetArrayLength(object arr)
        {
            if (arr == null) return 0;
            if (arr is System.Array sysArr) return sysArr.Length;
            try
            {
                var t = arr.GetType();
                var lenP = t.GetProperty("Length", BindingFlags.Public | BindingFlags.Instance);
                if (lenP != null) return (int)lenP.GetValue(arr);
                var countP = t.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                if (countP != null) return (int)countP.GetValue(arr);
            }
            catch { }
            return 0;
        }
        private static object GetArrayElement(object arr, int index)
        {
            if (arr == null) return null;
            if (arr is System.Array sysArr)
            {
                if (index < 0 || index >= sysArr.Length) return null;
                return sysArr.GetValue(index);
            }
            try
            {
                var t = arr.GetType();
                var indexerP = t.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance, null, typeof(object), new[] { typeof(int) }, null);
                if (indexerP != null) return indexerP.GetValue(arr, new object[] { index });
            }
            catch { }
            try
            {

                var ie = arr as System.Collections.IEnumerable;
                if (ie != null)
                {
                    int i = 0;
                    foreach (var item in ie)
                    {
                        if (i == index) return item;
                        i++;
                        if (i > index) break;
                    }
                }
            }
            catch { }
            return null;
        }
        private static int CountGameObjectChildren(object go)
        {
            if (go == null) return -1;
            try
            {
                var tP = go.GetType().GetProperty("transform", BindingFlags.Public | BindingFlags.Instance);
                object tr = tP != null ? tP.GetValue(go) : null;
                if (tr == null) return -1;
                var ccP = tr.GetType().GetProperty("childCount", BindingFlags.Public | BindingFlags.Instance);
                if (ccP == null) return -1;
                return (int)ccP.GetValue(tr);
            }
            catch { return -1; }
        }
        private static int TryForceActivatePlayModeUI(object go, string reason)
        {
            int activated = 0;
            try
            {
                if (go == null) return 0;
                var t = go.GetType();

                var nameP = t.GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                string name = nameP != null ? (nameP.GetValue(go) as string ?? "") : "";
                string lower = name.ToLowerInvariant();
                bool isPlayUi = (lower.Contains("playmode") || lower.Contains("ingameui") || lower.Contains("playmodeui"))
                 && !lower.Contains("loading")
                 && !lower.Contains("boot")
                 && !lower.Contains("splash");
                if (isPlayUi)
                {
                    var activeP = t.GetProperty("activeSelf", BindingFlags.Public | BindingFlags.Instance);
                    bool active = activeP != null && (bool)activeP.GetValue(go);
                    if (!active)
                    {
                        var setActiveM = t.GetMethod("SetActive", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
                        if (setActiveM != null)
                        {
                            try
                            {
                                setActiveM.Invoke(go, new object[] { true });
                                DriveLog($"BruteForceHide[{reason}]: FORCE-ACTIVATED '{name}' (was inactive)");
                                activated++;
                            }
                            catch (System.Exception sex)
                            {
                                DriveLog($"BruteForceHide[{reason}]: SetActive({name}) threw: {sex.GetType().Name}: {sex.InnerException?.Message ?? sex.Message}");
                            }
                        }
                    }
                }

                var tP = t.GetProperty("transform", BindingFlags.Public | BindingFlags.Instance);
                object tr = tP != null ? tP.GetValue(go) : null;
                if (tr != null)
                {
                    var getChildM = tr.GetType().GetMethod("GetChild", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                    var ccP = tr.GetType().GetProperty("childCount", BindingFlags.Public | BindingFlags.Instance);
                    if (getChildM != null && ccP != null)
                    {
                        int cc = (int)ccP.GetValue(tr);
                        for (int i = 0; i < cc; i++)
                        {
                            object child = getChildM.Invoke(tr, new object[] { i });
                            if (child == null) continue;
                            var goP = child.GetType().GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
                            object cgo = goP != null ? goP.GetValue(child) : null;
                            if (cgo != null)
                            {
                                activated += TryForceActivatePlayModeUI(cgo, reason);
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"BruteForceHide[{reason}]: TryForceActivatePlayModeUI threw on '{go}': {ex.GetType().Name}: {ex.InnerException?.Message ?? ex.Message}");
            }
            return activated;
        }        private static bool _placeholderLevelCreated = false;
        private static int _placeholderLevelRetryCount = 0;
        private static string _placeholderLevelLastActiveScene = "";
        private static void TryCreatePlaceholderLevel(string reason)
        {
            try
            {
                var smType2 = typeof(UnityEngine.SceneManagement.SceneManager);
                var getActiveSceneM2 = smType2.GetMethod("GetActiveScene", BindingFlags.Public | BindingFlags.Static, null, System.Type.EmptyTypes, null);
                if (getActiveSceneM2 != null)
                {
                    object activeScene2 = getActiveSceneM2.Invoke(null, null);
                    if (activeScene2 != null)
                    {
                        var asT2 = activeScene2.GetType();
                        var an2 = asT2.GetProperty("name")?.GetValue(activeScene2) as string ?? "";
                        if (!string.IsNullOrEmpty(an2) && an2 != _placeholderLevelLastActiveScene)
                        {

                            if (_placeholderLevelCreated)
                            {
                                DriveLog($"PlaceholderLevel[{reason}]: active scene changed '{_placeholderLevelLastActiveScene}'→'{an2}', allowing retry (old objects were destroyed with old scene)");
                            }
                            _placeholderLevelCreated = false;
                            _placeholderLevelLastActiveScene = an2;
                        }
                    }
                }
            }
            catch { }
            if (_placeholderLevelCreated) return;
            if (_placeholderLevelRetryCount >= 16) return;
            _placeholderLevelRetryCount++;
            try
            {
                _placeholderLevelCreated = true;
                DriveLog($"PlaceholderLevel[{reason}]: starting (retry#{_placeholderLevelRetryCount})");
                System.Type cameraType = FindTypeInAnyAssembly("UnityEngine.Camera");
                System.Type lightType = FindTypeInAnyAssembly("UnityEngine.Light");
                System.Type primitiveTypeEnum = FindTypeInAnyAssembly("UnityEngine.PrimitiveType");
                System.Type gameObjectType = FindTypeInAnyAssembly("UnityEngine.GameObject");
                if (cameraType == null) { DriveLog("PlaceholderLevel: Camera type not found"); return; }
                if (gameObjectType == null) { DriveLog("PlaceholderLevel: GameObject type not found"); return; }
                if (primitiveTypeEnum == null) { DriveLog("PlaceholderLevel: PrimitiveType enum not found"); return; }
                var smType = typeof(UnityEngine.SceneManagement.SceneManager);
                var getActiveSceneM = smType.GetMethod("GetActiveScene", BindingFlags.Public | BindingFlags.Static, null, System.Type.EmptyTypes, null);
                if (getActiveSceneM == null) { DriveLog("PlaceholderLevel: GetActiveScene not found"); return; }
                object activeScene = getActiveSceneM.Invoke(null, null);
                if (activeScene == null) { DriveLog("PlaceholderLevel: active scene is null"); return; }

                System.Func<string, object> createGO = (name) =>
                {
                    try
                    {
                        var ctor = gameObjectType.GetConstructor(new[] { typeof(string) });
                        if (ctor == null) return null;
                        return ctor.Invoke(new object[] { name });
                    }
                    catch (System.Exception gex) { DriveLog($"PlaceholderLevel: GameObject ctor({name}) failed: {gex.GetType().Name}"); return null; }
                };
                System.Reflection.MethodInfo _genericAddCompDef = null;
                System.Func<object, System.Type, object> addCompGeneric = (targetGO, compType) =>
                {
                    try
                    {
                        if (_genericAddCompDef == null)
                        {
                            foreach (var m in gameObjectType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                            {
                                if (m.Name == "AddComponent" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1)
                                {
                                    _genericAddCompDef = m;
                                    break;
                                }
                            }
                            if (_genericAddCompDef == null)
                            {
                                DriveLog("PlaceholderLevel: no generic AddComponent<T>() found on GameObject");
                                return null;
                            }
                        }
                        var closed = _genericAddCompDef.MakeGenericMethod(compType);
                        return closed.Invoke(targetGO, null);
                    }
                    catch (System.Exception ax) { DriveLog($"PlaceholderLevel: generic AddComponent<{compType?.Name}> failed: {ax.GetType().Name}"); return null; }
                };

                System.Action<object, float, float, float> setPos = (target, x, y, z) =>
                {
                    try
                    {
                        var trP = gameObjectType.GetProperty("transform", BindingFlags.Public | BindingFlags.Instance);
                        object tr = trP != null ? trP.GetValue(target) : null;
                        if (tr == null) return;
                        var tP = FindTypeInAnyAssembly("UnityEngine.Transform");
                        var posP = tP?.GetProperty("position", BindingFlags.Public | BindingFlags.Instance);
                        var vector3Type = FindTypeInAnyAssembly("UnityEngine.Vector3");
                        var v3ctor = vector3Type?.GetConstructor(new[] { typeof(float), typeof(float), typeof(float) });
                        if (posP != null && v3ctor != null)
                        {
                            object pos = v3ctor.Invoke(new object[] { x, y, z });
                            posP.SetValue(tr, pos);
                        }
                    }
                    catch { }
                };

                System.Action<object, float, float, float> setRot = (target, x, y, z) =>
                {
                    try
                    {
                        var trP = gameObjectType.GetProperty("transform", BindingFlags.Public | BindingFlags.Instance);
                        object tr = trP != null ? trP.GetValue(target) : null;
                        if (tr == null) return;
                        var tP = FindTypeInAnyAssembly("UnityEngine.Transform");
                        var rotP = tP?.GetProperty("rotation", BindingFlags.Public | BindingFlags.Instance);
                        var quatType = FindTypeInAnyAssembly("UnityEngine.Quaternion");
                        var eulerM = quatType?.GetMethod("Euler", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(float), typeof(float), typeof(float) }, null);
                        if (rotP != null && eulerM != null)
                        {
                            object rot = eulerM.Invoke(null, new object[] { x, y, z });
                            rotP.SetValue(tr, rot);
                        }
                    }
                    catch { }
                };

                System.Action<object, float, float, float> setScale = (target, x, y, z) =>
                {
                    try
                    {
                        var trP = gameObjectType.GetProperty("transform", BindingFlags.Public | BindingFlags.Instance);
                        object tr = trP != null ? trP.GetValue(target) : null;
                        if (tr == null) return;
                        var tP = FindTypeInAnyAssembly("UnityEngine.Transform");
                        var sclP = tP?.GetProperty("localScale", BindingFlags.Public | BindingFlags.Instance);
                        var vector3Type = FindTypeInAnyAssembly("UnityEngine.Vector3");
                        var v3ctor = vector3Type?.GetConstructor(new[] { typeof(float), typeof(float), typeof(float) });
                        if (sclP != null && v3ctor != null)
                        {
                            object scl = v3ctor.Invoke(new object[] { x, y, z });
                            sclP.SetValue(tr, scl);
                        }
                    }
                    catch { }
                };
                System.Action<object, float, float, float> setMatColor = (renderer, r, g, b) =>
                {
                    try
                    {
                        if (renderer == null) return;
                        var rendererType = renderer.GetType();
                        var matP = rendererType.GetProperty("material", BindingFlags.Public | BindingFlags.Instance);
                        if (matP == null) return;
                        object mat = matP.GetValue(renderer);
                        if (mat == null) return;
                        var matType = mat.GetType();
                        var colorType = FindTypeInAnyAssembly("UnityEngine.Color");
                        var ctor = colorType?.GetConstructor(new[] { typeof(float), typeof(float), typeof(float) });
                        if (ctor == null) return;
                        object col = ctor.Invoke(new object[] { r, g, b });
                        bool anySucceeded = false;
                        try
                        {
                            var colorP = matType.GetProperty("color", BindingFlags.Public | BindingFlags.Instance);
                            if (colorP != null && colorP.CanWrite)
                            {
                                colorP.SetValue(mat, col);
                                anySucceeded = true;
                            }
                        }
                        catch { }
                        try
                        {
                            var setColorM = matType.GetMethod("SetColor", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string), colorType }, null);
                            if (setColorM != null)
                            {
                                setColorM.Invoke(mat, new object[] { "_Color", col });
                                setColorM.Invoke(mat, new object[] { "_BaseColor", col });
                                anySucceeded = true;
                            }
                        }
                        catch { }
                        try
                        {
                            var setColorIntM = matType.GetMethod("SetColor", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int), colorType }, null);
                            if (setColorIntM != null)
                            {
                                var shaderType = FindTypeInAnyAssembly("UnityEngine.Shader");
                                var propToIDM = shaderType?.GetMethod("PropertyToID", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                                if (propToIDM != null)
                                {
                                    object id_Color = propToIDM.Invoke(null, new object[] { "_Color" });
                                    if (id_Color != null)
                                    {
                                        setColorIntM.Invoke(mat, new object[] { (int)id_Color, col });
                                        anySucceeded = true;
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                    catch { }
                };
                System.Action<object, float, float, float> replaceMatWithUnlit = (renderer, r, g, b) =>
                {
                    try
                    {
                        if (renderer == null) return;
                        var rendererType = renderer.GetType();
                        var shaderType = FindTypeInAnyAssembly("UnityEngine.Shader");
                        var materialType = FindTypeInAnyAssembly("UnityEngine.Material");
                        if (shaderType == null || materialType == null) return;
                        var findM = shaderType.GetMethod("Find", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                        if (findM == null) return;
                        string[] shaderNames = { "Unlit/Color", "Sprites/Default", "Hidden/Internal-Colored", "UI/Default" };
                        object shader = null;
                        foreach (var sn in shaderNames)
                        {
                            try
                            {
                                shader = findM.Invoke(null, new object[] { sn });
                                if (shader != null) break;
                            }
                            catch { }
                        }
                        if (shader == null) return;
                        var matCtor = materialType.GetConstructor(new[] { shaderType });
                        if (matCtor == null) return;
                        object newMat = matCtor.Invoke(new object[] { shader });
                        if (newMat == null) return;

                        try
                        {
                            var colorP = newMat.GetType().GetProperty("color", BindingFlags.Public | BindingFlags.Instance);
                            if (colorP != null)
                            {
                                var colorType = FindTypeInAnyAssembly("UnityEngine.Color");
                                var ctor = colorType?.GetConstructor(new[] { typeof(float), typeof(float), typeof(float) });
                                if (ctor != null)
                                {
                                    object col = ctor.Invoke(new object[] { r, g, b });
                                    colorP.SetValue(newMat, col);
                                }
                            }
                        }
                        catch { }

                        try
                        {
                            var sharedMatP = rendererType.GetProperty("sharedMaterial", BindingFlags.Public | BindingFlags.Instance);
                            sharedMatP?.SetValue(renderer, newMat);
                        }
                        catch { }
                    }
                    catch (System.Exception ex) { DriveLog($"replaceMatWithUnlit: {ex.GetType().Name}: {ex.Message}"); }
                };
                System.Func<System.Type, string, object> createPrimitive = (primTypeEnum, primName) =>
                {
                    try
                    {
                        if (primTypeEnum == null) return null;

                        System.Reflection.MethodInfo createPrimM = null;
                        foreach (var m in gameObjectType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        {
                            if (m.Name == "CreatePrimitive" && m.GetParameters().Length == 1)
                            {
                                createPrimM = m;
                                break;
                            }
                        }
                        if (createPrimM == null)
                        {
                            DriveLog("PlaceholderLevel: GameObject.CreatePrimitive not found");
                            return null;
                        }
                        object primValue = null;
                        try
                        {

                            var namedF = primTypeEnum.GetField(primName, BindingFlags.Public | BindingFlags.Static);
                            if (namedF != null) primValue = namedF.GetValue(null);
                            else
                            {
                                primValue = System.Enum.Parse(primTypeEnum, primName);
                            }
                        }
                        catch { }
                        if (primValue == null) return null;
                        return createPrimM.Invoke(null, new object[] { primValue });
                    }
                    catch (System.Exception pex) { DriveLog($"PlaceholderLevel: CreatePrimitive({primName}) failed: {pex.GetType().Name}"); return null; }
                };
                try
                {
                    DriveLog("PlaceholderLevel: step 1  creating camera");
                    object camGO = createGO("PlaceholderMainCamera");
                    DriveLog($"PlaceholderLevel: createGO returned {(camGO == null ? "NULL" : camGO.GetType().FullName)}");
                    if (camGO != null)
                    {
                        object mainCam = null;
                        string howAdded = "none";

                        try
                        {
                            var addCompM_a = gameObjectType.GetMethod("AddComponent", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(System.Type) }, null);
                            if (addCompM_a != null)
                            {
                                mainCam = addCompM_a.Invoke(camGO, new object[] { cameraType });
                                if (mainCam != null) howAdded = "non-generic(Type)";
                            }
                        }
                        catch (System.Exception exA) { DriveLog($"PlaceholderLevel: AddComponent(a) threw: {exA.GetType().Name}: {exA.Message}"); }

                        if (mainCam == null)
                        {
                            try
                            {
                                var allAdd = gameObjectType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                                foreach (var m in allAdd)
                                {
                                    if (m.Name != "AddComponent") continue;
                                    var ps = m.GetParameters();
                                    if (ps.Length == 1 && ps[0].ParameterType == typeof(System.Type))
                                    {
                                        try
                                        {
                                            mainCam = m.Invoke(camGO, new object[] { cameraType });
                                            if (mainCam != null) { howAdded = "scan(Type)"; break; }
                                        }
                                        catch { }
                                    }
                                }
                            }
                            catch (System.Exception exB) { DriveLog($"PlaceholderLevel: AddComponent(b) threw: {exB.GetType().Name}"); }
                        }


                        if (mainCam == null)
                        {
                            try
                            {
                                System.Reflection.MethodInfo genericDef = null;
                                foreach (var m in gameObjectType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                {
                                    if (m.Name == "AddComponent" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1)
                                    {
                                        genericDef = m;
                                        break;
                                    }
                                }
                                if (genericDef != null)
                                {
                                    var closed = genericDef.MakeGenericMethod(cameraType);
                                    mainCam = closed.Invoke(camGO, null);
                                    if (mainCam != null) howAdded = "generic<T>";
                                }
                                else
                                {
                                    DriveLog("PlaceholderLevel: no generic AddComponent<T> found");
                                }
                            }
                            catch (System.Exception exC) { DriveLog($"PlaceholderLevel: AddComponent(c) threw: {exC.GetType().Name}: {exC.Message}"); }
                        }

                        DriveLog($"PlaceholderLevel: addComponent(Camera) returned {(mainCam == null ? "NULL" : mainCam.GetType().FullName)} via {howAdded}");
                        if (mainCam != null)
                        {
                            try
                            {
                                var tagP = gameObjectType.GetProperty("tag", BindingFlags.Public | BindingFlags.Instance);
                                if (tagP != null) { try { tagP.SetValue(camGO, "MainCamera"); } catch { } }
                            }
                            catch { }


                            var cfP = cameraType.GetProperty("clearFlags", BindingFlags.Public | BindingFlags.Instance);
                            if (cfP != null) { try { cfP.SetValue(mainCam, 2); } catch { } }


                            var bgP = cameraType.GetProperty("backgroundColor", BindingFlags.Public | BindingFlags.Instance);
                            if (bgP != null)
                            {
                                try
                                {
                                    var colorType = FindTypeInAnyAssembly("UnityEngine.Color");
                                    if (colorType != null)
                                    {
                                        var ctor = colorType.GetConstructor(new[] { typeof(float), typeof(float), typeof(float) });
                                        if (ctor != null)
                                        {
                                            object blue = ctor.Invoke(new object[] { 0.4f, 0.7f, 1.0f });
                                            bgP.SetValue(mainCam, blue);
                                        }
                                    }
                                }
                                catch { }
                            }
                            try
                            {
                                var trP = gameObjectType.GetProperty("transform", BindingFlags.Public | BindingFlags.Instance);
                                object tr = trP != null ? trP.GetValue(camGO) : null;
                                if (tr != null)
                                {
                                    var tP = FindTypeInAnyAssembly("UnityEngine.Transform");
                                    if (tP != null)
                                    {
                                        var posP = tP.GetProperty("position", BindingFlags.Public | BindingFlags.Instance);
                                        var rotP = tP.GetProperty("rotation", BindingFlags.Public | BindingFlags.Instance);
                                        var vector3Type = FindTypeInAnyAssembly("UnityEngine.Vector3");
                                        var quatType = FindTypeInAnyAssembly("UnityEngine.Quaternion");
                                        if (vector3Type != null && quatType != null)
                                        {
                                            var v3ctor = vector3Type.GetConstructor(new[] { typeof(float), typeof(float), typeof(float) });
                                            var eulerM = quatType.GetMethod("Euler", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(float), typeof(float), typeof(float) }, null);
                                            if (v3ctor != null && eulerM != null)
                                            {
                                                object pos = v3ctor.Invoke(new object[] { 0f, 3f, -8f });
                                                object rot = eulerM.Invoke(null, new object[] { 25f, 0f, 0f });
                                                try { posP?.SetValue(tr, pos); } catch { }
                                                try { rotP?.SetValue(tr, rot); } catch { }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (System.Exception camPoseEx) { DriveLog($"PlaceholderLevel: camera position failed: {camPoseEx.GetType().Name}"); }
                            try
                            {
                                var setActiveM = gameObjectType.GetMethod("SetActive", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
                                if (setActiveM != null) { try { setActiveM.Invoke(camGO, new object[] { true }); } catch { } }
                            }
                            catch { }

                            DriveLog("PlaceholderLevel: created PlaceholderMainCamera (active scene, tagged MainCamera)");
                        }
                        else
                        {
                            DriveLog("PlaceholderLevel: Camera component could not be added (mainCam null). CameraType=" + (cameraType?.FullName ?? "NULL"));
                        }
                    }
                }
                catch (System.Exception ccEx) { DriveLog($"PlaceholderLevel: new main camera creation failed: {ccEx.GetType().Name}: {ccEx.Message}"); }
                try
                {
                    var mainCamM = cameraType.GetMethod("get_main", BindingFlags.Public | BindingFlags.Static, null, System.Type.EmptyTypes, null);
                    object mainCam = mainCamM != null ? mainCamM.Invoke(null, null) : null;
                    if (mainCam != null)
                    {

                        var cfP = cameraType.GetProperty("clearFlags", BindingFlags.Public | BindingFlags.Instance);
                        if (cfP != null)
                        {
                            try { cfP.SetValue(mainCam, 2); }
                            catch { }
                        }

                        var bgP = cameraType.GetProperty("backgroundColor", BindingFlags.Public | BindingFlags.Instance);
                        if (bgP != null)
                        {
                            try
                            {
                                var colorType = FindTypeInAnyAssembly("UnityEngine.Color");
                                if (colorType != null)
                                {
                                    var ctor = colorType.GetConstructor(new[] { typeof(float), typeof(float), typeof(float) });
                                    if (ctor != null)
                                    {
                                        object blue = ctor.Invoke(new object[] { 0.4f, 0.7f, 1.0f });
                                        bgP.SetValue(mainCam, blue);
                                    }
                                }
                            }
                            catch { }
                        }
                        DriveLog("PlaceholderLevel: set main camera clearFlags=SolidColor, bg=lightblue");
                    }
                }
                catch (System.Exception cex) { DriveLog($"PlaceholderLevel: camera config failed: {cex.GetType().Name}"); }


                try
                {
                    object lightGO = createGO("PlaceholderSun");
                    if (lightGO != null)
                    {

                        object light = addCompGeneric(lightGO, lightType);
                        if (light != null)
                        {

                            var typeP = lightType.GetProperty("type", BindingFlags.Public | BindingFlags.Instance);
                            if (typeP != null) { try { typeP.SetValue(light, 1); } catch { } }

                            var intP = lightType.GetProperty("intensity", BindingFlags.Public | BindingFlags.Instance);
                            if (intP != null) { try { intP.SetValue(light, 1.0f); } catch { } }

                            var shP = lightType.GetProperty("shadows", BindingFlags.Public | BindingFlags.Instance);
                            if (shP != null) { try { shP.SetValue(light, 2); } catch { } }
                        }
                        else
                        {
                            DriveLog("PlaceholderLevel: Light component could not be added (light null)");
                        }

                        setPos(lightGO, 0f, 50f, 0f);
                        setRot(lightGO, 50f, -30f, 0f);
                        DriveLog("PlaceholderLevel: created PlaceholderSun light");
                    }
                }
                catch (System.Exception lex) { DriveLog($"PlaceholderLevel: light failed: {lex.GetType().Name}"); }
                try
                {
                    object plane = createPrimitive(primitiveTypeEnum, "Plane");
                    if (plane != null)
                    {

                        try
                        {
                            var nameP = gameObjectType.GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                            nameP?.SetValue(plane, "PlaceholderGround");
                        }
                        catch { }
                        setPos(plane, 0f, 0f, 0f);
                        setScale(plane, 2f, 1f, 2f);
                        try
                        {
                            var meshRendererType = FindTypeInAnyAssembly("UnityEngine.MeshRenderer");
                            if (meshRendererType != null)
                            {
                                var getCompM = gameObjectType.GetMethod("GetComponent", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(System.Type) }, null);
                                object mr = getCompM != null ? getCompM.Invoke(plane, new object[] { meshRendererType }) : null;
                                setMatColor(mr, 0.2f, 0.7f, 0.2f);
                            }
                        }
                        catch { }

                        DriveLog("PlaceholderLevel: created PlaceholderGround (Plane primitive, green)");
                    }
                    else
                    {
                        DriveLog("PlaceholderLevel: CreatePrimitive(Plane) returned null  falling back to empty GO");

                        createGO("PlaceholderGround");
                    }
                }
                catch (System.Exception gex) { DriveLog($"PlaceholderLevel: ground failed: {gex.GetType().Name}"); }


                string[] cubeNames = { "PlaceholderCube1", "PlaceholderCube2", "PlaceholderCube3", "PlaceholderCube4" };
                float[][] cubePositions = {
                    new[] { 5f, 1f, 5f },
                    new[] { -5f, 1f, 5f },
                    new[] { 5f, 1f, -5f },
                    new[] { -5f, 1f, -5f }
                };
                float[][] cubeColors = {
                    new[] { 0.9f, 0.3f, 0.3f },
                    new[] { 0.3f, 0.5f, 0.9f },
                    new[] { 0.9f, 0.9f, 0.3f },
                    new[] { 0.6f, 0.3f, 0.9f }
                };
                int cubesCreated = 0;
                for (int i = 0; i < cubeNames.Length; i++)
                {
                    try
                    {
                        object cube = createPrimitive(primitiveTypeEnum, "Cube");
                        if (cube == null) continue;
                        try
                        {
                            var nameP = gameObjectType.GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                            nameP?.SetValue(cube, cubeNames[i]);
                        }
                        catch { }
                        setPos(cube, cubePositions[i][0], cubePositions[i][1], cubePositions[i][2]);
                        setScale(cube, 2f, 2f, 2f);

                        try
                        {
                            var meshRendererType = FindTypeInAnyAssembly("UnityEngine.MeshRenderer");
                            if (meshRendererType != null)
                            {
                                var getCompM = gameObjectType.GetMethod("GetComponent", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(System.Type) }, null);
                                object mr = getCompM != null ? getCompM.Invoke(cube, new object[] { meshRendererType }) : null;
                                setMatColor(mr, cubeColors[i][0], cubeColors[i][1], cubeColors[i][2]);
                            }
                        }
                        catch { }
                        cubesCreated++;
                    }
                    catch (System.Exception cx) { DriveLog($"PlaceholderLevel: cube[{i}] failed: {cx.GetType().Name}"); }
                }
                DriveLog($"PlaceholderLevel: created {cubesCreated}/{cubeNames.Length} cube obstacles (Cube primitives)");

                DriveLog($"PlaceholderLevel[{reason}]: COMPLETE");
            }
            catch (System.Exception ex)
            {
                DriveLog($"PlaceholderLevel OUTER error: {ex.GetType().Name}: {ex.Message}");
                _placeholderLevelCreated = false;
            }
        }
        private static void LogGameObjectChildren(object go, string reason, int depth, int maxDepth)
        {
            if (go == null || depth > maxDepth) return;
            try
            {
                var tP = go.GetType().GetProperty("transform", BindingFlags.Public | BindingFlags.Instance);
                object tr = tP != null ? tP.GetValue(go) : null;
                if (tr == null) return;
                var getChildM = tr.GetType().GetMethod("GetChild", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                var ccP = tr.GetType().GetProperty("childCount", BindingFlags.Public | BindingFlags.Instance);
                if (getChildM == null || ccP == null) return;
                int cc = (int)ccP.GetValue(tr);
                for (int i = 0; i < cc; i++)
                {
                    object child = getChildM.Invoke(tr, new object[] { i });
                    if (child == null) continue;
                    try
                    {
                        var goP = child.GetType().GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
                        object cgo = goP != null ? goP.GetValue(child) : null;
                        if (cgo == null) continue;
                        var nameP = cgo.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                        string cn = nameP != null ? (nameP.GetValue(cgo) as string ?? "?") : "?";
                        var activeP = cgo.GetType().GetProperty("activeSelf", BindingFlags.Public | BindingFlags.Instance);
                        bool active = activeP != null && (bool)activeP.GetValue(cgo);
                        var ccp = cgo.GetType().GetProperty("transform", BindingFlags.Public | BindingFlags.Instance);
                        object ctr = ccp != null ? ccp.GetValue(cgo) : null;
                        int ccc = -1;
                        if (ctr != null)
                        {
                            var cccP = ctr.GetType().GetProperty("childCount", BindingFlags.Public | BindingFlags.Instance);
                            if (cccP != null) ccc = (int)cccP.GetValue(ctr);
                        }
                        string indent = new string(' ', (depth + 1) * 2);
                        DriveLog($"BruteForceHide[{reason}]:{indent}CHILD [{i}] '{cn}' (active={active}, children={ccc})");
                        if (depth < maxDepth)
                        {
                            LogGameObjectChildren(cgo, reason, depth + 1, maxDepth);
                        }
                    }
                    catch (System.Exception cx)
                    {
                        DriveLog($"BruteForceHide[{reason}]: child[{i}] log failed: {cx.GetType().Name}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                DriveLog($"BruteForceHide[{reason}]: LogGameObjectChildren threw: {ex.GetType().Name}");
            }
        }
        private static void TrySynthesizeGameSessionData()
        {
            try
            {
                var controllerType =
                    FindTypeInAnyAssembly("Il2Cpp.MVGameControllerBase") ??
                    FindTypeInAnyAssembly("MVGameControllerBase");
                if (controllerType == null)
                {
                    MelonLogger.Warning("[KoGaMaPatch] Can't synthesize GameSessionData  controller type not found.");
                    return;
                }
                var sessionDataType =
                    FindTypeInAnyAssembly("Il2Cpp.GameSessionData") ??
                    FindTypeInAnyAssembly("GameSessionData");
                if (sessionDataType == null)
                {
                    MelonLogger.Warning("[KoGaMaPatch] GameSessionData type not found.");
                    return;
                }
                MelonLogger.Msg($"[KoGaMaPatch] GameSessionData: {sessionDataType.FullName}");


                var ctor = sessionDataType.GetConstructor(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null,
                    types: System.Type.EmptyTypes,
                    modifiers: null);
                if (ctor == null)
                {
                    MelonLogger.Warning("[KoGaMaPatch] GameSessionData() parameterless ctor not found.");
                    return;
                }
                var sessionData = ctor.Invoke(null);
                if (sessionData == null)
                {
                    MelonLogger.Warning("[KoGaMaPatch] GameSessionData() returned null.");
                    return;
                }
                  const string LocalUrl  = "http://127.0.0.1:8080";
                const string LocalIP   = "127.0.0.1";
                foreach (var fld in sessionDataType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    try
                    {
                        if (fld.FieldType == typeof(string))
                        {
                            if (string.Equals(fld.Name, "serverIP", System.StringComparison.OrdinalIgnoreCase))
                                fld.SetValue(sessionData, LocalIP);
                            else if (string.Equals(fld.Name, "region", System.StringComparison.OrdinalIgnoreCase))
                                fld.SetValue(sessionData, "local");
                            else
                                fld.SetValue(sessionData, LocalUrl);
                        }
                else if (fld.FieldType == typeof(int))
                {
                    if (string.Equals(fld.Name, "planetID", StringComparison.OrdinalIgnoreCase))
                        fld.SetValue(sessionData, 1);
                    else if (string.Equals(fld.Name, "gameMode", StringComparison.OrdinalIgnoreCase))
                        fld.SetValue(sessionData, 1);
                    else
                        fld.SetValue(sessionData, 0);
                }
                    }
                    catch {}
                }
                var prop = controllerType.GetProperty(
                    "GameSessionData",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (prop == null)
                {
                    MelonLogger.Warning("[KoGaMaPatch] GameSessionData property on controller not found.");
                    return;
                }
                var setter = prop.GetSetMethod(nonPublic: true);
                if (setter == null)
                {
                    MelonLogger.Warning($"[KoGaMaPatch] GameSessionData setter not found. Property: {prop}");
                    return;
                }
                setter.Invoke(null, new object[] { sessionData });
                MelonLogger.Msg($"[KoGaMaPatch] GameSessionData synthesized and assigned to MVGameControllerBase.GameSessionData.");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[KoGaMaPatch] TrySynthesizeGameSessionData failed: {ex.Message}");
            }
        }

        public static Type FindTypeInAnyAssembly(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName, throwOnError: false); }
                catch {}
                if (t != null) return t;
            }
            return null;
        }
    }
    public static class PhotonInProcessStub
    {
        private static bool _applied = false;
        private static System.Collections.Generic.List<System.Type> _hookedPeerTypes
            = new System.Collections.Generic.List<System.Type>();
        private static System.Type _opResponseType;
        private static System.Type _eventDataType;
        private static PropertyInfo _listenerProp;
        private const byte OpJoin = 255;
        public static void Postfix_GetPeerState_AlwaysConnected(ref object __result)
        {
            try
            {
                var stateType = __result?.GetType();
                if (stateType != null && stateType.IsEnum)
                {

                    __result = System.Enum.ToObject(stateType, 3);
                }
            }
            catch {}
        }

        private const int LocalActorNumber = 1;

        public static bool Prefix_SkipMethod()
{
    return false;
}

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            if (_applied) return;
            var peerType = FindTypeInAnyAssembly("Il2CppExitGames.Client.Photon.PhotonPeer") ?? FindTypeInAnyAssembly("ExitGames.Client.Photon.PhotonPeer");
            if (peerType != null)
            {
                var peerStateProp = peerType.GetProperty("PeerState", BindingFlags.Public | BindingFlags.Instance);
                if (peerStateProp != null)
                {
                    var peerStateGetter = peerStateProp.GetGetMethod();
                    if (peerStateGetter != null)
                    {
                        harmony.Patch(peerStateGetter, postfix: new HarmonyMethod(typeof(BypassMVGameControllerInit), nameof(Postfix_GetPeerState_AlwaysConnected)));
                        MelonLogger.Msg("[KoGaMaPatch] Hooked PhotonPeer.get_PeerState (always returning Connected).");
                    }
                }
            }

            MelonLogger.Msg($"[PhotonInProcessStub] Found {peerType.FullName}, applying hooks...");
            var sendOutgoing = peerType.GetMethod(
                "SendOutgoingCommands",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (sendOutgoing != null)
            {
                harmony.Patch(sendOutgoing, prefix: new HarmonyMethod(typeof(PhotonInProcessStub), nameof(Prefix_SendOutgoingCommands)));
                MelonLogger.Msg("[PhotonInProcessStub] Hooked PhotonPeer.SendOutgoingCommands (short-circuits real sends).");
            }
            else
            {
                MelonLogger.Warning("[PhotonInProcessStub] SendOutgoingCommands not found.");
            }
            var service = peerType.GetMethod(
                "Service",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (service != null)
            {
                harmony.Patch(service, prefix: new HarmonyMethod(typeof(PhotonInProcessStub), nameof(Prefix_Service)));
                MelonLogger.Msg("[PhotonInProcessStub] Hooked PhotonPeer.Service (logging ticks).");
            }
            else
            {
                MelonLogger.Warning("[PhotonInProcessStub] Service not found.");
            }
            var enqStatus = FindMethodOnInheritanceChain(peerType, "EnqueueStatusCallback");
            if (enqStatus != null)
            {
                harmony.Patch(enqStatus, postfix: new HarmonyMethod(typeof(PhotonInProcessStub), nameof(Postfix_EnqueueStatusCallback)));
                MelonLogger.Msg($"[PhotonInProcessStub] Hooked PhotonPeer.EnqueueStatusCallback (logging, on {enqStatus.DeclaringType.Name}).");
            }
            else
            {
                MelonLogger.Warning("[PhotonInProcessStub] EnqueueStatusCallback not found.");
            }
            var enqOp = FindMethodOnInheritanceChain(peerType, "EnqueueOperation");
            if (enqOp != null)
            {
                harmony.Patch(enqOp, postfix: new HarmonyMethod(typeof(PhotonInProcessStub), nameof(Postfix_EnqueueOperation)));
                MelonLogger.Msg($"[PhotonInProcessStub] Hooked PhotonPeer.EnqueueOperation (logging, on {enqOp.DeclaringType.Name}).");
            }
            else
            {
                MelonLogger.Warning("[PhotonInProcessStub] EnqueueOperation not found.");
            }
            var opResponseType = FindTypeInAnyAssembly("Il2CppExitGames.Client.Photon.OperationResponse")
                               ?? FindTypeInAnyAssembly("ExitGames.Client.Photon.OperationResponse");
            var eventDataType = FindTypeInAnyAssembly("Il2CppExitGames.Client.Photon.EventData")
                              ?? FindTypeInAnyAssembly("ExitGames.Client.Photon.EventData");

            if (opResponseType == null || eventDataType == null)
            {
                MelonLogger.Warning("[PhotonInProcessStub] OperationResponse/EventData types not found  SendOperation stub disabled.");
            }
            else
            {
                _opResponseType = opResponseType;
                _eventDataType = eventDataType;
                _listenerProp = peerType.GetProperty("Listener", BindingFlags.Public | BindingFlags.Instance);

                int sendOpHooked = 0;
                foreach (var m in peerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.Name != "SendOperation") continue;
                    harmony.Patch(m, prefix: new HarmonyMethod(typeof(PhotonInProcessStub), nameof(Prefix_SendOperation)));
                    sendOpHooked++;
                }
                MelonLogger.Msg($"[PhotonInProcessStub] Hooked {sendOpHooked} SendOperation overload(s)  join/ops answered in-process.");
            }
            
            _applied = true;
            MelonLogger.Msg("[PhotonInProcessStub] Apply complete.");
        }
        public static bool Prefix_SendOutgoingCommands(object[] __args)
        {
            return true;
        }

                public static object CreateIl2CppObjectDictionary(System.Collections.Generic.Dictionary<object, object> bclDict)
        {
            var il2cppObjectType = FindTypeInAnyAssembly("Il2CppSystem.Object");
            var dictType = FindTypeInAnyAssembly("Il2CppSystem.Collections.Generic.Dictionary`2");
            if (il2cppObjectType == null || dictType == null) return null;

            var closedDictType = dictType.MakeGenericType(il2cppObjectType, il2cppObjectType);
            

            object il2cppDict = null;
            var ctorCap = closedDictType.GetConstructor(new[] { typeof(int) });
            if (ctorCap != null)
            {
                il2cppDict = ctorCap.Invoke(new object[] { bclDict.Count });
            }
            else
            {
                il2cppDict = System.Activator.CreateInstance(closedDictType);
            }

            System.Reflection.MethodInfo addMethod = null;
            foreach (var m in closedDictType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (m.Name != "Add") continue;
                var ps = m.GetParameters();
                if (ps.Length == 2 && ps[0].ParameterType == il2cppObjectType && ps[1].ParameterType == il2cppObjectType)
                {
                    addMethod = m;
                    break;
                }
            }

            if (addMethod == null) return null;

            foreach (var kvp in bclDict)
            {
                object boxedKey = BoxToIl2CppObject(il2cppObjectType, kvp.Key);
                object boxedVal = BoxToIl2CppObject(il2cppObjectType, kvp.Value);

                if (boxedKey != null && boxedVal != null)
                {
                    try 
                    { 
                        addMethod.Invoke(il2cppDict, new[] { boxedKey, boxedVal }); 
                    } 
                    catch (System.Exception ex) 
                    {
                        MelonLogger.Warning($"[Dict] Add failed for key {kvp.Key}: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }
            return il2cppDict;
        }

private static object BoxToIl2CppObject(System.Type il2cppObjectType, object clrValue)
{
    if (clrValue == null) return null;
    Type clrType = clrValue.GetType();

    if (typeof(Il2CppSystem.Object).IsAssignableFrom(clrType))
    {
        return clrValue;
    }
    if (clrType == typeof(System.Collections.Generic.Dictionary<object, object>))
    {
        return CreateIl2CppObjectDictionary((System.Collections.Generic.Dictionary<object, object>)clrValue);
    }
    if (clrType == typeof(System.Collections.Generic.List<object>))
            {
                
            }
    var method = il2cppObjectType.GetMethod("op_Implicit", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, new[] { clrType }, null);
    if (method != null)
    {
        return method.Invoke(null, new object[] { clrValue });
    }


    if (clrType.IsValueType || clrType == typeof(string))
    {
        var refBoxType = FindTypeInAnyAssembly("Il2CppInterop.Runtime.InteropTypes.Il2CppReferenceBox`1");
        if (refBoxType != null)
        {
            var concreteRefBox = refBoxType.MakeGenericType(clrType);
            var ctor = concreteRefBox.GetConstructor(new[] { clrType });
            if (ctor != null)
            {
                return ctor.Invoke(new object[] { clrValue });
            }
        }
    }
    
    return clrValue;
}
        public static bool Prefix_Disconnect()
        {
            MelonLogger.Msg("[PhotonInProcessStub] Suppressed PhotonPeer.Disconnect()!");
            return false;
        }
        private static int _serviceTick = 0;


        public static bool Prefix_Service()
        {
            return false;
        }
        public static void Postfix_EnqueueStatusCallback(object[] __args)
        {
            try
            {
                if (__args != null && __args.Length > 0 && __args[0] != null)
                {
                    int statusValue = (int)__args[0];
                    BypassMVGameControllerInit.DriveLog($"PhotonPeer.EnqueueStatusCallback(status={statusValue})");
                }
            }
            catch {}
        }
        public static void Postfix_EnqueueOperation(object[] __args)
        {
            try
            {
                if (__args != null && __args.Length >= 2)
                {
                    object opParams = __args[0];
                    object opCode = __args[1];
                    int opCodeValue = opCode is byte b ? b : (int)opCode;
                    BypassMVGameControllerInit.DriveLog($"PhotonPeer.EnqueueOperation(opCode=0x{opCodeValue:X2}/{opCodeValue})");
                }
            }
            catch (System.Exception ex)
            {
                BypassMVGameControllerInit.DriveLog($"EnqueueOperation log error: {ex.GetType().Name}: {ex.Message}");
            }
        }
        private static bool _inSendOperation = false;
private static System.Collections.Generic.HashSet<byte> _processingOpcodes = new System.Collections.Generic.HashSet<byte>();

public static bool Prefix_SendOperation(object __instance, byte operationCode, object operationParameters, object sendOptions, ref bool __result)
{
    if (_processingOpcodes.Contains(operationCode))
    {
        MelonLogger.Warning($"[PhotonInProcessStub] Reentrant call for SAME opcode {operationCode}  letting through to avoid loop.");
        return true; 
    }
    _processingOpcodes.Add(operationCode);
    try
    {
        MelonLogger.Msg($"[PhotonInProcessStub] SendOperation(code={operationCode} / 0x{operationCode:X2})");

        object listener = _listenerProp?.GetValue(__instance);
        if (listener == null)
        {
            MelonLogger.Warning("[PhotonInProcessStub] No Listener set yet  dropping SendOperation.");
            __result = true;
            return false;
        }
        switch (operationCode)
        {
            case 113:
            {
                MelonLogger.Msg("[PhotonInProcessStub] CreateSpawnRole(113) returning fake success (avatar handled by direct clone).");
                InvokeOperationResponse(listener, 113, 0, "", new System.Collections.Generic.Dictionary<byte, object>());
                break;
            }
            case 255:
                HandleJoin(listener);
                break;

            case 248:

                InvokeOperationResponse(listener, 248, 0, "", new System.Collections.Generic.Dictionary<byte, object>());
                MelonLogger.Msg("[PhotonInProcessStub]   → faked empty OK for Handshake (248)");
                break;

            case 249:

                InvokeOperationResponse(listener, 249, 0, "", new System.Collections.Generic.Dictionary<byte, object>());
                MelonLogger.Msg("[PhotonInProcessStub]   → faked empty OK for Ping (249)");
                break;
            case 254:

                break;
        default:
    MelonLogger.Msg($"[PhotonInProcessStub] *** UNHANDLED op {operationCode}  faking empty OK ***");

    if (operationParameters != null)
    {
        try
        {
            var dictType = operationParameters.GetType();
            var getEnumeratorM = dictType.GetMethod("GetEnumerator");
            var enumerator = getEnumeratorM?.Invoke(operationParameters, null);
            if (enumerator != null)
            {
                var moveNextM = enumerator.GetType().GetMethod("MoveNext");
                var currentProp = enumerator.GetType().GetProperty("Current");
                while ((bool)moveNextM.Invoke(enumerator, null))
                {
                    var entry = currentProp.GetValue(enumerator);
                    var keyProp = entry.GetType().GetProperty("Key");
                    var valProp = entry.GetType().GetProperty("Value");
                    MelonLogger.Msg($"    sent param: key={keyProp?.GetValue(entry)}, value={valProp?.GetValue(entry)}");
                }
            }
        }
        catch { }
    }
    InvokeOperationResponse(listener, operationCode, 0, "", new Dictionary<byte, object>());
    break;
    }
    }
    catch (System.Exception ex)
    {
        MelonLogger.Error($"[PhotonInProcessStub] Exception in Prefix_SendOperation: {ex}");
    }
    finally
    {
        _processingOpcodes.Remove(operationCode);
    }

    __result = true;
    return false;
}
private static void HandleJoin(object listener)
{
    MelonLogger.Msg("[PhotonInProcessStub] Faking successful room Join.");

    var responseParams = new ThrowingDictionary
    {
        { 254, LocalActorNumber },
        { 252, new int[] { LocalActorNumber } },
    };
    InvokeOperationResponse(listener, OpJoin, 0, null, responseParams);

    var eventParams = new ThrowingDictionary { { 254, LocalActorNumber } };
    InvokeEvent(listener, OpJoin, eventParams, LocalActorNumber);
}
private static void InvokeOperationResponse(object listener, byte opCode, short returnCode, string debugMessage, System.Collections.Generic.Dictionary<byte, object> parameters)
{
    object response = System.Activator.CreateInstance(_opResponseType);
    SetMember(_opResponseType, response, "OperationCode", opCode);
    SetMember(_opResponseType, response, "ReturnCode", returnCode);
    SetMember(_opResponseType, response, "DebugMessage", debugMessage);
    SetMember(_opResponseType, response, "Parameters", parameters);

    var method = listener.GetType().GetMethod("OnOperationResponse", BindingFlags.Public | BindingFlags.Instance);
    if (method == null)
    {
        MelonLogger.Warning("[PhotonInProcessStub] Listener has no OnOperationResponse.");
        return;
    }
    try
    {
        method.Invoke(listener, new object[] { response });
    }
    catch (System.Exception ex)
    {
        var inner = ex.InnerException ?? ex;
        MelonLogger.Error($"[PhotonInProcessStub] OnOperationResponse threw: {inner.GetType().Name}: {inner.Message}");
        BypassMVGameControllerInit.DriveLog($"OnOperationResponse threw: {inner.GetType().Name}: {inner.Message}");
    }
}

public class ThrowingDictionary : System.Collections.Generic.Dictionary<byte, object>
{
    public new object this[byte key]
    {
        get
        {
            if (TryGetValue(key, out var val)) return val;

            throw new System.Collections.Generic.KeyNotFoundException($"KogamaOfflinePatch: Missing Photon Parameter key '{key}' (0x{key:X2})");
        }
        set { base[key] = value; }
    }
}
        private static void InvokeEvent(object listener, byte code, System.Collections.Generic.Dictionary<byte, object> parameters, int senderActorNr)
        {
            object evt = System.Activator.CreateInstance(_eventDataType);
            SetMember(_eventDataType, evt, "Code", code);
            SetMember(_eventDataType, evt, "Parameters", parameters);
            SetMember(_eventDataType, evt, "sender", senderActorNr);

            var method = listener.GetType().GetMethod("OnEvent", BindingFlags.Public | BindingFlags.Instance);
            if (method == null)
            {
                MelonLogger.Warning("[PhotonInProcessStub] Listener has no OnEvent.");
                return;
            }
            method.Invoke(listener, new object[] { evt });
        }

private static void SetMember(System.Type type, object instance, string name, object value)
{
    try
    {
        System.Reflection.FieldInfo field = null;
        System.Reflection.PropertyInfo prop = null;
        System.Type memberType = null;

        field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            memberType = field.FieldType;
        }
        else
        {
            prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite)
            {
                memberType = prop.PropertyType;
            }
        }

        if (memberType == null)
        {
            MelonLogger.Warning($"[PhotonInProcessStub] Could not find field/writable property '{name}' on {type.FullName}.");
            return;
        }


        bool directAssignOK = value == null || memberType.IsAssignableFrom(value.GetType());

        if (!directAssignOK && value != null)
        {
            System.Type valueType = value.GetType();
            

            bool isBclDict = valueType == typeof(System.Collections.Generic.Dictionary<byte, object>) ||
                             valueType.IsSubclassOf(typeof(System.Collections.Generic.Dictionary<byte, object>));

            if (isBclDict && (memberType.FullName ?? "").StartsWith("Il2CppSystem.Collections.Generic.Dictionary"))
            {
                object il2cppDict = CreateIl2CppDictionary(memberType, value);
                if (il2cppDict != null)
                {
                    if (field != null) field.SetValue(instance, il2cppDict);
                    else prop.SetValue(instance, il2cppDict);
                    
                    MelonLogger.Msg($"[PhotonInProcessStub] SetMember('{name}'): BCL dict → Il2Cpp dict OK.");
                    return;
                }
                MelonLogger.Warning($"[PhotonInProcessStub] SetMember('{name}'): dict conversion returned null, leaving default.");
                return;
            }
        }


        if (field != null) field.SetValue(instance, value);
        else if (prop != null) prop.SetValue(instance, value);
    }
    catch (System.Exception ex)
    {
        MelonLogger.Warning($"[PhotonInProcessStub] SetMember('{name}') failed ({ex.GetType().Name}: {ex.Message})  skipping this field only.");
    }
}

private static object CreateIl2CppDictionary(System.Type il2cppDictType, object bclDictObj)
{
    try
    {

        object il2cppDict = null;


        var ctor0 = il2cppDictType.GetConstructor(System.Type.EmptyTypes);
        if (ctor0 != null)
        {
            try { il2cppDict = ctor0.Invoke(null); } catch { }
        }


        if (il2cppDict == null)
        {
            var ctorCap = il2cppDictType.GetConstructor(new[] { typeof(int) });
            if (ctorCap != null)
            {
                try { il2cppDict = ctorCap.Invoke(new object[] { 0 }); } catch { }
            }
        }


        if (il2cppDict == null)
        {
            try
            {
                var il2cppInteropRuntime = FindTypeInAnyAssembly("Il2CppInterop.Runtime");
                if (il2cppInteropRuntime != null)
                {
                    var classPtrProp = il2cppDictType.GetProperty("Class",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (classPtrProp != null)
                    {
                        var classPtr = (System.IntPtr)classPtrProp.GetValue(null);
                        var il2cppType = FindTypeInAnyAssembly("Il2CppInterop.Runtime.IL2CPP");
                        if (il2cppType != null)
                        {
                            var newObjM = il2cppType.GetMethod("il2cpp_object_new",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                            if (newObjM != null)
                            {
                                var ptr = (System.IntPtr)newObjM.Invoke(null, new object[] { classPtr });
                                var ipCtor = il2cppDictType.GetConstructor(
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                                    null, new[] { typeof(System.IntPtr) }, null);
                                if (ipCtor != null)
                                    il2cppDict = ipCtor.Invoke(new object[] { ptr });
                            }
                        }
                    }
                }
            }
            catch { }
        }

        if (il2cppDict == null)
        {
            MelonLogger.Warning("[PhotonInProcessStub] CreateIl2CppDictionary: could not instantiate dict.");
            return null;
        }


        System.Reflection.MethodInfo addMethod = null;
        foreach (var m in il2cppDictType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.Name != "Add") continue;
            var ps = m.GetParameters();
            if (ps.Length != 2) continue;
            if (ps[0].ParameterType != typeof(byte)) continue;
            addMethod = m;
            break;
        }
        if (addMethod == null)
        {
            MelonLogger.Warning("[PhotonInProcessStub] CreateIl2CppDictionary: no Add(byte, ...) found.");
            return null;
        }


        var valueParamType = addMethod.GetParameters()[1].ParameterType;
        var bclDict = (System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<byte, object>>)bclDictObj;
        int added = 0, failed = 0;
        foreach (var kvp in bclDict)
        {
            object val = ConvertValueForIl2Cpp(kvp.Value, valueParamType);
            try
            {
                addMethod.Invoke(il2cppDict, new object[] { kvp.Key, val });
                added++;
            }
            catch (System.Exception ex)
            {
                failed++;
                MelonLogger.Warning($"[PhotonInProcessStub] dict Add({kvp.Key}) failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }
        MelonLogger.Msg($"[PhotonInProcessStub] Il2Cpp dict populated: {added} OK, {failed} failed.");
        return il2cppDict;
    }
    catch (System.Exception ex)
    {
        MelonLogger.Warning($"[PhotonInProcessStub] CreateIl2CppDictionary: {ex.GetType().Name}: {ex.Message}");
        return null;
    }
}

private static object ConvertValueForIl2Cpp(object value, System.Type targetValueType)
{
    if (value == null) return null;


    if (targetValueType != null && targetValueType.FullName == "Il2CppSystem.Object")
    {

        if (value is int || value is byte || value is short || value is long ||
            value is float || value is double || value is bool || value is string)
        {
            var boxed = BoxToIl2CppObject(targetValueType, value);
            if (boxed != null) return boxed;
        }


        if (value is int[] intArr)
        {
            var il2cppArr = WrapPrimitiveArray(intArr);
            if (il2cppArr != null)
            {
                var wrappedObj = WrapAsIl2CppObject(targetValueType, il2cppArr);
                if (wrappedObj != null) return wrappedObj;
            }
        }
        if (value is byte[] byteArr)
        {
            var il2cppArr = WrapPrimitiveArray(byteArr);
            if (il2cppArr != null)
            {
                var wrappedObj = WrapAsIl2CppObject(targetValueType, il2cppArr);
                if (wrappedObj != null) return wrappedObj;
            }
        }


        var valueType = value.GetType();
        bool looksLikeIl2CppWrapper =
            (valueType.Namespace ?? "").StartsWith("Il2CppInterop") ||
            (valueType.FullName ?? "").Contains("Il2Cpp");
        if (looksLikeIl2CppWrapper)
        {
            var wrapped = WrapAsIl2CppObject(targetValueType, value);
            if (wrapped != null) return wrapped;
        }
    }

    return value;
}
private static object WrapAsIl2CppObject(System.Type il2cppObjectType, object il2cppValue)
{
    try
    {
        System.IntPtr ptr = System.IntPtr.Zero;
        for (var t = il2cppValue.GetType(); t != null; t = t.BaseType)
        {
            var p = t.GetProperty("Pointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.PropertyType == typeof(System.IntPtr))
            {
                ptr = (System.IntPtr)p.GetValue(il2cppValue);
                break;
            }
        }
        if (ptr == System.IntPtr.Zero)
        {
            MelonLogger.Warning($"[PhotonInProcessStub] WrapAsIl2CppObject: no Pointer found on {il2cppValue.GetType().FullName}.");
            return null;
        }

        var ctor = il2cppObjectType.GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(System.IntPtr) },
            modifiers: null);
        if (ctor == null)
        {
            MelonLogger.Warning($"[PhotonInProcessStub] WrapAsIl2CppObject: no {il2cppObjectType.Name}(IntPtr) ctor found.");
            return null;
        }
        return ctor.Invoke(new object[] { ptr });
    }
    catch (System.Exception ex)
    {
        MelonLogger.Warning($"[PhotonInProcessStub] WrapAsIl2CppObject failed: {ex.GetType().Name}: {ex.Message}");
        return null;
    }
}
private static object WrapPrimitiveArray<T>(T[] clrArray) where T : struct
{
    try
    {
        var structArrayType = FindTypeInAnyAssembly("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1");
        if (structArrayType == null)
        {
            MelonLogger.Warning("[PhotonInProcessStub] WrapPrimitiveArray: Il2CppStructArray<T> type not found.");
            return null;
        }
        var closed = structArrayType.MakeGenericType(typeof(T));
        var ctor = closed.GetConstructor(new[] { typeof(T[]) });
        if (ctor == null)
        {
            MelonLogger.Warning($"[PhotonInProcessStub] WrapPrimitiveArray: no {closed.Name}(T[]) ctor found.");
            return null;
        }
        return ctor.Invoke(new object[] { clrArray });
    }
    catch (System.Exception ex)
    {
        MelonLogger.Warning($"[PhotonInProcessStub] WrapPrimitiveArray<{typeof(T).Name}> failed: {ex.GetType().Name}: {ex.Message}");
        return null;
    }
}
        private static System.Type FindTypeInAnyAssembly(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type t = null;
                try { t = asm.GetType(fullName, throwOnError: false); }
                catch {}
                if (t != null) return t;
            }
            return null;
        }
        private static System.Reflection.MethodInfo FindMethodOnInheritanceChain(System.Type t, string name)
        {
            var allFlags = BindingFlags.Public | BindingFlags.NonPublic |
                           BindingFlags.Instance | BindingFlags.Static |
                           BindingFlags.FlattenHierarchy;
            for (var cur = t; cur != null; cur = cur.BaseType)
            {
                try
                {
                    var m = cur.GetMethod(name, allFlags);
                    if (m != null) return m;
                }
                catch {}
            }
            return null;
        }
        private static bool TypesMatch(System.Type a, System.Type b)
        {
            if (a == null || b == null) return false;
            if (object.ReferenceEquals(a, b)) return true;
            if (a.FullName == b.FullName) return true;

            string aN = a.FullName ?? "";
            string bN = b.FullName ?? "";
            if (aN.Replace("Il2CppMV.", "MV.") == bN.Replace("Il2CppMV.", "MV.")) return true;
            return false;
        }
        private static bool IsMVGameModeType(System.Type t)
        {
            if (t == null) return false;
            if (!t.IsEnum) return false;
            string n = t.FullName ?? "";
            if (n == "MVGameMode") return true;
            if (n == "MV.Common.MVGameMode") return true;
            if (n == "Il2CppMV.Common.MVGameMode") return true;
            return false;
        }
        
    }
}