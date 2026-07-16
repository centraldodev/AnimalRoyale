using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Scattered life orb. Pressing the consume key next to one fully restores (100%)
    /// the animal's health. Uses the imported LifeOrb model (Resources/Pickups/LifeOrb).
    /// </summary>
    public sealed class LifePickup : MonoBehaviour
    {
        private const float CollectRange = 2.0f;
        private const float SpinDegreesPerSecond = 24f;
        private static readonly Color OrbColor = new Color(0.4f, 0.92f, 0.5f);
        private static readonly List<LifePickup> activePickups = new List<LifePickup>();
        private static GameObject cachedPrefab;
        private static bool prefabLookedUp;

        private Vector3 basePosition;
        private bool collected;
        private float respawnAt;
        private GameObject visualObject;
        private GameObject labelObject;
        private GameObject highlightObject;
        private int motionGroup;
        private static int nextMotionGroup;

        public static IReadOnlyList<LifePickup> ActivePickups => activePickups;
        public bool IsAvailable => !collected;

        private void Awake() => motionGroup = nextMotionGroup++ & 1;
        private void OnEnable() { if (!activePickups.Contains(this)) activePickups.Add(this); }
        private void OnDisable() => activePickups.Remove(this);

        public static LifePickup Create(Vector3 position)
        {
            GameObject root = new GameObject("LifeOrb");
            root.transform.position = position + Vector3.up * 0.75f;
            LifePickup pickup = root.AddComponent<LifePickup>();
            pickup.basePosition = root.transform.position;
            pickup.BuildVisual();
            return pickup;
        }

        private void Update()
        {
            if ((Time.frameCount & 1) != motionGroup) return;
            if (collected)
            {
                if (Time.time < respawnAt) return;
                collected = false;
                SetVisualsActive(true);
                AttackVfx.CreateBurst(basePosition, OrbColor, 1.2f);
            }

            transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * 2.2f + basePosition.x) * 0.16f);
            transform.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }

        public static bool TryConsumeNearest(ThirdPersonAnimalController animal)
        {
            if (animal == null || animal.IsDefeated || animal.Health.IsDead) return false;
            LifePickup nearest = null;
            float nearestSqr = CollectRange * CollectRange;
            foreach (LifePickup pickup in activePickups)
            {
                if (pickup == null || pickup.collected || !pickup.CanBenefit(animal)) continue;
                float sqr = (pickup.transform.position - animal.transform.position).sqrMagnitude;
                if (sqr < nearestSqr) { nearest = pickup; nearestSqr = sqr; }
            }
            if (nearest == null) return false;
            nearest.Collect(animal);
            return true;
        }

        private bool CanBenefit(ThirdPersonAnimalController animal)
            => animal.Health.CurrentHealth < animal.Health.MaxHealth - 0.5f;

        private void Collect(ThirdPersonAnimalController animal)
        {
            collected = true;
            animal.Health.Heal(animal.Health.MaxHealth); // clamps to max -> full 100% heal
            AttackVfx.CreateBurst(transform.position, OrbColor, 2.0f);
            CombatFeedback.PlayFoodPickup(transform.position);
            respawnAt = Time.time + Countdown.DurationSeconds;
            SetVisualsActive(false);
            Countdown.Spawn(basePosition, Countdown.DurationSeconds);
        }

        private void BuildVisual()
        {
            if (!prefabLookedUp)
            {
                cachedPrefab = Resources.Load<GameObject>("Pickups/LifeOrb");
                prefabLookedUp = true;
            }

            if (cachedPrefab != null)
            {
                GameObject model = Instantiate(cachedPrefab, transform, false);
                model.name = "LifeOrbModel";
                model.transform.localPosition = Vector3.zero;
                visualObject = model;
                PrepareImportedVisual(model);
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                    if (collider != null) collider.enabled = false;
            }
            else
            {
                // Fallback if the model is missing: a simple green orb.
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(transform, false);
                sphere.transform.localScale = Vector3.one * 0.9f;
                Collider col = sphere.GetComponent<Collider>();
                if (col != null) col.enabled = false;
                Renderer renderer = sphere.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = new Material(ShaderLibrary.Lit) { color = OrbColor };
                visualObject = sphere;
            }

            CollectibleHighlight highlight = CollectibleHighlight.Attach(transform, OrbColor, 1.05f, -0.2f);
            highlightObject = highlight != null ? highlight.gameObject : null;

            GameObject label = new GameObject("Label");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = Vector3.up * 1.15f;
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = "F  VIDA\n+100% VIDA";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.045f;
            text.fontSize = 48;
            text.color = OrbColor;
            label.AddComponent<PickupLabel>();
            labelObject = label;
        }

        private void PrepareImportedVisual(GameObject model)
        {
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
                child.gameObject.SetActive(true);
            foreach (LODGroup lodGroup in model.GetComponentsInChildren<LODGroup>(true))
                lodGroup.enabled = false;

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = true;
                renderer.forceRenderingOff = false;
                bounds.Encapsulate(renderer.bounds);
            }
            model.transform.position += transform.position - bounds.center;
        }

        private void SetVisualsActive(bool value)
        {
            if (visualObject != null) visualObject.SetActive(value);
            if (labelObject != null) labelObject.SetActive(value);
            if (highlightObject != null) highlightObject.SetActive(value);
        }
    }
}
