using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dungeon_mesh_generator : MonoBehaviour
{
    public int map_width;
    public int map_height;
    public int[,] map;
    private Dictionary<Vector3, int> vertexLookup = new Dictionary<Vector3, int>();
    public float wallHeight = 3f;

    public class ControlNode
    {
        public Vector3 position;
        public bool active;

        public ControlNode(Vector3 pos, bool active)
        {
            position = pos;
            this.active = active;
        }
    }

    public class Square
    {
        public ControlNode topLeft, topRight, bottomRight, bottomLeft;
        public Vector3 centerTop, centerRight, centerBottom, centerLeft;
        public int configuration;

        public Square(ControlNode tl, ControlNode tr, ControlNode br, ControlNode bl)
        {
            topLeft = tl;
            topRight = tr;
            bottomRight = br;
            bottomLeft = bl;

            centerTop    = new Vector3((tl.position.x + tr.position.x) * 0.5f, 0, tl.position.z);
            centerRight  = new Vector3(tr.position.x, 0, (tr.position.z + br.position.z) * 0.5f);
            centerBottom = new Vector3((bl.position.x + br.position.x) * 0.5f, 0, bl.position.z);
            centerLeft   = new Vector3(bl.position.x, 0, (tl.position.z + bl.position.z) * 0.5f);

            if (tl.active) configuration |= 8;
            if (tr.active) configuration |= 4;
            if (br.active) configuration |= 2;
            if (bl.active) configuration |= 1;
        }
    }

    public Mesh GenerateMesh(float squareSize = 1f)
    {
        vertexLookup.Clear();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        ControlNode[,] controlNodes = new ControlNode[map_width, map_height];

        float xOffset = -map_width * 0.5f * squareSize;
        float yOffset = -map_height * 0.5f * squareSize;

        for (int x = 0; x < map_width; x++)
        {
            for (int y = 0; y < map_height; y++)
            {
                Vector3 pos = new Vector3(
                    x * squareSize + xOffset,
                    0.2f,
                    y * squareSize + yOffset
                );

                bool active = map[x, y] == 1;
                controlNodes[x, y] = new ControlNode(pos, active);
            }
        }

        for (int x = 0; x < map_width - 1; x++)
        {
            for (int y = 0; y < map_height - 1; y++)
            {
                Square square = new Square(
                    controlNodes[x, y + 1],
                    controlNodes[x + 1, y + 1],
                    controlNodes[x + 1, y],
                    controlNodes[x, y]
                );

                TriangulateSquare(square, vertices, triangles);
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        AddWalls(mesh, wallHeight);  
        return mesh;
    }

    void AddWalls(Mesh mesh, float height)
    {
        var verts = new List<Vector3>(mesh.vertices);
        var tris = new List<int>(mesh.triangles);

        Dictionary<(int, int), int> edgeUse = new Dictionary<(int, int), int>();

        for (int i = 0; i < tris.Count; i += 3)
        {
            int a = tris[i], b = tris[i + 1], c = tris[i + 2];
            AddEdge(edgeUse, a, b);
            AddEdge(edgeUse, b, c);
            AddEdge(edgeUse, c, a);
        }

        foreach (var edge in edgeUse)
        {
            if (edge.Value == 1)
            {
                int i0 = edge.Key.Item1;
                int i1 = edge.Key.Item2;

                Vector3 v0 = verts[i0];
                Vector3 v1 = verts[i1];

                Vector3 v0Top = v0 + Vector3.up * height;
                Vector3 v1Top = v1 + Vector3.up * height;

                int i0Top = verts.Count; verts.Add(v0Top);
                int i1Top = verts.Count; verts.Add(v1Top);

                tris.Add(i0); tris.Add(i0Top); tris.Add(i1);
                tris.Add(i1); tris.Add(i0Top); tris.Add(i1Top);

                tris.Add(i0); tris.Add(i1); tris.Add(i0Top);
                tris.Add(i1); tris.Add(i1Top); tris.Add(i0Top);
            }
        }

        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();

        // Compute normals manually
        Vector3[] normals = new Vector3[mesh.vertices.Length];
        for (int i = 0; i < tris.Count; i += 3)
        {
            int i0 = tris[i];
            int i1 = tris[i + 1];
            int i2 = tris[i + 2];

            Vector3 a = mesh.vertices[i0];
            Vector3 b = mesh.vertices[i1];
            Vector3 c = mesh.vertices[i2];

            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

            normals[i0] = normal;
            normals[i1] = normal;
            normals[i2] = normal;
        }

        mesh.normals = normals;
    }

    void AddEdge(Dictionary<(int, int), int> edgeUse, int a, int b)
    {
        var edge = (Mathf.Min(a, b), Mathf.Max(a, b));
        if (edgeUse.ContainsKey(edge)) edgeUse[edge]++;
        else edgeUse[edge] = 1;
    }
        void TriangulateSquare(Square square, List<Vector3> vertices, List<int> triangles)
    {
        switch (square.configuration)
        {
            case 0:
                // Empty - no triangles
                break;

            // 1 point configurations
            case 1: // Bottom Left only
                AddTriangle(square.centerLeft, square.bottomLeft.position, square.centerBottom, vertices, triangles);
                break;

            case 2: // Bottom Right only
                AddTriangle(square.centerBottom, square.bottomRight.position, square.centerRight, vertices, triangles);
                break;

            case 4: // Top Right only
                AddTriangle(square.centerTop, square.topRight.position, square.centerRight, vertices, triangles);
                break;

            case 8: // Top Left only
                AddTriangle(square.topLeft.position, square.centerTop, square.centerLeft, vertices, triangles);
                break;

            case 3: // Bottom edge (BL + BR)
                AddTriangle(square.centerLeft, square.centerRight, square.bottomRight.position, vertices, triangles);
                AddTriangle(square.centerLeft, square.bottomRight.position, square.bottomLeft.position, vertices, triangles);
                break;

            case 6: // Right edge (TR + BR)
                AddTriangle(square.centerTop, square.topRight.position, square.bottomRight.position, vertices, triangles);
                AddTriangle(square.centerTop, square.bottomRight.position, square.centerBottom, vertices, triangles);
                break;

            case 9: // Left edge (TL + BL)
                AddTriangle(square.topLeft.position, square.centerTop, square.centerBottom, vertices, triangles);
                AddTriangle(square.topLeft.position, square.centerBottom, square.bottomLeft.position, vertices, triangles);
                break;

            case 12: // Top edge (TL + TR)
                AddTriangle(square.topLeft.position, square.topRight.position, square.centerRight, vertices, triangles);
                AddTriangle(square.topLeft.position, square.centerRight, square.centerLeft, vertices, triangles);
                break;

            case 5: // Diagonal (TR + BL)
                AddTriangle(square.centerTop, square.topRight.position, square.centerRight, vertices, triangles);
                AddTriangle(square.centerLeft, square.bottomLeft.position, square.centerBottom, vertices, triangles);
                break;

            case 10: // Diagonal (TL + BR)
                AddTriangle(square.topLeft.position, square.centerTop, square.centerLeft, vertices, triangles);
                AddTriangle(square.centerBottom, square.bottomRight.position, square.centerRight, vertices, triangles);
                break;

            case 7: // All but Top Left (missing TL)
                AddTriangle(square.centerLeft, square.centerTop, square.topRight.position, vertices, triangles);
                AddTriangle(square.centerLeft, square.topRight.position, square.bottomRight.position, vertices, triangles);
                AddTriangle(square.centerLeft, square.bottomRight.position, square.bottomLeft.position, vertices, triangles);
                break;

            case 11: // All but Top Right (missing TR)
                AddTriangle(square.centerTop, square.centerRight, square.bottomRight.position, vertices, triangles);
                AddTriangle(square.centerTop, square.bottomRight.position, square.bottomLeft.position, vertices, triangles);
                AddTriangle(square.centerTop, square.bottomLeft.position, square.topLeft.position, vertices, triangles);
                break;

            case 13: // All but Bottom Right (missing BR)
                AddTriangle(square.centerBottom, square.centerRight, square.topRight.position, vertices, triangles);
                AddTriangle(square.centerBottom, square.topRight.position, square.topLeft.position, vertices, triangles);
                AddTriangle(square.centerBottom, square.topLeft.position, square.bottomLeft.position, vertices, triangles);
                break;

            case 14: // All but Bottom Left (missing BL)
                AddTriangle(square.centerLeft, square.centerBottom, square.bottomRight.position, vertices, triangles);
                AddTriangle(square.centerLeft, square.bottomRight.position, square.topRight.position, vertices, triangles);
                AddTriangle(square.centerLeft, square.topRight.position, square.topLeft.position, vertices, triangles);
                break;

            case 15: // Full square
                AddTriangle(square.topLeft.position, square.topRight.position, square.bottomRight.position, vertices, triangles);
                AddTriangle(square.topLeft.position, square.bottomRight.position, square.bottomLeft.position, vertices, triangles);
                break;
        }
    }

    void AddTriangle(Vector3 a, Vector3 b, Vector3 c, List<Vector3> verts, List<int> tris)
    {
        int i0 = GetVertexIndex(a, verts);
        int i1 = GetVertexIndex(b, verts);
        int i2 = GetVertexIndex(c, verts);

        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 cross = Vector3.Cross(ab, ac);

        if (cross.y < 0)
        {
            tris.Add(i0);
            tris.Add(i2);
            tris.Add(i1);
        }
        else
        {
            tris.Add(i0);
            tris.Add(i1);
            tris.Add(i2);
        }
    }
    
    int GetVertexIndex(Vector3 v, List<Vector3> verts)
    {
        if (vertexLookup.TryGetValue(v, out int index))
            return index;

        index = verts.Count;
        verts.Add(v);
        vertexLookup[v] = index;
        return index;
    }

}