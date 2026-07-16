using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Regressive 10→1 countdown shown where an ammo/life pickup was collected. Swaps a
    /// 3D number model (children "Num_1".."Num_10") once per second, then disappears — the
    /// pickup reappears when it ends. Prefab: Resources/Countdown/Countdown.
    /// </summary>
    public sealed class Countdown : MonoBehaviour
    {
        public const float DurationSeconds = 10f;
        private const float SpinDegreesPerSecond = 24f;

        private readonly GameObject[] numbers = new GameObject[11]; // 1..10
        private float endTime;
        private float spinAngle;
        private bool running;
        private Vector3 basePosition;
        private static GameObject cachedPrefab;
        private static bool prefabLookedUp;

        private void Awake()
        {
            for (int n = 1; n <= 10; n++)
            {
                Transform t = transform.Find("Num_" + n);
                if (t != null) { numbers[n] = t.gameObject; numbers[n].SetActive(false); }
            }
        }

        public static Countdown Spawn(Vector3 position, float seconds)
        {
            if (!prefabLookedUp)
            {
                cachedPrefab = Resources.Load<GameObject>("Countdown/Countdown");
                prefabLookedUp = true;
            }
            if (cachedPrefab == null) return null;
            GameObject obj = Instantiate(cachedPrefab);
            obj.transform.position = position + Vector3.up * 0.9f;
            Countdown countdown = obj.GetComponent<Countdown>();
            if (countdown == null) countdown = obj.AddComponent<Countdown>();
            countdown.basePosition = obj.transform.position;
            countdown.endTime = Time.time + Mathf.Max(1f, seconds);
            countdown.running = true;
            countdown.spinAngle = 0f;
            countdown.ShowNumber(Mathf.Clamp(Mathf.CeilToInt(seconds), 1, 10));
            return countdown;
        }

        private void Update()
        {
            if (!running) return;
            float remaining = endTime - Time.time;
            if (remaining <= 0f) { Destroy(gameObject); return; }

            ShowNumber(Mathf.Clamp(Mathf.CeilToInt(remaining), 1, 10));
            spinAngle = Mathf.Repeat(spinAngle + SpinDegreesPerSecond * Time.deltaTime, 360f);
            RotateActiveNumber(spinAngle);

            // Gentle bob and billboard toward the camera so the number stays readable.
            transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.08f);
            Transform viewer = CameraCache.MainTransform;
            if (viewer != null)
            {
                Vector3 flat = viewer.position - transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }
        }

        private void ShowNumber(int active)
        {
            for (int n = 1; n <= 10; n++)
            {
                if (numbers[n] == null) continue;
                bool shouldShow = n == active;
                if (numbers[n].activeSelf != shouldShow) numbers[n].SetActive(shouldShow);
            }
        }

        private void RotateActiveNumber(float angle)
        {
            for (int n = 1; n <= 10; n++)
            {
                if (numbers[n] == null || !numbers[n].activeSelf) continue;
                numbers[n].transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                break;
            }
        }
    }
}
