// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using Mafi;
using System;
using System.Collections.Generic;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Entities.Static.Layout;
using Mafi.Unity;
using Mafi.Unity.Entities;
using Mafi.Unity.InputControl;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoIDesignerToolkit;

/// <summary>
/// Draws semi-transparent bounding boxes for every layout tile of every static
/// entity when Layout Box Mode is enabled (Alt+B). The cache of matrices is
/// rebuilt only when entities are added/removed.
/// </summary>
[GlobalDependency(RegistrationMode.AsSelf, false, false)]
public class LayoutBoxRendererMb : MonoBehaviour
{
    private struct EntityCacheEntry
    {
        public Vector3 Position;
        public Matrix4x4[] BodyMatrices;
        public Matrix4x4[] TopMatrices;
        public Matrix4x4[] OverlappableTopMatrices;
        public Matrix4x4[] VehicleSurfaceBodyMatrices;
        public Matrix4x4[] VehicleSurfaceTopMatrices;
        public Mesh? VehicleSurfaceMesh;
    }

    private Mesh m_cubeMesh = null!;
    private Material m_boxMaterial = null!;
    private Material m_topMaterial = null!;
    private Material m_overlappableTopMaterial = null!;
    private Material m_vehicleSurfaceBodyMaterial = null!;
    private Material m_vehicleSurfaceMaterial = null!;
    private readonly List<EntityCacheEntry> m_cache = new List<EntityCacheEntry>();
    private IEntitiesManager m_entitiesManager = null!;
    private ShortcutsManager m_shortcutsManager = null!;
    private bool m_isCacheDirty = true;
    private bool m_initFailed;

    public void Init(IEntitiesManager entitiesManager, ShortcutsManager shortcutsManager)
    {
        m_entitiesManager = entitiesManager;
        m_shortcutsManager = shortcutsManager;

        m_entitiesManager.StaticEntityAdded.AddNonSaveable(this, OnEntityChanged);
        m_entitiesManager.StaticEntityRemoved.AddNonSaveable(this, OnEntityChanged);

        // --- Mesh -----------------------------------------------------------
        // Build the cube mesh manually so we're not dependent on the lifecycle
        // of a temporary primitive GameObject.
        m_cubeMesh = BuildUnitCubeMesh();
        if (m_cubeMesh == null)
        {
            Log.Error("[LayoutBoxMode] Failed to build cube mesh.");
            m_initFailed = true;
            return;
        }

        // --- Material -------------------------------------------------------
        // The Standard shader may be stripped in some Unity builds. Try it
        // first, then fall back to shaders that are always present.
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            Log.Warning("[LayoutBoxMode] 'Standard' shader not found, trying 'Sprites/Default'.");
            shader = Shader.Find("Sprites/Default");
        }
        if (shader == null)
        {
            Log.Warning("[LayoutBoxMode] 'Sprites/Default' shader not found, trying 'Hidden/Internal-Colored'.");
            shader = Shader.Find("Hidden/Internal-Colored");
        }
        if (shader == null)
        {
            Log.Error("[LayoutBoxMode] No usable shader found. Layout boxes disabled.");
            m_initFailed = true;
            return;
        }
        Log.Info($"[LayoutBoxMode] Using shader: {shader.name}");

        // Use a shader with explicit alpha blending for all overlay geometry.
        // Standard's material mode changes have appeared opaque in game even
        // with low color alpha, so prefer Unity's built-in transparent shader.
        Shader overlayShader = Shader.Find("Hidden/Internal-Colored");
        if (overlayShader == null)
        {
            overlayShader = Shader.Find("Sprites/Default") ?? shader;
        }
        Log.Info($"[LayoutBoxMode] Using overlay shader: {overlayShader.name}");

        m_boxMaterial = new Material(overlayShader);
        // Semi-transparent light blue side walls.
        m_boxMaterial.color = new Color(0.2f, 0.8f, 1f, 0.1f);
        SetupTransparentMaterial(m_boxMaterial);

        m_topMaterial = new Material(overlayShader);
        // Amber caps stay translucent so the building remains visible below them.
        m_topMaterial.color = new Color(1f, 0.62f, 0.08f, 0.25f);
        SetupTransparentMaterial(m_topMaterial);

        m_overlappableTopMaterial = new Material(overlayShader);
        // Blue caps distinguish cells that allow overlap with another vehicle surface.
        m_overlappableTopMaterial.color = new Color(0.2f, 0.8f, 1f, 0.05f);
        SetupTransparentMaterial(m_overlappableTopMaterial);

        m_vehicleSurfaceBodyMaterial = new Material(overlayShader);
        // Keep green side walls as transparent as the blue box walls.
        m_vehicleSurfaceBodyMaterial.color = new Color(0.15f, 1f, 0.2f, 0.05f);
        SetupTransparentMaterial(m_vehicleSurfaceBodyMaterial);

