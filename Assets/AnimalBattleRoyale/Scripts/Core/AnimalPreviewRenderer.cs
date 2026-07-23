using UnityEngine;

namespace AnimalBattleRoyale
{
    public static class AnimalPreviewRenderer
    {
        private const int PreviewLayer = 31;

        public static RenderTexture Create(AnimalType type, int size = 256)
        {
            AnimalPackageCatalog catalog = Resources.Load<AnimalPackageCatalog>("AnimalPackageCatalog");
            GameObject prefab = catalog != null ? catalog.GetPrefab(type) : null;
            if (prefab == null) return null;

            Vector3 stagePosition = new Vector3((int)type * 20f, -600f, 0f);
            GameObject model = Object.Instantiate(prefab);
            model.name = type + "_MenuPreview";
            model.transform.position = stagePosition;
            model.transform.localScale = AnimalDefinition.Get(type).VisualScale;
            SetLayer(model.transform, PreviewLayer);
            foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;

            Animator animator = model.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.Update(0f);
            }

            Bounds bounds = CalculateBounds(model, stagePosition);
            float radius = Mathf.Max(0.45f, bounds.extents.magnitude);

            GameObject cameraObject = new GameObject(type + "_PreviewCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.cullingMask = 1 << PreviewLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.025f, 0.027f, 0f);
            camera.fieldOfView = 28f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = radius * 8f + 10f;
            Vector3 target = bounds.center + Vector3.up * (bounds.extents.y * 0.05f);
            camera.transform.position = target + new Vector3(radius * 0.18f, radius * 0.08f, radius * 3.25f);
            camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);

            GameObject lightObject = new GameObject(type + "_PreviewLight");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.55f;
            light.color = new Color(1f, 0.9f, 0.76f);
            light.cullingMask = 1 << PreviewLayer;
            light.transform.rotation = Quaternion.Euler(38f, -28f, 0f);

            RenderTexture texture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
            {
                name = type + "_PackagePortrait",
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear
            };
            texture.Create();
            camera.targetTexture = texture;
            AnimalPreviewLighting.RenderWithNeutralEnvironment(camera);
            camera.targetTexture = null;

            model.SetActive(false);
            cameraObject.SetActive(false);
            lightObject.SetActive(false);
            Object.Destroy(model);
            Object.Destroy(cameraObject);
            Object.Destroy(lightObject);
            return texture;
        }

        public static void Release(RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            Object.Destroy(texture);
        }

        private static Bounds CalculateBounds(GameObject model, Vector3 fallbackCenter)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(fallbackCenter + Vector3.up, Vector3.one * 2f);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i), layer);
        }
    }
}
