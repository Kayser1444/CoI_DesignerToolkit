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
    }

    private Mesh m_cubeMesh = null!;
    private Material m_boxMaterial = null!;
    private Material m_topMaterial = null!;
    private readonly List<EntityCacheEntry> m_cache = new List<EntityCacheEntry>();
    private IEntitiesManager m_entitiesManager = null!;
    private bool m_isCacheDirty = true;
    private bool m_initFailed;

    public void Init(IEntitiesManager entitiesManager)
    {
        m_entitiesManager = entitiesManager;

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

        m_boxMaterial = new Material(shader);
        // Semi-transparent light blue side walls.
        m_boxMaterial.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        SetupTransparentMaterial(m_boxMaterial);

        m_topMaterial = new Material(shader);
        // More opaque vibrant amber/yellow roof caps.
        m_topMaterial.color = new Color(1f, 0.75f, 0.1f, 0.85f);
        SetupTransparentMaterial(m_topMaterial);

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

                bodyMatrices.Add(Matrix4x4.TRS(bodyPos, Quaternion.identity, bodyScale));
                topMatrices.Add(Matrix4x4.TRS(capPos, Quaternion.identity, capScale));
                totalBoxes++;
            }

            if (bodyMatrices.Count > 0)
            {
                Vector3 entityPos = new Vector3(
                    (transform.Position.X + 0.5f) * 2f,
                    transform.Position.Z * 2f,
                    (transform.Position.Y + 0.5f) * 2f);

                m_cache.Add(new EntityCacheEntry
                {
                    Position = entityPos,
                    BodyMatrices = bodyMatrices.ToArray(),
                    TopMatrices = topMatrices.ToArray()
                });
            }
        }

        Log.Info($"[LayoutBoxMode] Rebuilt cache: {totalBoxes} boxes from {m_cache.Count} " +
                 $"entities (total checked static entities: {entityCount}).");
        m_isCacheDirty = false;
    }

    private void Update()
    {
        if (m_initFailed) return;

        if (DesignerToolkitSettings.LayoutBoxModeToggleHotkey.IsPressed())
        {
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
            }
        }
    }

    /// <summary>
    /// Creates a unit cube mesh (vertices from -0.5 to +0.5) with proper
    /// normals. This avoids relying on CreatePrimitive whose shared mesh
    /// may become invalid after the temp GameObject is destroyed.
    /// </summary>
    private static Mesh BuildUnitCubeMesh()
    {
        Mesh mesh = new Mesh { name = "BDT_LayoutBoxCube" };

        // 24 vertices (4 per face, unique normals).
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
            // Bottom (Y-)
            new(-0.5f, -0.5f, -0.5f), new( 0.5f, -0.5f, -0.5f),
            new( 0.5f, -0.5f, 0.5f), new(-0.5f, -0.5f, 0.5f),
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
            Vector3.down,    Vector3.down,    Vector3.down,    Vector3.down,
            Vector3.right,   Vector3.right,   Vector3.right,   Vector3.right,
            Vector3.left,    Vector3.left,    Vector3.left,    Vector3.left,
        };

        int[] triangles =
        {
             0, 2, 1, 0, 3, 2, // Front
             4, 6, 5, 4, 7, 6, // Back
             8,10, 9, 8,11,10, // Top
            12,14,13, 12,15,14, // Bottom
            16,18,17, 16,19,18, // Right
            20,22,21, 20,23,22, // Left
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
