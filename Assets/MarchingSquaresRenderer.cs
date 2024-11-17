using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


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


    [Range(0.01f, 1f)]
    public float tolerance=0.1f;

        [Range(5f, 60f)]
    public int scale = 60;   



    public PolygonCollider2D collider;

private List<List<Vector2>> GroupLineSegmentsIntoPaths(LineSegment[] lineSegments, float tolerance = 0.1f)
{
    List<List<Vector2>> allPaths = new List<List<Vector2>>();

    // Build adjacency list for fast lookup
    Dictionary<Vector2, List<LineSegment>> adjacencyList = new Dictionary<Vector2, List<LineSegment>>();
    foreach (var segment in lineSegments)
    {
        if (!adjacencyList.ContainsKey(segment.start))
            adjacencyList[segment.start] = new List<LineSegment>();
        if (!adjacencyList.ContainsKey(segment.end))
            adjacencyList[segment.end] = new List<LineSegment>();

        adjacencyList[segment.start].Add(segment);
        adjacencyList[segment.end].Add(segment);
    }

    HashSet<LineSegment> visitedSegments = new HashSet<LineSegment>();

    foreach (var segment in lineSegments)
    {
        if (visitedSegments.Contains(segment)) continue;

        // Start a new path
        List<Vector2> currentPath = new List<Vector2>();
        Stack<LineSegment> stack = new Stack<LineSegment>();

        stack.Push(segment);
        currentPath.Add(segment.start);
        currentPath.Add(segment.end);
        visitedSegments.Add(segment);

        // Perform DFS to traverse connected edges
        while (stack.Count > 0)
        {
            LineSegment currentSegment = stack.Pop();
            Vector2 currentPoint = currentPath[currentPath.Count - 1]; // Current path's endpoint

            if (adjacencyList.TryGetValue(currentPoint, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (visitedSegments.Contains(neighbor)) continue;

                    // Determine the next point to add to the path
                    Vector2 nextPoint = ArePointsApproximatelyEqual(currentPoint, neighbor.start, tolerance) 
                        ? neighbor.end 
                        : neighbor.start;

                    // Add the segment to the path
                    currentPath.Add(nextPoint);
                    stack.Push(neighbor);
                    visitedSegments.Add(neighbor);
                    break; // Continue DFS from this segment
                }
            }
        }

        // Check if the path forms a closed loop
        if (ArePointsApproximatelyEqual(currentPath[0], currentPath[currentPath.Count - 1], tolerance))
        {
            currentPath.RemoveAt(currentPath.Count - 1); // Remove duplicate endpoint
            if (currentPath.Count > 2) // Only closed loops with at least 3 points
                allPaths.Add(currentPath);
        }
    }

    return allPaths;
}

private List<List<Vector2>> GroupLineSegmentsIntoPaths_old2(LineSegment[] lineSegments, float tolerance = 0.1f)
{
    List<List<Vector2>> allPaths = new List<List<Vector2>>();
    HashSet<int> remainingIndices = new HashSet<int>(Enumerable.Range(0, lineSegments.Length));  // Track unused segments
    Dictionary<Vector2, List<int>> pointToSegmentMap = new Dictionary<Vector2, List<int>>(); // To quickly find segments by points

    // Map start and end points of each segment to their indices
    for (int i = 0; i < lineSegments.Length; i++)
    {
        var start = lineSegments[i].start;
        var end = lineSegments[i].end;
        
        if (!pointToSegmentMap.ContainsKey(start))
            pointToSegmentMap[start] = new List<int>();
        pointToSegmentMap[start].Add(i);
        
        if (!pointToSegmentMap.ContainsKey(end))
            pointToSegmentMap[end] = new List<int>();
        pointToSegmentMap[end].Add(i);
    }

    while (remainingIndices.Count > 0)
    {
        List<Vector2> currentPath = new List<Vector2>();
        int currentIndex = remainingIndices.First(); // Get any remaining index
        LineSegment currentSegment = lineSegments[currentIndex];
        remainingIndices.Remove(currentIndex);
        
        currentPath.Add(currentSegment.start);
        currentPath.Add(currentSegment.end);
        
        Vector2 firstPoint = currentSegment.start;
        bool pathComplete = false;
        
        // Continue building path until complete or no more connected segments
        while (!pathComplete && remainingIndices.Count > 0)
        {
            bool found = false;
            
            // Try to find the next segment connected to the current path
            foreach (var point in new[] { currentPath[currentPath.Count - 1], firstPoint })
            {
                if (pointToSegmentMap.ContainsKey(point))
                {
                    var candidates = pointToSegmentMap[point];
                    
                    foreach (var candidateIndex in candidates)
                    {
                        if (!remainingIndices.Contains(candidateIndex))
                            continue;

                        LineSegment candidateSegment = lineSegments[candidateIndex];
                        
                        if (ArePointsApproximatelyEqual(currentPath[currentPath.Count - 1], candidateSegment.start, tolerance))
                        {
                            currentPath.Add(candidateSegment.end);
                            remainingIndices.Remove(candidateIndex);
                            found = true;

                            if (ArePointsApproximatelyEqual(firstPoint, candidateSegment.end, tolerance))
                            {
                                pathComplete = true;
                            }

                            break;
                        }
                        else if (ArePointsApproximatelyEqual(currentPath[currentPath.Count - 1], candidateSegment.end, tolerance))
                        {
                            currentPath.Add(candidateSegment.start);
                            remainingIndices.Remove(candidateIndex);
                            found = true;

                            if (ArePointsApproximatelyEqual(firstPoint, candidateSegment.start, tolerance))
                            {
                                pathComplete = true;
                            }

                            break;
                        }
                    }

                    if (found)
                        break;
                }
                
                if (found)
                    break;
            }

            if (!found) break;
        }

        // Add the completed path if it's valid
        if (currentPath.Count > 2)
        {
            allPaths.Add(currentPath);
        }
    }

    return allPaths;
}

