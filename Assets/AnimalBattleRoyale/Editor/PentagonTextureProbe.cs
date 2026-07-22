using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    // Temporary diagnostic: RuntimeGuiTheme.PentagonTexture/PentagonRingTexture are plain
    // Texture2D objects (no camera/GameObject needed to inspect them) — just encode them to
    // PNG directly to check the signed-distance-field pentagon shape actually looks right.
    public static class PentagonTextureProbe
    {
        [MenuItem("AnimalBattleRoyale/Debug/Probe Pentagon Textures")]
        public static void Probe()
        {
            string scratchDir = "/private/tmp/claude-501/-Users-macbookpro-Dev-AnimalBattleRoyale-UnityStarter/42aec13b-9a36-4999-aabb-39584a6059f3/scratchpad";
            Directory.CreateDirectory(scratchDir);

            File.WriteAllBytes(Path.Combine(scratchDir, "heart_fill.png"), RuntimeGuiTheme.HeartTexture.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(scratchDir, "heart_ring.png"), RuntimeGuiTheme.HeartRingTexture.EncodeToPNG());
            Debug.Log("[PentagonTextureProbe] Saved.");
        }
    }
}
