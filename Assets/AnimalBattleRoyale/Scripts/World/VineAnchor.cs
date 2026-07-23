using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalBattleRoyale
{
    /// <summary>Hang point on jungle trees used by the monkey's Q leap. Grappling-hook style:
    /// aiming Q at a tree within <see cref="ThrowRange"/> throws a vine at it and swings the
    /// monkey there — the anchor is created on demand at throw time and torn down once the
    /// monkey leaves it, rather than being a permanent decoration pre-placed on trees.</summary>
    public sealed class VineAnchor : MonoBehaviour
    {
        // Max distance to land a throw, grabbing the first tree or chaining mid-swing alike.
        public const float ThrowRange = 30f;
        private const int IndicatorSegments = 24;
        private const float MaximumSwingAngle = 32f;
        private const float SwingSpring = 24f;
        private const float SwingDamping = 3.1f;
        private const float AmbientSwayDegrees = 5f;
        private static readonly List<VineAnchor> anchors = new List<VineAnchor>();
        private static readonly List<Transform> throwableTrees = new List<Transform>();
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
        private float ambientPhaseX;
        private float ambientPhaseZ;

        public Vector3 SwingVelocity => swingVelocity;

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
            if (swingPivot == null || lastDrivenFrame == Time.frameCount) return;
            SimulateSwing(Vector3.zero, Time.deltaTime);
        }

        /// <summary>Pushes the bottom of the vine toward the requested world direction.</summary>
        public void DriveSwing(Vector3 worldDirection, float deltaTime)
        {
            if (swingPivot == null) return;
            lastDrivenFrame = Time.frameCount;
            SimulateSwing(worldDirection, deltaTime);
        }

        // Previews where a throw would actually land — one shared ring (not one per tree,
        // since there's no pre-placed anchor to hang it on anymore) shown on whichever tree
        // currently scores best against FindBestTree's aim/range test.
        internal static void TickIndicators(ThirdPersonAnimalController player, Camera camera, float time)
        {
            bool monkeyActive = player != null && camera != null && player.AnimalType == AnimalType.Monkey
                && !player.IsDefeated && !player.IsVineLeaping
                && (!player.IsHangingVine || player.CanChainToAnotherVine);

            Vector3 point = default;
            Transform target = monkeyActive ? FindBestTree(player, camera.transform, out point) : null;
            if (target == null)
            {
                if (previewIndicator != null) previewIndicator.enabled = false;
                return;
            }

            float pulse = Mathf.Sin(time * 7f);
            float radius = 0.2f + pulse * 0.018f;
            float width = 0.022f + pulse * 0.003f;
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

        /// <summary>A tree the monkey can currently throw a vine at, once every spawned tree
        /// has been registered via <see cref="RegisterThrowableTree"/>.</summary>
        public static void RegisterThrowableTree(Transform tree)
        {
            if (tree != null && !throwableTrees.Contains(tree)) throwableTrees.Add(tree);
        }

        // How close (horizontally) the aim ray has to pass by a tree's trunk to count as
        // "aiming at" it — generous enough that roughly aiming at the tree (not needing to
        // land a pixel-precise shot on a thin trunk) is enough.
        private const float GrabRadius = 3.5f;
        // Attach height is wherever the aim ray crosses the trunk, clamped to this band of
        // the tree's own height so a throw can never land below the roots or above the
        // canopy — the fraction is against tree.lossyScale.y since these prefabs' own local
        // bounds are ~1 unit tall, so world scale doubles as world height.
        private const float MinAttachHeightFraction = 0.2f;
        private const float MaxAttachHeightFraction = 0.85f;

        /// <summary>Where a throw at this tree would land, following the aim ray up/down the
        /// trunk (so aiming low grabs a low branch, aiming high grabs a high one) instead of a
        /// fixed height — and how far off-axis that ray passed, used to judge "is this even
        /// aimed at the tree" and to pick the best tree when more than one qualifies.</summary>
        private static bool TryGetAimedTrunkPoint(Transform tree, Vector3 rayOrigin, Vector3 rayDirection,
            out Vector3 worldPoint, out float horizontalDistance)
        {
            worldPoint = default;
            horizontalDistance = float.MaxValue;
            if (tree == null) return false;

            Vector2 rayDirXZ = new Vector2(rayDirection.x, rayDirection.z);
            float rayDirXZSqrLength = rayDirXZ.sqrMagnitude;
            if (rayDirXZSqrLength < 0.0001f) return false; // aiming straight up/down

            Vector2 toTreeXZ = new Vector2(tree.position.x - rayOrigin.x, tree.position.z - rayOrigin.z);
            float t = Vector2.Dot(toTreeXZ, rayDirXZ) / rayDirXZSqrLength;
            if (t < 0f) return false; // trunk is behind the camera

            Vector3 closestOnRay = rayOrigin + rayDirection * t;
            horizontalDistance = Vector2.Distance(new Vector2(closestOnRay.x, closestOnRay.z),
                new Vector2(tree.position.x, tree.position.z));
            if (horizontalDistance > GrabRadius) return false;

            float height = Mathf.Max(1f, tree.lossyScale.y);
            float attachHeight = Mathf.Clamp(closestOnRay.y - tree.position.y,
                height * MinAttachHeightFraction, height * MaxAttachHeightFraction);
            worldPoint = new Vector3(tree.position.x, tree.position.y + attachHeight, tree.position.z);
            return true;
        }

        // Nudges the anchor slightly toward whichever side the ray approached from and down
        // a bit further for the free-hanging grab handle, purely so the vine reads as tied
        // onto the trunk surface and has a bit of hang instead of floating exactly on the
        // (otherwise invisible) trunk centerline.
        private static void GetAttachmentLocalPoints(Transform tree, Vector3 worldAttachPoint,
            Vector3 fromPosition, out Vector3 localStart, out Vector3 localEnd)
        {
            Vector3 fromLocal = tree.InverseTransformPoint(fromPosition);
            float side = fromLocal.x >= 0f ? 1f : -1f;
            float front = fromLocal.z >= 0f ? 1f : -1f;
            Vector3 attachLocal = tree.InverseTransformPoint(worldAttachPoint);
            localStart = attachLocal + new Vector3(side * 0.17f, 0f, front * 0.17f);
            localEnd = attachLocal + new Vector3(side * 0.22f, -0.36f, front * 0.2f);
        }

        /// <summary>Finds the best tree to throw at right now: any tree the aim ray passes
        /// close enough to its trunk, preferring the most precisely aimed one when several
        /// qualify (e.g. two trees roughly in line with each other).</summary>
        private static Transform FindBestTree(ThirdPersonAnimalController monkey, Transform camera,
            out Vector3 bestPoint)
        {
            bestPoint = default;
            if (monkey == null) return null;
            Vector3 rayOrigin = camera != null ? camera.position : monkey.transform.position;
            Vector3 rayDirection = monkey.ViewAimDirection;

            Transform best = null;
            float bestHorizontalDistance = float.MaxValue;
            for (int i = 0; i < throwableTrees.Count; i++)
            {
                Transform tree = throwableTrees[i];
                if (tree == null) continue;
                if (!TryGetAimedTrunkPoint(tree, rayOrigin, rayDirection, out Vector3 point, out float horizontalDistance)) continue;
                if ((point - monkey.transform.position).sqrMagnitude > ThrowRange * ThrowRange) continue;
                if (horizontalDistance >= bestHorizontalDistance) continue;
                best = tree;
                bestHorizontalDistance = horizontalDistance;
                bestPoint = point;
            }
            return best;
        }

        /// <summary>Throws a vine at whatever tree is currently aimed at (see FindBestTree)
        /// and, if one's in range, grabs/chains to it exactly like the old pre-placed-vine
        /// flow — BeginVineLeap etc. don't care whether the anchor was already there or was
        /// just created for this throw.</summary>
        public static bool TryThrowVine(ThirdPersonAnimalController monkey)
        {
            if (monkey == null || monkey.AnimalType != AnimalType.Monkey || monkey.IsVineLeaping) return false;
            if (monkey.IsHangingVine && !monkey.CanChainToAnotherVine) return false;

            Transform camera = CameraCache.MainTransform;
            Transform tree = FindBestTree(monkey, camera, out Vector3 point);
            if (tree == null) return false;

            VineAnchor thrown = CreateThrowAnchor(tree, point, monkey.transform.position);
            if (thrown == null) return false;

            return monkey.IsHangingVine
                ? monkey.TryLaunchToVine(thrown.transform)
                : monkey.TryGrabVine(thrown.transform);
        }

        private static VineAnchor CreateThrowAnchor(Transform tree, Vector3 worldAttachPoint, Vector3 fromPosition)
        {
            GetAttachmentLocalPoints(tree, worldAttachPoint, fromPosition, out Vector3 localStart, out Vector3 localEnd);
            return Create(tree, localStart, localEnd, GetThrowVineMaterial());
        }

        private static Material GetThrowVineMaterial()
        {
            if (sharedThrowVineMaterial != null) return sharedThrowVineMaterial;
            Shader shader = ShaderLibrary.Lit;
            Color color = new Color(0.48f, 0.88f, 0.16f);
            sharedThrowVineMaterial = new Material(shader) { name = "ThrownVine", color = color };
            if (sharedThrowVineMaterial.HasProperty("_BaseColor")) sharedThrowVineMaterial.SetColor("_BaseColor", color);
            if (sharedThrowVineMaterial.HasProperty("_Glossiness")) sharedThrowVineMaterial.SetFloat("_Glossiness", 0.18f);
            if (sharedThrowVineMaterial.HasProperty("_Smoothness")) sharedThrowVineMaterial.SetFloat("_Smoothness", 0.18f);
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
            if (player == null || player.AnimalType != AnimalType.Monkey || player.IsDefeated || player.IsVineLeaping) return false;
            if (player.IsHangingVine && !player.CanChainToAnotherVine) return false;
            return FindBestTree(player, CameraCache.MainTransform, out _) != null;
        }
    }
}