private List<List<Vector2>> GroupLineSegmentsIntoPaths_old(LineSegment[] lineSegments, float tolerance = 0.1f)
{
    List<List<Vector2>> allPaths = new List<List<Vector2>>();
    List<LineSegment> remainingSegments = new List<LineSegment>(lineSegments);

    while (remainingSegments.Count > 0)
    {
        List<Vector2> currentPath = new List<Vector2>();
        LineSegment currentSegment = remainingSegments[0];
        remainingSegments.RemoveAt(0);
        currentPath.Add(currentSegment.start);
        currentPath.Add(currentSegment.end);

        Vector2 firstPoint = currentSegment.start;

        // Find and add connected segments
        bool pathComplete = false;
        int maxIters = 100; // Prevent infinite loops
        int iterations = 0;

        while (iterations < maxIters && !pathComplete && remainingSegments.Count > 0)
        {
            bool found = false;
            for (int i = 0; i < remainingSegments.Count; i++)
            {
                LineSegment segment = remainingSegments[i];

                // Check if this segment connects to the current path approximately
                if (ArePointsApproximatelyEqual(currentPath[currentPath.Count - 1], segment.end, tolerance))
                {
                    found = true;
                    currentPath.Add(segment.start);
                    remainingSegments.RemoveAt(i);

                    if (ArePointsApproximatelyEqual(firstPoint, segment.start, tolerance))
                    {
                        pathComplete = true;
                    }
                    break;
                }
                else if (ArePointsApproximatelyEqual(currentPath[currentPath.Count - 1], segment.start, tolerance))
                {
                    found = true;
                    currentPath.Add(segment.end);
                    remainingSegments.RemoveAt(i);

                    if (ArePointsApproximatelyEqual(firstPoint, segment.end, tolerance))
                    {
                        pathComplete = true;
                    }
                    break;
                }
            }

            if (!found) break;
            iterations++;
        }

        // Only add paths with at least 3 points (forming a closed loop)
        if (currentPath.Count > 2)
        {
            allPaths.Add(currentPath);
        }
    }

    return allPaths;
}

// Helper method to check approximate equality of two points
private bool ArePointsApproximatelyEqual(Vector2 point1, Vector2 point2, float tolerance)
{
    return Vector2.Distance(point1, point2) <= tolerance;
}


private void GenerateColliderForChunk(LineSegment[] lineSegments)
{
    // Create a new PolygonCollider2D for the chunk
    //PolygonCollider2D collider = gameObject.AddComponent<PolygonCollider2D>();
    

    List<List<Vector2>> allPaths = GroupLineSegmentsIntoPaths(lineSegments,tolerance);

    collider.pathCount=allPaths.Count;
    GenerateCollidersForPaths(allPaths);
}

private void GenerateCollidersForPaths(List<List<Vector2>> allPaths)
{int i=0;
    foreach (var path in allPaths)
    {
        // Create a new PolygonCollider2D or EdgeCollider2D based on the path type
        //PolygonCollider2D collider = gameObject.AddComponent<PolygonCollider2D>();

        // Convert the path into points for the collider
        collider.SetPath(i, path.ToArray());
        i+=1;
    }
}


    void Start()
    {
        // Initialize buffers and find compute shader kernel
        computeShader = Instantiate(computeShader);
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
        //TransformLineSegments();
        GenerateColliderForChunk(lineSegments);
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
        computeShader.SetFloat("scale", scale);

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
                 computeShader.SetFloat("scale", scale);


        // Dispatch the compute shader
        int threadGroupsX = Mathf.CeilToInt(scalarFieldTexture.width / (1000f/scale));
        int threadGroupsY = Mathf.CeilToInt(scalarFieldTexture.height / (1000f/scale));

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
private void TransformLineSegments()
{
    for (int i = 0; i < lineSegments.Length; i++)
    {
        // Transform the start point
        Vector3 start3D = new Vector3(lineSegments[i].start.x, lineSegments[i].start.y, 0);
        Vector3 transformedStart3D = transform.TransformPoint(start3D);
        Vector2 transformedStart2D = new Vector2(transformedStart3D.x, transformedStart3D.y);

        // Transform the end point
        Vector3 end3D = new Vector3(lineSegments[i].end.x, lineSegments[i].end.y, 0);
        Vector3 transformedEnd3D = transform.TransformPoint(end3D);
        Vector2 transformedEnd2D = new Vector2(transformedEnd3D.x, transformedEnd3D.y);

        // Update the LineSegment with the transformed points
        lineSegments[i] = new LineSegment(transformedStart2D, transformedEnd2D);
    }
}

    void OnRenderObject()
    {
        return;
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

    public LineSegment(Vector2 start, Vector2 end)
    {
        this.start = start;
        this.end = end;
    }
}
}
