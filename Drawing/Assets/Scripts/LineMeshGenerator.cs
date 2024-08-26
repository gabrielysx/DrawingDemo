using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using UnityEngine.UIElements;

[Serializable]
public struct LinePoint
{
    public LinePoint(Vector2 position, float time)
    {
        this.position = position;
        this.time = time;
    }
    public Vector2 position;
    public float time;
}

public class LineMeshGenerator : MonoBehaviour
{
    [SerializeField] private List<LinePoint> points = new List<LinePoint>();
    public float width = 0.2f;
    private float originalWidth;

    public Color32 color = Color.black;
    private List<Color32> vertexColors = new List<Color32>();

    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private MeshFilter meshFilter;

    [SerializeField] private int cornerSubdivisions = 64;
    [SerializeField] private float cornerAngleThreshold = 45;
    [SerializeField] private float endTailVelocityThreshold = 1.5f;

    public bool isDebug = false;
    public HashSet<Vector2> debugVertices = new HashSet<Vector2>();
    List<Vector2> accList = new List<Vector2>();
    List<Vector2> velList = new List<Vector2>();

    private void OnDrawGizmos()
    {
        if (!isDebug) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        if (points.Count > 0)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Gizmos.color = Color.cyan;
                Vector2 debugPoint = points[i].position;
                Gizmos.DrawWireSphere(debugPoint, 0.015f);
                if (i != points.Count - 1)
                {
                    Vector2 nextPoint = points[i + 1].position;
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(debugPoint, nextPoint);
                }

                if (velList.Count > 0 && accList.Count > 0)
                {
                    Gizmos.color = Color.red;
                    int temp = Mathf.Clamp(i, 0, velList.Count - 1);

                    try
                    {
                        Gizmos.DrawLine(debugPoint, debugPoint + velList[temp] / 1000f);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"list length: {velList.Count}, current index: {temp}");
                    }

                    Gizmos.color = Color.blue;
                    temp = Mathf.Clamp(i, 0, accList.Count - 1);
                    try
                    {
                        Gizmos.DrawLine(debugPoint, debugPoint + accList[temp] / 1000f);
                    }
                    catch (Exception)
                    {
                        Debug.LogError($"list length: {accList.Count}, current index: {temp}");
                    }
                }
            }
        }

        if (debugVertices.Count > 0)
        {
            int i = 0;
            foreach (Vector2 vert in debugVertices)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(vert, 0.01f);
                Handles.Label(transform.TransformPoint(vert), i.ToString());
                i++;
            }
        }
    }

    public void RefreshPoints(List<LinePoint> newPoints)
    {
        points = newPoints;
        DrawLine();
    }

    private void OnEnable()
    {
        originalWidth = width;
    }

    void DrawLine()
    {
        Profiler.BeginSample("Line Mesh Generation");
        width = originalWidth;
        int curVertexIndex = 0;
        int curTriangleIndex = 0;
        List<Vector3> verticesList = new List<Vector3>();
        vertexColors.Clear();
        List<int> trianglesList = new List<int>();

        if (points.Count < 2)
        {
            Profiler.EndSample();
            return;
        }

        int tailStartIndex = points.Count;
        float tailLastTime = 0;

        //initialize debug acc list and velocity list
        accList.Clear();
        velList.Clear();
        foreach (LinePoint point in points)
        {
            accList.Add(Vector2.zero);
            velList.Add(Vector2.zero);
        }

        //post process the end points for the tail
        if (points.Count > 2)
        {
            Vector2 tempVel = (points[points.Count - 1].position - points[points.Count - 2].position)/(points[points.Count - 1].time - points[points.Count - 2].time);
            if(tempVel.magnitude > endTailVelocityThreshold)
            {
                Vector2 curVel = tempVel;
                //trace back from the end point to find the last point that don't accelerate
                for (int i = points.Count - 2; i >= 1; i--)
                {
                    Debug.Log($"time offset: {points[i].time - points[i - 1].time}");
                    Vector2 prevVel = (points[i].position - points[i - 1].position) / (points[i].time - points[i - 1].time);
                    Debug.Log($"Previous velocity: {prevVel}");
                    Vector2 acc = (curVel - prevVel) / (points[i].time - points[i - 1].time);
                    Debug.Log($"Accelleration: {acc}");

                    accList[i - 1] = acc;
                    velList[i] = curVel;
                    velList[i - 1] = prevVel;

                    float dirMatch = Vector2.Dot(acc.normalized, prevVel.normalized);
                    Debug.Log($"Dot product: {dirMatch}");
                    if (dirMatch < 0)
                    {
                        tailStartIndex = i;
                        tailLastTime = points[points.Count - 1].time - points[i].time;
                        Debug.Log($"Tail last time: {tailLastTime}");
                        break;
                    }
                    else if (i == 1)
                    {
                        tailStartIndex = i;
                        tailLastTime = points[points.Count - 1].time - points[i].time;
                        Debug.Log($"Tail last time: {tailLastTime}");
                        break;
                    }
                    curVel = prevVel;
                }
            }
            //if the tail is too short then remove the tail
            if (tailStartIndex > points.Count - 3)
            {
                tailStartIndex = points.Count;
                tailLastTime = 0;
            }
        }

        for(int i = 0; i < points.Count ; i++)
        {
            Vector2 pointPos = points[i].position;
            Vector2 normal = Vector2.zero;
            Vector2 dir = Vector2.zero;

            if (i >= tailStartIndex) 
            {
                float curTimePortion = (points[i].time - points[tailStartIndex].time) / tailLastTime;
                Debug.Log($"Current time portion: {curTimePortion}");
                width = Mathf.Clamp(originalWidth * (1 - curTimePortion), 0.001f, originalWidth);
                Debug.Log($"Current width: {width}");
            }

            if (i != points.Count - 1)
            {
                Vector2 nextPointPos = points[i + 1].position;
                dir = nextPointPos - pointPos;
                normal = new Vector2(-dir.y, dir.x).normalized;

                if (i > 0 && i < points.Count - 1)
                {
                    Vector2 prevDir = pointPos - points[i - 1].position;
                    float angle = Vector3.Angle(prevDir, dir);
                    Vector3 t = Vector3.Cross(prevDir, dir);
                    angle *= Mathf.Sign(-t.z);
                    if (Mathf.Abs(angle) > cornerAngleThreshold)
                    {
                        Vector2 prevNormal = new Vector2(-prevDir.y, prevDir.x).normalized;
                        DrawVerticesForCornerOptimized(pointPos, prevNormal, angle, width, ref verticesList,ref trianglesList, ref curVertexIndex, ref curTriangleIndex);
                        continue;
                    }
                    else DrawVerticesForPoint(pointPos, normal, width, ref verticesList, ref trianglesList, ref curVertexIndex, ref curTriangleIndex);
                }
                else DrawVerticesForPoint(pointPos, normal, width, ref verticesList, ref trianglesList, ref curVertexIndex, ref curTriangleIndex);
            }
            else
            {
                dir = pointPos - points[i - 1].position;
                normal = new Vector2(-dir.y, dir.x).normalized;
                DrawVerticesForPoint(pointPos, normal, width, ref verticesList, ref trianglesList, ref curVertexIndex, ref curTriangleIndex);
                FillEndTriangles(ref verticesList, ref trianglesList, ref curVertexIndex, ref curTriangleIndex);
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = verticesList.ToArray();
        mesh.triangles = trianglesList.ToArray();
        mesh.colors32 = vertexColors.ToArray();
        meshFilter.mesh = mesh;


        Profiler.EndSample();
    }

    void DrawVerticesForCornerOptimized(Vector2 curPoint, Vector2 normal, float angle, float thickness, ref List<Vector3> verticesList, ref List<int> trianglesList, ref int curVertexIndex, ref int curTriangleIndex)
    {
        //vertex.color = color;
        float angleIncrement = Mathf.Sign(angle) * 360f / (float)cornerSubdivisions;
        int incTimes = Mathf.CeilToInt(Mathf.Abs(angle / angleIncrement));
        //start from the begin normal
        Vector3 vPos = (Vector3)(curPoint - thickness * normal);
        AddVert(ref verticesList, vPos);
        debugVertices.Add(vPos);

        vPos = (Vector3)(curPoint + thickness * normal);
        AddVert(ref verticesList, vPos);
        debugVertices.Add(vPos);

        if (curVertexIndex > 1 && verticesList.Count >= 4)
        {
            AddTriangle(ref trianglesList, ref curTriangleIndex, curVertexIndex - 2, curVertexIndex - 1, curVertexIndex + 1);
            AddTriangle(ref trianglesList, ref curTriangleIndex, curVertexIndex + 1, curVertexIndex, curVertexIndex - 2);
            curVertexIndex += 2;
            AddTriangle(ref trianglesList, ref curTriangleIndex, curVertexIndex - 2, curVertexIndex - 1, curVertexIndex + 1);
            AddTriangle(ref trianglesList, ref curTriangleIndex, curVertexIndex + 1, curVertexIndex, curVertexIndex - 2);
        }
        else curVertexIndex = verticesList.Count - 2;

        //deal with the round corner vetices
        vPos = (Vector3)curPoint;
        AddVert(ref verticesList, vPos);
        debugVertices.Add(vPos);


        int centerIndex = curVertexIndex + 2;
        AddTriangle(ref trianglesList, ref curTriangleIndex, centerIndex - 1, centerIndex, centerIndex + 1);
        //set the first round corner triangle
        curVertexIndex += 3;
        float tempAngle = angleIncrement;
        if (Mathf.Abs(tempAngle) > Mathf.Abs(angle)) tempAngle = angle;
        Vector2 tempNormal = Quaternion.AngleAxis(tempAngle, Vector3.back) * normal;

        if (angleIncrement < 0) vPos = (Vector3)(curPoint - thickness * tempNormal);
        else vPos = (Vector3)(curPoint + thickness * tempNormal);
        AddVert(ref verticesList, vPos);
        debugVertices.Add(vPos);

        if (angleIncrement < 0) AddTriangle(ref trianglesList, ref curTriangleIndex, curVertexIndex - 3, curVertexIndex - 1, curVertexIndex);
        else AddTriangle(ref trianglesList, ref curTriangleIndex, curVertexIndex - 1, curVertexIndex - 2, curVertexIndex);

        for (int i = 2; i < incTimes; i++)
        {
            float rotAngle = angleIncrement * i;
            if (Mathf.Abs(rotAngle) > Mathf.Abs(angle)) rotAngle = angle;
            Vector2 currentNormal = Quaternion.AngleAxis(rotAngle, Vector3.back) * normal;

            if (angleIncrement < 0) vPos = (Vector3)(curPoint - thickness * currentNormal);
            else vPos = (Vector3)(curPoint + thickness * currentNormal);
            AddVert(ref verticesList, vPos);
            debugVertices.Add(vPos);

            //add triangles
            if (angleIncrement < 0) AddTriangle(ref trianglesList, ref curTriangleIndex, centerIndex, curVertexIndex + 1, curVertexIndex);
            else AddTriangle(ref trianglesList, ref curTriangleIndex, centerIndex, curVertexIndex, curVertexIndex + 1);
            curVertexIndex++;

        }

        //end with the end normal
        tempNormal = Quaternion.AngleAxis(angle, Vector3.back) * normal;
        vPos = (Vector3)(curPoint - thickness * tempNormal);
        AddVert(ref verticesList, vPos);
        debugVertices.Add(vPos);

        vPos = (Vector3)(curPoint + thickness * tempNormal);
        AddVert(ref verticesList, vPos);
        debugVertices.Add(vPos);

        curVertexIndex += 1;
        //vh.AddTriangle(curTriangleIndex, curTriangleIndex - 1, curTriangleIndex + 1);

    }

    void DrawVerticesForPoint(Vector2 curPoint, Vector2 normal, float thickness, ref List<Vector3> verticesList, ref List<int> trianglesList, ref int curVertexIndex, ref int curTriangleIndex)
    {
        //vertex.color = color;

        Vector3 vPos = (Vector3)(curPoint - thickness * normal);
        AddVert(ref verticesList, vPos);
        debugVertices.Add(vPos);

        vPos = (Vector3)(curPoint + thickness * normal);
        AddVert(ref verticesList, vPos);
        debugVertices.Add(vPos);

        if (curVertexIndex > 1 && verticesList.Count >= 4)
        {
            AddTriangle(ref trianglesList, ref curTriangleIndex, curVertexIndex - 2, curVertexIndex - 1, curVertexIndex + 1);
            AddTriangle(ref trianglesList, ref curTriangleIndex, curVertexIndex + 1, curVertexIndex, curVertexIndex - 2);
            curVertexIndex += 2;
        }
        else curVertexIndex = verticesList.Count - 2;
    }

    void FillEndTriangles(ref List<Vector3> verticesList, ref List<int> trianglesList, ref int curVertexIndex, ref int curTriangleIndex)
    {
        if (curVertexIndex > 1 && verticesList.Count >= 4)
        {
            AddTriangle(ref trianglesList,ref curTriangleIndex, curVertexIndex - 2, curVertexIndex - 1, curVertexIndex + 1);
            AddTriangle(ref trianglesList, ref curTriangleIndex, curVertexIndex + 1, curVertexIndex, curVertexIndex - 2);
            curVertexIndex += 2;
        }
    }

    void AddVert(ref List<Vector3> verticesList, Vector3 newVertex)
    {
        verticesList.Add(newVertex);
        vertexColors.Add(color);
    }

    void AddTriangle(ref List<int> trianglesList, ref int curTriangleIndex, int index0, int index1, int index2)
    {
        trianglesList.Add(index0);
        trianglesList.Add(index1);
        trianglesList.Add(index2);
        curTriangleIndex += 3;
    }
}
