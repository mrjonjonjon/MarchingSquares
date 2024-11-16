using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarchingSquaresRenderer : MonoBehaviour
{
    public ComputeShader computeShader;
    public Texture2D scalarFieldTexture; // The scalar field (grayscale) as input
    [Range(0.01f, 1f)]
    public float threshold = 0.5f;       // Isovalue for contour
    public Material lineMaterial;       // Material for drawing lines

    private ComputeBuffer lineBuffer;
    private ComputeBuffer counterBuffer;
    private LineSegment[] lineSegments;
    private int maxLines = 1000000;      // Maximum number of line segments
    private int kernelHandle;

    void Start()
    {
        // Initialize buffers and find compute shader kernel
        InitializeBuffers();
        kernelHandle = computeShader.FindKernel("CSMain");
        SetupComputeShader();
        DispatchComputeShader();
        RetrieveLineSegments();
    }

    void Update()
    {
        // Update shader settings and regenerate line segments
        DispatchComputeShader();
        RetrieveLineSegments();
    }

    private void InitializeBuffers()
    {
        // Set up compute buffers
        lineBuffer = new ComputeBuffer(maxLines, sizeof(float) * 4, ComputeBufferType.Append);
        counterBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        lineBuffer.SetCounterValue(0);
    }

    private void SetupComputeShader()
    {
        // Set parameters for compute shader
        computeShader.SetFloat("threshold", threshold);
        computeShader.SetInt("fieldWidth", scalarFieldTexture.width);
        computeShader.SetInt("fieldHeight", scalarFieldTexture.height);
        computeShader.SetTexture(kernelHandle, "ScalarField", scalarFieldTexture);
        computeShader.SetBuffer(kernelHandle, "LineSegments", lineBuffer);
        computeShader.SetBuffer(kernelHandle, "CounterBuffer", counterBuffer);
    }

    private void DispatchComputeShader()
    {
        // Reset the counter buffer
        lineBuffer.SetCounterValue(0);

         computeShader.SetFloat("threshold", threshold);

        // Dispatch the compute shader
        int threadGroupsX = Mathf.CeilToInt(scalarFieldTexture.width / 16.0f);
        int threadGroupsY = Mathf.CeilToInt(scalarFieldTexture.height / 16.0f);
        computeShader.Dispatch(kernelHandle, threadGroupsX, threadGroupsY, 1);
    }

    private void RetrieveLineSegments()
    {
        // Get the number of written segments
        int[] count = new int[1];
        ComputeBuffer.CopyCount(lineBuffer, counterBuffer, 0);
        counterBuffer.GetData(count);
        int numSegments = Mathf.Clamp(count[0], 0, maxLines);

        // Retrieve the segments
        lineSegments = new LineSegment[numSegments];
        lineBuffer.GetData(lineSegments, 0, 0, numSegments);
    }

    void OnRenderObject()
    {
        // Ensure a material is assigned
        if (!lineMaterial || lineSegments == null) return;

        // Use the line material
        lineMaterial.SetPass(0);

        // Draw the lines
        GL.Begin(GL.LINES);
        foreach (var line in lineSegments)
        {
            GL.Vertex(new Vector3(line.start.x, line.start.y, 0));
            GL.Vertex(new Vector3(line.end.x, line.end.y, 0));
        }
        GL.End();
    }

    private void ReleaseBuffers()
    {
        if (lineBuffer != null)
        {
            lineBuffer.Release();
            lineBuffer = null;
        }
        if (counterBuffer != null)
        {
            counterBuffer.Release();
            counterBuffer = null;
        }
    }

    void OnDestroy()
    {
        // Release resources on destroy
        ReleaseBuffers();
    }

    void OnApplicationQuit()
    {
        // Release resources on application quit
        ReleaseBuffers();
    }

    // Struct must match the shader
    private struct LineSegment
    {
        public Vector2 start;
        public Vector2 end;
    }
}
