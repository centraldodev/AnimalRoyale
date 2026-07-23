using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    public static class AnimalVisualFactory
    {
        private static AnimalPackageCatalog catalog;

        public static Transform Build(Transform parent, AnimalType type, Color mainColor, Vector3 scale)
        {
            Transform existing = parent.Find("VisualRoot");
            if (existing != null)
            {
                if (Application.isPlaying) Object.Destroy(existing.gameObject);
                else Object.DestroyImmediate(existing.gameObject);
            }

            GameObject visualRootObject = new GameObject("VisualRoot");
            Transform visualRoot = visualRootObject.transform;
            visualRoot.SetParent(parent, false);
            visualRoot.localScale = scale;

            catalog ??= Resources.Load<AnimalPackageCatalog>("AnimalPackageCatalog");
            GameObject prefab = catalog != null ? catalog.GetPrefab(type) : null;
            if (prefab == null)
            {
                Debug.LogError($"Modelo do pacote não configurado para {type}. Verifique AnimalPackageCatalog.");
                BuildMissingModelMarker(visualRoot, mainColor);
            }
            else
            {
                GameObject instance = Object.Instantiate(prefab, visualRoot, false);
                instance.name = type + "_PackageModel";
                instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                DisablePackageGameplayComponents(instance);
                ConfigureRenderers(instance);
                // Weapon is now sculpted directly into the hand/back mesh (matching how the
                // Cow model already carries its milk guns) instead of being a separate
                // attached prop — only a muzzle marker is needed for projectile spawning.
                AttachWeaponMuzzle(instance.transform, type);
            }

            visualRootObject.AddComponent<AnimalVisualMotion>().Initialize(type);
            return visualRoot;
        }

        private static void DisablePackageGameplayComponents(GameObject instance)
        {
            CharacterController nestedController = instance.GetComponent<CharacterController>();
            if (nestedController != null) nestedController.enabled = false;

            // The Asset Store prefabs include their own input/movement demonstration
            // scripts. The battle-royale root owns movement, physics and camera input.
            MonoBehaviour[] behaviours = instance.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour != null) behaviour.enabled = false;
            }

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                if (collider != null) collider.enabled = false;
            }
        }

        private static void ConfigureRenderers(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null) material.enableInstancing = true;
                }
            }
        }

        // The gun is now sculpted directly into the hand (Tiger/Ant/Monkey) or back (Eagle,
        // which needs both hands free for wings) mesh instead of being a separate attached
        // prop, so all that's needed is a lightweight marker for where projectiles spawn from.
        private static void AttachWeaponMuzzle(Transform modelRoot, AnimalType type)
        {
            GameObject muzzleObject = new GameObject("WeaponMuzzle");
            Transform muzzle = muzzleObject.transform;
            WeaponMuzzleSocket socket = muzzleObject.AddComponent<WeaponMuzzleSocket>();
            socket.ForwardReference = modelRoot;

            if (type == AnimalType.Eagle)
            {
                // Weapon rides on the back near the shoulder, not in a hand/talon — fixed
                // offset from modelRoot (right=x, up=y, forward=z, character-relative).
                muzzle.SetParent(modelRoot, false);
                muzzle.localPosition = new Vector3(0f, 1.25f, 0.55f);
                return;
            }

            Transform hand = FindBone(modelRoot, "R_Hand");
            if (hand == null)
            {
                muzzle.SetParent(modelRoot, false);
                muzzle.localPosition = new Vector3(0f, 1f, 0.5f);
                return;
            }

            // R_Hand is already pinned rigid by AnimalVisualMotion's arm-swing override, so
            // (unlike the old separate-prop socket) the muzzle needs no further stabilizing —
            // it just rides along at a fixed offset from the hand. That offset is authored in
            // character-relative space (right/up/forward from modelRoot) and converted into
            // the hand bone's own local space here, since the hand's local axes are rolled/
            // twisted relative to the body.
            muzzle.SetParent(hand, false);
            Vector3 worldOffset = modelRoot.TransformDirection(WeaponMuzzleHandOffset(type));
            muzzle.localPosition = hand.InverseTransformDirection(worldOffset);
        }

        private static Vector3 WeaponMuzzleHandOffset(AnimalType type) => type switch
        {
            AnimalType.Ant => new Vector3(0f, 0.02f, 0.28f),
            AnimalType.Monkey => new Vector3(0f, 0.02f, 0.30f),
            _ => new Vector3(0f, 0.02f, 0.32f)
        };

        private static Transform FindBone(Transform root, string boneName)
        {
            if (root.name == boneName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindBone(root.GetChild(i), boneName);
                if (found != null) return found;
            }
            return null;
        }

        private static void BuildMissingModelMarker(Transform root, Color color)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.name = "MissingPackageModel";
            marker.transform.SetParent(root, false);
            marker.transform.localPosition = Vector3.up * 0.5f;
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer == null) return;
            Shader shader = ShaderLibrary.Lit;
            renderer.sharedMaterial = new Material(shader) { color = color };
        }
    }

    /// <summary>Marks where projectiles spawn from — the tip of the gun sculpted into the
    /// model itself. No locking/following logic needed here (unlike the old separate-prop
    /// socket): for Tiger/Ant/Monkey the parent hand bone is already pinned rigid by
    /// AnimalVisualMotion's arm-swing override, and Eagle's is a fixed offset from modelRoot,
    /// so this transform's position is already stable by construction.</summary>
    public sealed class WeaponMuzzleSocket : MonoBehaviour
    {
        public Transform ForwardReference;

        public Vector3 MuzzlePosition => transform.position;
        public Quaternion MuzzleRotation => ForwardReference != null ? ForwardReference.rotation : transform.rotation;
    }
}