        m_vehicleSurfaceMaterial = new Material(overlayShader);
        // Green caps and planes match the transparency of the amber caps.
        m_vehicleSurfaceMaterial.color = new Color(0.15f, 1f, 0.2f, 0.05f);
        SetupTransparentMaterial(m_vehicleSurfaceMaterial);

        Log.Info($"[LayoutBoxMode] Init complete. Mesh={m_cubeMesh.name}, " +
                 $"BodyMaterial={m_boxMaterial.name}, TopMaterial={m_topMaterial.name}, " +
                 $"Shader={m_boxMaterial.shader.name}");
    }

    private void OnEntityChanged(IStaticEntity entity)
    {
        m_isCacheDirty = true;
    }

    private void RebuildCache()
    {
        ReleaseVehicleSurfaceMeshes();
        m_cache.Clear();

        int totalBoxes = 0;
        int entityCount = 0;

        foreach (IEntity baseEntity in m_entitiesManager.Entities)
        {
            if (!(baseEntity is IStaticEntity entity)) continue;
            if (entity.IsDestroyed) continue;

            if (!(entity.Prototype is ILayoutEntityProto layoutProto)) continue;
            EntityLayout layout = layoutProto.Layout;
            if (layout == null) continue;

            if (!(entity is ILayoutEntity layoutEntity)) continue;
            Mafi.Core.TileTransform transform = layoutEntity.Transform;

            entityCount++;

            List<Matrix4x4> bodyMatrices = new List<Matrix4x4>();
            List<Matrix4x4> topMatrices = new List<Matrix4x4>();
            List<Matrix4x4> overlappableTopMatrices = new List<Matrix4x4>();
            List<Matrix4x4> vehicleSurfaceBodyMatrices = new List<Matrix4x4>();
            List<Matrix4x4> vehicleSurfaceTopMatrices = new List<Matrix4x4>();
            List<Vector3> vehicleSurfaceVertices = new List<Vector3>();
            List<int> vehicleSurfaceTriangles = new List<int>();

            Dictionary<Tile2i, float> vehicleSurfaceHeights = new Dictionary<Tile2i, float>();
            foreach (KeyValuePair<Tile2i, HeightTilesF> surfaceHeight in entity.VehicleSurfaceHeights)
            {
                vehicleSurfaceHeights[surfaceHeight.Key] = surfaceHeight.Value.Value.ToFloat() * 2f;
            }

            foreach (LayoutTile tile in layout.LayoutTiles)
            {
                int heightFrom = tile.OccupiedThickness.From.Value;
                int heightTo = tile.OccupiedThickness.To.Value;
                if (heightFrom >= heightTo) continue;

                RelTile2i coord = tile.Coord;
                Tile3i absoluteTile = layout.Transform(coord.ExtendZ(heightFrom), transform);

                float deltaH = heightTo - heightFrom;
                float zCenter = absoluteTile.Z + (deltaH / 2.0f);

                // Cap geometry: a flat box at the very top of the box.
                // It has a height of 0.08 units and resides in [top_height - 0.08, top_height].
                float capHeight = 0.08f;
                float topY = (absoluteTile.Z + deltaH) * 2f;
                Vector3 capPos = new Vector3(
                    (absoluteTile.X + 0.5f) * 2f,
                    topY - (capHeight / 2f),
                    (absoluteTile.Y + 0.5f) * 2f);
                Vector3 capScale = new Vector3(2f, capHeight, 2f);

                // Body geometry: covers from bottom_height to top_height - 0.08.
                // Height = (deltaH * 2) - 0.08.
                // Center Y is shifted down by 0.04 to perfectly align with bottom and cap.
                float bodyHeight = (deltaH * 2f) - capHeight;
                Vector3 bodyPos = new Vector3(
                    (absoluteTile.X + 0.5f) * 2f,
                    (zCenter * 2f) - (capHeight / 2f),
                    (absoluteTile.Y + 0.5f) * 2f);
                Vector3 bodyScale = new Vector3(2f, bodyHeight, 2f);

                Matrix4x4 bodyMatrix = Matrix4x4.TRS(bodyPos, Quaternion.identity, bodyScale);
                if (tile.HasVehicleSurface)
                {
                    vehicleSurfaceBodyMatrices.Add(bodyMatrix);
                }
                else
                {
                    bodyMatrices.Add(bodyMatrix);
                }

                bool isOverlappableVehicleSurface =
                    (tile.Constraint & LayoutTileConstraint.OverlappableVehicleSurface) != LayoutTileConstraint.None;
                Matrix4x4 capMatrix = Matrix4x4.TRS(capPos, Quaternion.identity, capScale);
                if (tile.HasVehicleSurface)
                {
                    vehicleSurfaceTopMatrices.Add(capMatrix);
                }
                else if (isOverlappableVehicleSurface)
                {
                    overlappableTopMatrices.Add(capMatrix);
                }
                else
                {
                    topMatrices.Add(capMatrix);
                }
                totalBoxes++;

                if (tile.HasVehicleSurface &&
                    TryGetVehicleSurfaceHeight(vehicleSurfaceHeights, absoluteTile.X, absoluteTile.Y, out float h00) &&
                    TryGetVehicleSurfaceHeight(vehicleSurfaceHeights, absoluteTile.X + 1, absoluteTile.Y, out float h10) &&
                    TryGetVehicleSurfaceHeight(vehicleSurfaceHeights, absoluteTile.X + 1, absoluteTile.Y + 1, out float h11) &&
                    TryGetVehicleSurfaceHeight(vehicleSurfaceHeights, absoluteTile.X, absoluteTile.Y + 1, out float h01))
                {
                    AddVehicleSurfaceQuad(
                        vehicleSurfaceVertices,
                        vehicleSurfaceTriangles,
                        absoluteTile.X * 2f,
                        absoluteTile.Y * 2f,
                        h00,
                        h10,
                        h11,
                        h01);
                }
            }

            if (bodyMatrices.Count > 0 || vehicleSurfaceBodyMatrices.Count > 0)
            {
                Vector3 entityPos = new Vector3(
                    (transform.Position.X + 0.5f) * 2f,
                    transform.Position.Z * 2f,
                    (transform.Position.Y + 0.5f) * 2f);

                m_cache.Add(new EntityCacheEntry
                {
                    Position = entityPos,
                    BodyMatrices = bodyMatrices.ToArray(),
                    TopMatrices = topMatrices.ToArray(),
                    OverlappableTopMatrices = overlappableTopMatrices.ToArray(),
                    VehicleSurfaceBodyMatrices = vehicleSurfaceBodyMatrices.ToArray(),
                    VehicleSurfaceTopMatrices = vehicleSurfaceTopMatrices.ToArray(),
                    VehicleSurfaceMesh = BuildVehicleSurfaceMesh(vehicleSurfaceVertices, vehicleSurfaceTriangles)
                });
            }
        }

        Log.Info($"[LayoutBoxMode] Rebuilt cache: {totalBoxes} boxes from {m_cache.Count} " +
                 $"entities (total checked static entities: {entityCount}).");
        m_isCacheDirty = false;
    }

    private void OnDestroy()
    {
        ReleaseVehicleSurfaceMeshes();
        if (m_cubeMesh != null) Destroy(m_cubeMesh);
        if (m_boxMaterial != null) Destroy(m_boxMaterial);
        if (m_topMaterial != null) Destroy(m_topMaterial);
        if (m_overlappableTopMaterial != null) Destroy(m_overlappableTopMaterial);
        if (m_vehicleSurfaceBodyMaterial != null) Destroy(m_vehicleSurfaceBodyMaterial);
        if (m_vehicleSurfaceMaterial != null) Destroy(m_vehicleSurfaceMaterial);
    }

    private void ReleaseVehicleSurfaceMeshes()
    {
        for (int i = 0; i < m_cache.Count; i++)
        {
            Mesh? mesh = m_cache[i].VehicleSurfaceMesh;
            if (mesh != null) Destroy(mesh);
        }
    }

    private void Update()
    {
        if (m_initFailed) return;

        if (m_shortcutsManager.IsDown(HotkeysRegistry.LayoutBoxModeToggle))
        {
            HotkeysRegistry.PlayClickSound();
            DesignerToolkitSettings.SetLayoutBoxModeEnabled(!DesignerToolkitSettings.LayoutBoxModeEnabled);
            m_isCacheDirty = true;
            Log.Info($"[LayoutBoxMode] Toggled to {DesignerToolkitSettings.LayoutBoxModeEnabled}");
        }

        if (!DesignerToolkitSettings.LayoutBoxModeEnabled || m_entitiesManager == null)
            return;

        if (m_isCacheDirty)
        {
            RebuildCache();
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;
        float maxDistSq = 350f * 350f; // 350 meters range

        for (int i = 0; i < m_cache.Count; i++)
        {
            EntityCacheEntry entry = m_cache[i];
            if ((entry.Position - camPos).sqrMagnitude < maxDistSq)
            {
                for (int j = 0; j < entry.BodyMatrices.Length; j++)
                {
                    Graphics.DrawMesh(m_cubeMesh, entry.BodyMatrices[j], m_boxMaterial, 0);
                }
                for (int j = 0; j < entry.TopMatrices.Length; j++)
                {
                    Graphics.DrawMesh(m_cubeMesh, entry.TopMatrices[j], m_topMaterial, 0);
                }
                for (int j = 0; j < entry.OverlappableTopMatrices.Length; j++)
                {
                    Graphics.DrawMesh(m_cubeMesh, entry.OverlappableTopMatrices[j], m_overlappableTopMaterial, 0);
                }
                for (int j = 0; j < entry.VehicleSurfaceBodyMatrices.Length; j++)
                {
                    Graphics.DrawMesh(m_cubeMesh, entry.VehicleSurfaceBodyMatrices[j], m_vehicleSurfaceBodyMaterial, 0);
                }
                for (int j = 0; j < entry.VehicleSurfaceTopMatrices.Length; j++)
                {
                    Graphics.DrawMesh(m_cubeMesh, entry.VehicleSurfaceTopMatrices[j], m_vehicleSurfaceMaterial, 0);
                }
                if (entry.VehicleSurfaceMesh != null)
                {
                    Graphics.DrawMesh(entry.VehicleSurfaceMesh, Matrix4x4.identity, m_vehicleSurfaceMaterial, 0);
                }
            }
        }
    }

    private static bool TryGetVehicleSurfaceHeight(
        Dictionary<Tile2i, float> heights,
        int x,
        int y,
        out float height)
    {
        return heights.TryGetValue(new Tile2i(x, y), out height);
    }

    private static void AddVehicleSurfaceQuad(
        List<Vector3> vertices,
        List<int> triangles,
        float x,
        float z,
        float h00,
        float h10,
        float h11,
        float h01)
    {
        int firstVertex = vertices.Count;
        // Use the final per-corner heights from the layout. Open box bottoms
        // leave no coplanar face to obscure the vehicle-surface plane.
        vertices.Add(new Vector3(x, h00, z));
        vertices.Add(new Vector3(x + 2f, h10, z));
        vertices.Add(new Vector3(x + 2f, h11, z + 2f));
        vertices.Add(new Vector3(x, h01, z + 2f));

        // The game grid's Y axis maps to Unity's Z axis. This winding faces up.
        triangles.Add(firstVertex);
        triangles.Add(firstVertex + 2);
        triangles.Add(firstVertex + 1);
        triangles.Add(firstVertex);
        triangles.Add(firstVertex + 3);
        triangles.Add(firstVertex + 2);
    }

    private static Mesh? BuildVehicleSurfaceMesh(List<Vector3> vertices, List<int> triangles)
    {
        if (vertices.Count == 0) return null;

        Mesh mesh = new Mesh { name = "BDT_LayoutBoxVehicleSurface" };
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Creates an open-bottom unit box mesh (vertices from -0.5 to +0.5) with
    /// proper normals. This avoids relying on CreatePrimitive whose shared mesh
    /// may become invalid after the temp GameObject is destroyed.
    /// </summary>
    private static Mesh BuildUnitCubeMesh()
    {
        Mesh mesh = new Mesh { name = "BDT_LayoutBoxOpenBottomBox" };

        // 20 vertices (4 per rendered face, unique normals). The bottom face is
        // omitted so layout volumes remain visible without hiding surfaces below them.
        Vector3[] vertices =
        {
            // Front (Z+)
            new(-0.5f, -0.5f, 0.5f), new( 0.5f, -0.5f, 0.5f),
            new( 0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f),
            // Back (Z-)
            new( 0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, -0.5f),
            new(-0.5f, 0.5f, -0.5f), new( 0.5f, 0.5f, -0.5f),
            // Top (Y+)
            new(-0.5f, 0.5f, 0.5f), new( 0.5f, 0.5f, 0.5f),
            new( 0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f),
            // Right (X+)
            new( 0.5f, -0.5f, 0.5f), new( 0.5f, -0.5f, -0.5f),
            new( 0.5f, 0.5f, -0.5f), new( 0.5f, 0.5f, 0.5f),
            // Left (X-)
            new(-0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, 0.5f),
            new(-0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, -0.5f),
        };

        Vector3[] normals =
        {
            Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
            Vector3.back,    Vector3.back,    Vector3.back,    Vector3.back,
            Vector3.up,      Vector3.up,      Vector3.up,      Vector3.up,
            Vector3.right,   Vector3.right,   Vector3.right,   Vector3.right,
            Vector3.left,    Vector3.left,    Vector3.left,    Vector3.left,
        };

        int[] triangles =
        {
             0, 1, 2, 0, 2, 3, // Front
             4, 5, 6, 4, 6, 7, // Back
             8, 9,10, 8,10,11, // Top
            12,13,14, 12,14,15, // Right
            16,17,18, 16,18,19, // Left
        };

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void SetupTransparentMaterial(Material mat)
    {
        // Standard shader transparency setup.
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_Mode", 3f); // Transparent
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.enableInstancing = true;
    }
}
