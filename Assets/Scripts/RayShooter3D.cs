using UnityEngine;
using UnityEngine.UI;

public class RayShooter3D : MonoBehaviour
{
    public ComputeShader cs;
    public RawImage preview;

    [Header("Rays")]
    public int   rayCount   = 200000;
    public float stepSize   = 0.05f;
    public int   maxBounces = 0;

    [Header("Emitter")]
    public Vector3 emitterPos = new Vector3(-0.4f, 0, 0);
    public Vector3 axis       = new Vector3(1, 0, 0);
    public float   sigmaDeg   = 2.0f;  // angular sigma

    [Header("Detector")]
    public int detW = 512, detH = 512;
    public float detZ = 0.5f;          // plane at z = detZ
    public Vector2 pixelsPerUnit = new Vector2(200, 200); // world→pixel scale

    ComputeBuffer rays;
    RenderTexture detector; // R32_UINT
    int kInit, kStep, kClear;
    int groups;

    void Awake()
    {
        if (!cs) { Debug.LogError("Assign compute shader"); enabled = false; return; }

        kInit  = cs.FindKernel("InitRays");
        kStep  = cs.FindKernel("StepRays");
        kClear = cs.FindKernel("ClearDetector");

        detector = new RenderTexture(detW, detH, 0, RenderTextureFormat.RInt);
        detector.enableRandomWrite = true;
        detector.filterMode = FilterMode.Point;
        detector.wrapMode = TextureWrapMode.Clamp;
        detector.Create();

        // Ray struct: float3 pos (12) + float3 dir (12) + float weight (4) + uint alive (4) + uint bounces (4) = 36 bytes
        rays = new ComputeBuffer(rayCount, 36);

        const int THREADS = 256;
        groups = Mathf.CeilToInt(rayCount / (float)THREADS);

        PushParams();

        cs.SetBuffer(kInit, "Rays", rays);
        cs.Dispatch(kInit, groups, 1, 1);

        cs.SetTexture(kClear, "Detector", detector);
        cs.SetFloats("DetSize", detW, detH);
        cs.Dispatch(kClear, Mathf.CeilToInt(detW / 16f), Mathf.CeilToInt(detH / 16f), 1);

        if (preview) preview.texture = detector;
    }

    void PushParams()
    {
        cs.SetInt("RayCount", rayCount);
        cs.SetFloat("StepSize", stepSize);
        cs.SetInt("MaxBounces", Mathf.Max(0, maxBounces));

        cs.SetFloats("EmitterPos", emitterPos.x, emitterPos.y, emitterPos.z);
        Vector3 ax = axis.normalized;
        cs.SetFloats("Axis", ax.x, ax.y, ax.z);
        cs.SetFloat("SigmaRad", Mathf.Deg2Rad * sigmaDeg);

        cs.SetFloats("DetSize", detW, detH);

        // plane z = detZ → n=(0,0,1), d = -detZ  so n·x + d = 0
        cs.SetFloats("DetPlane", 0, 0, 1, -detZ);

        // detector basis U along +X, V along +Y
        cs.SetFloats("DetU", 1, 0, 0);
        cs.SetFloats("DetV", 0, 1, 0);

        // world→pixels scale along U,V
        cs.SetFloats("DetPixScale", pixelsPerUnit.x, pixelsPerUnit.y);
    }

    void Update()
    {
        PushParams();
        cs.SetBuffer(kStep, "Rays", rays);
        cs.SetTexture(kStep, "Detector", detector);

        // a few steps per frame
        for (int i = 0; i < 4; i++)
            cs.Dispatch(kStep, groups, 1, 1);
    }

    void OnDestroy()
    {
        rays?.Dispose();
        if (detector) detector.Release();
    }
}
