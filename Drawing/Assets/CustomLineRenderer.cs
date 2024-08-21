using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class CustomLineRenderer : Graphic
{
    public List<Vector2> points = new List<Vector2>();
    public float width = 0.2f;
    public Color color = Color.black;

    [SerializeField]private int cornerSubdivisions = 64;
    [SerializeField]private float cornerAngleThreshold = 45;

    public bool isDebug = false;
    public HashSet<Vector2> debugVertices = new HashSet<Vector2>();

    private void OnDrawGizmos()
    {
        if (!isDebug) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        if (points.Count > 0)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 point = points[i];
                Gizmos.color = Color.red;
                Vector2 debugPoint = point;
                Gizmos.DrawWireSphere(debugPoint, 3);
                if (i != points.Count - 1)
                {
                    Vector2 nextPoint = points[i + 1];
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(debugPoint, nextPoint);
                }
            }
        }

        if(debugVertices.Count > 0)
        {
            int i = 0;
            foreach(Vector2 vert in debugVertices)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(vert, 2);
                Handles.Label(transform.TransformPoint(vert), i.ToString());
                i++;
            }
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        int curVertexIndex = 0;

        if (points.Count < 2)
            return;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 point = points[i];
            Vector2 normal = Vector2.zero;
            Vector2 dir = Vector2.zero;
            if (i != points.Count - 1)
            {
                Vector2 nextPoint = points[i + 1];
                dir = nextPoint - point;

                //debnug draw line
                //Vector2 debugPoint = point + new Vector2(Screen.width / 2, Screen.height / 2);
                //Debug.DrawLine(debugPoint, debugPoint + dir.normalized * 1.5f, Color.red, 30f);

                normal = new Vector2(-dir.y, dir.x).normalized;
                //Debug.DrawLine(debugPoint, debugPoint + normal * 1.5f, Color.green, 30f);

                if (i > 0 && i < points.Count - 1)
                {
                    //check if this point is the start of a corner
                    Vector2 prevDir = point - points[i-1];
                    float angle = Vector3.Angle(prevDir,dir);
                    Vector3 t = Vector3.Cross(prevDir, dir);
                    angle *= Mathf.Sign(-t.z);
                    //Debug.Log($"Normal: {dir}, NextNormal: {nextDir}, CrossValue: {t}, Angle: {angle}");
                    //if yes, draw the corner vertices and continue to next point
                    if (Mathf.Abs(angle) > cornerAngleThreshold)
                    {
                        Vector2 prevNormal = new Vector2(-prevDir.y, prevDir.x).normalized;
                        DrawVerticesForCornerOptimized(point, prevNormal, angle, vh, width, ref curVertexIndex);
                        continue;
                    }
                    else DrawVerticesForPoint(point, normal, vh, width, ref curVertexIndex);
                }
                else DrawVerticesForPoint(point, normal, vh, width, ref curVertexIndex);
                
            }
            else 
            {
                dir = point - points[i - 1];
                normal = new Vector2(-dir.y, dir.x).normalized;

                Vector2 debugPoint = point + new Vector2(Screen.width / 2, Screen.height / 2);
                //Debug.DrawLine(debugPoint, debugPoint + dir.normalized * 3f, Color.cyan, 1f);
                //Debug.DrawLine(debugPoint, debugPoint + normal.normalized * 3f, Color.magenta, 1f);
                //Debug.Log($"CurDir: {dir}, Normal: {normal}");

                //Debug.Log($"CurTriangleIndex: {curTriangleIndex}, Total vertices: {vh.currentVertCount}");
                DrawVerticesForPoint(point, normal, vh, width, ref curVertexIndex);
                //Debug.Log($"CurTriangleIndex: {curTriangleIndex}, Total vertices: {vh.currentVertCount}");
                FillEndTriangles(vh, ref curVertexIndex);
                Debug.Log($"Total vertices: {vh.currentVertCount}");
            }
            
            
        }

        //for (int i = 4; i < points.Count - 1; i++)
        //{
        //    int index = i * 2;
        //    vh.AddTriangle(index, index + 1, index + 3);
        //    vh.AddTriangle(index + 3, index + 2, index);
        //    //vh.AddTriangle(index + 2, index + 3, index + 5);
        //    //vh.AddTriangle(index + 5, index + 4, index + 3);
        //}

    }

    void DrawVerticesForCorner(Vector2 curPoint, Vector2 normal,float angle, VertexHelper vh, float thickness, ref int curVertecIndex)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        float angleIncrement = Mathf.Sign(angle) * 360f / (float)cornerSubdivisions;
        int incTimes = Mathf.CeilToInt(Mathf.Abs(angle / angleIncrement));
        for(int i = 0; i < incTimes+1; i++)
        {
            float rotAngle = angleIncrement * i;
            if(Mathf.Abs(rotAngle) > Mathf.Abs(angle)) rotAngle = angle;
            Vector2 currentNormal = Quaternion.AngleAxis(rotAngle, Vector3.back) * normal;

            vertex.position = (Vector3)(curPoint - thickness * currentNormal);
            vh.AddVert(vertex);
            debugVertices.Add(vertex.position);

            vertex.position = (Vector3)(curPoint + thickness * currentNormal);
            vh.AddVert(vertex);
            debugVertices.Add(vertex.position);

            //add triangles
            if (curVertecIndex > 1 && vh.currentVertCount >= 4)
            {
                vh.AddTriangle(curVertecIndex - 2, curVertecIndex - 1, curVertecIndex + 1);
                vh.AddTriangle(curVertecIndex + 1, curVertecIndex, curVertecIndex - 2);
                curVertecIndex += 2;
            }
            else curVertecIndex = vh.currentVertCount - 2;

        }

    }

    void DrawVerticesForCornerOptimized(Vector2 curPoint, Vector2 normal, float angle, VertexHelper vh, float thickness, ref int curVertexIndex)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        float angleIncrement = Mathf.Sign(angle) * 360f / (float)cornerSubdivisions;
        int incTimes = Mathf.CeilToInt(Mathf.Abs(angle / angleIncrement));
        //start from the begin normal
        vertex.position = (Vector3)(curPoint - thickness * normal);
        vh.AddVert(vertex);
        debugVertices.Add(vertex.position);

        vertex.position = (Vector3)(curPoint + thickness * normal);
        vh.AddVert(vertex);
        debugVertices.Add(vertex.position);

        if (curVertexIndex > 1 && vh.currentVertCount >= 4)
        {
            vh.AddTriangle(curVertexIndex - 2, curVertexIndex - 1, curVertexIndex + 1);
            vh.AddTriangle(curVertexIndex + 1, curVertexIndex, curVertexIndex - 2);
            curVertexIndex += 2;
            vh.AddTriangle(curVertexIndex - 2, curVertexIndex - 1, curVertexIndex + 1);
            vh.AddTriangle(curVertexIndex + 1, curVertexIndex, curVertexIndex - 2);
        }
        else curVertexIndex = vh.currentVertCount - 2;

        //deal with the round corner vetices
        vertex.position = (Vector3)curPoint;
        vh.AddVert(vertex);
        debugVertices.Add(vertex.position);

        
        int centerIndex = curVertexIndex + 2;
        vh.AddTriangle(centerIndex - 1, centerIndex, centerIndex + 1);
        //set the first round corner triangle
        curVertexIndex += 3;
        float tempAngle = angleIncrement;
        if (Mathf.Abs(tempAngle) > Mathf.Abs(angle)) tempAngle = angle;
        Vector2 tempNormal = Quaternion.AngleAxis(tempAngle, Vector3.back) * normal;

        if (angleIncrement < 0) vertex.position = (Vector3)(curPoint - thickness * tempNormal);
        else vertex.position = (Vector3)(curPoint + thickness * tempNormal);
        vh.AddVert(vertex);
        debugVertices.Add(vertex.position);

        if (angleIncrement < 0) vh.AddTriangle(curVertexIndex - 3, curVertexIndex - 1, curVertexIndex);
        else vh.AddTriangle(curVertexIndex - 1, curVertexIndex - 2, curVertexIndex);

        for (int i = 2; i < incTimes; i++)
        {
            float rotAngle = angleIncrement * i;
            if (Mathf.Abs(rotAngle) > Mathf.Abs(angle)) rotAngle = angle;
            Vector2 currentNormal = Quaternion.AngleAxis(rotAngle, Vector3.back) * normal;

            if(angleIncrement < 0) vertex.position = (Vector3)(curPoint - thickness * currentNormal);
            else vertex.position = (Vector3)(curPoint + thickness * currentNormal);
            vh.AddVert(vertex);
            debugVertices.Add(vertex.position);

            //add triangles
            if (angleIncrement < 0) vh.AddTriangle(centerIndex, curVertexIndex + 1, curVertexIndex);
            else vh.AddTriangle(centerIndex, curVertexIndex, curVertexIndex + 1);
            curVertexIndex ++;
            
        }

        //end with the end normal
        tempNormal = Quaternion.AngleAxis(angle, Vector3.back) * normal;
        vertex.position = (Vector3)(curPoint - thickness * tempNormal);
        vh.AddVert(vertex);
        debugVertices.Add(vertex.position);

        vertex.position = (Vector3)(curPoint + thickness * tempNormal);
        vh.AddVert(vertex);
        debugVertices.Add(vertex.position);

        curVertexIndex += 1;
        //vh.AddTriangle(curTriangleIndex, curTriangleIndex - 1, curTriangleIndex + 1);

    }

    void DrawVerticesForPoint(Vector2 curPoint, Vector2 normal, VertexHelper vh, float thickness, ref int curTriangleIndex)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = (Vector3)(curPoint - thickness * normal);
        vh.AddVert(vertex);
        debugVertices.Add(vertex.position);

        vertex.position = (Vector3)(curPoint + thickness * normal);
        vh.AddVert(vertex);
        debugVertices.Add(vertex.position);

        if (curTriangleIndex > 1 && vh.currentVertCount >= 4)
        {
            vh.AddTriangle(curTriangleIndex - 2, curTriangleIndex - 1, curTriangleIndex + 1);
            vh.AddTriangle(curTriangleIndex + 1, curTriangleIndex, curTriangleIndex - 2);
            curTriangleIndex += 2;
        }
        else curTriangleIndex = vh.currentVertCount - 2;
    }

    void FillEndTriangles(VertexHelper vh, ref int curTriangleIndex)
    {
        if (curTriangleIndex > 1 && vh.currentVertCount >= 4)
        {
            vh.AddTriangle(curTriangleIndex - 2, curTriangleIndex - 1, curTriangleIndex + 1);
            vh.AddTriangle(curTriangleIndex + 1, curTriangleIndex, curTriangleIndex - 2);
            curTriangleIndex += 2;
        }
    }

    private void Update()
    {
        SetVerticesDirty();
    }

}

