using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Surface entrances for the Ant's hidden tunnel network. While burrowed the Ant walks
    /// the map normally (same camera, same movement) but is hidden/untargetable; every other
    /// entrance glows as a tall beacon so it can be found and walked to, and pressing the
    /// ability key near one surfaces the Ant there. A time limit forces a surface at the
    /// nearest entrance to wherever the Ant currently is if none is chosen in time.
    /// </summary>
    public sealed class AntTunnelEntrance : MonoBehaviour
    {
        private const float EnterRange = 3.1f;
        private const float ExitRange = 2.6f;
        private const float TunnelDuration = 18f;
        private const int RingSegments = 26;
        private const int LaserSpiralSegments = 42;
        private const float ExitBeamHeight = 40f;
        private const float RadarSize = 288f;
        private const float RadarPadding = 30f;
        public const float VisualEmbedDepth = 0.14f;

        private sealed class TunnelSession
        {
            public AntTunnelEntrance Source;
            public float ExpiresAt;
        }

        private static readonly List<AntTunnelEntrance> entrances = new List<AntTunnelEntrance>();
        private static readonly Dictionary<ThirdPersonAnimalController, TunnelSession> sessions = new Dictionary<ThirdPersonAnimalController, TunnelSession>();
        private static GameObject cachedHolePrefab;
        private static bool holePrefabLookedUp;
        private static Material cachedHoleMaterial;

        private LineRenderer exitRing;
        private LineRenderer exitOuterRing;
        private LineRenderer exitTopRing;
        private LineRenderer exitBeam;
        private LineRenderer exitBeamBody;
        private LineRenderer exitBeamCore;
        private LineRenderer exitBeamSpiral;
        private TextMesh exitText;
        private Material markerMaterial;
        private GUIStyle radarTitleStyle;
        private GUIStyle radarHintStyle;
        private GUIStyle radarDistanceStyle;

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
            if (localPlayer != null && localPlayer.IsLocalPlayer && sessions.ContainsKey(localPlayer))
            {
                show = true;
                selected = FindNearest(localPlayer.transform.position, float.MaxValue, null) == this;
            }
            SetExitMarkerVisible(show, selected);
        }

        private void OnGUI()
        {
            ThirdPersonAnimalController localPlayer = BattleRoyaleManager.Instance != null
                ? BattleRoyaleManager.Instance.LocalPlayer
                : null;
            if (localPlayer == null || !localPlayer.IsLocalPlayer
                || !sessions.TryGetValue(localPlayer, out TunnelSession session)
                || session.Source != this)
            {
                return;
            }

            DrawTunnelRadar(localPlayer, session);
        }

        public static AntTunnelEntrance Create(Vector3 position, Material rimMaterial, Material holeMaterial)
        {
            GameObject root = new GameObject("AntTunnelEntrance");
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            AntTunnelEntrance entrance = root.AddComponent<AntTunnelEntrance>();

            if (!holePrefabLookedUp)
            {
                cachedHolePrefab = Resources.Load<GameObject>("Environment/TunnelHole");
                holePrefabLookedUp = true;
            }

            if (cachedHolePrefab != null)
            {
                GameObject hole = Instantiate(cachedHolePrefab, root.transform, false);
                hole.name = "TunnelHoleVisual";
                Material material = GetHoleMaterial();
                if (material != null)
                {
                    foreach (Renderer r in hole.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = material;
                }
                // The source FBX's authored scale isn't guaranteed, so rescale to a known
                // footprint before embedding it, instead of trusting it as-is.
                ImportedPropVisual.NormalizeScale(hole, 1.4f, out _);
                hole.transform.localPosition = Vector3.down * VisualEmbedDepth;
                foreach (Collider c in hole.GetComponentsInChildren<Collider>(true)) if (c != null) c.enabled = false;
            }
            else
            {
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
            }

            GameObject label = new GameObject("TunnelLabel");
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = Vector3.up * 0.42f;
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = "TÚNEL\nQ";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.04f;
            text.fontSize = 44;
            text.color = new Color(0.9f, 0.62f, 0.2f);
            label.AddComponent<PickupLabel>();
            return entrance;
        }

        private static Material GetHoleMaterial()
        {
            if (cachedHoleMaterial != null) return cachedHoleMaterial;
            Texture2D albedo = Resources.Load<Texture2D>("Environment/TunnelHole_basecolor");
            if (albedo == null) return null;
            Material material = new Material(ShaderLibrary.Lit)
            {
                name = "TunnelHole_RuntimePBR",
                color = Color.white,
                enableInstancing = true
            };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.15f);
            cachedHoleMaterial = material;
            return cachedHoleMaterial;
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
            sessions[ant] = new TunnelSession { Source = nearest, ExpiresAt = Time.time + TunnelDuration };
            AttackVfx.CreateBurst(nearest.transform.position, new Color(0.65f, 0.28f, 0.06f), 2.1f);
            return true;
        }

        /// <summary>Surfaces at whichever entrance is currently within reach, if any.</summary>
        public static bool TryExitNearest(ThirdPersonAnimalController ant)
        {
            if (ant == null || !sessions.ContainsKey(ant)) return false;
            AntTunnelEntrance nearest = FindNearest(ant.transform.position, ExitRange, null);
            if (nearest == null) return false;
            ExitTunnel(ant, nearest);
            return true;
        }

        /// <summary>Surfaces immediately at whichever entrance the Ant is aiming at, within range,
        /// without needing to actually walk over to it first.</summary>
        public static bool TryExitAimed(ThirdPersonAnimalController ant, Vector3 aimOrigin, Vector3 aimDirection, float maxRange)
        {
            if (ant == null || !sessions.ContainsKey(ant)) return false;
            Vector3 flatAim = new Vector3(aimDirection.x, 0f, aimDirection.z);
            if (flatAim.sqrMagnitude < 0.01f) return false;
            flatAim.Normalize();

            AntTunnelEntrance best = null;
            float bestScore = float.NegativeInfinity;
            foreach (AntTunnelEntrance entrance in entrances)
            {
                if (entrance == null) continue;
                Vector3 offset = entrance.transform.position - aimOrigin;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance < 0.01f || distance > maxRange) continue;
                float alignment = Vector3.Dot(flatAim, offset / distance);
                if (alignment < 0.8f) continue;
                float score = alignment * 100f - distance * 0.05f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = entrance;
            }
            if (best == null) return false;
            ExitTunnel(ant, best);
            return true;
        }

        /// <summary>Checked every frame while burrowed; forces a surface once the time limit runs out.</summary>
        public static void Tick(ThirdPersonAnimalController ant)
        {
            if (ant == null || !sessions.TryGetValue(ant, out TunnelSession session)) return;
            if (Time.time >= session.ExpiresAt)
                ExitTunnel(ant, FindNearest(ant.transform.position, float.MaxValue, null));
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

        private void CreateExitMarker()
        {
            GameObject marker = new GameObject("AntTunnelExitMarker");
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = Vector3.up * 0.2f;

            markerMaterial = new Material(ShaderLibrary.Sprite);
            markerMaterial.name = "AntTunnelNeonMarker";
            markerMaterial.color = new Color(0.02f, 1f, 0.72f, 1f);
            markerMaterial.renderQueue = (int)RenderQueue.Overlay;
            if (markerMaterial.HasProperty("_ZWrite")) markerMaterial.SetFloat("_ZWrite", 0f);
            if (markerMaterial.HasProperty("_ZTest")) markerMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);

            exitRing = marker.AddComponent<LineRenderer>();
            exitRing.useWorldSpace = false;
            exitRing.loop = true;
            exitRing.positionCount = RingSegments;
            exitRing.widthMultiplier = 0.14f;
            exitRing.numCornerVertices = 3;
            exitRing.numCapVertices = 3;
            exitRing.shadowCastingMode = ShadowCastingMode.Off;
            exitRing.receiveShadows = false;
            exitRing.sortingOrder = 120;
            exitRing.sharedMaterial = markerMaterial;
            ConfigureRing(exitRing, 1.4f, 0f);

            exitOuterRing = CreateMarkerRing(marker.transform, "ExitOuterRing", 2.05f, 0.16f);
            exitTopRing = CreateMarkerRing(marker.transform, "ExitTopRing", 0.95f, ExitBeamHeight);

            exitBeam = CreateBeamLayer(marker.transform, "ExitLaserGlow", 0.82f, 119);
            exitBeamBody = CreateBeamLayer(marker.transform, "ExitLaserBody", 0.38f, 120);
            exitBeamCore = CreateBeamLayer(marker.transform, "ExitLaserCore", 0.1f, 122);

            GameObject spiral = new GameObject("ExitLaserEnergySpiral");
            spiral.transform.SetParent(marker.transform, false);
            exitBeamSpiral = spiral.AddComponent<LineRenderer>();
            exitBeamSpiral.useWorldSpace = false;
            exitBeamSpiral.loop = false;
            exitBeamSpiral.positionCount = LaserSpiralSegments;
            exitBeamSpiral.widthMultiplier = 0.055f;
            exitBeamSpiral.numCornerVertices = 4;
            exitBeamSpiral.numCapVertices = 4;
            exitBeamSpiral.shadowCastingMode = ShadowCastingMode.Off;
            exitBeamSpiral.receiveShadows = false;
            exitBeamSpiral.sortingOrder = 123;
            exitBeamSpiral.sharedMaterial = markerMaterial;

            GameObject text = new GameObject("ExitText");
            text.transform.SetParent(marker.transform, false);
            text.transform.localPosition = Vector3.up * (ExitBeamHeight + 0.65f);
            exitText = text.AddComponent<TextMesh>();
            exitText.anchor = TextAnchor.MiddleCenter;
            exitText.alignment = TextAlignment.Center;
            exitText.characterSize = 0.072f;
            exitText.fontSize = 56;
            exitText.fontStyle = FontStyle.Bold;
            exitText.color = Color.white;
            MeshRenderer textRenderer = text.GetComponent<MeshRenderer>();
            if (textRenderer != null)
            {
                textRenderer.shadowCastingMode = ShadowCastingMode.Off;
                textRenderer.receiveShadows = false;
                textRenderer.sortingOrder = 122;
            }
            marker.SetActive(false);
        }

        private LineRenderer CreateBeamLayer(Transform parent, string layerName, float width, int sortingOrder)
        {
            GameObject beamObject = new GameObject(layerName);
            beamObject.transform.SetParent(parent, false);
            LineRenderer beam = beamObject.AddComponent<LineRenderer>();
            beam.useWorldSpace = false;
            beam.positionCount = 2;
            beam.widthMultiplier = width;
            beam.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.58f),
                new Keyframe(0.04f, 1f),
                new Keyframe(0.9f, 1f),
                new Keyframe(1f, 0.22f));
            beam.numCornerVertices = 4;
            beam.numCapVertices = 4;
            beam.shadowCastingMode = ShadowCastingMode.Off;
            beam.receiveShadows = false;
            beam.sortingOrder = sortingOrder;
            beam.sharedMaterial = markerMaterial;
            beam.SetPosition(0, Vector3.zero);
            beam.SetPosition(1, Vector3.up * ExitBeamHeight);
            return beam;
        }

        private LineRenderer CreateMarkerRing(Transform parent, string ringName, float radius, float height)
        {
            GameObject ringObject = new GameObject(ringName);
            ringObject.transform.SetParent(parent, false);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = RingSegments;
            ring.widthMultiplier = 0.09f;
            ring.numCornerVertices = 3;
            ring.numCapVertices = 3;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.sortingOrder = 120;
            ring.sharedMaterial = markerMaterial;
            ConfigureRing(ring, radius, height);
            return ring;
        }

        private static void ConfigureRing(LineRenderer ring, float radius, float height)
        {
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
            }
        }

        private void SetExitMarkerVisible(bool visible, bool selected)
        {
            if (exitRing == null) return;
            GameObject marker = exitRing.gameObject;
            if (marker.activeSelf != visible) marker.SetActive(visible);
            if (!visible) return;

            float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.12f;
            marker.transform.localScale = Vector3.one * (selected ? 1.32f * pulse : pulse);
            Color color = selected ? new Color(1f, 0.74f, 0.02f, 1f) : new Color(0.02f, 1f, 0.72f, 1f);
            exitRing.startColor = color;
            exitRing.endColor = color;
            exitOuterRing.startColor = new Color(color.r, color.g, color.b, 0.78f);
            exitOuterRing.endColor = new Color(color.r, color.g, color.b, 0.78f);
            exitTopRing.startColor = color;
            exitTopRing.endColor = color;
            float positionPhase = transform.position.x * 0.071f + transform.position.z * 0.113f;
            float laserPulse = 1f + Mathf.Sin(Time.time * 18f + positionPhase) * 0.09f;
            float selectedBoost = selected ? 1.22f : 1f;
            exitBeam.widthMultiplier = 0.58f * laserPulse * selectedBoost;
            exitBeamBody.widthMultiplier = 0.27f * laserPulse * selectedBoost;
            exitBeamCore.widthMultiplier = 0.075f * laserPulse * selectedBoost;
            exitBeamSpiral.widthMultiplier = 0.055f * laserPulse * selectedBoost;

            SetLineColor(exitBeam, new Color(color.r, color.g, color.b, selected ? 0.32f : 0.2f));
            SetLineColor(exitBeamBody, new Color(color.r, color.g, color.b, selected ? 0.8f : 0.62f));
            Color coreColor = Color.Lerp(color, Color.white, selected ? 0.78f : 0.68f);
            coreColor.a = 1f;
            SetLineColor(exitBeamCore, coreColor);
            SetLineColor(exitBeamSpiral, Color.Lerp(color, Color.white, 0.46f));
            UpdateLaserSpiral(selected);

            exitRing.widthMultiplier = (selected ? 0.2f : 0.14f) * laserPulse;
            exitOuterRing.widthMultiplier = (selected ? 0.14f : 0.09f) * laserPulse;
            exitTopRing.widthMultiplier = (selected ? 0.15f : 0.1f) * laserPulse;
            exitText.text = selected ? "SAÍDA MAIS PRÓXIMA\nAPERTE Q" : "SAÍDA";
            exitText.color = color;

            Transform viewer = CameraCache.MainTransform;
            if (viewer != null) exitText.transform.rotation = viewer.rotation;
        }

        private void UpdateLaserSpiral(bool selected)
        {
            float phase = Time.time * (selected ? 8.5f : 6.2f)
                          + transform.position.x * 0.071f + transform.position.z * 0.113f;
            float baseRadius = selected ? 0.34f : 0.27f;
            for (int i = 0; i < LaserSpiralSegments; i++)
            {
                float progress = i / (float)(LaserSpiralSegments - 1);
                float angle = phase + progress * Mathf.PI * 12f;
                float radius = baseRadius * (0.78f + Mathf.Sin(progress * Mathf.PI) * 0.22f);
                exitBeamSpiral.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius,
                    progress * ExitBeamHeight,
                    Mathf.Sin(angle) * radius));
            }
        }

        private static void SetLineColor(LineRenderer line, Color color)
        {
            line.startColor = color;
            line.endColor = color;
        }

        private void DrawTunnelRadar(ThirdPersonAnimalController ant, TunnelSession session)
        {
            EnsureRadarStyles();
            int previousDepth = GUI.depth;
            GUI.depth = -900;

            float size = Mathf.Min(RadarSize, Screen.height * 0.34f);
            Rect panel = new Rect(Screen.width - size - 22f, 76f, size, size);
            DrawSolidRect(new Rect(panel.x - 3f, panel.y - 3f, panel.width + 6f, panel.height + 6f),
                new Color(0.02f, 1f, 0.72f, 0.96f));
            DrawSolidRect(panel, new Color(0.012f, 0.035f, 0.04f, 0.94f));

            int exitCount = 0;
            foreach (AntTunnelEntrance entrance in entrances)
                if (entrance != null) exitCount++;

            float secondsLeft = Mathf.Max(0f, session.ExpiresAt - Time.time);
            GUI.Label(new Rect(panel.x + 8f, panel.y + 6f, panel.width - 16f, 24f),
                $"REDE DE TÚNEIS  ({exitCount} SAÍDAS)", radarTitleStyle);
            GUI.Label(new Rect(panel.x + 8f, panel.y + 27f, panel.width - 16f, 20f),
                $"TEMPO RESTANTE  {secondsLeft:0}s", radarHintStyle);

            Camera camera = Camera.main;
            Vector3 forward = camera != null ? camera.transform.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            Vector2 center = new Vector2(panel.center.x, panel.center.y + 12f);
            float radarRadius = Mathf.Max(34f, size * 0.5f - RadarPadding);
            // A fixed range (rather than scaling to whichever exit happens to be furthest)
            // keeps nearby exits spread out and readable instead of clumped at the center.
            const float radarRange = 110f;

            DrawRadarAxes(center, radarRadius);
            AntTunnelEntrance nearestExit = null;
            float nearestDistance = float.MaxValue;
            Vector3 antPosition = ant.transform.position;
            foreach (AntTunnelEntrance entrance in entrances)
            {
                if (entrance == null) continue;
                Vector3 offset = entrance.transform.position - antPosition;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance < nearestDistance) { nearestDistance = distance; nearestExit = entrance; }

                Vector2 radarOffset = new Vector2(Vector3.Dot(offset, right), -Vector3.Dot(offset, forward));
                radarOffset = Vector2.ClampMagnitude(radarOffset / radarRange, 1f) * radarRadius;
                bool nearest = entrance == nearestExit && distance <= radarRange;
                float dotSize = nearest ? 14f : 8f;
                Color dotColor = nearest
                    ? new Color(1f, 0.74f, 0.02f, 1f)
                    : new Color(0.02f, 1f, 0.72f, 1f);
                DrawSolidRect(new Rect(center.x + radarOffset.x - dotSize * 0.5f,
                    center.y + radarOffset.y - dotSize * 0.5f, dotSize, dotSize), dotColor);
            }

            DrawSolidRect(new Rect(center.x - 6f, center.y - 6f, 12f, 12f), new Color(1f, 0.28f, 0.06f, 1f));

            if (nearestExit != null)
            {
                GUI.Label(new Rect(panel.x + 8f, panel.yMax - 46f, panel.width - 16f, 20f),
                    $"MAIS PRÓXIMA  {nearestDistance:0} m — APERTE Q PARA SAIR", radarDistanceStyle);
            }

            GUI.depth = previousDepth;
        }

        private void DrawRadarAxes(Vector2 center, float radius)
        {
            Color axisColor = new Color(0.12f, 0.72f, 0.58f, 0.35f);
            DrawSolidRect(new Rect(center.x - radius, center.y - 1f, radius * 2f, 2f), axisColor);
            DrawSolidRect(new Rect(center.x - 1f, center.y - radius, 2f, radius * 2f), axisColor);

            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                float nextAngle = (i + 1) * Mathf.PI * 2f / RingSegments;
                Vector2 from = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Vector2 to = center + new Vector2(Mathf.Cos(nextAngle), Mathf.Sin(nextAngle)) * radius;
                DrawGuiLine(from, to, 2f, new Color(0.02f, 1f, 0.72f, 0.72f));
            }
        }

        private static void DrawGuiLine(Vector2 from, Vector2 to, float width, Color color)
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            Vector2 delta = to - from;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.DrawTexture(new Rect(from.x, from.y - width * 0.5f, delta.magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void EnsureRadarStyles()
        {
            if (radarTitleStyle != null) return;
            radarTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                normal = { textColor = Color.white }
            };
            radarHintStyle = new GUIStyle(radarTitleStyle)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.02f, 1f, 0.72f, 1f) }
            };
            radarDistanceStyle = new GUIStyle(radarTitleStyle)
            {
                fontSize = 11,
                normal = { textColor = new Color(1f, 0.74f, 0.02f, 1f) }
            };
        }
    }
}
