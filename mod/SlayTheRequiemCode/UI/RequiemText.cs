using System.Text.RegularExpressions;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

public static class RequiemText
{
    public static string Plain(string text) => Regex.Replace(text.Replace("[star]", "✦").Replace("[energy:1]", "⚡"), @"\[[^\]]+\]", "");
    public static string Branch(SkillBranch branch) => RequiemUi.TipText($"UI.branch.{branch}.title");
}
