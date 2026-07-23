using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Resolution-independent secondary-aim lens. The scene stays sharp in the center while
    /// radial distortion, blur and chromatic separation grow toward both sides of the rim.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ScopeLensEffect : MonoBehaviour
    {
        private const string ShaderResourcePath = "Shaders/ScopeLens";

        [SerializeField, Range(0.28f, 0.48f)] private float lensRadius = 0.41f;
        [SerializeField, Range(0f, 0.35f)] private float distortionStrength = 0.2f;
        [SerializeField, Range(0f, 14f)] private float edgeBlurPixels = 8.5f;
        [SerializeField, Range(0f, 0.5f)] private float outsideBrightness = 0.13f;
        [SerializeField] private Color reticleColor = new Color(0.18f, 1f, 0.08f, 1f);

        private Material material;
        private ThirdPersonCamera thirdPersonCamera;
        private bool missingShaderReported;

        public static ScopeLensEffect Ensure(Camera targetCamera)
        {
            if (targetCamera == null) return null;
            ScopeLensEffect effect = targetCamera.GetComponent<ScopeLensEffect>();
            if (effect == null) effect = targetCamera.gameObject.AddComponent<ScopeLensEffect>();
            return effect;
        }

        private void Awake()
        {
            thirdPersonCamera = GetComponent<ThirdPersonCamera>();
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (thirdPersonCamera == null) thirdPersonCamera = GetComponent<ThirdPersonCamera>();
            float blend = thirdPersonCamera != null ? thirdPersonCamera.AimZoomBlend01 : 0f;
            if (blend <= 0.001f || !EnsureMaterial())
            {
                Graphics.Blit(source, destination);
                return;
            }

            material.SetFloat("_Blend", blend);
            material.SetFloat("_Aspect", source.height > 0 ? source.width / (float)source.height : 1f);
            material.SetFloat("_LensRadius", lensRadius);
            material.SetFloat("_Distortion", distortionStrength);
            material.SetFloat("_EdgeBlurPixels", edgeBlurPixels);
            material.SetFloat("_OutsideBrightness", outsideBrightness);
            material.SetColor("_ReticleColor", reticleColor);
            Graphics.Blit(source, destination, material);
        }

        private bool EnsureMaterial()
        {
            if (material != null) return true;
            Shader shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader == null) shader = ShaderLibrary.Find("Hidden/AnimalBattleRoyale/ScopeLens");
            if (shader == null || !shader.isSupported)
            {
                if (!missingShaderReported)
                {
                    missingShaderReported = true;
                    Debug.LogWarning("Shader da lente de mira indisponível; mantendo somente o zoom da câmera.", this);
                }
                return false;
            }

            material = new Material(shader)
            {
                name = "ScopeLens_Runtime",
                hideFlags = HideFlags.HideAndDontSave
            };
            return true;
        }

        private void OnDestroy()
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
            material = null;
        }
    }
}
