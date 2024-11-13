using System;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[ExecuteInEditMode]
public class TMK_TerrainGenerator : MonoBehaviour
{
    public bool RaycastFromAbove = true;
    public float PathHeightMultiplier = 0.99f;

    public double PathMargins = 10;
    public float PathMarginsHeightMultiplier = 1.01f;

    [HideInInspector]
    private float[,] RefHeights;

    [HideInInspector]
    private Terrain TerrainParent;

    #if UNITY_EDITOR
    public void GenerateTerrain()
    {
        TerrainParent = gameObject.GetComponent<Terrain>();

        // Assume the heightmap is a square and get the height array from the existing terrain data
        var terrainData = TerrainParent.terrainData;
        var heightmapDimension = terrainData.heightmapResolution;
        var heights = terrainData.GetHeights(0, 0, heightmapDimension, heightmapDimension);
        RefHeights = heights;
        Undo.RecordObject(terrainData, "Automatically generated terrain");

        // Get the global position of the terrain by walking up the hierarchy and adding each parent's position value
        var globalPosition = transform.position;
        var parent = transform.parent;
        while (parent != null)
        {
            globalPosition += parent.position;
            parent = parent.parent;
        }

        // Step 1: Set the terrain heights to snap to the track
        SetTerrainToPathHeightWithRaycasts(globalPosition, terrainData, heights);
        RefHeights = Clone2DArray(heights);

        // Step 2: Apply margins around the path
        SetHeightsForMarginsAroundPath(heights);
        RefHeights = Clone2DArray(heights);

        // Step 3: Try to make the rest of the area fit with the set positions by sampling nearest set neighbors and calculating a weighted average of the heights
        SetRemainingArea_WeightedAverages(heights);

        // Set the heightmap after all the manipulations are finished
        terrainData.SetHeights(0, 0, heights);
    }

    private static T[,] Clone2DArray<T>(T[,] array) => (T[,])array.Clone();

    private void SetTerrainToPathHeightWithRaycasts(Vector3 globalPosition, TerrainData terrainData, float[,] heights)
    {
        // Get the two corners of the terrain which will allow values within the heights array to be converted to (X, Z) coordinates
        var maxTerrainHeight = terrainData.size.y;
        var bounds = terrainData.bounds;
        var globalMinCorner = globalPosition + bounds.min;
        var globalMaxCorner = globalPosition + bounds.max;

        // Temporarily disable the terrain's collider to ensure raycasts don't collide with the terrain
        var collider = GetComponent<TerrainCollider>();
        collider.enabled = false;

        // Go through each point in the heightmap array, calculate a point from which to conduct a raycast, and check if any colliders are hit
        // If no colliders are hit, set the height to 0 otherwise calculate the collision height value from 0 to 1 using a proportion of the max terrain height
        var xLength = heights.GetLength(0);
        var zLength = heights.GetLength(1);
        for (var x = 0; x < xLength; x++)
        {
            for (var z = 0; z < zLength; z++)
            {
                var xRaycastPt = ((globalMaxCorner.x - globalMinCorner.x) * (x * 1.0f / xLength)) + globalPosition.x;
                var zRaycastPt = ((globalMaxCorner.z - globalMinCorner.z) * (z * 1.0f / zLength)) + globalPosition.z;

                // Set the raycast point Y-value and vector differently depending on whether the raycast should occur from above or below
                // Some colliders may only be 1-sided, so raycasting from below might not work which is why the option to raycast from above is provided
                var raycastVector = Vector3.up;
                var numAdditionalCasts = 0;
                var yRaycastPt = globalPosition.y;
                if (RaycastFromAbove)
                {
                    raycastVector = Vector3.down;
                    yRaycastPt += maxTerrainHeight;
                    numAdditionalCasts = 100;
                }
                var raycastPt = new Vector3(xRaycastPt, yRaycastPt, zRaycastPt);

                // Continue raycasting up to N times until no more colliders are hit or until all casts are exhausted
                // This helps in cases where a road runs under another road and prevents the terrain from covering the lower road when raycasting from above
                var hitPtHeight = RaycastUntilLastPointHit(raycastPt, raycastVector, null, numAdditionalCasts);
                if (hitPtHeight != null)
                {
                    var heightDelta = hitPtHeight!.Value - globalPosition.y;
                    var heightValue = heightDelta / maxTerrainHeight;
                    heights[z, x] = heightValue * PathHeightMultiplier;
                }
                else
                {
                    heights[z, x] = 0;
                }
            }
        }

        // Reenable the collider
        collider.enabled = true;
    }

