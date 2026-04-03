using UnityEngine;

namespace FWTCG.FX
{
    /// <summary>
    /// Attaches a FX GameObject to a target Transform so it follows it each frame.
    /// Auto-destroys if the target is lost.
    /// Adapted from TCG Engine's SnapFX — namespace changed to FWTCG.FX.
    /// </summary>
    public class SnapFX : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = Vector3.zero;

        void Update()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = target.position + offset;
        }
    }
}
