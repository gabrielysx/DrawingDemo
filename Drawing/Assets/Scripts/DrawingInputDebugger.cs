using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

public class DrawingInputDebugger : MonoBehaviour
{
    
    [SerializeField] bool isDebug = false;
    [SerializeField] private List<Vector2> pointsDebug = new List<Vector2>();
    private void OnDrawGizmosSelected()
    {
        if (!isDebug) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        if (pointsDebug.Count > 0)
        {
            for (int i = 0; i < pointsDebug.Count; i++)
            {
                UnityEngine.Vector2 tempPoint = pointsDebug[i];
                Gizmos.DrawWireSphere(tempPoint, 0.01f);
                Handles.Label(transform.TransformPoint(tempPoint), i.ToString());
                if (i != pointsDebug.Count - 1)
                {
                    Gizmos.DrawLine(tempPoint, pointsDebug[i + 1]);
                }
            }
        }


    }

    private void Update()
    {
        
    }

}
