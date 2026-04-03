using UnityEngine;

namespace FWTCG.FX
{
    /// <summary>
    /// Static helpers for spawning FX prefabs in world space.
    /// Adapted from TCG Engine's FXTool — removed GameBoard/Projectile dependencies,
    /// uses Quaternion.identity rotation (2D UI-based project).
    /// DoProjectileFX will be implemented in VFX-8.
    /// </summary>
    public static class FXTool
    {
        private const float DefaultFXLifetime = 5f;

        /// <summary>Instantiate a FX prefab at world position, auto-destroy after <paramref name="duration"/> seconds.</summary>
        public static GameObject DoFX(GameObject fxPrefab, Vector3 pos, float duration = DefaultFXLifetime)
        {
            if (fxPrefab == null)
                return null;

            GameObject fx = Object.Instantiate(fxPrefab, pos, Quaternion.identity);
            Object.Destroy(fx, duration);
            return fx;
        }

        /// <summary>Instantiate a FX prefab that snaps to (and follows) <paramref name="snapTarget"/>.</summary>
        public static GameObject DoSnapFX(GameObject fxPrefab, Transform snapTarget, float duration = DefaultFXLifetime)
        {
            return DoSnapFX(fxPrefab, snapTarget, Vector3.zero, duration);
        }

        /// <summary>Instantiate a FX prefab that snaps to <paramref name="snapTarget"/> with an offset.</summary>
        public static GameObject DoSnapFX(GameObject fxPrefab, Transform snapTarget, Vector3 offset, float duration = DefaultFXLifetime)
        {
            if (fxPrefab == null || snapTarget == null)
                return null;

            GameObject fx = Object.Instantiate(fxPrefab, snapTarget.position + offset, Quaternion.identity);

            // HIGH-fix: expose duration so callers can control lifetime (not hardcoded 5f)
            if (duration > 0f)
                Object.Destroy(fx, duration);

            // LOW-fix: null-check AddComponent result
            SnapFX snap = fx.AddComponent<SnapFX>();
            if (snap != null)
            {
                snap.target = snapTarget;
                snap.offset = offset;
            }

            return fx;
        }

        // DoProjectileFX — implemented in VFX-8 (Projectile system)
    }
}
