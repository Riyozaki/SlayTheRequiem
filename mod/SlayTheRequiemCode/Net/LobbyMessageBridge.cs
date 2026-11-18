using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace SlayTheRequiem.SlayTheRequiemCode.Net;

/// <summary>
/// BaseLib 3.4.8's NetClientCustomMessagePatches resolves the HOST constructor. Do not rely on that patch to
/// install the client receive handler before RunManager.InitializeShared: the run-settings nonce is needed IN
/// the lobby. Re-register BaseLib's OWN handler, idempotently, on the actual lobby service. Do not add a second
/// dispatcher or swallow an incompatible BaseLib API; TrackStartLobby is critical and startup must report it.
/// Remove this version-specific bridge only together with the pinned dependency/runtime regression.
/// </summary>
internal static class LobbyMessageBridge
{
    private static readonly Action<INetGameService> Unregister = Resolve("Unregister");
    private static readonly Action<INetGameService> Register = Resolve("Register");

    private static Action<INetGameService> Resolve(string name) =>
        (Action<INetGameService>)(AccessTools.DeclaredMethod(typeof(CustomMessageWrapper), name,
            [typeof(INetGameService)]) ?? throw new MissingMethodException(typeof(CustomMessageWrapper).FullName, name))
        .CreateDelegate(typeof(Action<INetGameService>));

    internal static void Ensure(INetGameService service)
    {
        if (service.Type is not (NetGameType.Host or NetGameType.Client)) return;
        Unregister(service); // native Unregister removes every matching delegate and is safe if absent
        Register(service);
        MainFile.Logger.Info($"[RunSettings] lobby custom-message receiver ready service={service.Type}");
    }
}
