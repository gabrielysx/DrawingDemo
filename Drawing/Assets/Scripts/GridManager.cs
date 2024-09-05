using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public enum MoveDirection
{
    Left = 0,
    LeftUp = 1,
    Up = 2,
    RightUp = 3,
    Right = 4,
    RightDown = 5,
    Down = 6,
    LeftDown = 7
}

public class HullPoint
{
    public Vector2Int gridPos;
    public MoveDirection moveDirection;
    public HullPoint(Vector2Int gridPos, MoveDirection moveDirection)
    {
        this.gridPos = gridPos;
        this.moveDirection = moveDirection;
    }
}

[RequireComponent(typeof(BoxCollider2D))]
public class GridManager : MonoBehaviour
{
    [SerializeField] int horizontalResolution = 2560;
    [SerializeField] int verticalResolution = 1440;
    [SerializeField] BoxCollider2D canvasBoundCollider;
    [SerializeField] bool isDebug;
    public Func<Vector2> GetMousePosition;
    bool debugInputLock = false;
    List<HullPoint> hullPoints;
    Vector2Int debugMouseGridPos;
    bool[,] gridBlock;

    readonly Vector2Int[] eightDirections = new Vector2Int[] { 
        new Vector2Int(-1, 0),
        new Vector2Int(-1, 1),
        new Vector2Int(0, 1),
        new Vector2Int(1, 1),
        new Vector2Int(1, 0),
        new Vector2Int(1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, -1) };

