using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System;

public class DrawingManager : MonoBehaviour
{
    [Header("Canvas Scale Settings")]
    [SerializeField] float minCanvasScale = 0.75f;
    [SerializeField] float maxCanvasScale = 1.25f;
    [SerializeField] float canvasScaleStep = 0.1f;
    private float currentCanvasScale = 1f;

    [Header("Line Simplification Settings")]
    [SerializeField] float RDPThreshold = 0.01f;
    [SerializeField] float refineTolerance = 0.1f;

    [Header("Drawing Dependencies")]
    [SerializeField] Transform linesHolder;
    [SerializeField] GameObject linePrefab;
    private List<GameObject> lines = new List<GameObject>();
    int curLineIndex = -1;
    float curTotalDrawingTime = 0;

    [Header("Input Settings")]
    [SerializeField] private int drawingSampleRate = 60;
    [SerializeField] private float inputDistanceThreshold = 0.1f;
    private float drawingSampleTimer;
    int prevPointCount = 0;
    private List<LinePoint> userInput = new List<LinePoint>();
    private bool isDrawing = false;

    [Header("Grid Settings")]
    [SerializeField] private GridManager gridManager;

    [Header("Debug Settings")]
    [SerializeField] bool isDebug = false;
    [SerializeField] private List<LinePoint> pointsDebug = new List<LinePoint>();

    private void OnDrawGizmosSelected()
    {
        if(!isDebug) return;

        //Debug the last drawing trail
        Gizmos.matrix = transform.localToWorldMatrix;
        if (pointsDebug.Count > 0)
        {
            for (int i = 0; i < pointsDebug.Count; i++)
            {
                UnityEngine.Vector2 tempPoint = pointsDebug[i].position;
                Gizmos.DrawWireSphere(tempPoint, 0.02f);
                if (i != pointsDebug.Count - 1)
                {
                    Gizmos.DrawLine(tempPoint, pointsDebug[i + 1].position);
                }
            }
        }
    }

    private void Start()
    {
        if(gridManager != null) gridManager.GetMousePosition = GetMousePos;
    }

