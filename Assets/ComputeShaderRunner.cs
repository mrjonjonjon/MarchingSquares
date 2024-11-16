using UnityEngine;

public class ComputeShaderRunner : MonoBehaviour
{
    public ComputeShader computeShader;  // Assign this in the Inspector
    public Material displayMaterial;     // Material to display the texture on a quad

    private RenderTexture renderTexture;

    void Start()
    {
        // Set up the render texture (256x256, 24-bit depth buffer)
        renderTexture = new RenderTexture(256, 256, 24);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();

        // Find the kernel handle for the compute shader
        int kernelHandle = computeShader.FindKernel("CSMain");

        // Check if the kernel is valid
        if (kernelHandle == -1)
        {
            Debug.LogError("Failed to find the kernel.");
            return;
        }

        // Set the texture for the compute shader to write to
        computeShader.SetTexture(kernelHandle, "Result", renderTexture);

        // Initial dispatch of the compute shader
        computeShader.Dispatch(kernelHandle, renderTexture.width / 8, renderTexture.height / 8, 1);
    }

    void Update()
    {
        // Get the current time for animation
        float time = Time.time; // Get current time in seconds
        
        // Find the kernel handle for the compute shader again (required for each dispatch)
        int kernelHandle = computeShader.FindKernel("CSMain");
        
        // Set the time as a global float in the compute shader
        computeShader.SetFloat("_Time", time);

        // Dispatch the compute shader to update the render texture with the new time value
        computeShader.Dispatch(kernelHandle, renderTexture.width / 8, renderTexture.height / 8, 1);
    }

    void OnRenderObject()
    {
        // Use the material to display the result on a quad
        if (displayMaterial != null)
        {
            // Assign the computed texture to the material
            displayMaterial.SetTexture("_MainTex", renderTexture);
        }
    }
}
