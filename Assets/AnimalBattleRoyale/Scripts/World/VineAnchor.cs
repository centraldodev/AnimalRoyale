using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    /// <summary>Anchor used by the monkey's grappling vine. The thrown variant can attach to
    /// any collider hit by the center-camera ray — terrain, props, trees or another animal —
    /// and follows moving targets until the monkey releases or throws the next vine.</summary>
    public sealed class VineAnchor : MonoBehaviour
    {
        public const float ThrowRange = 50f;
        private const int IndicatorSegments = 24;
        private const int GrappleLineSegments = 10;
        private const float MaximumSwingAngle = 68f;
        private const float SwingSpring = 24f;
        private const float SwingDamping = 3.1f;
        private const float AmbientSwayDegrees = 5f;
        private static readonly List<VineAnchor> anchors = new List<VineAnchor>();
        private static readonly RaycastHit[] aimHits = new RaycastHit[64];
        private static Material sharedThrowVineMaterial;
        private static LineRenderer previewIndicator;
        private static Material sharedIndicatorMaterial;
        private Transform swingPivot;
        private Quaternion restLocalRotation = Quaternion.identity;
        private Vector2 swingAngles;
        private Vector2 swingAngularVelocity;
        private Vector3 previousAnchorPosition;
        private Vector3 swingVelocity;
        private int lastDrivenFrame = -1;
        private bool swingPositionInitialized;
        private Transform attachmentTarget;
        private Vector3 attachmentLocalPoint;
        private Vector3 attachmentLocalNormal;
        private LineRenderer grappleOuterLine;
        private LineRenderer grappleCoreLine;
        private Vector3 grappleVisualEndPoint;
        private bool hasExternalVisualEndPoint;
        private float ambientPhaseX;
        private float ambientPhaseZ;

        public Vector3 SwingVelocity => swingVelocity;
        public Vector3 AttachmentPosition => swingPivot != null ? swingPivot.position : transform.position;
        public Vector3 AttachmentNormal => attachmentTarget != null
            ? attachmentTarget.TransformDirection(attachmentLocalNormal).normalized
            : Vector3.up;

        private void OnEnable()
        {
            if (!anchors.Contains(this)) anchors.Add(this);
        }

        private void OnDisable()
        {
            anchors.Remove(this);
        }

        private void LateUpdate()
        {
            FollowAttachment();
            if (swingPivot == null) return;
            if (lastDrivenFrame != Time.frameCount) SimulateSwing(Vector3.zero, Time.deltaTime);
            UpdateGrappleVisual();
        }

        /// <summary>Pushes the bottom of the vine toward the requested world direction.</summary>
        public void DriveSwing(Vector3 worldDirection, float deltaTime)
        {
            if (swingPivot == null) return;
            FollowAttachment();
            lastDrivenFrame = Time.frameCount;
            SimulateSwing(worldDirection, deltaTime);
        }

        // One shared ring previews the exact surface point where the center-camera ray lands.
        internal static void TickIndicators(ThirdPersonAnimalController player, Camera camera, float time)
        {
            bool monkeyActive = player != null && camera != null && player.AnimalType == AnimalType.Monkey
                && !player.IsDefeated
                && (!player.IsHangingVine || player.CanChainToAnotherVine);

            Vector3 point = default;
            bool hasTarget = monkeyActive && TryFindAttachment(player, camera.transform.position,
                player.ViewAimDirection, out _, out point, out _);
            if (!hasTarget)
            {
                if (previewIndicator != null) previewIndicator.enabled = false;
                return;
            }

            float pulse = Mathf.Sin(time * 7f);
            float targetDistance = Vector3.Distance(camera.transform.position, point);
            float distanceScale = Mathf.Lerp(1f, 3f, Mathf.InverseLerp(8f, ThrowRange, targetDistance));
            float radius = (0.2f + pulse * 0.018f) * distanceScale;
            float width = (0.022f + pulse * 0.003f) * Mathf.Lerp(1f, 2f,
                Mathf.InverseLerp(8f, ThrowRange, targetDistance));
            Vector3 cameraRight = camera.transform.right;
            Vector3 cameraUp = camera.transform.up;

            EnsurePreviewIndicator();
            previewIndicator.enabled = true;
            previewIndicator.widthMultiplier = width;
            for (int i = 0; i <= IndicatorSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / IndicatorSegments;
                Vector3 ringPoint = point + cameraRight * (Mathf.Cos(angle) * radius) + cameraUp * (Mathf.Sin(angle) * radius);
                previewIndicator.SetPosition(i, ringPoint);
            }
        }

        private static void EnsurePreviewIndicator()
        {
            if (previewIndicator != null) return;
            GameObject circle = new GameObject("MonkeyThrowPreviewRing");
            Object.DontDestroyOnLoad(circle);
            previewIndicator = circle.AddComponent<LineRenderer>();
            previewIndicator.useWorldSpace = true;
            previewIndicator.loop = false;
            previewIndicator.positionCount = IndicatorSegments + 1;
            previewIndicator.numCornerVertices = 3;
            previewIndicator.numCapVertices = 3;
            previewIndicator.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            previewIndicator.receiveShadows = false;
            previewIndicator.sortingOrder = 20;
            EnsureIndicatorMaterial();
            previewIndicator.sharedMaterial = sharedIndicatorMaterial;
            previewIndicator.startColor = new Color(1f, 0.98f, 0.45f, 1f);
            previewIndicator.endColor = new Color(0.2f, 1f, 0.8f, 1f);
            previewIndicator.enabled = false;
        }

        private static void EnsureIndicatorMaterial()
        {
            if (sharedIndicatorMaterial != null) return;
            Shader shader = ShaderLibrary.Sprite;
            sharedIndicatorMaterial = new Material(shader)
            {
                name = "SharedMonkeyVineIndicator",
                color = new Color(1f, 0.86f, 0.1f, 0.96f),
                enableInstancing = true
            };
        }

        public static VineAnchor Create(Transform tree, Vector3 localStart, Vector3 localEnd, Material material)
        {
            Vector3 direction = localEnd - localStart;
            float length = direction.magnitude;
            if (tree == null || length <= 0.01f) return null;

            GameObject pivot = new GameObject("VineSwingPivot");
            pivot.transform.SetParent(tree, false);
            pivot.transform.localPosition = localStart;
            pivot.transform.localRotation = Quaternion.FromToRotation(Vector3.down, direction.normalized);

            GameObject anchor = new GameObject("VineAnchor");
            anchor.transform.SetParent(pivot.transform, false);
            anchor.transform.localPosition = Vector3.down * length;
            VineAnchor vineAnchor = anchor.AddComponent<VineAnchor>();
            vineAnchor.ConfigureSwing(pivot.transform);

            // Thickness is a real-world target (~0.2m), not a fraction of the tree — trees
            // spawned at very different scales (a giant 30-unit tree vs. a small 8-unit one)
            // would otherwise get a vine 30x/8x as fat, since localScale here multiplies with
            // whatever scale "tree" itself has.
            float parentScale = Mathf.Max(0.0001f, tree.lossyScale.x);
            CreateVineVisual(pivot.transform, length, parentScale, material);
            return vineAnchor;
        }

        // A single straight cylinder read as a rigid pole, not a hanging vine. This instead
        // walks a gentle randomized S-curve from the attachment point down to the grab point
        // and builds it out of several short segments, so it reads as organic rope/vine
        // instead of a machined rod — while the curve's start and end still land exactly on
        // the pivot and the original Vector3.down * length anchor point, so the swing physics
        // and grab range are unaffected.
        private static void CreateVineVisual(Transform pivot, float length, float parentScale, Material material)
        {
            const int segmentCount = 8;
            float thickness = 0.2f / parentScale;
            float amplitudeX = length * Random.Range(0.05f, 0.09f);
            float amplitudeZ = length * Random.Range(0.05f, 0.09f);
            float frequencyX = Random.Range(1.3f, 2.1f);
            float frequencyZ = Random.Range(1.1f, 1.9f);
            float phaseX = Random.Range(0f, Mathf.PI * 2f);
            float phaseZ = Random.Range(0f, Mathf.PI * 2f);

            GameObject container = new GameObject("ClimbableVine");
            container.transform.SetParent(pivot, false);

            // The bare cylinder end used to just float next to the branch with nothing
            // visually tying it there. A wider wrap knot at the attachment point reads as
            // the vine actually looped/tied around the branch instead of floating beside it.
            CreateVineWrapKnot(container.transform, thickness, material);

            Vector3 previousPoint = Vector3.zero;
            for (int i = 1; i <= segmentCount; i++)
            {
                float t = (float)i / segmentCount;
                // Waviness fades to zero at both ends (Mathf.Sin(t*PI) is 0 at t=0 and t=1) so
                // the curve is tightest right at the attachment point and the grab handle,
                // fullest in the middle — and the bottom point lands exactly on the original
                // straight-line anchor position.
                float taper = Mathf.Sin(t * Mathf.PI);
                float x = Mathf.Sin(t * frequencyX * Mathf.PI * 2f + phaseX) * amplitudeX * taper;
                float z = Mathf.Sin(t * frequencyZ * Mathf.PI * 2f + phaseZ) * amplitudeZ * taper;
                Vector3 point = new Vector3(x, -length * t, z);

                CreateVineSegment(container.transform, previousPoint, point, thickness, material, i);
                // Each cylinder segment has a flat end cap, so bending segments at a joint
                // leaves a visible notch/seam there — a small sphere the same thickness
                // covers it and reads as a natural knot/thickening in the vine instead.
                if (i < segmentCount) CreateVineJoint(container.transform, point, thickness, material, i);
                previousPoint = point;
            }
        }

        // A small cluster of overlapping spheres around the attachment point, mimicking a
        // coil of vine wrapped/tied around the branch instead of a bare cylinder end
        // floating next to it.
        private static void CreateVineWrapKnot(Transform parent, float thickness, Material material)
        {
            const int coilCount = 3;
            float coilRadius = thickness * 0.85f;
            for (int i = 0; i < coilCount; i++)
            {
                float angle = i * Mathf.PI * 2f / coilCount + Random.Range(-0.2f, 0.2f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle * 2f) * 0.35f, Mathf.Sin(angle)) * coilRadius;
                GameObject coil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                coil.name = "VineWrapCoil" + i;
                coil.transform.SetParent(parent, false);
                coil.transform.localPosition = offset;
                coil.transform.localScale = Vector3.one * thickness * 1.4f;
                ConfigureVinePart(coil, material);
            }
        }

        private static void CreateVineJoint(Transform parent, Vector3 position, float thickness, Material material, int index)
        {
            GameObject joint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            joint.name = "VineJoint" + index;
            joint.transform.SetParent(parent, false);
            joint.transform.localPosition = position;
            joint.transform.localScale = Vector3.one * thickness;
            ConfigureVinePart(joint, material);
        }

        private static void CreateVineSegment(Transform parent, Vector3 from, Vector3 to,
            float thickness, Material material, int index)
        {
            Vector3 delta = to - from;
            float segmentLength = delta.magnitude;
            if (segmentLength <= 0.0001f) return;

            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            segment.name = "VineSegment" + index;
            segment.transform.SetParent(parent, false);
            segment.transform.localPosition = (from + to) * 0.5f;
            segment.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            // Slightly irregular thickness segment-to-segment reads as organic vine surface
            // instead of a uniform machined pole.
            float segmentThickness = thickness * Random.Range(0.82f, 1.18f);
            segment.transform.localScale = new Vector3(segmentThickness, segmentLength * 0.5f, segmentThickness);
            ConfigureVinePart(segment, material);
        }

        // Shared setup for every small primitive making up a vine (~18 per vine, ~2000 across
        // a full map) — shadows off since foliage-scale shadow detail here is imperceptible
        // but not free (each part is a separate shadow-caster draw), and the collider is
        // destroyed rather than just disabled so it isn't carried around as dead weight.
        private static void ConfigureVinePart(GameObject part, Material material)
        {
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
        }

        private void ConfigureSwing(Transform pivot)
        {
            swingPivot = pivot;
            restLocalRotation = pivot != null ? pivot.localRotation : Quaternion.identity;
            previousAnchorPosition = transform.position;
            swingPositionInitialized = true;
            // Randomized per vine so a whole tree line doesn't sway in lockstep.
            ambientPhaseX = Random.Range(0f, Mathf.PI * 2f);
            ambientPhaseZ = Random.Range(0f, Mathf.PI * 2f);
        }

        private void ConfigureAttachment(Transform target, Vector3 worldPoint, Vector3 worldNormal)
        {
            attachmentTarget = target;
            attachmentLocalPoint = target != null ? target.InverseTransformPoint(worldPoint) : worldPoint;
            attachmentLocalNormal = target != null
                ? target.InverseTransformDirection(worldNormal).normalized
                : worldNormal.normalized;
        }

        public void SetGrappleVisualEndPoint(Vector3 worldPoint)
        {
            grappleVisualEndPoint = worldPoint;
            hasExternalVisualEndPoint = true;
        }

        private void FollowAttachment()
        {
            if (swingPivot == null || attachmentTarget == null) return;
            swingPivot.position = attachmentTarget.TransformPoint(attachmentLocalPoint);
        }

        // The thrown vine is rendered as a slim two-tone filament rather than the chunky
        // cylinder chain used by decorative jungle vines. A dark outline keeps it readable
        // against foliage while the narrow lime core gives it the look of a green web strand.
        private void ConfigureGrappleVisual()
        {
            grappleOuterLine = CreateGrappleLine("GrappleWebOutline", 0.045f,
                new Color(0.035f, 0.38f, 0.08f, 0.94f), 12);
            grappleCoreLine = CreateGrappleLine("GrappleWebCore", 0.018f,
                new Color(0.48f, 1f, 0.24f, 1f), 13);
            UpdateGrappleVisual();
        }

        private LineRenderer CreateGrappleLine(string objectName, float width, Color color,
            int sortingOrder)
        {
            GameObject strand = new GameObject(objectName);
            strand.transform.SetParent(swingPivot, false);
            LineRenderer line = strand.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = GrappleLineSegments + 1;
            line.widthMultiplier = width;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.4f),
                new Keyframe(0.08f, 1f),
                new Keyframe(0.92f, 1f),
                new Keyframe(1f, 0.4f));
            line.numCornerVertices = 3;
            line.numCapVertices = 4;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = sortingOrder;
            line.sharedMaterial = GetThrowVineMaterial();
            line.startColor = color;
            line.endColor = color;
            return line;
        }

        private void UpdateGrappleVisual()
        {
            if (grappleOuterLine == null || grappleCoreLine == null || swingPivot == null) return;

            Vector3 start = swingPivot.position;
            Vector3 end = hasExternalVisualEndPoint ? grappleVisualEndPoint : transform.position;
            Vector3 direction = end - start;
            float length = direction.magnitude;
            Vector3 side = length > 0.001f
                ? Vector3.Cross(direction / length, Vector3.up)
                : Vector3.right;
            if (side.sqrMagnitude < 0.001f) side = Vector3.right;
            else side.Normalize();

            // Just enough organic curve to avoid a synthetic laser-line appearance while
            // remaining taut and much thinner than the old segmented rope.
            float sag = Mathf.Min(0.16f, length * 0.006f);
            for (int i = 0; i <= GrappleLineSegments; i++)
            {
                float t = (float)i / GrappleLineSegments;
                float taper = Mathf.Sin(t * Mathf.PI);
                float weave = Mathf.Sin(t * Mathf.PI * 4f + Time.time * 2.4f) * 0.012f * taper;
                Vector3 point = Vector3.Lerp(start, end, t)
                                + Vector3.down * (sag * taper)
                                + side * weave;
                grappleOuterLine.SetPosition(i, point);
                grappleCoreLine.SetPosition(i, point);
            }
        }

        private void SimulateSwing(Vector3 worldDirection, float deltaTime)
        {
            float step = Mathf.Clamp(deltaTime, 0f, 0.05f);
            if (step <= 0f || swingPivot == null) return;

            Vector3 localDirection = worldDirection;
            if (swingPivot.parent != null)
                localDirection = swingPivot.parent.InverseTransformDirection(worldDirection);
            localDirection = Quaternion.Inverse(restLocalRotation) * localDirection;
            localDirection.y = 0f;
            localDirection = Vector3.ClampMagnitude(localDirection, 1f);

            // A slow, per-vine sinusoidal sway so hanging vines read as alive even when
            // nobody is swinging on them, on top of whatever the monkey is driving.
            Vector2 ambientSway = new Vector2(
                Mathf.Sin(Time.time * 0.55f + ambientPhaseX),
                Mathf.Sin(Time.time * 0.4f + ambientPhaseZ)) * AmbientSwayDegrees;

            Vector2 targetAngles = new Vector2(-localDirection.z, localDirection.x) * MaximumSwingAngle + ambientSway;
            Vector2 acceleration = (targetAngles - swingAngles) * SwingSpring;
            swingAngularVelocity += acceleration * step;
            swingAngularVelocity *= Mathf.Exp(-SwingDamping * step);
            swingAngles += swingAngularVelocity * step;

            if (swingAngles.magnitude > MaximumSwingAngle)
            {
                swingAngles = swingAngles.normalized * MaximumSwingAngle;
                if (Vector2.Dot(swingAngularVelocity, swingAngles) > 0f)
                    swingAngularVelocity *= 0.45f;
            }

            if (localDirection.sqrMagnitude < 0.0001f
                && swingAngles.sqrMagnitude < 0.0004f
                && swingAngularVelocity.sqrMagnitude < 0.0025f)
            {
                swingAngles = Vector2.zero;
                swingAngularVelocity = Vector2.zero;
            }

            swingPivot.localRotation = restLocalRotation
                                       * Quaternion.Euler(swingAngles.x, 0f, swingAngles.y);
            Vector3 currentPosition = transform.position;
            if (swingPositionInitialized)
                swingVelocity = (currentPosition - previousAnchorPosition) / step;
            else
                swingPositionInitialized = true;
            previousAnchorPosition = currentPosition;
        }

        public static int RegisterExistingVines(Transform treeVisual)
        {
            if (treeVisual == null) return 0;
            int registered = 0;
            Transform[] descendants = treeVisual.GetComponentsInChildren<Transform>(true);
            foreach (Transform vineVisual in descendants)
            {
                if (vineVisual == null || !vineVisual.name.Contains("HangingVine")) continue;
                if (vineVisual.GetComponentInChildren<VineAnchor>(true) != null) continue;
                Renderer vineRenderer = vineVisual.GetComponentInChildren<Renderer>(true);
                if (vineRenderer == null) continue;

                Bounds bounds = vineRenderer.bounds;
                Vector3 gripPosition = new Vector3(bounds.center.x, bounds.min.y + 0.16f, bounds.center.z);
                GameObject anchorObject = new GameObject("VineAnchor_" + vineVisual.name);
                anchorObject.transform.SetParent(vineVisual, true);
                anchorObject.transform.position = gripPosition;
                anchorObject.AddComponent<VineAnchor>();
                registered++;
            }
            return registered;
        }

        public static bool IsWithinUseRange(ThirdPersonAnimalController monkey, Transform vine)
        {
            if (monkey == null || vine == null) return false;
            return (vine.position - monkey.transform.position).sqrMagnitude <= ThrowRange * ThrowRange;
        }

        // Kept for map-generation compatibility. Explicit registration is no longer needed:
        // every non-trigger collider is now a valid grapple surface.
        public static void RegisterThrowableTree(Transform tree) { }

        private static bool TryFindAttachment(ThirdPersonAnimalController monkey, Vector3 rayOrigin,
            Vector3 rayDirection, out Transform attachment, out Vector3 point, out Vector3 normal)
        {
            attachment = null;
            point = default;
            normal = Vector3.up;
            if (monkey == null || rayDirection.sqrMagnitude < 0.0001f) return false;

            rayDirection.Normalize();
            float rayLength = ThrowRange + Vector3.Distance(rayOrigin, monkey.transform.position);
            int hitCount = Physics.RaycastNonAlloc(new Ray(rayOrigin, rayDirection), aimHits,
                rayLength, ~0, QueryTriggerInteraction.Ignore);
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = aimHits[i];
                if (hit.collider == null || hit.distance >= nearestDistance) continue;
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == monkey.transform || hitTransform.IsChildOf(monkey.transform)) continue;
                if ((hit.point - monkey.transform.position).sqrMagnitude > ThrowRange * ThrowRange) continue;

                // Attaching to an animal's controller root makes the hit point follow its
                // movement and rotation. Other surfaces follow the exact collider transform.
                ThirdPersonAnimalController hitAnimal = hit.collider.GetComponentInParent<ThirdPersonAnimalController>();
                if (hitAnimal == monkey) continue;
                attachment = hitAnimal != null ? hitAnimal.transform : hitTransform;
                point = hit.point;
                normal = hit.normal;
                nearestDistance = hit.distance;
            }

            return attachment != null;
        }

        /// <summary>Throws at the exact collider point under the crosshair. The resulting
        /// strand spans from that point to the monkey while the controller pulls the animal
        /// toward the attachment.</summary>
        public static bool TryThrowVine(ThirdPersonAnimalController monkey, Vector3 fallbackAimDirection)
        {
            if (monkey == null || monkey.AnimalType != AnimalType.Monkey) return false;
            if (monkey.IsHangingVine && !monkey.CanChainToAnotherVine) return false;

            Transform camera = monkey.IsLocalPlayer ? CameraCache.MainTransform : null;
            Vector3 rayOrigin = camera != null
                ? camera.position
                : monkey.transform.position + Vector3.up * (monkey.Stats.ControllerHeight * 0.7f);
            Vector3 rayDirection = camera != null ? monkey.ViewAimDirection : fallbackAimDirection;
            if (!TryFindAttachment(monkey, rayOrigin, rayDirection, out Transform attachment,
                    out Vector3 point, out Vector3 normal)) return false;

            Vector3 gripPoint = monkey.transform.position
                                + Vector3.up * (monkey.Stats.ControllerHeight * 0.82f);
            VineAnchor thrown = CreateThrowAnchor(attachment, point, normal, gripPoint);
            if (thrown == null) return false;

            bool attached = monkey.IsHangingVine || monkey.IsVineLeaping
                ? monkey.TryLaunchToVine(thrown.transform)
                : monkey.TryGrabVine(thrown.transform);
            if (!attached) DestroyThrown(thrown.transform);
            return attached;
        }

        private static VineAnchor CreateThrowAnchor(Transform attachment, Vector3 worldAttachPoint,
            Vector3 worldAttachNormal, Vector3 worldGripPoint)
        {
            Vector3 direction = worldGripPoint - worldAttachPoint;
            float length = direction.magnitude;
            if (attachment == null || length <= 0.1f) return null;

            GameObject pivot = new GameObject("VineSwingPivot");
            pivot.transform.SetPositionAndRotation(worldAttachPoint,
                Quaternion.FromToRotation(Vector3.down, direction.normalized));

            GameObject anchor = new GameObject("VineAnchor");
            anchor.transform.SetParent(pivot.transform, false);
            anchor.transform.localPosition = Vector3.down * length;
            VineAnchor vineAnchor = anchor.AddComponent<VineAnchor>();
            vineAnchor.ConfigureSwing(pivot.transform);
            vineAnchor.ConfigureAttachment(attachment, worldAttachPoint, worldAttachNormal);
            vineAnchor.SetGrappleVisualEndPoint(worldGripPoint);
            vineAnchor.ConfigureGrappleVisual();
            return vineAnchor;
        }

        private static Material GetThrowVineMaterial()
        {
            if (sharedThrowVineMaterial != null) return sharedThrowVineMaterial;
            Shader shader = ShaderLibrary.Sprite;
            sharedThrowVineMaterial = new Material(shader)
            {
                name = "GreenGrappleWeb",
                color = Color.white
            };
            sharedThrowVineMaterial.enableInstancing = true;
            return sharedThrowVineMaterial;
        }

        /// <summary>Thrown vines are single-use — the swing pivot (the vine's own parent, see
        /// Create) owns both the grabbable anchor and the visual rope, so destroying it tears
        /// the whole thing down at once. Safe to call with a vine that was never dynamically
        /// thrown (e.g. null) — it's just a no-op then.</summary>
        public static void DestroyThrown(Transform vine)
        {
            if (vine == null) return;
            Transform pivot = vine.parent;
            Object.Destroy(pivot != null ? pivot.gameObject : vine.gameObject);
        }

        public static bool IsLookedAtBy(ThirdPersonAnimalController player)
        {
            if (player == null || player.AnimalType != AnimalType.Monkey || player.IsDefeated) return false;
            if (player.IsHangingVine && !player.CanChainToAnotherVine) return false;
            Transform camera = CameraCache.MainTransform;
            Vector3 origin = camera != null ? camera.position : player.transform.position;
            return TryFindAttachment(player, origin, player.ViewAimDirection, out _, out _, out _);
        }
    }
}
