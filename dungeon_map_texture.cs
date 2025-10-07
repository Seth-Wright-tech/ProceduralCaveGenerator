using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dungeon_map_texture : MonoBehaviour
{
    public int map_width = 64;
    public int map_height = 64;
    private int[,] map;
    public int seed = 1234;
    private float fillPercent = 0.52f;

    public GameObject chestPrefab;
    public float chestChance = 0.02f;

    void Start()
    {
        Random.InitState(seed);

        bool mapValid = false;
        while (!mapValid)
        {
            GenerateMap();
            SmoothMap(6);
            mapValid = CleanMapWithFloodFill();
        }

        // create a new GameObject and give it a MeshFilter and a MeshRenderer
        GameObject dungeonObj = new GameObject("Dungeon Mesh");
        MeshFilter mf = dungeonObj.AddComponent<MeshFilter>();
        MeshRenderer mr = dungeonObj.AddComponent<MeshRenderer>();
        dungeonObj.transform.position = Vector3.zero;

        // Create generator
        dungeon_mesh_generator generator = dungeonObj.AddComponent<dungeon_mesh_generator>();
        generator.map_width = map_width;
        generator.map_height = map_height;
        generator.map = map;

        // Generate mesh
        Mesh dungeonMesh = generator.GenerateMesh();
        mf.mesh = dungeonMesh;
        PlaceChests();

        Material mat = new Material(Shader.Find("Standard"));
        mat.color = Color.gray;
        mr.material = mat;
    }

    void GenerateMap()
    {
        map = new int[map_width, map_height];


        for (int x = 0; x < map_width; x++)
        {
            for (int y = 0; y < map_height; y++)
            {
                if (x == 0 || y == 0 || x == map_width - 1 || y == map_height - 1)
                {
                    map[x, y] = 1;
                }
                else
                {
                    map[x, y] = Random.value > fillPercent ? 1 : 0; // 1 for wall, 0 for floor
                }
            }
        }
    }

    void SmoothMap(int iterations = 5)
    {
        for (int i = 0; i < iterations; i++)
        {
            int[,] newMap = new int[map_width, map_height];

            for (int x = 0; x < map_width; x++)
            {
                for (int y = 0; y < map_height; y++)
                {
                    int wallCount = GetAdjacentWallCount(x, y);

                    if (map[x, y] == 1)
                    {
                        // Wall survives if 4+ neighbors
                        newMap[x, y] = (wallCount >= 4) ? 1 : 0;
                    }
                    else
                    {
                        // Floor becomes wall if 5+ neighbors
                        newMap[x, y] = (wallCount >= 5) ? 1 : 0;
                    }
                }
            }

            map = newMap;
        }
    }

    int GetAdjacentWallCount(int gridX, int gridY)
    {
        int wallCount = 0;
        for (int neighborX = gridX - 1; neighborX <= gridX + 1; neighborX++)
        {
            for (int neighborY = gridY - 1; neighborY <= gridY + 1; neighborY++)
            {
                if (neighborX >= 0 && neighborX < map_width && neighborY >= 0 && neighborY < map_height)
                {
                    if (neighborX != gridX || neighborY != gridY)
                    {
                        wallCount += map[neighborX, neighborY];
                    }
                }
                else
                {
                    wallCount++;
                }
            }
        }
        return wallCount;
    }

    bool CleanMapWithFloodFill()
    {
        int totalCells = map_width * map_height;

        List<Vector2Int> floorCells = new List<Vector2Int>();
        for (int x = 0; x < map_width; x++)
        {
            for (int y = 0; y < map_height; y++)
            {
                if (map[x, y] == 0) floorCells.Add(new Vector2Int(x, y));
            }
        }

        if (floorCells.Count == 0) return false; // no floor at all

        const int maxAttempts = 10;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2Int start = floorCells[Random.Range(0, floorCells.Count)];
            bool[,] visited = new bool[map_width, map_height];
            List<Vector2Int> filled = FloodFill(map, visited, start.x, start.y);

            if (filled.Count > totalCells * 0.45f)
            {
                // fill in all unvisited floor cells as walls
                for (int x = 0; x < map_width; x++)
                {
                    for (int y = 0; y < map_height; y++)
                    {
                        if (map[x, y] == 0 && !visited[x, y])
                        {
                            map[x, y] = 1;
                        }
                    }
                }
                return true; // cleanup successful
            }
        }
        return false; // cleanup failed
    }

    List<Vector2Int> FloodFill(int[,] map, bool[,] visited, int startX, int startY)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        List<Vector2Int> cells = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        visited[startX, startY] = true;

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            cells.Add(current);

            for (int i = 0; i < 4; i++)
            {
                int nx = current.x + dx[i];
                int ny = current.y + dy[i];

                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    if (!visited[nx, ny] && map[nx, ny] == 0)
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
        }

        return cells;
    }

    
    void PlaceChests()
    {
        float squareSize = 1f; 
        float chestHeight = 0.17f;
        float halfWidth = map_width * 0.5f * squareSize;  
        float halfHeight = map_height * 0.5f * squareSize;  

        for (int x = 1; x < map_width - 1; x++)
        {
            for (int y = 1; y < map_height - 1; y++)
            {
                if (map[x, y] == 0)
                {
                    int wallCount = 0;
                    if (map[x + 1, y] == 1) wallCount++;
                    if (map[x - 1, y] == 1) wallCount++;
                    if (map[x, y + 1] == 1) wallCount++;
                    if (map[x, y - 1] == 1) wallCount++;

                    if (wallCount <= 3 && Random.value < chestChance)
                    {
                        float localX = (x - map_width / 2f) * squareSize;
                        float localZ = (y - map_height / 2f) * squareSize;

                        localX = Mathf.Clamp(localX, -halfWidth + 0.5f, halfWidth - 0.5f);
                        localZ = Mathf.Clamp(localZ, -halfHeight + 0.5f, halfHeight - 0.5f);

                        Vector3 localChestPos = new Vector3(localX, chestHeight, localZ);

                        Vector3 worldChestPos = transform.TransformPoint(localChestPos);

                        Instantiate(chestPrefab, worldChestPos, Quaternion.identity);
                    }
                }
            }
        }
    }
}