    private void Update()
    {
        //canvas scale control
        if (Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            float val = currentCanvasScale + Input.GetAxis("Mouse ScrollWheel") * canvasScaleStep;
            val = Mathf.Clamp(val, minCanvasScale, maxCanvasScale);
            linesHolder.localScale = new UnityEngine.Vector3(val, val, 1);
            currentCanvasScale = val;
        }

        //drawing control
        if (Input.GetMouseButtonDown(0))
        {
            isDrawing = true;
            curTotalDrawingTime = 0;
            //draw new line
            GameObject newLine = Instantiate(linePrefab, linesHolder.position, UnityEngine.Quaternion.identity, linesHolder);
            lines.Add(newLine);
            curLineIndex++;
        }
        
        if (Input.GetMouseButtonUp(0))
        {
            isDrawing = false;

            //This part is disabled for now. It is used to simplify the drawing input points with RDP algorithm
            /*
            //Refine the points with RDP
            List<UnityEngine.Vector2> pointsPos = new List<UnityEngine.Vector2>();
            List<int> refinedPointsIndexes = new List<int>();
            pointsPos = userInput.Select(x => x.position).ToList();
            
            RDP(pointsPos, RDPThreshold, 0, userInput.Count - 1, ref refinedPointsIndexes);
            refinedPointsIndexes = refinedPointsIndexes.Distinct().ToList();
            refinedPointsIndexes.Sort();
            List<LinePoint> refinedPoints = new List<LinePoint>();
            for(int i = 0; i < refinedPointsIndexes.Count;i++) //get the refined points
            {
                refinedPoints.Add(userInput[refinedPointsIndexes[i]]);

            }
            Debug.Log($"Total {userInput.Count} Input points, {pointsPos.Count} RDP points");
            List<LinePoint> newPoints = refinedPoints;
            */

            List<LinePoint> newPoints = new List<LinePoint>();
            userInput.ForEach(input => newPoints.Add(input));

            //This part below is disabled for now. It is used process the input points with Fast Fourier Transform
            /*
            for (int j = 0; j < points.Count; j++)
            {
                Debug.Log($"Point {j}: {points[j]} | FFTResult: {fftData[j]}");
            }

            //Fourier transform for potential optimization
            Complex[] fftData = ApplyFFT(points);
            Complex[] refinedFFTData = FFTDataRefine(fftData);
            List<UnityEngine.Vector2> newPoints = GeneratePointsFromData(refinedFFTData, 1);
            */

            //Refresh the line mesh renderer and pass the debug flag
            LineMeshGenerator lineMeshGenerator = lines[curLineIndex].GetComponent<LineMeshGenerator>();
            lineMeshGenerator.debugVertices.Clear();
            lineMeshGenerator.RefreshPoints(newPoints);
            lineMeshGenerator.isDebug = isDebug;

            //Below part is depracted as we are using mesh renderer to draw the lines now
            /*
            LineRenderer lineRenderer = lines[curLineIndex].GetComponent<LineRenderer>();
            lineRenderer.positionCount = newPoints.Count;
            for (int i = 0; i < newPoints.Count; i++)
            {
                lineRenderer.SetPosition(i, newPoints[i]);
            }
            */

            //Refresh the Grid Info
            gridManager.RefreshGrid();

            //Debug part
            pointsDebug = newPoints;

            //Reset the drawing input
            userInput.Clear();
            prevPointCount = 0;
        }

        //Update input points when drawing
        if (isDrawing)
        {
            drawingSampleTimer += Time.deltaTime;
            curTotalDrawingTime += Time.deltaTime;
            if(drawingSampleTimer > 1f / drawingSampleRate)
            {
                drawingSampleTimer = 0;
                UnityEngine.Vector2 mousePos = linesHolder.worldToLocalMatrix * Camera.main.ScreenToWorldPoint(Input.mousePosition);
                if (userInput.Count == 0) userInput.Add(new LinePoint(mousePos,curTotalDrawingTime));
                else 
                {
                    //Ignore the input point if it is too close to the last point in the list
                    float prevDis = UnityEngine.Vector2.Distance(userInput.Last().position, mousePos);
                    //Debug.Log($"Distance: {prevDis}");
                    if (prevDis > inputDistanceThreshold) userInput.Add(new LinePoint(mousePos, curTotalDrawingTime));
                    else userInput[userInput.Count - 1] = new LinePoint(mousePos, curTotalDrawingTime);
                }
                
            }
        }

        //Update the line mesh when the input points are updating
        if(userInput.Count > 0)
        {
            //Only update the line when there is new input point
            if(prevPointCount != userInput.Count)
            {
                List<LinePoint> newPoints = new List<LinePoint>();
                foreach (var input in userInput)
                {
                    newPoints.Add(input);
                }

                //Below part is depracted as we are using mesh renderer to draw the lines now
                /*
                LineRenderer lineRenderer = lines[curLineIndex].GetComponent<LineRenderer>();
                lineRenderer.positionCount = newPoints.Count;
                for (int i = 0; i < newPoints.Count; i++)
                {
                    lineRenderer.SetPosition(i, newPoints[i]);
                }
                */

                LineMeshGenerator lineMeshGenerator = lines[curLineIndex].GetComponent<LineMeshGenerator>();
                lineMeshGenerator.RefreshPoints(newPoints);
                lineMeshGenerator.isDebug = isDebug;

                //Refresh the input point count
                prevPointCount = userInput.Count;

                //Debug part
                pointsDebug = newPoints;
            }
        }

    }
    /// <summary>
    /// This method applies the Fast Fourier Transform on the input points
    /// </summary>
    /// <param name="points">The points list you want to apply the Fast Fourier Transform on</param>
    /// <returns>The complex array generated after Fast Fourier Transform</returns>
    Complex[] ApplyFFT(List<UnityEngine.Vector2> points)
    {
        Complex[] freqDomain = new Complex[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            freqDomain[i] = new Complex(points[i].x, points[i].y);
        }
        Fourier.Forward(freqDomain, FourierOptions.Matlab);

        return freqDomain;
    }

    /// <summary>
    /// This method simplified the Fast Fourier Transform data by setting the points with magnitude lower than the tolerance to 0
    /// </summary>
    /// <param name="fftData">The input Fast Fourier Transform data</param>
    /// <returns>The simplified data</returns>
    Complex[] FFTDataSimplify(Complex[] fftData)
    {
        List<Complex> refinedData = new List<Complex>();
        refinedData = fftData.ToList();

        int refinedCount = 0;
        for(int i = 0; i < refinedData.Count; i++)
        {
            float magnitude = (float)refinedData[i].Magnitude;
            if (magnitude < refineTolerance && i > refinedData.Count * 0.75 && refinedData.Count > 30)
            {
                refinedData[i] = new Complex(0, 0);
                refinedCount++;
            }
        }
        Debug.Log($"Total {fftData.Length} FFT points, {fftData.Length - refinedCount} Refined FFT points");
        return refinedData.ToArray();
    }

