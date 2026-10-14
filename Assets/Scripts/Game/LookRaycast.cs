using UnityEngine;

/// <summary>
/// One shared "what is the player looking at?" ray per frame, without allocations.
///
/// Doors, chairs, the phone prompt and the computers all ask the same question every frame: what is the first
/// thing in front of the camera (ignoring the player's own colliders)? Each used to fire its own RaycastAll,
/// allocating a new array every time. Now the ray is cast once into a reusable buffer and the result is shared.
/// Each caller still applies its own reach: "nothing within my distance" is exactly "the first hit is farther
/// than my distance", so behaviour is unchanged. The cache is rebuilt if the camera moves or turns mid-frame.
/// </summary>
public static class LookRaycast
{
    /// <summary>Longest reach of any interaction (chairs use 4 m). Callers compare hit.distance to their own reach.</summary>
    public const float MaxDistance = 6f;

    static readonly RaycastHit[] buffer = new RaycastHit[32];
    static int cachedFrame = -1;
    static Camera cachedCamera;
    static Vector3 cachedPos;
    static Quaternion cachedRot;
    static bool cachedHasHit;
    static RaycastHit cachedHit;

    /// <summary>
    /// The first collider in front of <paramref name="cam"/> (triggers ignored), skipping colliders under
    /// <paramref name="ignoreRoot"/> (the player). Returns false if nothing is hit within <paramref name="maxDistance"/>.
    /// </summary>
    public static bool TryGetFirstHit(Camera cam, Transform ignoreRoot, float maxDistance, out RaycastHit hit)
    {
        hit = default;
        if (cam == null) return false;
        Transform c = cam.transform;

        if (cachedFrame != Time.frameCount || cachedCamera != cam || c.position != cachedPos || c.rotation != cachedRot)
        {
            cachedFrame = Time.frameCount;
            cachedCamera = cam;
            cachedPos = c.position;
            cachedRot = c.rotation;
            cachedHasHit = false;

            int n = Physics.RaycastNonAlloc(cachedPos, c.forward, buffer, MaxDistance, ~0, QueryTriggerInteraction.Ignore);
            Transform playerRoot = ignoreRoot != null ? ignoreRoot.root : c.root;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (buffer[i].transform.root == playerRoot) continue;   // the player's own collider
                if (buffer[i].distance < best) { best = buffer[i].distance; cachedHit = buffer[i]; cachedHasHit = true; }
            }
        }

        if (!cachedHasHit || cachedHit.distance > maxDistance) return false;
        hit = cachedHit;
        return true;
    }
}
