using UnityEngine;

/// <summary>
/// All the 3D math the game relies on, in one place so it is easy to read, test and
/// explain in an interview. Every gameplay script calls into here.
/// </summary>
public static class GameMath
{
    // ------------------------------------------------------------------
    // 1. CONE TESTS (dot product)
    // ------------------------------------------------------------------
    // For unit vectors a and b:  dot(a, b) = cos(angle between them).
    // So "is b within X degrees of a?"  ==  dot(a, b) >= cos(X).
    // This avoids calling Acos every frame — comparing against a cosine is cheaper.

    /// <summary>Is <paramref name="direction"/> within <paramref name="halfAngleDeg"/> of <paramref name="forward"/>?</summary>
    /// <param name="flatten">Ignore height (y) — useful for "is it in front of me" checks on the ground plane.</param>
    public static bool DirectionInCone(Vector3 forward, Vector3 direction, float halfAngleDeg, bool flatten = false)
    {
        if (flatten)
        {
            forward.y = 0f;
            direction.y = 0f;
        }
        if (forward.sqrMagnitude < 1e-6f || direction.sqrMagnitude < 1e-6f) return true;
        return Vector3.Dot(forward.normalized, direction.normalized) >= Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
    }

    /// <summary>Is <paramref name="point"/> inside a vision cone that starts at <paramref name="origin"/>?</summary>
    public static bool PointInCone(Vector3 origin, Vector3 forward, Vector3 point, float halfAngleDeg, bool flatten = false)
    {
        return DirectionInCone(forward, point - origin, halfAngleDeg, flatten);
    }

    // ------------------------------------------------------------------
    // 2. RANDOM DIRECTION INSIDE A CONE (weapon spread)
    // ------------------------------------------------------------------
    // Picking z = cos(theta) uniformly in [cos(max), 1] gives directions spread evenly
    // over the cone's cap (picking the angle itself uniformly would bunch them at the centre).
    // We build the direction around +Z and rotate it onto `forward` with a quaternion.
    public static Vector3 RandomDirectionInCone(Vector3 forward, float halfAngleDeg)
    {
        if (halfAngleDeg <= 0f || forward.sqrMagnitude < 1e-6f) return forward.normalized;

        float cosMax = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
        float z = Random.Range(cosMax, 1f);
        float phi = Random.Range(0f, 2f * Mathf.PI);
        float r = Mathf.Sqrt(1f - z * z);
        Vector3 local = new Vector3(r * Mathf.Cos(phi), r * Mathf.Sin(phi), z);

        return Quaternion.LookRotation(forward) * local; // rotate +Z onto forward
    }

    // ------------------------------------------------------------------
    // 3. RICOCHET (reflection)
    // ------------------------------------------------------------------
    // r = d - 2 (d·n) n    — Unity's Vector3.Reflect does exactly this.
    // Written out here so you can explain it.
    public static Vector3 Reflect(Vector3 incoming, Vector3 surfaceNormal)
    {
        return incoming - 2f * Vector3.Dot(incoming, surfaceNormal) * surfaceNormal;
    }

    /// <summary>Angle (deg) between the incoming ray and the surface normal. 0 = head-on, ~90 = grazing.</summary>
    public static float IncidenceAngle(Vector3 incoming, Vector3 surfaceNormal)
    {
        return Vector3.Angle(-incoming, surfaceNormal);
    }

    // ------------------------------------------------------------------
    // 4. PREDICTIVE AIM / INTERCEPT (quadratic equation)
    // ------------------------------------------------------------------
    // Target at P moving with velocity V; projectile speed s fired from S.
    // Find time t where  |P + V t - S| = s t.
    // Let D = P - S. Squaring:  (V·V - s²) t² + 2 (D·V) t + D·D = 0   ->  solve for t.
    // Aim at P + V t.
    public static bool TryGetInterceptDirection(Vector3 shooter, Vector3 target, Vector3 targetVelocity,
                                                float projectileSpeed, out Vector3 aimDirection)
    {
        Vector3 d = target - shooter;
        aimDirection = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;

        float a = Vector3.Dot(targetVelocity, targetVelocity) - projectileSpeed * projectileSpeed;
        float b = 2f * Vector3.Dot(d, targetVelocity);
        float c = Vector3.Dot(d, d);

        float t;
        if (Mathf.Abs(a) < 1e-4f)
        {
            // Target as fast as the bullet: equation becomes linear  b t + c = 0
            if (Mathf.Abs(b) < 1e-4f) return false;
            t = -c / b;
        }
        else
        {
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f) return false; // target outruns the projectile — no solution

            float sqrt = Mathf.Sqrt(discriminant);
            float t1 = (-b - sqrt) / (2f * a);
            float t2 = (-b + sqrt) / (2f * a);

            // smallest positive time
            if (t1 > 0f && t2 > 0f) t = Mathf.Min(t1, t2);
            else t = Mathf.Max(t1, t2);
        }

        if (t <= 0f) return false;

        aimDirection = (d + targetVelocity * t).normalized;
        return true;
    }

    // ------------------------------------------------------------------
    // 5. POINTS ON A CIRCLE (spawning) — polar to cartesian
    // ------------------------------------------------------------------
    public static Vector3 PointOnRing(Vector3 center, float radius, float angleRad)
    {
        return center + new Vector3(Mathf.Cos(angleRad) * radius, 0f, Mathf.Sin(angleRad) * radius);
    }

    // ------------------------------------------------------------------
    // 6. WORLD DIRECTION -> HUD ANGLE (damage indicator)
    // ------------------------------------------------------------------
    // Convert to the viewer's local space, then atan2(x, z) gives the signed yaw angle:
    // 0 = in front, +90 = right, -90 = left, ±180 = behind.
    public static float SignedYawAngle(Transform viewer, Vector3 worldDirection)
    {
        Vector3 local = viewer.InverseTransformDirection(worldDirection);
        return Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
    }
}
