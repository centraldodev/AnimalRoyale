using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    /// <summary>An objective crystal carried by a fighter and dropped on death.</summary>
    public sealed class DiamondPickup : MonoBehaviour
    {
        private static readonly List<DiamondPickup> activePickups = new List<DiamondPickup>();
        private static Material diamondMaterial;
        private static Material baseMaterial;
        private static int nextMotionGroup;

        private Vector3 basePosition;
        private bool available = true;
        private int motionGroup;

        public static IReadOnlyList<DiamondPickup> ActivePickups => activePickups;
        public bool IsAvailable => available;

        private void OnEnable()
        {
            if (!activePickups.Contains(this)) activePickups.Add(this);
        }

        private void Awake()
        {
            motionGroup = nextMotionGroup++ & 1;
        }

        private void OnDisable()
        {
            activePickups.Remove(this);
        }

        public static DiamondPickup Create(Vector3 groundPosition)
        {
            GameObject root = new GameObject("ObjectiveDiamond");
            root.transform.position = groundPosition + Vector3.up * 0.85f;
            DiamondPickup pickup = root.AddComponent<DiamondPickup>();
            pickup.basePosition = root.transform.position;
            pickup.BuildVisual();
            return pickup;
        }

        public static bool TryCollectNearest(ThirdPersonAnimalController animal)
        {
            if (animal == null || animal.IsDefeated || animal.Health.IsDead) return false;
            DiamondPickup nearest = null;
            float nearestSqrDistance = 2.25f * 2.25f;
            foreach (DiamondPickup pickup in activePickups)
            {
                if (pickup == null || !pickup.available) continue;
                float sqrDistance = (pickup.transform.position - animal.transform.position).sqrMagnitude;
                if (sqrDistance >= nearestSqrDistance) continue;
                nearest = pickup;
                nearestSqrDistance = sqrDistance;
            }
            if (nearest == null) return false;
            return nearest.Collect(animal);
        }

        public static DiamondPickup FindClosest(Vector3 position)
        {
            DiamondPickup closest = null;
            float closestSqrDistance = float.MaxValue;
            foreach (DiamondPickup pickup in activePickups)
            {
                if (pickup == null || !pickup.available) continue;
                float sqrDistance = (pickup.transform.position - position).sqrMagnitude;
                if (sqrDistance >= closestSqrDistance) continue;
                closest = pickup;
                closestSqrDistance = sqrDistance;
            }
            return closest;
        }

        public void Relocate(Vector3 groundPosition)
        {
            basePosition = groundPosition + Vector3.up * 0.85f;
            transform.position = basePosition;
            AttackVfx.CreateBurst(transform.position, new Color(0.2f, 0.82f, 1f), 1.25f);
        }

        private bool Collect(ThirdPersonAnimalController animal)
        {
            if (!available || DiamondObjectiveManager.Instance == null) return false;
            available = false;
            DiamondObjectiveManager.Instance.Collect(animal, this);
            return true;
        }

        public void RemoveFromWorld()
        {
            available = false;
            Destroy(gameObject);
        }

        private void Update()
        {
            if ((Time.frameCount & 1) != motionGroup) return;
            transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * 2.8f + basePosition.x) * 0.22f);
            transform.Rotate(0f, 164f * Time.deltaTime, 0f, Space.World);
        }

        private void BuildVisual()
        {
            EnsureMaterials();
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "DiamondPedestal";
            pedestal.transform.SetParent(transform, false);
            pedestal.transform.localPosition = Vector3.down * 0.62f;
            pedestal.transform.localScale = new Vector3(0.52f, 0.09f, 0.52f);
            pedestal.GetComponent<Renderer>().sharedMaterial = baseMaterial;
            DisableCollider(pedestal);

            GameObject diamond = new GameObject("KeyDiamond");
            diamond.transform.SetParent(transform, false);
            diamond.transform.localScale = new Vector3(0.78f, 1.42f, 0.78f);
            diamond.transform.localRotation = Quaternion.Euler(-7f, 18f, 8f);
            diamond.AddComponent<MeshFilter>().sharedMesh = JungleGenerator.GetCrystalMesh();
            MeshRenderer renderer = diamond.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = diamondMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            CollectibleHighlight.Attach(transform, new Color(0.18f, 0.86f, 1f), 1.05f, -0.46f);

            GameObject label = new GameObject("DiamondLabel");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = Vector3.up * 1.35f;
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = "F  CRISTAL-CHAVE";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.042f;
            text.fontSize = 50;
            text.color = new Color(0.35f, 0.92f, 1f);
            label.AddComponent<PickupLabel>();
        }

        private static void EnsureMaterials()
        {
            if (diamondMaterial != null && baseMaterial != null) return;
            Shader shader = ShaderLibrary.Lit;
            diamondMaterial = new Material(shader) { color = new Color(0.08f, 0.72f, 1f), enableInstancing = true };
            if (diamondMaterial.HasProperty("_BaseColor")) diamondMaterial.SetColor("_BaseColor", new Color(0.08f, 0.72f, 1f));
            if (diamondMaterial.HasProperty("_EmissionColor"))
            {
                diamondMaterial.EnableKeyword("_EMISSION");
                diamondMaterial.SetColor("_EmissionColor", new Color(0.04f, 0.42f, 1f) * 1.8f);
            }
            baseMaterial = new Material(shader) { color = new Color(0.08f, 0.18f, 0.25f), enableInstancing = true };
            if (baseMaterial.HasProperty("_BaseColor")) baseMaterial.SetColor("_BaseColor", baseMaterial.color);
        }

        private static void DisableCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }
    }
}
