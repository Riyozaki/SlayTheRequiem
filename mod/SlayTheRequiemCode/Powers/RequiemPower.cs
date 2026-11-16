using BaseLib.Abstracts;
using BaseLib.Extensions;
using SlayTheRequiem.SlayTheRequiemCode.Art;
using SlayTheRequiem.SlayTheRequiemCode.Extensions;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

/// <summary>
/// This is the base class for your mod's powers, which is set up to load the power's images from your mod's resources.
/// When creating a power, right click the Powers folder and create a new file with the Custom Power template.
/// This will generate a class that extends this one.
/// You can also just create the class manually; just make sure to inherit from this class.
/// </summary>
public abstract class RequiemPower : CustomPowerModel
{
    // Свой значок: images/powers/<id>.png (64x64) и images/powers/big/<id>.png (256x256);
    // пока своего нет — ванильный по таблице tools/art/vanilla_art.csv (Art/VanillaArt).
    private string ArtFile => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png";
    public override string CustomPackedIconPath => ArtFile.PowerImagePath(VanillaArt.PowerIcon(GetType().Name));
    public override string CustomBigIconPath =>
        ArtFile.BigPowerImagePath(VanillaArt.PowerBigIcon(GetType().Name), VanillaArt.PowerIcon(GetType().Name));

    /// <summary>
    /// Whether this power is a buff or debuff.
    /// </summary>
    public abstract override PowerType Type { get; }
    
    /// <summary>
    /// How this power stacks if reapplied. Counter is the most common type, where applying the power again just
    /// adds to the amount. Single means the power does not stack, like Barricade. None functions identically to
    /// Single, but you're suggested to use Single as it is more explicit about how it will work.
    /// </summary>
    public abstract override PowerStackType StackType { get; }
}