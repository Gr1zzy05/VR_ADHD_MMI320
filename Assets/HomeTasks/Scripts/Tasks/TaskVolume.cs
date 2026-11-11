using UnityEngine;

namespace HomeTasks
{
    public static class TaskVolume
    {
        // Check in the collider's local space so rotated or resized stations still work.
        public static bool Contains(BoxCollider volume, Vector3 point)
        {
            if (volume == null || !volume.enabled) return false;
            var p = volume.transform.InverseTransformPoint(point) - volume.center;
            var half = volume.size * 0.5f;
            return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z;
        }
    }
}
