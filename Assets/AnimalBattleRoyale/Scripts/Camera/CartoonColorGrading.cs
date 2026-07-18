using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Lightweight Built-in Render Pipeline color grade shared by the gameplay
    /// camera and the isolated menu preview. UI and the static menu background
    /// are drawn afterwards, so their authored colors remain unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CartoonColorGrading : MonoBehaviour
    {
        private const string ShaderResourcePath = "Shaders/CartoonColorGrade";

        [SerializeField, Range(0.5f, 1.6f)] private float saturation = 1.24f;
        [SerializeField, Range(0.7f, 1.4f)] private float contrast = 1.05f;
        [SerializeField, Range(0.7f, 1.2f)] private float brightness = 0.95f;

        private Material material;
        private bool missingShaderReported;

        public static CartoonColorGrading Ensure(Camera targetCamera, float targetSaturation = 1.24f,
            float targetContrast = 1.05f, float targetBrightness = 0.95f)
        {
            if (targetCamera == null) return null;
            CartoonColorGrading grading = targetCamera.GetComponent<CartoonColorGrading>();
            if (grading == null) grading = targetCamera.gameObject.AddComponent<CartoonColorGrading>();
            grading.Configure(targetSaturation, targetContrast, targetBrightness);
            return grading;
        }

        public void Configure(float newSaturation, float newContrast, float newBrightness)
        {
            saturation = Mathf.Clamp(newSaturation, 0.5f, 1.6f);
            contrast = Mathf.Clamp(newContrast, 0.7f, 1.4f);
            brightness = Mathf.Clamp(newBrightness, 0.7f, 1.2f);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!EnsureMaterial())
            {
                Graphics.Blit(source, destination);
                return;
            }

            material.SetFloat("_Saturation", saturation);
            material.SetFloat("_Contrast", contrast);
            material.SetFloat("_Brightness", brightness);
            Graphics.Blit(source, destination, material);
        }

        private bool EnsureMaterial()
        {
            if (material != null) return true;
            Shader shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader == null) shader = ShaderLibrary.Find("Hidden/AnimalBattleRoyale/CartoonColorGrade");
            if (shader == null || !shader.isSupported)
            {
                if (!missingShaderReported)
                {
                    missingShaderReported = true;
                    Debug.LogWarning("Correção de cor cartoon indisponível; renderização original mantida.", this);
                }
                return false;
            }

            material = new Material(shader)
            {
                name = "CartoonColorGrade_Runtime",
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