    private void OnDrawGizmos()
    {
        if (!isDebug)
        {
            return;
        }
        RaycastHit[] results = new RaycastHit[3];
        //return;
        Gizmos.color = Color.black;

        float minX = canvasBoundCollider.bounds.min.x;
        float minY = canvasBoundCollider.bounds.min.y;
        float maxX = canvasBoundCollider.bounds.max.x;
        float maxY = canvasBoundCollider.bounds.max.y;
        float cellWidth = (maxX - minX) / horizontalResolution;
        float cellHeight = (maxY - minY) / verticalResolution;
        for (int x = 0; x <= horizontalResolution; x++)
        {
            Vector3 start = new Vector3(x * cellWidth + canvasBoundCollider.bounds.min.x, minY, 0);
            Vector3 end = new Vector3(x * cellWidth + canvasBoundCollider.bounds.min.x, maxY, 0);
            Gizmos.DrawLine(start, end);
        }
        for (int y = 0; y <= verticalResolution; y++)
        {
            Vector3 start = new Vector3(minX, y * cellHeight + canvasBoundCollider.bounds.min.y, 0);
            Vector3 end = new Vector3(maxX, y * cellHeight + canvasBoundCollider.bounds.min.y, 0);
            Gizmos.DrawLine(start, end);
        }

        for (int x = 0; x < horizontalResolution; x++)
        {
            for (int y = 0; y < verticalResolution; y++)
            {
                Vector3 center = new Vector3(x * cellWidth + canvasBoundCollider.bounds.min.x + cellWidth / 2, y * cellHeight + canvasBoundCollider.bounds.min.y + cellHeight / 2, 0);
                if(gridBlock == null)
                {
                    continue;
                }
                
                if (gridBlock[x, y])
                {
                    Gizmos.color = Color.red;
                }
                else
                {
                    Gizmos.color = Color.blue;
                }
                Gizmos.DrawSphere(center, 0.3f* cellWidth);

                if (hullPoints != null && hullPoints.Count > 0)
                {
                    if (hullPoints.FindIndex((point) => point.gridPos == new Vector2Int(x, y)) != -1)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawSphere(center, 0.3f * cellWidth);
                    }
                }
            }
        }
        if(debugMouseGridPos != new Vector2Int(0,0))
        {
            Gizmos.color = Color.green;
            Vector3 center = new Vector3(debugMouseGridPos.x * cellWidth + canvasBoundCollider.bounds.min.x + cellWidth / 2, debugMouseGridPos.y * cellHeight + canvasBoundCollider.bounds.min.y + cellHeight / 2, 0);
            Gizmos.DrawSphere(center, 0.3f * cellWidth);
        }
    }

    private void Start()
    {
        gridBlock = new bool[horizontalResolution, verticalResolution];
        //Set the border of the grid to be blocked
        for(int x = 0; x < horizontalResolution; x++)
        {
            gridBlock[x, 0] = true;
            gridBlock[x, verticalResolution - 1] = true;
        }
        for (int y = 0; y < verticalResolution; y++)
        {
            gridBlock[0, y] = true;
            gridBlock[horizontalResolution - 1, y] = true;
        }

        if (canvasBoundCollider == null)
        {
            Debug.LogError("Canvas Bound Collider is not assigned in the inspector");
            return;
        }
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            if(debugInputLock)
            {
                return;
            }
            Vector2 mousePos = GetMousePosition();
            debugMouseGridPos = new Vector2Int(Mathf.FloorToInt((mousePos.x - canvasBoundCollider.bounds.min.x) / (canvasBoundCollider.bounds.size.x / horizontalResolution)), Mathf.FloorToInt((mousePos.y - canvasBoundCollider.bounds.min.y) / (canvasBoundCollider.bounds.size.y / verticalResolution)));
            bool isValidInput;
            hullPoints = new List<HullPoint>();
            hullPoints = HullSearcher(mousePos, out isValidInput);
            if (!isValidInput)
            {
                Debug.LogError("Invalid mouse position");
            }
            debugInputLock = true;
        }
        if(Input.GetKeyUp(KeyCode.Space))
        {
            if (!debugInputLock)
            {
                return;
            }
            debugInputLock = false;
        }
    }

    public void RefreshGrid()
    {
        float minX = canvasBoundCollider.bounds.min.x;
        float minY = canvasBoundCollider.bounds.min.y;
        float maxX = canvasBoundCollider.bounds.max.x;
        float maxY = canvasBoundCollider.bounds.max.y;
        float cellWidth = (maxX - minX) / horizontalResolution;
        float cellHeight = (maxY - minY) / verticalResolution;
        RaycastHit[] results = new RaycastHit[1];
        for (int x = 1; x < horizontalResolution-1; x++)
        {
            for (int y = 1; y < verticalResolution-1; y++)
            {
                Vector3 center = new Vector3(x * cellWidth + canvasBoundCollider.bounds.min.x + cellWidth / 2, y * cellHeight + canvasBoundCollider.bounds.min.y + cellHeight / 2, -1);
                if (Physics.RaycastNonAlloc(center, Vector3.forward, results) > 0)
                {
                    gridBlock[x, y] = true;
                }
                else
                {
                    gridBlock[x, y] = false;
                }
            }
        }
    }

    public List<HullPoint> HullSearcher(Vector2 mousePos, out bool isValidInput)
    {
        List<HullPoint> output = new List<HullPoint>();
        //Get Grid Info
        float minX = canvasBoundCollider.bounds.min.x;
        float minY = canvasBoundCollider.bounds.min.y;
        float maxX = canvasBoundCollider.bounds.max.x;
        float maxY = canvasBoundCollider.bounds.max.y;
        float cellWidth = (maxX - minX) / horizontalResolution;
        float cellHeight = (maxY - minY) / verticalResolution;

        //Get the mouse position in the grid
        int mouseGridX = Mathf.FloorToInt((mousePos.x - minX) / cellWidth);
        int mouseGridY = Mathf.FloorToInt((mousePos.y - minY) / cellHeight);

        //Check if the mouse position is valid to get the hull of the shape
        if(gridBlock == null || mouseGridX < 0 || mouseGridX >= horizontalResolution || mouseGridY < 0 || mouseGridY >= verticalResolution || gridBlock[mouseGridX,mouseGridY])
        {
            isValidInput = false;
            return output;
        }
        isValidInput = true;

        //Start the search by moving from the mouse position to the bottom until it reaches a block
        List<HullPoint> currentHullPoints = new List<HullPoint>();
        Stack<HullPoint> currentHullPointsStack = new Stack<HullPoint>();
        HashSet<Vector2Int> currentHullPointsSet = new HashSet<Vector2Int>();
        int currentX = mouseGridX;
        int currentY = mouseGridY;

        for(int y = currentY; y>=0; y--)
        {
            if(gridBlock[currentX, y])
            {
                currentY = y;
                break;
            }
        }

        HullPoint startPoint = new HullPoint(new Vector2Int(currentX, currentY), MoveDirection.Left);
        //Add the first point to the hull and start the search
        currentHullPoints.Add(startPoint);
        currentHullPointsStack.Push(startPoint);
        currentHullPointsSet.Add(startPoint.gridPos);
        HullPoint curPoint = startPoint;
        int traversePointCount = 0;
        while (traversePointCount < gridBlock.Length)
        {
            //Check the near points in four directions (follow left-up-right-down order)
            bool found = false;
            bool lastPoint = false;
            for(int i = 0; i < 8; i++)
            {
                //ignore the direction where it came from
                if((int)curPoint.moveDirection == i - 4 || (int)curPoint.moveDirection == i + 4)
                {
                    continue;
                }
                Vector2Int newGridPos = curPoint.gridPos + eightDirections[i];
                //If the new point is the same as the start point, the hull is completed so breaks the while loop
                if(newGridPos == startPoint.gridPos)
                {
                    lastPoint = true;
                    break;
                }
                if (newGridPos.x < 0 || newGridPos.x >= horizontalResolution || newGridPos.y < 0 || newGridPos.y >= verticalResolution || !gridBlock[newGridPos.x, newGridPos.y])
                    continue;
                if (GetUnblockedCellsCountAround(newGridPos) > 0)
                {
                    //If the new point is not in the current hull, add it to the hull
                    if (!currentHullPointsSet.Contains(newGridPos))
                    {
                        HullPoint temp = new HullPoint(newGridPos, (MoveDirection)i);
                        currentHullPoints.Add(temp);
                        currentHullPointsStack.Push(temp);
                        currentHullPointsSet.Add(newGridPos);
                        curPoint = temp;
                        found = true;
                        traversePointCount++;
                        break;
                    }
                    Debug.LogWarning("Duplicated points");
                }
            }

            if (lastPoint)
            {
                Debug.LogWarning("Hull Completed");
                break;
            }
            if (!found)
            {
                //Pop the current point from stack and go back to the previous point to search for more points
                currentHullPointsStack.Pop();
                //ToDo: maybe add the poped point again to the list so that the hull will closed follow the same traverse direction
                if (currentHullPointsStack.Count == 0)
                {
                    Debug.LogError("The stack is empty and search stops");
                    traversePointCount++;
                    break;
                }
                curPoint = currentHullPointsStack.Peek();

            }
            traversePointCount++;

        }

        Debug.Log($"Current traversed points count: {traversePointCount}");
        output = currentHullPoints;
        return output;
    }

    public int GetUnblockedCellsCountAround(Vector2Int gridPos)
    {

        int count = 0;
        for(int i = 0;i < eightDirections.Length; i++)
        {
            Vector2Int newGridPos = gridPos + eightDirections[i];
            if (newGridPos.x < 0 || newGridPos.x >= horizontalResolution || newGridPos.y < 0 || newGridPos.y >= verticalResolution)
                continue;
            if(!gridBlock[newGridPos.x, newGridPos.y])
            {
                count++;
            }
        }
        return count;
    }

}
