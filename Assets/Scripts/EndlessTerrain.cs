﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour {
    private const float scale = 5f;
    private const float viewerMoveThresholdForChunkUpdate = 25f;

    private const float sqrViewerMoveThresholdForChunkUpdate =
        viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;

    public LODInfo[] detailLevels;
    public static float maxViewDst;
    public Transform viewer;
    public Material mapMaterial;

    public static Vector2 viewerPosition;
    Vector2 viewerPositionOld;
    private static MapGenerator mapGenerator;
    private int chunkSize;
    private int chunksVisibleInViewDst;

    private Dictionary<Vector2, TerrainChunk> terrainChunkDictionnary = new Dictionary<Vector2, TerrainChunk>();
    static  List<TerrainChunk> terrainChunksLastUpdate = new List<TerrainChunk>();

    void Start() {
        mapGenerator = FindObjectOfType<MapGenerator>();

        maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
        chunkSize = MapGenerator.mapChunkSize - 1;
        chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / chunkSize);

        UpdateVisibleChunks();
    }

    private void Update() {  
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / scale;
        if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate) {
            viewerPositionOld = viewerPosition;
            UpdateVisibleChunks();
        }
    }

    void UpdateVisibleChunks() {
        for (int i = 0; i < terrainChunksLastUpdate.Count; i++) {
            terrainChunksLastUpdate[i].SetVisible(false);
        }

        terrainChunksLastUpdate.Clear();
        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++) {
            for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++) {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (terrainChunkDictionnary.ContainsKey(viewedChunkCoord)) {
                    terrainChunkDictionnary[viewedChunkCoord].UpdateTerrainChunk();
                } else {
                    terrainChunkDictionnary.Add(viewedChunkCoord,
                        new TerrainChunk(viewedChunkCoord, chunkSize, detailLevels, transform, mapMaterial));
                }
            }
        }
    }

    public class TerrainChunk {
        private Vector2 position;
        private GameObject meshObject;
        private Bounds bounds;

        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;

        private LODInfo[] detailLevels;
        private LODMesh[] lodMeshes;

        private MapData mapData;
        private bool mapDataReceived;
        private int previousLODIndex = -1;

        public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, Transform parent, Material material) {
            this.detailLevels = detailLevels;
            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("TerrainChunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshRenderer.material = material;
            meshObject.transform.position = positionV3 * scale;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale = Vector3.one * scale;
            SetVisible(false);

            lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i < detailLevels.Length; i++) {
                lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
            }

            mapGenerator.RequestMapData(position, onMapDataReceived);
        }

        void onMapDataReceived(MapData mapData) {
            this.mapData = mapData;
            this.mapDataReceived = true;

            Texture2D texture = TextureGenerator.TextureFromColourMap(mapData.colorMap, MapGenerator.mapChunkSize,
                MapGenerator.mapChunkSize);

            meshRenderer.material.mainTexture = texture;
            UpdateTerrainChunk();
        }

        public void UpdateTerrainChunk() {
            if (!mapDataReceived) return;
            float viewerDistanceFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
            bool visible = viewerDistanceFromNearestEdge < maxViewDst;

            if (visible) {
                int lodIndex = 0;

                for (int i = 0; i < detailLevels.Length - 1; i++) {
                    if (viewerDistanceFromNearestEdge > detailLevels[i].visibleDstThreshold) {
                        lodIndex = i + 1;
                    } else {
                        break;
                    }
                }

                if (lodIndex != previousLODIndex) {
                    LODMesh lodMesh = lodMeshes[lodIndex];
                    if (lodMesh.hasMesh) {
                        previousLODIndex = lodIndex;
                        meshFilter.mesh = lodMesh.mesh;
                    } else if (!lodMesh.hasRequestedMesh) {
                        lodMesh.RequestMesh(mapData);
                    } 
                }
                terrainChunksLastUpdate.Add(this);
            }

            SetVisible(visible);
        }

        public void SetVisible(bool visible) {
            meshObject.SetActive(visible);
        }

        public bool isVisible() {
            return meshObject.activeSelf;
        }
    }

    class LODMesh {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        private int lod;
        private System.Action updateCallback;

        public LODMesh(int lod, System.Action updateCallback) {
            this.lod = lod;
            this.updateCallback = updateCallback;
        }

        void onMeshDataReceived(MeshData meshData) {
            mesh = meshData.CreateMesh();
            hasMesh = true;

            updateCallback();
        }

        public void RequestMesh(MapData mapData) {
            hasRequestedMesh = true;
            mapGenerator.RequestMeshData(mapData, lod, onMeshDataReceived);
        }
    }

    [System.Serializable]
    public struct LODInfo {
        public int lod;
        public float visibleDstThreshold;
    }
}