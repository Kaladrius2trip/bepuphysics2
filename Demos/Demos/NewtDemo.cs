﻿using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Collections;
using BepuUtilities.Memory;
using DemoContentLoader;
using DemoRenderer;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Demos.Demos
{
    //(You might notice that this demo is really large, uses some older idioms, and is a little out of place. I just pulled most of this stuff out of my older GPU deformable physics project.)
    using CellSet = QuickSet<Cell, CellComparer>;
    using CellList = QuickList<Cell>;
    public static class BoxTriangleCollider
    {
        private const float IntersectionEpsilon = 1e-4f;
        private static bool OverlapsAlongAxis(ref Vector3 axis, ref Vector3 halfExtents, ref Vector3 a, ref Vector3 b, ref Vector3 c)
        {
            var da = Vector3.Dot(a, axis);
            var db = Vector3.Dot(b, axis);
            var dc = Vector3.Dot(c, axis);

            float min, max;
            if (da < db && da < dc)
            {
                min = da;
                max = db > dc ? db : dc;
            }
            else if (db < dc)
            {
                min = db;
                max = da > dc ? da : dc;
            }
            else
            {
                min = dc;
                max = da > db ? da : db;
            }

            Vector3 boxExtremePoint;
            if (axis.X > 0)
                boxExtremePoint.X = halfExtents.X;
            else
                boxExtremePoint.X = -halfExtents.X;

            if (axis.Y > 0)
                boxExtremePoint.Y = halfExtents.Y;
            else
                boxExtremePoint.Y = -halfExtents.Y;

            if (axis.Z > 0)
                boxExtremePoint.Z = halfExtents.Z;
            else
                boxExtremePoint.Z = -halfExtents.Z;

            var boxMax = Vector3.Dot(boxExtremePoint, axis);
            var boxMin = -boxMax;

            return !(max + IntersectionEpsilon < boxMin || min - IntersectionEpsilon > boxMax);

        }

        /// <summary>
        /// Determines if a triangle in a box's local space intersects that box.
        /// </summary>
        /// <param name="halfExtents">Half extents of the box.</param>
        /// <param name="a">First vertex of the triangle in the box's local space.</param>
        /// <param name="b">Second vertex of the triangle in the box's local space.</param>
        /// <param name="c">Third vertex of the triangle in the box's local space.</param>
        /// <returns>True if the triangle intersects the box, false otherwise.</returns>
        public static bool Intersecting(ref Vector3 halfExtents, ref Vector3 a, ref Vector3 b, ref Vector3 c)
        {
            //Need to test 3 box faces, 1 triangle face, and 3 * 3 edges.

            //Test each of the box's faces.
            //NOTE: In dermocat, this condition will never be hit because we only select cells which have an overlapping bounding box.
            //Despite that, this will stay in for correctness. It's extremely cheap anyway.
            Vector3 expandedHalfExtents;
            expandedHalfExtents.X = halfExtents.X + IntersectionEpsilon;
            expandedHalfExtents.Y = halfExtents.Y + IntersectionEpsilon;
            expandedHalfExtents.Z = halfExtents.Z + IntersectionEpsilon;
            if ((a.X > expandedHalfExtents.X && b.X > expandedHalfExtents.X && c.X > expandedHalfExtents.X) ||
                (a.Y > expandedHalfExtents.Y && b.Y > expandedHalfExtents.Y && c.Y > expandedHalfExtents.Y) ||
                (a.Z > expandedHalfExtents.Z && b.Z > expandedHalfExtents.Z && c.Z > expandedHalfExtents.Z) ||
                (a.X < -expandedHalfExtents.X && b.X < -expandedHalfExtents.X && c.X < -expandedHalfExtents.X) ||
                (a.Y < -expandedHalfExtents.Y && b.Y < -expandedHalfExtents.Y && c.Y < -expandedHalfExtents.Y) ||
                (a.Z < -expandedHalfExtents.Z && b.Z < -expandedHalfExtents.Z && c.Z < -expandedHalfExtents.Z))
            {
                return false;
            }

            //Test the triangle face.
            //Note that we don't use the axis overlap test here.
            //We can do better since we know that all triangle vertices have the same value.
            var ab = b - a;
            var ac = c - a;
            var normal = Vector3.Cross(ab, ac);
            var d = Vector3.Dot(normal, a);
            if (d < 0)
            {
                //Ensure that the normal points away from the origin (direction choice is arbitrary, just need to be consistent).
                normal = -normal;
                d = -d;
            }

            Vector3 boxExtremePoint;
            if (normal.X > 0)
                boxExtremePoint.X = halfExtents.X;
            else
                boxExtremePoint.X = -halfExtents.X;

            if (normal.Y > 0)
                boxExtremePoint.Y = halfExtents.Y;
            else
                boxExtremePoint.Y = -halfExtents.Y;

            if (normal.Z > 0)
                boxExtremePoint.Z = halfExtents.Z;
            else
                boxExtremePoint.Z = -halfExtents.Z;

            float extremePointDot = Vector3.Dot(boxExtremePoint, normal);
            if (extremePointDot + IntersectionEpsilon < d)
            {
                //No collision.
                return false;
            }

            //Test every edge direction.
            //The three box directions all have two zeroes and one one, so the cross product simplifies a lot.
            var bc = c - b;
            Vector3 direction;
            //(1,0,0) x ab:
            direction = new Vector3(0, -ab.Z, ab.Y);
            if (!OverlapsAlongAxis(ref direction, ref halfExtents, ref a, ref b, ref c))
                return false;
            //(1,0,0) x ac
            direction = new Vector3(0, -ac.Z, ac.Y);
            if (!OverlapsAlongAxis(ref direction, ref halfExtents, ref a, ref b, ref c))
                return false;
            //(1,0,0) x bc:
            direction = new Vector3(0, -bc.Z, bc.Y);
            if (!OverlapsAlongAxis(ref direction, ref halfExtents, ref a, ref b, ref c))
                return false;

            //(0,1,0) x ab:
            direction = new Vector3(ab.Z, 0, -ab.X);
            if (!OverlapsAlongAxis(ref direction, ref halfExtents, ref a, ref b, ref c))
                return false;
            //(0,1,0) x ac
            direction = new Vector3(ac.Z, 0, -ac.X);
            if (!OverlapsAlongAxis(ref direction, ref halfExtents, ref a, ref b, ref c))
                return false;
            //(0,1,0) x bc:
            direction = new Vector3(bc.Z, 0, -bc.X);
            if (!OverlapsAlongAxis(ref direction, ref halfExtents, ref a, ref b, ref c))
                return false;

            //(0,0,1) x ab:
            direction = new Vector3(-ab.Y, ab.X, 0);
            if (!OverlapsAlongAxis(ref direction, ref halfExtents, ref a, ref b, ref c))
                return false;
            //(0,0,1) x ac
            direction = new Vector3(-ac.Y, ac.X, 0);
            if (!OverlapsAlongAxis(ref direction, ref halfExtents, ref a, ref b, ref c))
                return false;
            //(0,0,1) x bc:
            direction = new Vector3(-bc.Y, bc.X, 0);
            if (!OverlapsAlongAxis(ref direction, ref halfExtents, ref a, ref b, ref c))
                return false;

            return true;
        }
    }

    internal static class TriangleRasterizer
    {
        public static void RasterizeTriangle(ref Vector3 a, ref Vector3 b, ref Vector3 c, float cellSize, ref Vector3 gridOrigin, BufferPool pool, ref QuickSet<Cell, CellComparer> cells)
        {
            var gridA = a - gridOrigin;
            var gridB = b - gridOrigin;
            var gridC = c - gridOrigin;

            //Compute the bounding box of the triangle.
            var max = Vector3.Max(Vector3.Max(gridA, gridB), gridC);
            var min = Vector3.Min(Vector3.Min(gridA, gridB), gridC);

            var epsilon = new Vector3(1e-5f);
            min -= epsilon;
            max += epsilon;

            //Discretize the bounding box.
            //All indices are positive, so we can just truncate.
            int startX, endX, startY, endY, startZ, endZ;
            float inverseCellSize = 1f / cellSize;
            startX = (int)Math.Floor(min.X * inverseCellSize);
            endX = (int)Math.Floor(max.X * inverseCellSize);
            startY = (int)Math.Floor(min.Y * inverseCellSize);
            endY = (int)Math.Floor(max.Y * inverseCellSize);
            startZ = (int)Math.Floor(min.Z * inverseCellSize);
            endZ = (int)Math.Floor(max.Z * inverseCellSize);

            //Test the triangle against each cell.
            var halfExtents = new Vector3(cellSize * 0.5f);
            for (int i = startX; i <= endX; ++i)
            {
                for (int j = startY; j <= endY; ++j)
                {
                    for (int k = startZ; k <= endZ; ++k)
                    {
                        var cellIndex = new Vector3(i, j, k);
                        var cellOrigin = cellSize * cellIndex + halfExtents;
                        var shiftedA = gridA - cellOrigin;
                        var shiftedB = gridB - cellOrigin;
                        var shiftedC = gridC - cellOrigin;

                        if (BoxTriangleCollider.Intersecting(ref halfExtents, ref shiftedA, ref shiftedB, ref shiftedC))
                        {
                            cells.Add(new Cell { X = i, Y = j, Z = k }, pool);
                        }
                    }
                }
            }
        }

    }
    public struct CellVertexIndices
    {
        public int V000, V001, V010, V011, V100, V101, V110, V111;


    }

    public struct CellComparer : IEqualityComparerRef<Cell>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ref Cell a, ref Cell b)
        {
            return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Hash(ref Cell cell)
        {
            return (452930477 * cell.X) ^ (122949829 * cell.Z) ^ (654188429 * cell.Z);
        }
    }


    public struct Cell
    {
        public int X, Y, Z;
    }

    public struct TetrahedronVertices
    {
        public readonly int A, B, C, D;

        public TetrahedronVertices(int a, int b, int c, int d)
        {
            A = a;
            B = b;
            C = c;
            D = d;
        }
    }

    //Why dumb? Because in the original project, there was a less dumb variant. The less dumb variant was, unfortunately, way more complicated, so I didn't copy it over.
    //It's also pretty darn slow with denser cell sizes.
    public static class DumbTetrahedralizer
    {
        private static void AddVertexSpatialIndex(ref Cell vertexSpatialIndex, BufferPool pool, ref CellSet vertexIndices, out int index)
        {
            index = vertexIndices.IndexOf(vertexSpatialIndex);
            if (index < 0)
            {
                index = vertexIndices.Count;
                vertexIndices.Add(vertexSpatialIndex, pool);
            }
        }

        private struct VoxelizationBounds
        {
            /// <summary>
            /// Exclusive maximum voxel index along the X axis.
            /// </summary>
            public int X;
            /// <summary>
            /// Exclusive maximum voxel index along the Y axis.
            /// </summary>
            public int Y;
            /// <summary>
            /// Exclusive maximum voxel index along the Z axis.
            /// </summary>
            public int Z;
        }

        private static bool TryFloodFill(Cell cell, ref VoxelizationBounds bounds, BufferPool pool, ref CellSet occupiedCells, ref CellSet newlyFilledCells, ref CellList cellsToVisit)
        {
            if (cell.X > bounds.X || cell.Y > bounds.Y || cell.Z > bounds.Z || cell.X < -1 || cell.Y < -1 || cell.Z < -1)
            {
                //We've escaped the world; the start location was not inside a closed section. Abandon the flood fill.
                return false;
            }
            if (newlyFilledCells.Contains(cell) || occupiedCells.Contains(cell))
            {
                //We already traversed this cell before or during the current flood fill.
                return true;
            }

            newlyFilledCells.Add(cell, pool);

            cellsToVisit.Add(new Cell { X = cell.X, Y = cell.Y, Z = cell.Z - 1 }, pool);
            cellsToVisit.Add(new Cell { X = cell.X, Y = cell.Y, Z = cell.Z + 1 }, pool);
            cellsToVisit.Add(new Cell { X = cell.X, Y = cell.Y - 1, Z = cell.Z }, pool);
            cellsToVisit.Add(new Cell { X = cell.X, Y = cell.Y + 1, Z = cell.Z }, pool);
            cellsToVisit.Add(new Cell { X = cell.X - 1, Y = cell.Y, Z = cell.Z }, pool);
            cellsToVisit.Add(new Cell { X = cell.X + 1, Y = cell.Y, Z = cell.Z }, pool);

            return true;
        }

        static void InitiateFloodFill(Cell cell, ref VoxelizationBounds bounds, BufferPool pool, ref CellSet occupiedCells, ref CellSet newlyFilledCells, ref CellList cellsToVisit)
        {
            //Check to make sure that this cell isn't already occupied before starting a new fill.
            if (occupiedCells.Contains(cell))
                return;
            cellsToVisit.Add(cell, pool);
            while (cellsToVisit.Count > 0)
            {
                if (cellsToVisit.TryPop(out cell))
                {
                    if (!TryFloodFill(cell, ref bounds, pool, ref occupiedCells, ref newlyFilledCells, ref cellsToVisit))
                    {
                        //The flood fill escaped the voxel bounds. Must be an open area; don't fill.
                        cellsToVisit.Clear();
                        newlyFilledCells.Clear();
                        return;
                    }
                }
            }
            //Flood fill completed without reaching the voxel bounds. Dump newly filled cells.
            for (int i = 0; i < newlyFilledCells.Count; ++i)
            {
                occupiedCells.Add(newlyFilledCells[i], pool);
            }
            newlyFilledCells.Clear();
        }

        private static void FloodFillAdjacentCells(Cell cell, ref VoxelizationBounds bounds, BufferPool pool, ref CellSet occupiedCells, ref CellSet newlyFilledCells, ref CellList cellsToVisit)
        {
            InitiateFloodFill(new Cell { X = cell.X + 1, Y = cell.Y, Z = cell.Z }, ref bounds, pool, ref occupiedCells, ref newlyFilledCells, ref cellsToVisit);
            InitiateFloodFill(new Cell { X = cell.X - 1, Y = cell.Y, Z = cell.Z }, ref bounds, pool, ref occupiedCells, ref newlyFilledCells, ref cellsToVisit);
            InitiateFloodFill(new Cell { X = cell.X, Y = cell.Y + 1, Z = cell.Z }, ref bounds, pool, ref occupiedCells, ref newlyFilledCells, ref cellsToVisit);
            InitiateFloodFill(new Cell { X = cell.X, Y = cell.Y - 1, Z = cell.Z }, ref bounds, pool, ref occupiedCells, ref newlyFilledCells, ref cellsToVisit);
            InitiateFloodFill(new Cell { X = cell.X, Y = cell.Y, Z = cell.Z + 1 }, ref bounds, pool, ref occupiedCells, ref newlyFilledCells, ref cellsToVisit);
            InitiateFloodFill(new Cell { X = cell.X, Y = cell.Y, Z = cell.Z - 1 }, ref bounds, pool, ref occupiedCells, ref newlyFilledCells, ref cellsToVisit);
        }



        public static void Tetrahedralize(Span<TriangleContent> triangles, float cellSize, BufferPool pool,
            out Buffer<Vector3> vertices, out CellSet vertexSpatialIndices, out Buffer<CellVertexIndices> cellVertexIndices, out Buffer<TetrahedronVertices> tetrahedraVertexIndices)
        {
            //Compute the size of the 3d grid by scanning all vertices.
            Vector3 min = new(float.MaxValue), max = new(float.MinValue);
            for (int i = 0; i < triangles.Length; ++i)
            {
                ref var triangle = ref triangles[i];
                min = Vector3.Min(min, triangle.A);
                min = Vector3.Min(min, triangle.B);
                min = Vector3.Min(min, triangle.C);
                max = Vector3.Max(max, triangle.A);
                max = Vector3.Max(max, triangle.B);
                max = Vector3.Max(max, triangle.C);
            }
            //Add a little buffer.
            var buffer = new Vector3(cellSize);
            min -= buffer;

            var cells = new CellSet(triangles.Length, pool);
            for (int i = 0; i < triangles.Length; ++i)
            {
                ref var triangle = ref triangles[i];
                //Rasterize each triangle onto the grid.
                TriangleRasterizer.RasterizeTriangle(ref triangle.A, ref triangle.B, ref triangle.C, cellSize, ref min, pool, ref cells);

            }

            if (cells.Count == 0)
                throw new ArgumentException("Mesh seems to have no volume; triangle rasterization occupied no cells.");

            VoxelizationBounds bounds;
            Vector3 size = max - min;
            float inverseCellSize = 1f / cellSize;
            bounds.X = (int)(Math.Ceiling(inverseCellSize * size.X));
            bounds.Y = (int)(Math.Ceiling(inverseCellSize * size.Y));
            bounds.Z = (int)(Math.Ceiling(inverseCellSize * size.Z));
            //Perform a flood fill on every surface vertex.
            //We can use the cells set directly, since it behaves like a regular list with regard to element placement (always at the end).
            var floodFilledCells = new CellSet(32, pool);
            var cellsToVisit = new CellList(32, pool);
            for (int i = cells.Count - 1; i >= 0; --i)
            {
                ref var cell = ref cells[i];
                FloodFillAdjacentCells(cell, ref bounds, pool, ref cells, ref floodFilledCells, ref cellsToVisit);
            }

            //Build the vertex list and per-cell vertex index lists.
            vertexSpatialIndices = new CellSet(cells.Count * 4, pool);
            int cellIndex = 0;
            pool.Take(cells.Count, out cellVertexIndices);
            for (int i = 0; i < cells.Count; ++i)
            {
                ref var cell = ref cells[i];
                CellVertexIndices cellIndices;
                var vertexSpatialIndex = cell;
                AddVertexSpatialIndex(ref vertexSpatialIndex, pool, ref vertexSpatialIndices, out cellIndices.V000);
                vertexSpatialIndex.X = cell.X;
                vertexSpatialIndex.Y = cell.Y;
                vertexSpatialIndex.Z = cell.Z + 1;
                AddVertexSpatialIndex(ref vertexSpatialIndex, pool, ref vertexSpatialIndices, out cellIndices.V001);
                vertexSpatialIndex.X = cell.X;
                vertexSpatialIndex.Y = cell.Y + 1;
                vertexSpatialIndex.Z = cell.Z;
                AddVertexSpatialIndex(ref vertexSpatialIndex, pool, ref vertexSpatialIndices, out cellIndices.V010);
                vertexSpatialIndex.X = cell.X;
                vertexSpatialIndex.Y = cell.Y + 1;
                vertexSpatialIndex.Z = cell.Z + 1;
                AddVertexSpatialIndex(ref vertexSpatialIndex, pool, ref vertexSpatialIndices, out cellIndices.V011);
                vertexSpatialIndex.X = cell.X + 1;
                vertexSpatialIndex.Y = cell.Y;
                vertexSpatialIndex.Z = cell.Z;
                AddVertexSpatialIndex(ref vertexSpatialIndex, pool, ref vertexSpatialIndices, out cellIndices.V100);
                vertexSpatialIndex.X = cell.X + 1;
                vertexSpatialIndex.Y = cell.Y;
                vertexSpatialIndex.Z = cell.Z + 1;
                AddVertexSpatialIndex(ref vertexSpatialIndex, pool, ref vertexSpatialIndices, out cellIndices.V101);
                vertexSpatialIndex.X = cell.X + 1;
                vertexSpatialIndex.Y = cell.Y + 1;
                vertexSpatialIndex.Z = cell.Z;
                AddVertexSpatialIndex(ref vertexSpatialIndex, pool, ref vertexSpatialIndices, out cellIndices.V110);
                vertexSpatialIndex.X = cell.X + 1;
                vertexSpatialIndex.Y = cell.Y + 1;
                vertexSpatialIndex.Z = cell.Z + 1;
                AddVertexSpatialIndex(ref vertexSpatialIndex, pool, ref vertexSpatialIndices, out cellIndices.V111);

                cellVertexIndices[cellIndex++] = cellIndices;
            }

            //Create the tetrahedra.
            var tetrahedraCount = cellVertexIndices.Length * 5;
            pool.Take(tetrahedraCount, out tetrahedraVertexIndices);
            int tetrahedronIndex = 0;
            for (int i = 0; i < cellVertexIndices.Length; ++i)
            {
                var cellIndices = cellVertexIndices[i];
                tetrahedraVertexIndices[tetrahedronIndex++] = new TetrahedronVertices(cellIndices.V010, cellIndices.V111, cellIndices.V001, cellIndices.V100); //Central tetrahedron
                tetrahedraVertexIndices[tetrahedronIndex++] = new TetrahedronVertices(cellIndices.V000, cellIndices.V001, cellIndices.V010, cellIndices.V100); //Origin tetrahedron
                tetrahedraVertexIndices[tetrahedronIndex++] = new TetrahedronVertices(cellIndices.V010, cellIndices.V100, cellIndices.V111, cellIndices.V110);
                tetrahedraVertexIndices[tetrahedronIndex++] = new TetrahedronVertices(cellIndices.V010, cellIndices.V001, cellIndices.V111, cellIndices.V011);
                tetrahedraVertexIndices[tetrahedronIndex++] = new TetrahedronVertices(cellIndices.V101, cellIndices.V001, cellIndices.V100, cellIndices.V111);
            }

            //Create the vertices.
            pool.Take(vertexSpatialIndices.Count, out vertices);
            for (int i = 0; i < vertices.Length; ++i)
            {
                ref var index = ref vertexSpatialIndices[i];
                vertices[i] = new Vector3(index.X, index.Y, index.Z) * cellSize + min;
            }


            //We can fail to dispose the quick collections. All of the buffers are getting GC'd anyway.
            cells.Dispose(pool);
            floodFilledCells.Dispose(pool);
        }
    }

    struct DeformableCollisionFilter
    {
        int localIndices;
        int instanceId;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DeformableCollisionFilter(int x, int y, int z, int instanceId)
        {
            const int max = 1 << 10;
            Debug.Assert(x >= 0 && x < max && y >= 0 && y < max && z >= 0 && z < max, "This filter packs local indices, so their range is limited.");
            localIndices = x | (y << 10) | (z << 20);
            this.instanceId = instanceId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Test(in DeformableCollisionFilter a, in DeformableCollisionFilter b)
        {
            if (a.instanceId != b.instanceId)
                return true;
            //Disallow collisions between vertices which are near each other. We measure distance as max(abs(ax - bx), abs(ay - by), abs(az - bz)).
            const int minimumDistance = 3;
            const int mask = (1 << 10) - 1;
            var ax = a.localIndices & mask;
            var bx = b.localIndices & mask;
            var differenceX = ax - bx;
            if (differenceX < -minimumDistance || differenceX > minimumDistance)
                return true;
            var ay = (a.localIndices >> 10) & mask;
            var by = (b.localIndices >> 10) & mask;
            var differenceY = ay - by;
            if (differenceY < -minimumDistance || differenceY > minimumDistance)
                return true;
            var az = (a.localIndices >> 20) & mask;
            var bz = (b.localIndices >> 20) & mask;
            var differenceZ = az - bz;
            if (differenceZ < -minimumDistance || differenceZ > minimumDistance)
                return true;
            return false;
        }
    }


    struct DeformableCallbacks : INarrowPhaseCallbacks
    {
        public CollidableProperty<DeformableCollisionFilter> Filters;
        public void Initialize(Simulation simulation)
        {
            Filters.Initialize(simulation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b)
        {
            if (a.Mobility == CollidableMobility.Dynamic && b.Mobility == CollidableMobility.Dynamic)
            {
                return DeformableCollisionFilter.Test(Filters[a.BodyHandle], Filters[b.BodyHandle]);
            }
            return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
        {
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
        {
            pairMaterial.FrictionCoefficient = 1;
            pairMaterial.MaximumRecoveryVelocity = 2f;
            pairMaterial.SpringSettings = new SpringSettings(30, 1);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
        {
            return true;
        }

        public void Dispose()
        {
            Filters.Dispose();
        }
    }

    /// <summary>
    /// Some blobs composed of springy welds and volume preservation constraints.
    /// </summary>
    public class NewtDemo : Demo
    {
        struct Edge : IEqualityComparerRef<Edge>
        {
            public int A;
            public int B;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Hash(ref Edge item)
            {
                return item.A + item.B;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(ref Edge a, ref Edge b)
            {
                return (a.A == b.A && a.B == b.B) || (a.B == b.A && a.A == b.B);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void TryAddEdge(int a, int b, ref QuickSet<Edge, Edge> edges, ref Buffer<int> vertexEdgeCounts, BufferPool pool)
        {
            if (edges.Add(new Edge { A = a, B = b }, pool))
            {
                ++vertexEdgeCounts[a];
                ++vertexEdgeCounts[b];
            }
        }

        private static unsafe int CreateTetrahedralUniqueEdgesList(ref Buffer<TetrahedronVertices> tetrahedraVertices,
            ref Buffer<int> vertexEdgeCounts, BufferPool pool, ref QuickSet<Edge, Edge> cellEdges)
        {
            for (int i = 0; i < tetrahedraVertices.Length; ++i)
            {
                //Collect all unique hexahedral edges. We're going to stick welds between all of them.
                ref var tetrahedron = ref tetrahedraVertices[i];

                TryAddEdge(tetrahedron.A, tetrahedron.B, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(tetrahedron.A, tetrahedron.C, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(tetrahedron.A, tetrahedron.D, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(tetrahedron.B, tetrahedron.C, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(tetrahedron.B, tetrahedron.D, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(tetrahedron.C, tetrahedron.D, ref cellEdges, ref vertexEdgeCounts, pool);
            }
            return 18;
        }

        private static unsafe int CreateHexahedralUniqueEdgesList(ref Buffer<CellVertexIndices> cellVertexIndices,
            ref Buffer<int> vertexEdgeCounts, BufferPool pool, ref QuickSet<Edge, Edge> cellEdges)
        {
            for (int i = 0; i < cellVertexIndices.Length; ++i)
            {
                //Collect all unique hexahedral edges. We're going to stick welds between all of them.
                ref var cell = ref cellVertexIndices[i];
                TryAddEdge(cell.V000, cell.V001, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(cell.V000, cell.V010, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(cell.V000, cell.V100, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(cell.V001, cell.V011, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(cell.V001, cell.V101, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(cell.V010, cell.V011, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(cell.V010, cell.V110, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(cell.V011, cell.V111, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(cell.V100, cell.V101, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(cell.V100, cell.V110, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(cell.V101, cell.V111, ref cellEdges, ref vertexEdgeCounts, pool);
                TryAddEdge(cell.V110, cell.V111, ref cellEdges, ref vertexEdgeCounts, pool);
            }
            return 6;
        }

        internal unsafe static void CreateDeformable(Simulation simulation, in Vector3 position, in Quaternion orientation, float density, float cellSize, in SpringSettings weldSpringiness, in SpringSettings volumeSpringiness, int instanceId, CollidableProperty<DeformableCollisionFilter> filters,
            ref Buffer<Vector3> vertices, ref CellSet vertexSpatialIndices, ref Buffer<CellVertexIndices> cellVertexIndices, ref Buffer<TetrahedronVertices> tetrahedraVertexIndices)
        {
            var pool = simulation.BufferPool;
            pool.TakeAtLeast<int>(vertices.Length, out var vertexEdgeCounts);
            vertexEdgeCounts.Clear(0, vertices.Length);
            var edges = new QuickSet<Edge, Edge>(vertices.Length * 3, pool);
            var edgeCountForInternalVertex = CreateHexahedralUniqueEdgesList(ref cellVertexIndices, ref vertexEdgeCounts, pool, ref edges);
            //var edgeCountForInternalVertex = CreateTetrahedralUniqueEdgesList(ref tetrahedraVertexIndices, ref vertexEdgeCounts, ref cellEdgePool, ref intPool, ref edges);

            pool.TakeAtLeast<BodyHandle>(vertices.Length, out var vertexHandles);
            var vertexShape = new Sphere(cellSize * 0.7f);
            var massPerVertex = density * (cellSize * cellSize * cellSize);
            vertexShape.ComputeInertia(massPerVertex, out var vertexInertia);
            //vertexInertia.InverseInertiaTensor = default;
            var vertexShapeIndex = simulation.Shapes.Add(vertexShape);
            for (int i = 0; i < vertices.Length; ++i)
            {
                vertexHandles[i] = simulation.Bodies.Add(BodyDescription.CreateDynamic(new RigidPose(
                    position + QuaternionEx.Transform(vertices[i], orientation), orientation), vertexInertia,
                    //Bodies don't have to have collidables. Take advantage of this for all the internal vertices.
                    new CollidableDescription(vertexEdgeCounts[i] == edgeCountForInternalVertex ? new TypedIndex() : vertexShapeIndex, cellSize * 0.5f),
                    new BodyActivityDescription(-0.01f)));
                ref var vertexSpatialIndex = ref vertexSpatialIndices[i];
                filters.Allocate(vertexHandles[i]) = new DeformableCollisionFilter(vertexSpatialIndex.X, vertexSpatialIndex.Y, vertexSpatialIndex.Z, instanceId);
            }

            for (int i = 0; i < edges.Count; ++i)
            {
                ref var edge = ref edges[i];
                var offset = vertices[edge.B] - vertices[edge.A];
                simulation.Solver.Add(vertexHandles[edge.A], vertexHandles[edge.B],
                    new Weld
                    {
                        LocalOffset = offset,
                        LocalOrientation = Quaternion.Identity,
                        SpringSettings = weldSpringiness
                    });
                //simulation.Solver.Add(vertexHandles[edge.A], vertexHandles[edge.B],
                //    new CenterDistanceConstraint(offset.Length(), weldSpringiness));
                //simulation.Solver.Add(vertexHandles[edge.A], vertexHandles[edge.B],
                //    new BallSocket { LocalOffsetA = offset * 0.5f, LocalOffsetB = offset * -0.5f, SpringSettings = weldSpringiness });
            }
            //for (int i = 0; i < tetrahedraVertexIndices.Length; ++i)
            //{
            //    ref var tetrahedron = ref tetrahedraVertexIndices[i];
            //    simulation.Solver.Add(vertexHandles[tetrahedron.A], vertexHandles[tetrahedron.B], vertexHandles[tetrahedron.C], vertexHandles[tetrahedron.D],
            //        new VolumeConstraint(vertices[tetrahedron.A], vertices[tetrahedron.B], vertices[tetrahedron.C], vertices[tetrahedron.D], volumeSpringiness));
            //}

            pool.Return(ref vertexEdgeCounts);
            edges.Dispose(pool);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void Test()
        {
            Unsafe.SkipInit(out Vector3Wide a);
            Unsafe.SkipInit(out Vector3Wide b);
            Unsafe.SkipInit(out Vector3Wide c);
            Unsafe.SkipInit(out Vector<float> d);
            Unsafe.SkipInit(out Vector<int> e);
            Unsafe.SkipInit(out Symmetric3x3Wide s1);
            Unsafe.SkipInit(out Matrix3x3Wide s2);
            Unsafe.SkipInit(out QuaternionWide q1);
            Unsafe.SkipInit(out QuaternionWide q2);


            var r = Vector3Wide.Min(Vector3Wide.Max(Vector3Wide.Dot(a - b, b - c), a), b);
            var p = Vector3Wide.Cross((d * r) * d, -Vector3Wide.Abs(a));
            var n1 = Vector3Wide.Length(Vector3Wide.ConditionallyNegate(e, p));
            var n2 = (Vector3Wide.ConditionallyNegate(e, b)).Length();
            var uh = n1 + n2;
            uh += Vector3Wide.Dot(a * b, Vector3Wide.Broadcast(Vector3.Zero));

            var ehe = Vector3Wide.Normalize(a * q1 * q2);
            uh += ehe.LengthSquared() * q1.LengthSquared();

            var m0 = s1 * s2;
            uh += Vector3Wide.Dot(a * m0, a);

            uh += Vector3Wide.Dot(a * s1, a);
            var uh2 = a * s1;

            Console.WriteLine("$uah" + uh2);
            Console.WriteLine($"ree: {uh}");
        }

        void Test2()
        {
            const int pointCount = 16;
            var points = new QuickList<Vector3>(pointCount * 2, BufferPool);
            var random = new Random(5);
            for (int i = 0; i < pointCount; ++i)
            {
                points.AllocateUnsafely() = new Vector3(3 * (float)random.NextDouble(), 1 * (float)random.NextDouble(), 3 * (float)random.NextDouble());
            }
            var convexHull1 = new ConvexHull(points, BufferPool, out _);
            points.Count = 0;
            for (int i = 0; i < pointCount; ++i)
            {
                points.AllocateUnsafely() = new Vector3(3 * (float)random.NextDouble(), 1 * (float)random.NextDouble(), 3 * (float)random.NextDouble());
            }
            var convexHull2 = new ConvexHull(points, BufferPool, out _);
            convexHull1.ComputeInertia(1, out var scalarInertiaA);
            convexHull2.ComputeInertia(1, out var scalarInertiaB);

            BodyInertiaWide inertiaA;
            inertiaA.InverseMass = new Vector<float>(1);
            inertiaA.InverseInertiaTensor.XX = new Vector<float>(scalarInertiaA.InverseInertiaTensor.XX);
            inertiaA.InverseInertiaTensor.YX = new Vector<float>(scalarInertiaA.InverseInertiaTensor.YX);
            inertiaA.InverseInertiaTensor.YY = new Vector<float>(scalarInertiaA.InverseInertiaTensor.YY);
            inertiaA.InverseInertiaTensor.ZX = new Vector<float>(scalarInertiaA.InverseInertiaTensor.ZX);
            inertiaA.InverseInertiaTensor.ZY = new Vector<float>(scalarInertiaA.InverseInertiaTensor.ZY);
            inertiaA.InverseInertiaTensor.ZZ = new Vector<float>(scalarInertiaA.InverseInertiaTensor.ZZ);
            Vector3Wide.Broadcast(new Vector3(1, 3, 4), out var offset);
            BodyInertiaWide inertiaB;
            inertiaB.InverseMass = new Vector<float>(1);
            inertiaB.InverseInertiaTensor.XX = new Vector<float>(scalarInertiaB.InverseInertiaTensor.XX);
            inertiaB.InverseInertiaTensor.YX = new Vector<float>(scalarInertiaB.InverseInertiaTensor.YX);
            inertiaB.InverseInertiaTensor.YY = new Vector<float>(scalarInertiaB.InverseInertiaTensor.YY);
            inertiaB.InverseInertiaTensor.ZX = new Vector<float>(scalarInertiaB.InverseInertiaTensor.ZX);
            inertiaB.InverseInertiaTensor.ZY = new Vector<float>(scalarInertiaB.InverseInertiaTensor.ZY);
            inertiaB.InverseInertiaTensor.ZZ = new Vector<float>(scalarInertiaB.InverseInertiaTensor.ZZ);
            Vector3Wide.Broadcast(new Vector3(5, 4, 3), out var orientationCSV);
            Vector3Wide.Broadcast(new Vector3(-5, -4, -3), out var offsetCSV);

            convexHull1.Dispose(BufferPool);
            convexHull2.Dispose(BufferPool);
            points.Dispose(BufferPool);

            var jmjtA = inertiaA.InverseInertiaTensor + inertiaB.InverseInertiaTensor;
            Matrix3x3Wide.CreateCrossProduct(offset, out var xAB);
            var jmjtB = inertiaA.InverseInertiaTensor * xAB;
            Symmetric3x3Wide.CompleteMatrixSandwichTranspose(xAB, jmjtB, out var jmjtD);
            var diagonalAdd = inertiaA.InverseMass + inertiaB.InverseMass;
            jmjtD.XX += diagonalAdd;
            jmjtD.YY += diagonalAdd;
            jmjtD.ZZ += diagonalAdd;

            var inverseEffectiveMass = new Symmetric6x6Wide { A = jmjtA, B = jmjtB, D = jmjtD };

            const int iterationCount = 1000000;
            var startLDLT = Stopwatch.GetTimestamp();
            for (int i = 0; i < iterationCount; ++i)
            {
                Symmetric6x6Wide.LDLTSolve(orientationCSV, offsetCSV, jmjtA, jmjtB, jmjtD, out var orientationCSI, out var offsetCSI);
                //Symmetric6x6Wide.TransformWithoutOverlap(orientationCSI, offsetCSI, inverseEffectiveMass, out var roundtripOrientationCSV, out var roundtripOffsetCSV);
            }
            var endLDLT = Stopwatch.GetTimestamp();
            var startInverse = Stopwatch.GetTimestamp();
            for (int i = 0; i < iterationCount; ++i)
            {
                Symmetric6x6Wide.Invert(jmjtA, jmjtB, jmjtD, out var testEffectiveMass);
                Symmetric6x6Wide.TransformWithoutOverlap(orientationCSV, offsetCSV, testEffectiveMass, out var orientationCSI2, out var offsetCSI2);
                //Symmetric6x6Wide.TransformWithoutOverlap(orientationCSI2, offsetCSI2, inverseEffectiveMass, out var roundtripOrientationCSV2, out var roundtripOffsetCSV2);
            }
            var endInverse = Stopwatch.GetTimestamp();
            double ldltTime = (endLDLT - startLDLT) / (iterationCount * (double)Stopwatch.Frequency);
            double inverseTime = (endInverse - startInverse) / (iterationCount * (double)Stopwatch.Frequency);

            Console.WriteLine($"LDLT time (ns): {ldltTime * 1e9}");
            Console.WriteLine($"Inverse time (ns): {inverseTime * 1e9}");
            Console.WriteLine($"inverse / ldlt: {inverseTime / ldltTime}");

        }

        public unsafe override void Initialize(ContentArchive content, Camera camera)
        {
            camera.Position = new Vector3(-5f, 5.5f, 5f);
            camera.Yaw = MathHelper.Pi / 4;
            camera.Pitch = MathHelper.Pi * 0.15f;

            var filters = new CollidableProperty<DeformableCollisionFilter>();
            //The PositionFirstTimestepper is the simplest timestepping mode, but since it integrates velocity into position at the start of the frame, directly modified velocities outside of the timestep
            //will be integrated before collision detection or the solver has a chance to intervene. That's fine in this demo. Other built-in options include the PositionLastTimestepper and the SubsteppingTimestepper.
            //Note that the timestepper also has callbacks that you can use for executing logic between processing stages, like BeforeCollisionDetection.
            //Simulation = Simulation.Create(BufferPool, new DeformableCallbacks { Filters = filters }, new DemoPoseIntegratorCallbacks(new Vector3(0, -10, 0)), new EmbeddedSubsteppingTimestepper(1));
            Simulation = Simulation.Create(BufferPool, new DeformableCallbacks { Filters = filters }, new DemoPoseIntegratorCallbacks(new Vector3(0, 0, 0)), new EmbeddedSubsteppingTimestepper2(3), solverIterationCount: 1);
            //Simulation = Simulation.Create(BufferPool, new DeformableCallbacks { Filters = filters }, new DemoPoseIntegratorCallbacks(new Vector3(0, 0, 0)), new PositionFirstTimestepper2(), solverIterationCount: 5);
            //Simulation = Simulation.Create(BufferPool, new DeformableCallbacks { Filters = filters }, new DemoPoseIntegratorCallbacks(new Vector3(0, 0, 0)), new SubsteppingTimestepper2(3), solverIterationCount: 1);

            var meshContent = content.Load<MeshContent>("Content\\newt.obj");
            float cellSize = 0.1f;
            DumbTetrahedralizer.Tetrahedralize(meshContent.Triangles, cellSize, BufferPool,
                out var vertices, out var vertexSpatialIndices, out var cellVertexIndices, out var tetrahedraVertexIndices);
            var weldSpringiness = new SpringSettings(30f, 1f);
            var volumeSpringiness = new SpringSettings(30f, 1);
            for (int i = 0; i < 40; ++i)
            {
                //CreateDeformable(Simulation, new Vector3(i * 3, 5 + i * 1.5f, 0), QuaternionEx.CreateFromAxisAngle(new Vector3(1, 0, 0), MathF.PI * (i * 0.55f)), 1f, cellSize, weldSpringiness, volumeSpringiness, i, filters, ref vertices, ref vertexSpatialIndices, ref cellVertexIndices, ref tetrahedraVertexIndices);
                CreateDeformable(Simulation, new Vector3(i * 3, cellSize * 2f + i * 0f, 0), Quaternion.Identity, 1f, cellSize, weldSpringiness, volumeSpringiness, i, filters, ref vertices, ref vertexSpatialIndices, ref cellVertexIndices, ref tetrahedraVertexIndices);
            }
            //Console.WriteLine($"body count: {Simulation.Bodies.ActiveSet.Count}");
            //Console.WriteLine($"constraint count: {Simulation.Solver.CountConstraints()}");

            BufferPool.Return(ref vertices);
            vertexSpatialIndices.Dispose(BufferPool);
            BufferPool.Return(ref cellVertexIndices);
            BufferPool.Return(ref tetrahedraVertexIndices);

            //Simulation.Bodies.Add(BodyDescription.CreateConvexDynamic(new Vector3(0, 100, -.5f), 10, Simulation.Shapes, new Sphere(5)));

            Simulation.Statics.Add(new StaticDescription(new Vector3(0, -0.5f, 0), new CollidableDescription(Simulation.Shapes.Add(new Box(1500, 1, 1500)), 0.1f)));
            //Simulation.Statics.Add(new StaticDescription(new Vector3(0, -1.5f, 0), new CollidableDescription(Simulation.Shapes.Add(new Sphere(3)), 0.1f)));

        }


    }
}
