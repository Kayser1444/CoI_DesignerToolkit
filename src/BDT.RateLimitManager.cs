// CoI Designer Toolkit
// Copyright (c) 2026 Kayser1444
// Licensed under the MIT License.
using System;
using System.IO;
using Mafi;
using Mafi.Collections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Serialization;
using CoI.AutoHelpers.Logging;
using CoI.AutoHelpers.Persistence;

namespace CoIDesignerToolkit;

public static class RateLimitManager
{
    private const int SCHEMA_VERSION = 1;
    public const string CONFIG_KEY = "bdtRateLimitsStateJson";
    
    private static readonly ModLogger s_log = new ModLogger("BDT.RateLimitManager");
    private static IModStateJsonStore? s_store;
    private static readonly Dict<int, int> s_entityLimits = new Dict<int, int>();
    private static readonly Dict<int, float> s_tokens = new Dict<int, float>();
    private static readonly Dict<int, long> s_lastUpdateTicks = new Dict<int, long>();

    public static void Initialize(IModStateJsonStore store)
    {
        s_store = store;
        LoadFromStore();
    }

    public static void Clear()
    {
        s_entityLimits.Clear();
        s_tokens.Clear();
        s_lastUpdateTicks.Clear();
        s_store = null;
    }

    public static int? GetLimit(EntityId entityId)
    {
        if (s_entityLimits.TryGetValue(entityId.Value, out int limit))
        {
            return limit;
        }
        return null;
    }

    public static void SetLimit(EntityId entityId, int limit)
    {
        if (limit <= 0)
        {
            RemoveLimit(entityId);
            return;
        }

        s_entityLimits[entityId.Value] = limit;
        SaveToStore();
    }

    public static void RemoveLimit(EntityId entityId)
    {
        if (s_entityLimits.Remove(entityId.Value))
        {
            SaveToStore();
        }
    }

    public static void OnEntityRemoved(IEntity entity)
    {
        RemoveLimit(entity.Id);
    }

    // To avoid sleep issues, we generate tokens on-demand during consumption based on elapsed ticks.
    public static int ConsumeTokens(IEntity entity, int requested)
    {
        int limit = 0;
        if (!s_entityLimits.TryGetValue(entity.Id.Value, out limit)) return requested;

        long currentTick = entity.Context.Calendar.RealTime.Ticks;
        float tokens = 0f;

        if (!s_tokens.TryGetValue(entity.Id.Value, out tokens))
        {
            tokens = limit; // Start fully charged
        }

        long lastTick = 0;
        if (!s_lastUpdateTicks.TryGetValue(entity.Id.Value, out lastTick))
        {
            lastTick = currentTick;
        }

        long ticksElapsed = currentTick - lastTick;
        if (ticksElapsed > 0)
        {
            float tokensPerTick = limit / 600f;
            tokens += ticksElapsed * tokensPerTick;
            if (tokens > limit) tokens = limit;
        }

        int canTake = (int)Math.Floor(tokens);
        if (canTake > requested) canTake = requested;
        
        s_tokens[entity.Id.Value] = tokens - canTake;
        s_lastUpdateTicks[entity.Id.Value] = currentTick;
        
        return canTake;
    }

    private static void LoadFromStore()
    {
        s_entityLimits.Clear();
        if (s_store == null) return;

        string json = s_store.LoadJson();
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            object parsed = new JsonParser().Parse(new StringReader(json));
            if (!(parsed is Dict<string, object> root)) return;

            if (!root.TryGetValue("schemaVersion", out object? rawSchema))
                return;

            int schemaVersion = 0;
            if (rawSchema is int sInt) schemaVersion = sInt;
            else if (rawSchema is double sDouble) schemaVersion = (int)sDouble;
            else if (rawSchema is long sLong) schemaVersion = (int)sLong;

            if (schemaVersion != SCHEMA_VERSION)
                return;

            if (root.TryGetValue("limits", out object? rawLimits) && rawLimits is Dict<string, object> limits)
            {
                foreach (var kvp in limits)
                {
                    if (int.TryParse(kvp.Key, out int entityIdValue))
                    {
                        if (kvp.Value is int limitInt)
                        {
                            s_entityLimits[entityIdValue] = limitInt;
                        }
                        else if (kvp.Value is double limitDouble)
                        {
                            s_entityLimits[entityIdValue] = (int)limitDouble;
                        }
                        else if (kvp.Value is long limitLong)
                        {
                            s_entityLimits[entityIdValue] = (int)limitLong;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Failed to load rate limits state: {ex.Message}");
        }
    }

    private static void SaveToStore()
    {
        if (s_store == null) return;

        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"schemaVersion\": {SCHEMA_VERSION},");
            sb.AppendLine("  \"limits\": {");
            bool first = true;
            foreach (var kvp in s_entityLimits)
            {
                if (!first) sb.AppendLine(",");
                sb.Append($"    \"{kvp.Key}\": {kvp.Value}");
                first = false;
            }
            sb.AppendLine();
            sb.AppendLine("  }");
            sb.AppendLine("}");

            string json = sb.ToString();
            ModStateJsonSaveResult result = s_store.SaveJson(json);
            if (!result.Succeeded)
            {
                s_log.Warning($"Failed to save rate limits state: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            s_log.Warning($"Exception saving rate limits state: {ex.Message}");
        }
    }
}
