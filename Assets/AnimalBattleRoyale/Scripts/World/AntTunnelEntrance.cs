using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Surface entrances for the Ant's hidden tunnel network. The world stays visible while
    /// the Ant is underground; only its possible exits are drawn for the local player.
    /// </summary>
    public sealed class AntTunnelEntrance : MonoBehaviour
    {
        private const float EnterRange = 3.1f;
        private const float TunnelDuration = 7f;
        private const float SelectionTravelDelay = 0.45f;
        private const int RingSegments = 26;
        public const float VisualEmbedDepth = 0.14f;

        private sealed class TunnelSession
        {
            public AntTunnelEntrance Source;
            public AntTunnelEntrance SelectedExit;
            public float ExpiresAt;
            public float SelectionStartedAt;
        }

        private static readonly List<AntTunnelEntrance> entrances = new List<AntTunnelEntrance>();
        private static readonly Dictionary<ThirdPersonAnimalController, TunnelSession> sessions = new Dictionary<ThirdPersonAnimalController, TunnelSession>();

        private LineRenderer exitRing;
        private LineRenderer exitBeam;
        private TextMesh exitText;
        private Material markerMaterial;

        private void Awake()
        {
            CreateExitMarker();
        }

        private void OnEnable()
        {
            if (!entrances.Contains(this)) entrances.Add(this);
        }

        private void OnDisable()
        {
            entrances.Remove(this);
        }

        private void OnDestroy()
        {
            if (markerMaterial != null) Destroy(markerMaterial);
        }

        private void Update()
        {
            ThirdPersonAnimalController localPlayer = BattleRoyaleManager.Instance != null ? BattleRoyaleManager.Instance.LocalPlayer : null;
            bool show = false;
            bool selected = false;
            if (localPlayer != null && localPlayer.IsLocalPlayer && sessions.TryGetValue(localPlayer, out TunnelSession session))
            {
                show = session.Source != this;
                selected = show && session.SelectedExit == this;
            }
            SetExitMarkerVisible(show, selected);
        }

        public static AntTunnelEntrance Create(Vector3 position, Material rimMaterial, Material holeMaterial)
        {
            GameObject root = new GameObject("AntTunnelEntrance");
            root.transform.position = position;
            AntTunnelEntrance entrance = root.AddComponent<AntTunnelEntrance>();

            GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "TunnelRim";
            rim.transform.SetParent(root.transform, false);
            rim.transform.localPosition = Vector3.down * 0.06f;
            rim.transform.localScale = new Vector3(1.3f, 0.11f, 1.3f);
            rim.GetComponent<Renderer>().sharedMaterial = rimMaterial;
            Collider rimCollider = rim.GetComponent<Collider>();
            if (rimCollider != null) rimCollider.enabled = false;

            GameObject hole = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hole.name = "TunnelHole";
            hole.transform.SetParent(root.transform, false);
            hole.transform.localPosition = Vector3.down * VisualEmbedDepth;
            hole.transform.localScale = new Vector3(1.45f, 0.08f, 1.45f);
            hole.GetComponent<Renderer>().sharedMaterial = holeMaterial;
            Collider holeCollider = hole.GetComponent<Collider>();
            if (holeCollider != null) holeCollider.enabled = false;

            GameObject label = new GameObject("TunnelLabel");
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = Vector3.up * 0.42f;
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = "TÚNEL\nE";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.04f;
            text.fontSize = 44;
            text.color = new Color(0.9f, 0.62f, 0.2f);
            label.AddComponent<PickupLabel>();
            return entrance;
        }

        public static bool IsTraveling(ThirdPersonAnimalController ant)
        {
            return ant != null && sessions.ContainsKey(ant);
        }

        public static float SecondsRemaining(ThirdPersonAnimalController ant)
        {
            return ant != null && sessions.TryGetValue(ant, out TunnelSession session)
                ? Mathf.Max(0f, session.ExpiresAt - Time.time)
                : 0f;
        }

        public static void CancelTravel(ThirdPersonAnimalController ant)
        {
            if (ant != null) sessions.Remove(ant);
        }

        public static bool TryEnter(ThirdPersonAnimalController ant)
        {
            if (ant == null || sessions.ContainsKey(ant)) return false;
            AntTunnelEntrance nearest = FindNearest(ant.transform.position, EnterRange, null);
            if (nearest == null) return false;
            sessions[ant] = new TunnelSession
            {
                Source = nearest,
                SelectedExit = null,
                ExpiresAt = Time.time + TunnelDuration,
                SelectionStartedAt = -1f
            };
            AttackVfx.CreateBurst(nearest.transform.position, new Color(0.65f, 0.28f, 0.06f), 2.1f);
            return true;
        }

        /// <summary>The configured movement keys choose an exit by direction. Holding them briefly travels to that exit.</summary>
        public static void Navigate(ThirdPersonAnimalController ant, Vector3 movementDirection)
        {
            if (ant == null || !sessions.TryGetValue(ant, out TunnelSession session)) return;
            if (movementDirection.sqrMagnitude < 0.01f)
            {
                session.SelectionStartedAt = -1f;
                return;
            }

            AntTunnelEntrance directionExit = FindExitInDirection(session.Source, movementDirection);
            if (directionExit == null) return;
            if (session.SelectedExit != directionExit)
            {
                session.SelectedExit = directionExit;
                session.SelectionStartedAt = Time.time;
                return;
            }

            if (session.SelectionStartedAt < 0f) session.SelectionStartedAt = Time.time;
            if (Time.time >= session.SelectionStartedAt + SelectionTravelDelay) ExitTunnel(ant, session.SelectedExit);
        }

        public static void Tick(ThirdPersonAnimalController ant)
        {
            if (ant == null || !sessions.TryGetValue(ant, out TunnelSession session)) return;
            if (Time.time >= session.ExpiresAt)
            {
                // Time expired: surface at the closest valid exit, never at the entrance used.
                ExitTunnel(ant, FindNearest(session.Source.transform.position, float.MaxValue, session.Source));
            }
        }

        private static void ExitTunnel(ThirdPersonAnimalController ant, AntTunnelEntrance destination)
        {
            if (destination == null) return;
            sessions.Remove(ant);
            ant.TeleportTo(destination.transform.position + Vector3.up * 0.18f);
            AttackVfx.CreateBurst(destination.transform.position, new Color(0.65f, 0.28f, 0.06f), 2.1f);
            CombatFeedback.PlayPower(ant.AnimalType, 0, destination.transform.position);
        }

        private static AntTunnelEntrance FindNearest(Vector3 position, float maximumDistance, AntTunnelEntrance exclude)
        {
            AntTunnelEntrance nearest = null;
            float nearestDistance = maximumDistance * maximumDistance;
            foreach (AntTunnelEntrance entrance in entrances)
            {
                if (entrance == null || entrance == exclude) continue;
                float distance = (entrance.transform.position - position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = entrance;
            }
            return nearest;
        }

        private static AntTunnelEntrance FindExitInDirection(AntTunnelEntrance source, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return null;
            direction.Normalize();

            AntTunnelEntrance best = null;
            float bestScore = float.NegativeInfinity;
            foreach (AntTunnelEntrance entrance in entrances)
            {
                if (entrance == null || entrance == source) continue;
                Vector3 offset = entrance.transform.position - source.transform.position;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance < 0.01f) continue;
                float alignment = Vector3.Dot(direction, offset / distance);
                float score = alignment * 100f - distance * 0.12f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = entrance;
            }
            return best;
        }

        private void CreateExitMarker()
        {
            GameObject marker = new GameObject("AntTunnelExitMarker");
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = Vector3.up * 0.2f;

            markerMaterial = new Material(ShaderLibrary.Sprite);
            markerMaterial.color = new Color(0.25f, 1f, 0.65f, 0.96f);

            exitRing = marker.AddComponent<LineRenderer>();
            exitRing.useWorldSpace = false;
            exitRing.loop = true;
            exitRing.positionCount = RingSegments;
            exitRing.widthMultiplier = 0.075f;
            exitRing.numCornerVertices = 3;
            exitRing.material = markerMaterial;
            exitRing.startColor = new Color(0.25f, 1f, 0.65f, 1f);
            exitRing.endColor = new Color(0.1f, 0.65f, 1f, 1f);
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                exitRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * 1.15f, 0f, Mathf.Sin(angle) * 1.15f));
            }

            GameObject beam = new GameObject("ExitBeam");
            beam.transform.SetParent(marker.transform, false);
            exitBeam = beam.AddComponent<LineRenderer>();
            exitBeam.useWorldSpace = false;
            exitBeam.positionCount = 2;
            exitBeam.widthMultiplier = 0.065f;
            exitBeam.material = markerMaterial;
            exitBeam.SetPosition(0, Vector3.zero);
            exitBeam.SetPosition(1, Vector3.up * 3.1f);

            GameObject text = new GameObject("ExitText");
            text.transform.SetParent(marker.transform, false);
            text.transform.localPosition = Vector3.up * 3.35f;
            exitText = text.AddComponent<TextMesh>();
            exitText.anchor = TextAnchor.MiddleCenter;
            exitText.alignment = TextAlignment.Center;
            exitText.characterSize = 0.055f;
            exitText.fontSize = 48;
            exitText.color = Color.white;
            marker.SetActive(false);
        }

        private void SetExitMarkerVisible(bool visible, bool selected)
        {
            if (exitRing == null) return;
            GameObject marker = exitRing.gameObject;
            if (marker.activeSelf != visible) marker.SetActive(visible);
            if (!visible) return;

            float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.12f;
            marker.transform.localScale = Vector3.one * (selected ? 1.32f * pulse : pulse);
            Color color = selected ? new Color(1f, 0.75f, 0.12f, 1f) : new Color(0.22f, 1f, 0.62f, 0.9f);
            exitRing.startColor = color;
            exitRing.endColor = color;
            exitBeam.startColor = color;
            exitBeam.endColor = color;
            exitText.text = selected ? "SAIR\nMOVIMENTO" : "SAÍDA";
            exitText.color = color;

            Transform viewer = CameraCache.MainTransform;
            if (viewer != null) exitText.transform.rotation = viewer.rotation;
        }
    }
}
