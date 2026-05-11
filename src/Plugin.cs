using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace REPOShopkeeperLock;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "gampa.repo.shopkeeperlock";
    public const string PluginName = "Shopkeeper Lock";
    public const string PluginVersion = "0.2.0";

    private Harmony? _harmony;
    private ConfigEntry<float>? _scanIntervalSeconds;
    private ConfigEntry<bool>? _includeBroadShopFallback;
    private ConfigEntry<bool>? _logChanges;

    private void Awake()
    {
        _scanIntervalSeconds = Config.Bind(
            "General",
            "ScanIntervalSeconds",
            0.5f,
            "How often to re-force shopkeeper state. Lower is more aggressive; higher is cheaper.");

        _includeBroadShopFallback = Config.Bind(
            "General",
            "IncludeBroadShopFallback",
            false,
            "Also scan every MonoBehaviour with 'shop' in its type name. Disabled by default to avoid touching unrelated shop systems.");

        _logChanges = Config.Bind(
            "General",
            "LogChanges",
            true,
            "Log only when the mod actually changes an object/member state.");

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Forcing ShopKeeper objects enabled.");
        StartCoroutine(ForceShopkeeperLoop());
    }

    private IEnumerator ForceShopkeeperLoop()
    {
        while (true)
        {
            try
            {
                ShopkeeperForcer.ForceAll(
                    message =>
                    {
                        if (_logChanges?.Value ?? true)
                            Logger.LogInfo(message);
                    },
                    Logger.LogWarning,
                    _includeBroadShopFallback?.Value ?? false);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Force pass failed: {ex}");
            }

            var interval = Mathf.Max(0.1f, _scanIntervalSeconds?.Value ?? 0.5f);
            yield return new WaitForSeconds(interval);
        }
    }
}

internal static class ShopkeeperForcer
{
    // Verified from the inspected Assembly-CSharp.dll:
    // Assets/Models/Level/Shop/Shop Props/Shopkeeper/ShopKeeper.cs
    // Assets/Models/Level/Shop/Shop Props/Shopkeeper/shopkeeperDetectionSphere.cs
    private static readonly string[] ExactTypeNames =
    {
        "ShopKeeper",
        "shopkeeperDetectionSphere"
    };

    private static readonly string[] TargetTypeNeedles =
    {
        "shopkeeper",
        "shop keeper",
        "shopowner",
        "shop owner"
    };

    private static readonly string[] BroadFallbackTypeNeedles =
    {
        "shop"
    };

    private static readonly string[] EnableMemberNeedles =
    {
        "enabled",
        "enable",
        "active",
        "spawn",
        "spawned",
        "present",
        "shopkeeper",
        "shopowner"
    };

    private static readonly string[] DisableMemberNeedles =
    {
        "disabled",
        "disable",
        "despawn",
        "hide",
        "hidden",
        "off"
    };

    public static void ForceAll(Action<string> info, Action<string> warn, bool includeBroadShopFallback)
    {
        foreach (var behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            if (behaviour == null)
                continue;

            var type = behaviour.GetType();
            var typeName = type.FullName ?? type.Name;

            if (!LooksShopkeeperRelated(type, typeName, includeBroadShopFallback))
                continue;

            ForceObjectActive(behaviour, typeName, info);
            ForceBehaviourEnabled(behaviour, typeName, info);
            ForceMembers(behaviour, type, info, warn);
        }
    }

    private static void ForceObjectActive(MonoBehaviour behaviour, string typeName, Action<string> info)
    {
        var gameObject = behaviour.gameObject;
        if (gameObject == null || gameObject.activeSelf)
            return;

        gameObject.SetActive(true);
        info($"Activated GameObject: {GetPath(gameObject)} ({typeName})");
    }

    private static void ForceBehaviourEnabled(MonoBehaviour behaviour, string typeName, Action<string> info)
    {
        if (behaviour.enabled)
            return;

        behaviour.enabled = true;
        info($"Enabled Behaviour: {typeName}");
    }

    private static void ForceMembers(object instance, Type type, Action<string> info, Action<string> warn)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var field in type.GetFields(flags))
        {
            if (field.FieldType != typeof(bool) || field.IsInitOnly)
                continue;

            var name = field.Name.ToLowerInvariant();
            if (!LooksRelevantMember(name))
                continue;

            var target = ShouldBeTrue(name);
            var current = (bool)field.GetValue(instance);
            if (current == target)
                continue;

            field.SetValue(instance, target);
            info($"Forced {type.Name}.{field.Name} = {target}");
        }

        foreach (var prop in type.GetProperties(flags))
        {
            if (prop.PropertyType != typeof(bool) || !prop.CanWrite || prop.GetIndexParameters().Length != 0)
                continue;

            var name = prop.Name.ToLowerInvariant();
            if (!LooksRelevantMember(name))
                continue;

            var target = ShouldBeTrue(name);

            try
            {
                var current = prop.CanRead ? (bool)prop.GetValue(instance) : !target;
                if (current == target)
                    continue;

                prop.SetValue(instance, target);
                info($"Forced {type.Name}.{prop.Name} = {target}");
            }
            catch (Exception ex)
            {
                warn($"Could not force property {type.Name}.{prop.Name}: {ex.Message}");
            }
        }
    }

    private static bool LooksShopkeeperRelated(Type type, string typeName, bool includeBroadShopFallback)
    {
        if (ExactTypeNames.Any(exact => string.Equals(type.Name, exact, StringComparison.OrdinalIgnoreCase)))
            return true;

        var lower = typeName.ToLowerInvariant();
        if (TargetTypeNeedles.Any(lower.Contains))
            return true;

        return includeBroadShopFallback && BroadFallbackTypeNeedles.Any(lower.Contains);
    }

    private static bool LooksRelevantMember(string memberName)
    {
        return EnableMemberNeedles.Any(memberName.Contains)
            || DisableMemberNeedles.Any(memberName.Contains);
    }

    private static bool ShouldBeTrue(string memberName)
    {
        // e.g. disabled/despawned/hidden/off flags should be false; enabled/spawned/active should be true.
        return !DisableMemberNeedles.Any(memberName.Contains);
    }

    private static string GetPath(GameObject go)
    {
        var path = go.name;
        var current = go.transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
