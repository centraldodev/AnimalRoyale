using UnityEditor;
using UnityEngine;

namespace AnimalBattleRoyale.EditorTools
{
    /// <summary>
    /// One-off diagnostic for the user-supplied pickup/prop FBX (cura, municao, diamante3D,
    /// TunnelHole) that aren't showing up in-game. Instantiates each Resources prefab in
    /// edit mode, logs whether it has renderers and what their real bounds/material are,
    /// then destroys it — so the cause (missing resource, no renderer, tiny/huge bounds,
    /// missing material) shows up in the log without needing to press Play.
    /// </summary>
    [InitializeOnLoad]
    public static class PickupAssetDiagnostics
    {
        private static readonly string[] Paths =
        {
            "Pickups/Diamante/diamante3D",
            "Pickups/Municao/municao",
            "Pickups/Cura/cura",
            "Environment/TunnelHole",
        };

        static PickupAssetDiagnostics()
        {
            EditorApplication.delayCall += Run;
        }

        [MenuItem("AnimalBattleRoyale/Diagnose Pickup Assets")]
        private static void Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }

            foreach (string path in Paths)
            {
                GameObject prefab = Resources.Load<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[PickupDiag] {path}: Resources.Load retornou null (asset ausente ou caminho errado).");
                    continue;
                }

                GameObject instance = Object.Instantiate(prefab);
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    Debug.LogWarning($"[PickupDiag] {path}: instanciou mas SEM nenhum Renderer (childCount={instance.transform.childCount}).");
                }
                else
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    string materialName = renderers[0].sharedMaterial != null ? renderers[0].sharedMaterial.name : "null";
                    Debug.Log($"[PickupDiag] {path}: {renderers.Length} renderer(s), boundsSize={bounds.size}, " +
                              $"boundsCenter={bounds.center}, renderer0.enabled={renderers[0].enabled}, material0={materialName}.");
                }
                Object.DestroyImmediate(instance);
            }

            DiagnoseLive("Diamante", WeaponUpgradeCrystal.Create(Vector3.zero).gameObject);
            DiagnoseLive("Cura", FoodPickup.Create(Vector3.zero, FoodKind.Fruit).gameObject);
            DiagnoseLive("Municao", RangedAmmoPickup.Create(Vector3.zero, RangedSupplyKind.NaturalAmmo).gameObject);
            DiagnoseLive("TunnelEntrance", AntTunnelEntrance.Create(Vector3.zero,
                new Material(ShaderLibrary.Lit), new Material(ShaderLibrary.Lit)).gameObject);
        }

        // Runs the real gameplay factory (same code path JungleGenerator uses) and reports
        // what actually ends up in the hierarchy: active state and LODGroup culling included,
        // which the raw-prefab check above can't see.
        private static void DiagnoseLive(string label, GameObject root)
        {
            if (root == null)
            {
                Debug.LogWarning($"[PickupDiag] {label}: Create() retornou null.");
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[PickupDiag] {label}: hierarquia criada mas SEM Renderer nenhum.");
            }
            else
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                foreach (Renderer r in renderers)
                {
                    bool activeInHierarchy = r.gameObject.activeInHierarchy;
                    Debug.Log($"[PickupDiag] {label}/{r.name}: activeInHierarchy={activeInHierarchy}, " +
                              $"renderer.enabled={r.enabled}, localScale={r.transform.localScale}, worldPos={r.transform.position}, " +
                              $"material={(r.sharedMaterial != null ? r.sharedMaterial.name : "null")}.");
                }
                Debug.Log($"[PickupDiag] {label}: combined boundsSize={bounds.size}, boundsCenter={bounds.center}, rootActive={root.activeSelf}, rootWorldPos={root.transform.position}.");

                foreach (LODGroup lodGroup in root.GetComponentsInChildren<LODGroup>(true))
                {
                    LOD[] lods = lodGroup.GetLODs();
                    Debug.Log($"[PickupDiag] {label}: LODGroup enabled={lodGroup.enabled}, lodCount={lods.Length}, " +
                              $"lastLodScreenHeight={(lods.Length > 0 ? lods[lods.Length - 1].screenRelativeTransitionHeight : -1f)}.");
                }
            }
            Object.DestroyImmediate(root);
        }
    }
}
