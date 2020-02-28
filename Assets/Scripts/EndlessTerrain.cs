using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour
{
    public const float maxViewDst = 450;
    public Transform viewer;
    public Material mapMaterial;

    public static Vector2 viewerPosition;
    private static MapGenerator mapGenerator;
    private int chunkSize;
    private int chunksVisibleInViewDst;

    private Dictionary<Vector2, TerrainChunk> terrainChunkDictionnary = new Dictionary<Vector2, TerrainChunk>();
    List<TerrainChunk> terrainChunksLastUpdate = new List<TerrainChunk>();

    void Start()
    {
        chunkSize = MapGenerator.mapChunkSize - 1;
        chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / chunkSize);
        mapGenerator = FindObjectOfType<MapGenerator>();
    }

    private void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        UpdateVisibleChunks();
    }

    void UpdateVisibleChunks()
    {
        for (int i = 0; i < terrainChunksLastUpdate.Count; i++)
        {
            terrainChunksLastUpdate[i].SetVisible(false);
        }
        terrainChunksLastUpdate.Clear();
        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
        {

            for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (terrainChunkDictionnary.ContainsKey(viewedChunkCoord))
                {
                    terrainChunkDictionnary[viewedChunkCoord].UpdateTerrainChunk();
                    if (terrainChunkDictionnary[viewedChunkCoord].isVisible()) terrainChunksLastUpdate.Add(terrainChunkDictionnary[viewedChunkCoord]);
                } else {
                    terrainChunkDictionnary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, transform, mapMaterial));
                }
            }
        }
    }

    public class TerrainChunk
    {
        private Vector2 position;
        private GameObject meshObject;
        private Bounds bounds;

        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;

        public TerrainChunk(Vector2 coord, int size, Transform parent, Material material)
        {
            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("TerrainChunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshRenderer.material = material;
            meshObject.transform.position = positionV3;
            meshObject.transform.parent = parent;
            SetVisible(false);
            
            mapGenerator.RequestMapData(onMapDataReceived);
        }

        void onMapDataReceived(MapData mapData)
        {
            mapGenerator.RequestMeshData(mapData, onMeshDataReceived);
        }

        void onMeshDataReceived(MeshData meshData)
        {
            meshFilter.mesh = meshData.CreateMesh();
        }

        public void UpdateTerrainChunk()
        {
            float viewerDistanceFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
            bool visible = viewerDistanceFromNearestEdge < maxViewDst;
            SetVisible(visible);
        }

        public void SetVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }

        public bool isVisible()
        {
            return meshObject.activeSelf;
        }
    }
}
