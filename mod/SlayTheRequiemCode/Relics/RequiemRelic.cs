using BaseLib.Abstracts;
using BaseLib.Extensions;
using SlayTheRequiem.SlayTheRequiemCode.Art;
using SlayTheRequiem.SlayTheRequiemCode.Extensions;

namespace SlayTheRequiem.SlayTheRequiemCode.Relics;

/// <summary>
/// This is the base class for your mod's relics, which is set up to load the relic's images from your mod's resources.
/// When creating a relic, right click the Relics folder and create a new file with the Custom Relic template.
/// This will generate a class that extends this one.
/// You can also just create the class manually; just make sure to inherit from this class.
/// </summary>
public abstract class RequiemRelic : CustomRelicModel
{
    // Свой значок: images/relics/<id>.png, <id>_outline.png, big/<id>.png;
    // пока своего нет — ванильный по таблице tools/art/vanilla_art.csv (Art/VanillaArt).
    private string ArtName => Id.Entry.RemovePrefix().ToLowerInvariant();
    public override string PackedIconPath => $"{ArtName}.png".RelicImagePath(VanillaArt.RelicIcon(GetType().Name));
    protected override string PackedIconOutlinePath => $"{ArtName}_outline.png".RelicImagePath(VanillaArt.RelicOutline(GetType().Name));
    protected override string BigIconPath =>
        $"{ArtName}.png".BigRelicImagePath(VanillaArt.RelicBigIcon(GetType().Name), VanillaArt.RelicIcon(GetType().Name));
}