using UnityEngine;
using UnityEngine.UI;

public class BindTexture : MonoBehaviour
{
    public RayShooter sim;
    public RawImage img;

    void Start()
    {
        img.texture = sim.Output;
    }

}
