using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System;

public class DrawingManager : MonoBehaviour
{
    [SerializeField] float minCanvasScale = 0.75f;
    [SerializeField] float maxCanvasScale = 1.25f;
    [SerializeField] float canvasScaleStep = 0.1f;
    private float currentCanvasScale = 1f;

    [SerializeField] float RDPThreshold = 0.01f;
    [SerializeField] float refineTolerance = 0.1f;
    private List<LinePoint> userInput = new List<LinePoint>();

    private bool isDrawing = false;

    [SerializeField] Transform linesHolder;
    [SerializeField] GameObject linePrefab;
    private List<GameObject> lines = new List<GameObject>();
    int curLineIndex = -1;
    float curTotalDrawingTime = 0;

    [SerializeField] private int drawingSampleRate = 60;
    [SerializeField] private float inputDistanceThreshold = 0.1f;
    private float drawingSampleTimer;

    int prevPointCount = 0;

    [SerializeField] bool isDebug = false;
    [SerializeField] private List<LinePoint> pointsDebug = new List<LinePoint>();
    private void OnDrawGizmosSelected()
    {
        if(!isDebug) return;
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

    private void Update()
    {
        if (Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            float val = currentCanvasScale + Input.GetAxis("Mouse ScrollWheel") * canvasScaleStep;
            val = Mathf.Clamp(val, minCanvasScale, maxCanvasScale);
            linesHolder.localScale = new UnityEngine.Vector3(val, val, 1);
            currentCanvasScale = val;
        }

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

            /*
            //refine the points with RDP
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


            //for (int j = 0; j < points.Count; j++)
            //{
            //    Debug.Log($"Point {j}: {points[j]} | FFTResult: {fftData[j]}");
            //}

            //Fourier transform for potential optimization
            //Complex[] fftData = ApplyFFT(points);
            //Complex[] refinedFFTData = FFTDataRefine(fftData);
            //List<UnityEngine.Vector2> newPoints = GeneratePointsFromData(refinedFFTData, 1);

            //Debug part

            pointsDebug = newPoints;

            LineMeshGenerator lineMeshGenerator = lines[curLineIndex].GetComponent<LineMeshGenerator>();
            lineMeshGenerator.debugVertices.Clear();
            lineMeshGenerator.RefreshPoints(newPoints);
            lineMeshGenerator.isDebug = isDebug;
            //LineRenderer lineRenderer = lines[curLineIndex].GetComponent<LineRenderer>();
            //lineRenderer.positionCount = newPoints.Count;
            //for (int i = 0; i < newPoints.Count; i++)
            //{
            //    lineRenderer.SetPosition(i, newPoints[i]);
            //}

            userInput.Clear();
            prevPointCount = 0;
        }

        if(isDrawing)
        {
            drawingSampleTimer += Time.deltaTime;
            curTotalDrawingTime += Time.deltaTime;
            if(drawingSampleTimer > 1f / drawingSampleRate)
            {
                drawingSampleTimer = 0;
                //UnityEngine.Vector2 mousePos = new UnityEngine.Vector2(Input.mousePosition.x - Screen.width / 2f, Input.mousePosition.y - Screen.height / 2f);
                UnityEngine.Vector2 mousePos = linesHolder.worldToLocalMatrix * Camera.main.ScreenToWorldPoint(Input.mousePosition);
                if (userInput.Count == 0) userInput.Add(new LinePoint(mousePos,curTotalDrawingTime));
                else 
                {
                    float prevDis = UnityEngine.Vector2.Distance(userInput.Last().position, mousePos);
                    Debug.Log($"Distance: {prevDis}");
                    if (prevDis > inputDistanceThreshold) userInput.Add(new LinePoint(mousePos, curTotalDrawingTime));
                    else userInput[userInput.Count - 1] = new LinePoint(mousePos, curTotalDrawingTime);
                }
                
            }
        }

        if(userInput.Count > 0)
        {
            if(prevPointCount != userInput.Count)
            {
                List<LinePoint> newPoints = new List<LinePoint>();
                foreach (var input in userInput)
                {
                    newPoints.Add(input);
                }
                //LineRenderer lineRenderer = lines[curLineIndex].GetComponent<LineRenderer>();
                //lineRenderer.positionCount = newPoints.Count;
                //for (int i = 0; i < newPoints.Count; i++)
                //{
                //    lineRenderer.SetPosition(i, newPoints[i]);
                //}

                //Debug part
                pointsDebug = newPoints;

                LineMeshGenerator lineMeshGenerator = lines[curLineIndex].GetComponent<LineMeshGenerator>();
                lineMeshGenerator.RefreshPoints(newPoints);
                lineMeshGenerator.isDebug = isDebug;

                prevPointCount = userInput.Count;
            }
        }

    }

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

    Complex[] FFTDataRefine(Complex[] fftData)
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

    List<UnityEngine.Vector2> GeneratePointsFromData(Complex[] fftData, int step = 1)
    {
        List<UnityEngine.Vector2> points = new List<UnityEngine.Vector2>();
        int n = fftData.Length;
        for (int i = 0; i < n; i+=step)
        {
            List <Complex> circlePos = new List<Complex>();
            //if (i==0)
            //{
            //    Complex factor0 = fftData[j] * Complex.Exp(2 * Math.PI * time * Complex.ImaginaryOne * 0 / fftData.Length);
            //    points.Add(new UnityEngine.Vector2((float)factor0.Real, (float)factor0.Imaginary));
            //    continue;
            //}
            //int k = Mathf.FloorToInt(n*drawingSimplifiedRate);
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

    //get the perpendicular distance between a point and a line
    public double GetPerpendicularDistance(UnityEngine.Vector2 point, double lineK, double lineB)
    {
        double dis = (Math.Abs(lineK * point.x - point.y + lineB)) / Math.Sqrt(lineK * lineK + 1);
        return dis;
    }

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


    public static float remap(float val, float in1, float in2, float out1, float out2)
    {
        return out1 + (val - in1) * (out2 - out1) / (in2 - in1);
    }
}
