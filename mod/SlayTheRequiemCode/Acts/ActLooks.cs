using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Art;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Extensions;

namespace SlayTheRequiem.SlayTheRequiemCode.Acts;

/// <summary>
/// Свои фоны актов (M7 п. 2, <see cref="ActLookRules"/>). Каждая картинка необязательна: нет файла — остаётся ванильный облик
/// акта-основы (LookId), как было до M7. Ванильные сцены (боевой фон, лагерь) не подменяются целиком — их слои прячутся,
/// а наш рисунок встаёт на место фона, так что скрипты игры и предзагрузка (AssetPaths) видят прежние узлы и пути.
/// </summary>
public static class ActLooks
{
    /// <summary>Путь к своей картинке акта (res://…), если художник её положил.</summary>
    public static string? Own(ActKind kind, string piece) =>
        ActLookRules.PiecePath(kind, piece) is { } part && part.ImagePath() is var path && VanillaArt.Exists(path) ? path : null;

    /// <summary>Кусок карты: свой → ванильный акта-основы.</summary>
    public static string MapPiece(ActKind kind, string piece, string vanillaLookId)
    {
        var vanillaName = piece.Replace(".png", $"_{vanillaLookId}.png");
        return Own(kind, piece) ?? ImageHelper.GetImagePath($"packed/map/map_bgs/{vanillaLookId}/{vanillaName}");
    }

    public static Texture2D? Load(string? path) =>
        path == null ? null : ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);

    /// <summary>Слой во весь фон боя: те же якоря и размеры, что у ванильных Layer_xx (центр узла, 2881×1351).</summary>
    public static TextureRect CombatLayer(string name, Texture2D texture)
    {
        var rect = new TextureRect
        {
            Name = name,
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -ActLookRules.CombatHalfWidth,
            OffsetTop = -ActLookRules.CombatHalfHeight,
            OffsetRight = ActLookRules.CombatHalfWidth,
            OffsetBottom = ActLookRules.CombatHalfHeight,
        };
        return rect;
    }

    /// <summary>Бой: прячем ванильные слои (задники, туман, частицы акта-основы), кладём свой задник под всё и передний план поверх.</summary>
    public static void DressCombat(NCombatBackground? background, ActModel? act)
    {
        if (background == null || !GodotObject.IsInstanceValid(background)) return;
        var kind = ActKinds.Of(act);
        if (Load(Own(kind, ActLookRules.CombatBack)) is not { } back) return;
        try
        {
            foreach (var child in background.GetChildren())
                if (child is CanvasItem item) item.Visible = false;
            var backRect = CombatLayer("RequiemActBack", back);
            background.AddChild(backRect);
            background.MoveChild(backRect, 0);
            if (Load(Own(kind, ActLookRules.CombatFore)) is { } fore)
                background.AddChild(CombatLayer("RequiemActFore", fore));
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"ActLooks.DressCombat: {e.Message}");
        }
    }

    /// <summary>Лагерь: наш фон на месте RestSiteBG (RestSiteBG2), костёр и свет — ванильные, тематические узлы акта-основы скрыты.</summary>
    public static void DressCamp(Control? scene, ActModel? act)
    {
        if (scene == null || act == null) return;
        if (Load(Own(ActKinds.Of(act), ActLookRules.Camp)) is not { } camp) return;
        try
        {
            var lookId = act.RestSiteBackgroundPath.Contains("underdocks") ? "underdocks" : "";
            var target = ActLookRules.CampTarget(lookId);
            if (scene.GetNodeOrNull<TextureRect>(target) is not { } bg) return;
            foreach (var child in scene.GetChildren())
                if (child is CanvasItem item && !ActLookRules.CampKeeps(item.Name.ToString(), target))
                    item.Visible = false;
            var r = ActLookRules.CampRect;
            bg.Texture = camp;
            bg.Material = null;
            bg.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            bg.StretchMode = TextureRect.StretchModeEnum.Scale;
            bg.SetPosition(new Vector2(r[0], r[1]));
            bg.SetSize(new Vector2(r[2] - r[0], r[3] - r[1]));
            bg.Visible = true;
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"ActLooks.DressCamp: {e.Message}");
        }
    }

    /// <summary>Сундук: ванильная комната act2/act3 в цвете нашего акта и тёмный фон вокруг.</summary>
    public static void DressChest(Node? room, IRunState? run)
    {
        if (room == null || ActLookRules.ChestTint(ActKinds.Current(run)) is not { } tint) return;
        try
        {
            if (room.GetNodeOrNull<ColorRect>("ColorRect") is { } backdrop) backdrop.Color = new Color(tint.Backdrop);
            if (room.GetNodeOrNull<CanvasItem>("Chest/ChestVisual") is { } chest) chest.Modulate = new Color(tint.R, tint.G, tint.B);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"ActLooks.DressChest: {e.Message}");
        }
    }
}

/// <summary>Боевой фон акта Реквиема — свой задник и передний план (у встреч со своим фоном игры тоже: акт важнее).</summary>
[HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.CreateBackground))]
public static class ActCombatLookPatch
{
    [HarmonyPostfix]
    public static void Postfix(NCombatBackground __result, ActModel parentAct) => ActLooks.DressCombat(__result, parentAct);
}

/// <summary>Сцена лагеря акта Реквиема — свой фон поляны.</summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.CreateRestSiteBackground))]
public static class ActCampLookPatch
{
    [HarmonyPostfix]
    public static void Postfix(ActModel __instance, Control __result) => ActLooks.DressCamp(__result, __instance);
}

/// <summary>Комната сундука акта Реквиема — подкраска.</summary>
[HarmonyPatch(typeof(NTreasureRoom), nameof(NTreasureRoom._Ready))]
public static class ActChestLookPatch
{
    private static readonly AccessTools.FieldRef<NTreasureRoom, IRunState> RunStateRef =
        AccessTools.FieldRefAccess<NTreasureRoom, IRunState>("_runState");

    [HarmonyPostfix]
    public static void Postfix(NTreasureRoom __instance) => ActLooks.DressChest(__instance, RunStateRef(__instance));
}
