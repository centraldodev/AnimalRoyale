using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Caches the main camera transform. Reading <c>Camera.main</c> runs a
    /// <c>FindGameObjectWithTag("MainCamera")</c> every access, which is wasteful
    /// for billboards that face the camera every frame. The cached reference is
    /// refreshed automatically if the camera is destroyed (Unity's overloaded
    /// <c>==</c> makes a destroyed Transform compare equal to null).
    /// </summary>
    public static class CameraCache
    {
        private static Transform mainTransform;

        public static Transform MainTransform
        {
            get
            {
                if (mainTransform == null)
                {
                    Camera camera = Camera.main;
                    if (camera != null) mainTransform = camera.transform;
                }
                return mainTransform;
            }
        }
    }
}
