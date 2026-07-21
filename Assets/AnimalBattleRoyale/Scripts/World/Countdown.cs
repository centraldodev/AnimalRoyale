using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Floating respawn countdown shown where an ammo/weapon-crystal pickup was collected.
    /// Tinted to match the collected item's own color (blue diamond, green ammo, ...) instead
    /// of a single fixed color, so it's obvious at a glance which kind is about to respawn.
    /// </summary>
    public sealed class Countdown : MonoBehaviour
    {
        public const float DurationSeconds = 20f;

        private float endTime;
        private float totalSeconds;
        private Vector3 basePosition;
        private TextMesh text;

        public static Countdown Spawn(Vector3 position, float seconds, Color color, string label = null)
        {
            GameObject obj = new GameObject("Countdown");
            obj.transform.position = position;
            Countdown countdown = obj.AddComponent<Countdown>();
            countdown.basePosition = position;
            countdown.totalSeconds = Mathf.Max(1f, seconds);
            countdown.endTime = Time.time + countdown.totalSeconds;
            countdown.BuildVisual(color, label);
            return countdown;
        }

        private void BuildVisual(Color color, string label)
        {
            CollectibleHighlight.Attach(transform, color, 0.55f, 0.03f);

            if (!string.IsNullOrEmpty(label))
            {
                GameObject nameObject = new GameObject("CountdownLabel");
                nameObject.transform.SetParent(transform, false);
                nameObject.transform.localPosition = Vector3.up * 1.55f;
                TextMesh nameText = nameObject.AddComponent<TextMesh>();
                nameText.anchor = TextAnchor.MiddleCenter;
                nameText.alignment = TextAlignment.Center;
                nameText.characterSize = 0.05f;
                nameText.fontSize = 48;
                nameText.fontStyle = FontStyle.Bold;
                nameText.color = color;
                nameText.text = label;
                nameObject.AddComponent<PickupLabel>();
            }

            GameObject numberObject = new GameObject("CountdownNumber");
            numberObject.transform.SetParent(transform, false);
            numberObject.transform.localPosition = Vector3.up * 1.1f;
            text = numberObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.09f;
            text.fontSize = 64;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.text = Mathf.Clamp(Mathf.CeilToInt(totalSeconds), 1, 99).ToString();
            numberObject.AddComponent<PickupLabel>();
        }

        private void Update()
        {
            float remaining = endTime - Time.time;
            if (remaining <= 0f) { Destroy(gameObject); return; }

            text.text = Mathf.Clamp(Mathf.CeilToInt(remaining), 1, 99).ToString();
            float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.08f;
            text.transform.localScale = Vector3.one * pulse;
            transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.05f);
        }
    }
}