    private float? RaycastUntilLastPointHit(Vector3 raycastPt, Vector3 raycastVector, float? lastHitHeight, int maxNumAdditionalCastsRemaining)
    {
        if (Physics.Raycast(raycastPt, raycastVector, out var hitInfo, Mathf.Infinity))
        {
            if (maxNumAdditionalCastsRemaining > 0)
            {
                var newPt = raycastVector.normalized + hitInfo.point;
                return RaycastUntilLastPointHit(newPt, raycastVector, hitInfo.point.y, maxNumAdditionalCastsRemaining - 1);
            }
            else
            {
                return hitInfo.point.y;
            }
        }

        return lastHitHeight;
    }

    private void SetHeightsForMarginsAroundPath(float[,] heights)
    {
        // Find all the values with 0 and find the closest non-zero value within the margin distance
        // If any values are found, set it to the closest value physically
        var dim1 = heights.GetLength(0);
        var dim2 = heights.GetLength(1);
        for (var i = 0; i < dim1; i++)
        {
            for (var j = 0; j < dim2; j++)
            {
                var value = heights[i, j];
                if (value > 0)
                {
                    continue;
                }

                // Go through each possible point within range around this point and find the closest non-zero point
                // The previously saved reference values should be used rather than using the changing heights array
                var nonZeroPtsInRange = IterateSetPointsWithinRange(i, j, PathMargins, dim1, dim2).Where(tpl => RefHeights[tpl.x, tpl.y] > 0);
                if (nonZeroPtsInRange.Any())
                {
                    // Get the closest point and set the height of the current point to the closest point
                    var (x, y, _) = nonZeroPtsInRange.OrderBy(tpl => tpl.dist).First();
                    heights[i, j] = RefHeights[x, y] * PathMarginsHeightMultiplier;
                }
            }
        }
    }

    private IEnumerable<(int x, int y, double dist)> IterateSetPointsWithinRange(int xBase, int yBase, double range, int xRangeLimit, int yRangeLimit)
    {
        // Calculate the minimum possible X and Y values based on the selected range to avoid iterating the entire range
        var minPossibleX = Math.Max(0, (int)Math.Floor(xBase - range));
        var maxPossibleX = Math.Min(xRangeLimit, (int)Math.Ceiling(xBase + range));
        var minPossibleY = Math.Max(0, (int)Math.Floor(yBase - range));
        var maxPossibleY = Math.Max(yRangeLimit, (int)Math.Ceiling(yBase + range));

        // Go through the previously set points, check if it's within range of the base point, and yield it if it is
        for (var x = minPossibleX; x < Math.Min(maxPossibleX, RefHeights.GetLength(0) - 1); x++)
        {
            for (var y = minPossibleY; y < Math.Min(maxPossibleY, RefHeights.GetLength(1) - 1); y++)
            {
                // If the value for this point is 0, skip this point
                if (RefHeights[x, y] <= 0)
                {
                    continue;
                }

                // Calculate the distance and make sure it's less than the range
                var xDelta = x - xBase;
                var yDelta = y - yBase;
                var dist = Math.Sqrt((xDelta * xDelta) + (yDelta * yDelta));
                if (dist <= range)
                {
                    yield return (x, y, dist);
                }
            }
        }
    }

