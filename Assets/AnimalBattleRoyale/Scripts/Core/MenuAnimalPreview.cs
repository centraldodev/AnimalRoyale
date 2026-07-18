using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Maintains a live, map-independent character preview for the main menu.
    /// The package model is kept in its imported rest pose so its rig remains
    /// available without playing any of the old animation content.
    /// </summary>
    public sealed class MenuAnimalPreview : MonoBehaviour, IDisposable
    {
        private const int PreviewLayer = 31;
        // Rendering layer 7 is intentionally separate from the default layer 0,
        // keeping the map lights from changing the menu portrait.
        private const uint PreviewRenderingLayer = 1u << 7;
        private const float StageSpacing = 200f;

        private static int nextStageSlot;

        [SerializeField] private AnimalType selectedAnimal = AnimalType.Tiger;
        [SerializeField, Min(128)] private int textureWidth = 640;
        [SerializeField, Min(192)] private int textureHeight = 960;
        [SerializeField] private bool autoRotate = true;
        [SerializeField, Range(0f, 40f)] private float rotationSpeed = 10f;

        private AnimalPackageCatalog catalog;
        private GameObject stageRoot;
        private Transform turntable;
        private GameObject currentModel;
        private Camera previewCamera;
        private Light keyLight;
        private Light fillLight;
        private RenderTexture renderTexture;
        private AnimalType instantiatedAnimal;
        private bool hasInstantiatedAnimal;
        private bool disposed;

        /// <summary>The texture intended for a GUI.DrawTexture or RawImage.</summary>
        public Texture Texture
        {
            get
            {
                EnsureReady();
                return renderTexture;
            }
        }

        public RenderTexture RenderTexture
        {
            get
            {
                EnsureReady();
                return renderTexture;
            }
        }

        public AnimalType SelectedAnimal => selectedAnimal;
        public bool IsReady => currentModel != null && renderTexture != null && renderTexture.IsCreated();

        public bool AutoRotate
        {
            get => autoRotate;
            set
            {
                autoRotate = value;
                if (!autoRotate) RenderNow();
            }
        }

        public float RotationSpeed
        {
            get => rotationSpeed;
            set => rotationSpeed = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Creates the isolated preview stage. Height is always kept greater
        /// than width so the result remains a full-body portrait texture.
        /// </summary>
        public void Initialize(AnimalType initialAnimal, int textureWidth = 640,
            int textureHeight = 960, bool rotate = true)
        {
            if (disposed) return;

            this.textureWidth = Mathf.Max(128, textureWidth);
            this.textureHeight = Mathf.Max(this.textureWidth + 1, textureHeight);
            autoRotate = rotate;
            selectedAnimal = initialAnimal;

            if (!Application.isPlaying || !EnsureInfrastructure()) return;
            RecreateTextureIfNeeded();
            SetAnimal(initialAnimal);
        }

        /// <summary>Replaces only the model; camera, lights and texture are reused.</summary>
        public bool SetAnimal(AnimalType type)
        {
            if (disposed || !IsValidAnimal(type)) return false;
            selectedAnimal = type;
            if (!Application.isPlaying) return false;
            if (!EnsureInfrastructure()) return false;
            RecreateTextureIfNeeded();

            if (hasInstantiatedAnimal && instantiatedAnimal == type && currentModel != null)
            {
                RenderNow();
                return true;
            }

            return BuildAnimal(type);
        }

        private void LateUpdate()
        {
            if (!autoRotate || turntable == null || currentModel == null || previewCamera == null) return;
            turntable.Rotate(Vector3.up, rotationSpeed * Time.unscaledDeltaTime, Space.World);
            RenderNow();
        }

        public void RenderNow()
        {
            if (disposed || previewCamera == null || currentModel == null || renderTexture == null
                || !renderTexture.IsCreated()) return;

            previewCamera.targetTexture = renderTexture;
            previewCamera.Render();
        }

        private void EnsureReady()
        {
            if (disposed || !Application.isPlaying) return;
            if (!EnsureInfrastructure()) return;
            RecreateTextureIfNeeded();
            if (currentModel == null) BuildAnimal(selectedAnimal);
        }

        private bool EnsureInfrastructure()
        {
            if (disposed || !Application.isPlaying) return false;
            if (stageRoot != null && previewCamera != null) return catalog != null;

            catalog = Resources.Load<AnimalPackageCatalog>("AnimalPackageCatalog");
            if (catalog == null)
            {
                Debug.LogError("Catálogo dos animais não encontrado para o preview do menu.", this);
                return false;
            }

            int stageSlot = nextStageSlot++;
            Vector3 stagePosition = new Vector3(stageSlot * StageSpacing, -4000f, 0f);

            stageRoot = new GameObject($"MenuAnimalPreviewStage_{stageSlot}")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };
            stageRoot.transform.position = stagePosition;

            GameObject turntableObject = new GameObject("AnimalTurntable") { layer = PreviewLayer };
            turntable = turntableObject.transform;
            turntable.SetParent(stageRoot.transform, false);

            GameObject cameraObject = new GameObject("PreviewCamera") { layer = PreviewLayer };
            cameraObject.transform.SetParent(stageRoot.transform, false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.fieldOfView = 27f;
            previewCamera.nearClipPlane = 0.03f;
            previewCamera.allowHDR = true;
            previewCamera.allowMSAA = true;
            CartoonColorGrading.Ensure(previewCamera, 1.3f, 1.08f, 0.96f);

            keyLight = CreateSpotLight("PreviewKeyLight", new Color(1f, 0.94f, 0.84f), 6f, 58f);
            fillLight = CreateSpotLight("PreviewFillLight", new Color(0.68f, 0.82f, 1f), 3f, 68f);
            return true;
        }

        private Light CreateSpotLight(string objectName, Color color, float intensity, float angle)
        {
            GameObject lightObject = new GameObject(objectName) { layer = PreviewLayer };
            lightObject.transform.SetParent(stageRoot.transform, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.7f;
            light.cullingMask = 1 << PreviewLayer;
            light.renderingLayerMask = unchecked((int)PreviewRenderingLayer);
            light.shadows = LightShadows.None;
            return light;
        }

        private bool BuildAnimal(AnimalType type)
        {
            GameObject prefab = catalog.GetPrefab(type);
            if (prefab == null)
            {
                Debug.LogError($"Modelo de {type} não configurado no AnimalPackageCatalog.", this);
                return false;
            }

            DestroyCurrentModel();
            turntable.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            currentModel = Instantiate(prefab, stageRoot.transform, false);
            currentModel.name = type + "_MenuModel";
            currentModel.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            currentModel.transform.localScale = AnimalDefinition.Get(type).VisualScale;
            SetLayerAndRenderingMask(currentModel.transform);
            FreezePackage(currentModel);

            Bounds bounds = CalculateBounds(currentModel, stageRoot.transform.position + Vector3.up);

            // Reparent around the visual center so rotation does not make an
            // off-center package orbit through the portrait.
            Vector3 stagePosition = stageRoot.transform.position;
            turntable.position = new Vector3(bounds.center.x, stagePosition.y, bounds.center.z);
            currentModel.transform.SetParent(turntable, true);
            turntable.localRotation = Quaternion.identity;

            FrameAnimal(bounds);
            instantiatedAnimal = type;
            hasInstantiatedAnimal = true;
            RenderNow();
            return true;
        }

        private static void FreezePackage(GameObject model)
        {
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = null;
                animator.enabled = false;
            }

            foreach (Behaviour behaviour in model.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour != null) behaviour.enabled = false;
            }

            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                if (collider != null) collider.enabled = false;
            }

            foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            foreach (AudioSource source in model.GetComponentsInChildren<AudioSource>(true)) source.Stop();
            foreach (ParticleSystem particles in model.GetComponentsInChildren<ParticleSystem>(true))
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void FrameAnimal(Bounds bounds)
        {
            float aspect = textureWidth / (float)textureHeight;
            float halfVerticalFov = Mathf.Tan(previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float verticalDistance = bounds.extents.y / Mathf.Max(0.01f, halfVerticalFov);
            float horizontalDistance = bounds.extents.x / Mathf.Max(0.01f, halfVerticalFov * aspect);
            float framingDistance = Mathf.Max(verticalDistance,
                Mathf.Min(horizontalDistance, verticalDistance * 1.22f));
            float depthAllowance = Mathf.Min(bounds.extents.z, bounds.extents.y * 0.35f);
            float distance = (framingDistance + depthAllowance) * 1.02f;
            distance = Mathf.Max(1.5f, distance);

            Vector3 target = bounds.center + Vector3.up * bounds.size.y * 0.015f;
            previewCamera.transform.position = target + new Vector3(0f, bounds.size.y * 0.015f, distance);
            previewCamera.transform.rotation = Quaternion.LookRotation(target - previewCamera.transform.position, Vector3.up);
            previewCamera.farClipPlane = distance + bounds.extents.magnitude * 5f + 5f;

            float lightRange = Mathf.Max(5f, distance * 1.55f + bounds.extents.magnitude);
            PositionSpotLight(keyLight, target + new Vector3(-distance * 0.52f, distance * 0.6f, distance * 0.72f),
                target, lightRange);
            PositionSpotLight(fillLight, target + new Vector3(distance * 0.65f, distance * 0.18f, distance * 0.5f),
                target, lightRange);
        }

        private static void PositionSpotLight(Light light, Vector3 position, Vector3 target, float range)
        {
            light.transform.position = position;
            light.transform.rotation = Quaternion.LookRotation(target - position, Vector3.up);
            light.range = range;
        }

        private void RecreateTextureIfNeeded()
        {
            if (renderTexture != null && renderTexture.width == textureWidth
                                      && renderTexture.height == textureHeight && renderTexture.IsCreated()) return;

            ReleaseTexture();
            renderTexture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "MenuAnimalPreviewTexture",
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            renderTexture.Create();
            previewCamera.targetTexture = renderTexture;
        }

        private void DestroyCurrentModel()
        {
            hasInstantiatedAnimal = false;
            if (currentModel == null) return;
            currentModel.SetActive(false);
            DestroyUnityObject(currentModel);
            currentModel = null;
        }

        private void ReleaseTexture()
        {
            if (previewCamera != null) previewCamera.targetTexture = null;
            if (renderTexture == null) return;
            renderTexture.Release();
            DestroyUnityObject(renderTexture);
            renderTexture = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            DestroyCurrentModel();
            ReleaseTexture();
            if (stageRoot != null)
            {
                stageRoot.SetActive(false);
                DestroyUnityObject(stageRoot);
            }

            stageRoot = null;
            turntable = null;
            previewCamera = null;
            keyLight = null;
            fillLight = null;
            catalog = null;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private static Bounds CalculateBounds(GameObject model, Vector3 fallbackCenter)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                                     || renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? bounds : new Bounds(fallbackCenter, Vector3.one * 2f);
        }

        private static void SetLayerAndRenderingMask(Transform root)
        {
            root.gameObject.layer = PreviewLayer;
            Renderer renderer = root.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.renderingLayerMask = PreviewRenderingLayer;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            for (int i = 0; i < root.childCount; i++) SetLayerAndRenderingMask(root.GetChild(i));
        }

        private static bool IsValidAnimal(AnimalType type)
        {
            int index = (int)type;
            return index >= 0 && index < AnimalRoster.Count;
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