    /// <summary>
    /// This method reverses the Fast Fourier Transform data to the original points list
    /// </summary>
    /// <param name="fftData">The input Fast Fourier Transform data</param>
    /// <param name="step">The step of reverse process. This value is 1 by default so that all data will be processed.The higher the steps, the more data will be ignored when processing</param>
    /// <returns>The generated points list based on the reverse process</returns>
    List<UnityEngine.Vector2> GeneratePointsFromData(Complex[] fftData, int step = 1)
    {
        List<UnityEngine.Vector2> points = new List<UnityEngine.Vector2>();
        int n = fftData.Length;
        for (int i = 0; i < n; i+=step)
        {
            List <Complex> circlePos = new List<Complex>();
            for(int j = 0; j < n; j++)
            {
                if (j != 0 && fftData[j].Real == 0 && fftData[j].Imaginary == 0)
                {
                    circlePos.Add(circlePos[j - 1]);
                    continue;
                }
                Complex factor = fftData[j] * Complex.Exp(2 * Math.PI * i * Complex.ImaginaryOne * j / n) / n;
                //Debug.Log($"Factor {j}, {time}: {factor.Real}, {factor.Imaginary}");
                if (j!=0)
                {
                    circlePos.Add(circlePos[j - 1] + factor);
                }
                else
                {
                    circlePos.Add(factor);
                }
            }
            points.Add(new UnityEngine.Vector2((float)circlePos.Last().Real, (float)circlePos.Last().Imaginary));
            //Debug.Log($"Point {i}: {points[i]}");
        }

        return points;
    }

    /// <summary>
    /// Get the perpendicular distance between a point and a line
    /// </summary>
    /// <param name="point">The target point</param>
    /// <param name="lineK">The target line's k factor (y = kx+b)</param>
    /// <param name="lineB">The target line's b value (y = kx+b)</param>
    /// <returns></returns>
    public double GetPerpendicularDistance(UnityEngine.Vector2 point, double lineK, double lineB)
    {
        double dis = (Math.Abs(lineK * point.x - point.y + lineB)) / Math.Sqrt(lineK * lineK + 1);
        return dis;
    }


    /// <summary>
    /// This method is used to simplify the input points with Ramer-Douglas-Peucker algorithm
    /// </summary>
    /// <param name="points">The input points</param>
    /// <param name="epsilon">The minium distance tolerance for a point to be kept</param>
    /// <param name="startIndex">Current index of the start point</param>
    /// <param name="endIndex">Current index of the end point</param>
    /// <param name="outIndex">The result of kept indecies list after process</param>
    private void RDP(List<UnityEngine.Vector2> points, double epsilon, int startIndex, int endIndex, ref List<int> outIndex)
    {
        int debugT = 0;
        int n = points.Count;
        int indexOfMax = 0; //index of the point with the maximum distance
        double maxDistance = 0;
        if (epsilon == 0)
        {
            for(int i = 0; i < points.Count; i++)
            {
                outIndex.Clear();
                outIndex.Add(i); 
            }
        }
        else
        {
            //find the point with the maximum distance
            debugT++;
            double k = (points[endIndex].y - points[startIndex].y) / (points[endIndex].x - points[startIndex].x);
            double b = points[startIndex].y - k * points[startIndex].x;
            //Debug.Log($"Line{debugT}: y = {k}x + {b}, start: {points[startIndex].ToString()}, end: {points[endIndex].ToString()}");
            for (int i = startIndex; i <= endIndex; i++)
            {
                double pDistance = GetPerpendicularDistance(points[i], k, b); //calculate the distance between the point and the line
                if (pDistance >= maxDistance)
                {
                    indexOfMax = i;
                    maxDistance = pDistance;
                }
            }

            if(maxDistance == 0)
            {
                outIndex.Add(endIndex);
                outIndex.Add(startIndex);
                return;
            }

            if (maxDistance > epsilon && indexOfMax != 0)
            {
                //Add the largest point that exceeds the tolerance
                outIndex.Add(indexOfMax);
                //seperate the line segment into two parts and recursively simplify
                RDP(points, epsilon, startIndex, indexOfMax,ref outIndex);
                RDP(points, epsilon, indexOfMax, endIndex, ref outIndex);
            }
            else
            {
                outIndex.Add(endIndex);
                outIndex.Add(startIndex);
                return;
            }
        }
    }

    public UnityEngine.Vector2 GetMousePos()
    {
        return linesHolder.worldToLocalMatrix * Camera.main.ScreenToWorldPoint(Input.mousePosition);
    }

    //This method is used to remap a value from one range to another
    public static float remap(float val, float in1, float in2, float out1, float out2)
    {
        return out1 + (val - in1) * (out2 - out1) / (in2 - in1);
    }
}