    private void SetRemainingArea_WeightedAverages(float[,] heights)
    {
        // Go through each unset point and find the nearest set neighbors from one of the previous steps in 8 directions (45 degrees between each)
        // It's possible that some points will not find all 8 points, so that must be considered
        // The distance from the reference point to the set points is summed, and then a weighted average of the set point heights is calculated
        // Inverse distance weighting will be used to ensure closer points have more pull on the point being set
        var neighborInfo = new (double dist, float height)?[8 + 8];
        for (var x = 0; x < heights.GetLength(0); x++)
        {
            for (var y = 0; y < heights.GetLength(1); y++)
            {
                if (RefHeights[x, y] > 0)
                {
                    continue;
                }

                // Try to find neighbors in 8 directions
                neighborInfo[0] = FindNearestSetNeighbor_WeightedAverages(x, y, -1, -1);
                neighborInfo[1] = FindNearestSetNeighbor_WeightedAverages(x, y, -1, 0);
                neighborInfo[2] = FindNearestSetNeighbor_WeightedAverages(x, y, -1, 1);
                neighborInfo[3] = FindNearestSetNeighbor_WeightedAverages(x, y, 0, -1);
                neighborInfo[4] = FindNearestSetNeighbor_WeightedAverages(x, y, 0, 1);
                neighborInfo[5] = FindNearestSetNeighbor_WeightedAverages(x, y, 1, -1);
                neighborInfo[6] = FindNearestSetNeighbor_WeightedAverages(x, y, 1, 0);
                neighborInfo[7] = FindNearestSetNeighbor_WeightedAverages(x, y, 1, 1);

                // Add an additional 8 directions between the 8 other directions (to get smoother terrains)
                neighborInfo[8] = FindNearestSetNeighbor_WeightedAverages(x, y, -2, -1);
                neighborInfo[9] = FindNearestSetNeighbor_WeightedAverages(x, y, -1, -2);
                neighborInfo[10] = FindNearestSetNeighbor_WeightedAverages(x, y, 2, -1);
                neighborInfo[11] = FindNearestSetNeighbor_WeightedAverages(x, y, 1, -2);
                neighborInfo[12] = FindNearestSetNeighbor_WeightedAverages(x, y, -2, 1);
                neighborInfo[13] = FindNearestSetNeighbor_WeightedAverages(x, y, -1, 2);
                neighborInfo[14] = FindNearestSetNeighbor_WeightedAverages(x, y, 2, 1);
                neighborInfo[15] = FindNearestSetNeighbor_WeightedAverages(x, y, 1, 2);

                // Use a weighted average to calculate the new height
                var weightedSum = 0.0;
                var sumOfWeights = 0.0;
                foreach (var tpl in neighborInfo.Where(tpl => tpl != null))
                {
                    var weight = 1.0 / tpl.Value.dist;
                    sumOfWeights += weight;
                    weightedSum += weight * tpl.Value.height;
                }

                heights[x, y] = (float)(weightedSum / sumOfWeights);
            }
        }
    }

    private (double dist, float height)? FindNearestSetNeighbor_WeightedAverages(int x, int y, int xStepSize, int yStepSize)
    {
        var xNew = x;
        var yNew = y;
        var xDim = RefHeights.GetLength(0);
        var yDim = RefHeights.GetLength(1);
        while (true)
        {
            // Keep stepping points until a set point is found or the bounds of the array are exceeded
            xNew += xStepSize;
            yNew += yStepSize;

            // If the bounds of the array are exceeded, return a null value
            if ((xNew < 0) || (yNew < 0) || (xNew >= xDim) || (yNew >= yDim))
            {
                return null;
            }

            // If a set point was found, calculate the distance and return the tuple
            var height = RefHeights[xNew, yNew];
            if (height > 0)
            {
                var dist = Math.Sqrt(Math.Pow(xNew - x, 2) + Math.Pow(yNew - y, 2));
                return (dist, height);
            }
        }
    }
    #endif
}
