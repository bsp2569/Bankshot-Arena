using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Object pool of LineRenderers for bullet tracers.
/// Creating/destroying a GameObject per shot causes GC spikes and frame hitches;
/// a fixed pool reused round-robin allocates nothing after Awake.
/// </summary>
public class TracerPool : MonoBehaviour
{
    public int poolSize = 24;
    public float lifetime = 0.15f;
    public float width = 0.04f;
    public Color directColor = new Color(1f, 0.85f, 0.3f);
    public Color ricochetColor = new Color(0.3f, 0.9f, 1f);
    public Color chargedColor = new Color(0.6f, 0.4f, 1f);

    struct Tracer
    {
        public LineRenderer line;
        public float startTime;
        public bool active;
    }

    Tracer[] tracers;
    int next;

    void Awake()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(shader);

        tracers = new Tracer[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            var go = new GameObject("Tracer_" + i);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = material;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            lr.enabled = false;
            tracers[i].line = lr;
        }
    }

    public void Show(List<Vector3> points, int bounces, bool charged)
    {
        ref Tracer t = ref tracers[next];
        next = (next + 1) % tracers.Length; // round-robin: oldest tracer gets recycled

        Color c = charged ? chargedColor : (bounces > 0 ? ricochetColor : directColor);
        t.line.startColor = c;
        t.line.endColor = c;
        t.line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++) t.line.SetPosition(i, points[i]);
        t.line.widthMultiplier = width * (charged ? 2.5f : 1f);
        t.line.enabled = true;
        t.startTime = Time.time;
        t.active = true;
    }

    void Update()
    {
        for (int i = 0; i < tracers.Length; i++)
        {
            ref Tracer t = ref tracers[i];
            if (!t.active) continue;

            float k = (Time.time - t.startTime) / lifetime;
            if (k >= 1f)
            {
                t.active = false;
                t.line.enabled = false;
            }
            else
            {
                Color c = t.line.startColor;
                c.a = 1f - k; // fade out
                t.line.startColor = c;
                t.line.endColor = c;
            }
        }
    }
}
