using UnityEngine;
using UnityEngine.UI;

public class RayShooter : MonoBehaviour
{
    public ComputeShader rayCS;          // assign in Inspector
    public RawImage preview;             // optional: drag a UI RawImage here

    public int rayCount = 8192;
    public int texWidth = 512, texHeight = 512;
    public float stepSize = 1f;
    public float angleSpanDeg = 45f;

    RenderTexture target;
    ComputeBuffer rays;

    int kInit, kStep, kClear;
    int groups;
    int frame;

    void Awake()
    {
        if (!rayCS) { Debug.LogError("Assign the compute shader, please."); enabled = false; return; }

        // Find kernels by name exactly as in the .compute file
        kInit  = rayCS.FindKernel("InitRays");
        kStep  = rayCS.FindKernel("StepRays");
        kClear = rayCS.FindKernel("ClearTarget");

        // RenderTexture with RandomWrite BEFORE Create()
        target = new RenderTexture(texWidth, texHeight, 0, RenderTextureFormat.ARGBFloat);
        target.enableRandomWrite = true;
        target.filterMode = FilterMode.Point;
        target.wrapMode = TextureWrapMode.Clamp;
        target.Create();

        // Ray buffer: float2 pos + float2 dir + float alive = 5 floats
        rays = new ComputeBuffer(rayCount, sizeof(float) * 5);

        // Threadgroup count: our shader uses [numthreads(256,1,1)]
        const int THREADS = 256;
        groups = Mathf.CeilToInt(rayCount / (float)THREADS);

        // Push static/common params once here; we’ll update some each frame
        PushParams();
        
        // Bind resources to EACH kernel that uses them before you dispatch
        rayCS.SetBuffer(kInit,  "Rays",   rays);

        rayCS.SetTexture(kClear, "Target", target);
        rayCS.SetFloats("TargetSize", texWidth, texHeight);

        // Clear target first so you see clean trails
        rayCS.Dispatch(kClear, Mathf.CeilToInt(texWidth / 8f), Mathf.CeilToInt(texHeight / 8f), 1);

        // Initialize rays
        rayCS.Dispatch(kInit, groups, 1, 1);

        if (preview) preview.texture = target;
    }

    void PushParams()
    {
        rayCS.SetInt("RayCount", rayCount);
        rayCS.SetFloat("StepSize", stepSize);
        rayCS.SetFloat("AngleSpan", angleSpanDeg * Mathf.Deg2Rad);
        rayCS.SetFloats("TargetSize", texWidth, texHeight);
        rayCS.SetInt("Frame", frame);

        // Start at left-center
        Vector2 emitter = new Vector2(32, texHeight * 0.5f);
        rayCS.SetFloats("EmitterPos", emitter.x, emitter.y);
    }

    void Update()
    {
        frame++;
        PushParams();

        // Bind for StepRays too: both Rays and Target are used there
        rayCS.SetBuffer(kStep,  "Rays",   rays);
        rayCS.SetTexture(kStep, "Target", target);

        // march a few steps per frame
        for (int i = 0; i < 4; i++)
            rayCS.Dispatch(kStep, groups, 1, 1);
    }

    void OnDestroy()
    {
        rays?.Dispose();
        if (target) target.Release();
    }

    public RenderTexture Output => target;
}
