using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace REPOShopkeeperLock;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "gampa.repo.shopkeeperlock";
    public const string PluginName = "Shopkeeper Lock";
    public const string PluginVersion = "0.1.0";

    private Harmony? _harmony;

    private void Awake()
    {
        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Forcing shopkeeper enabled.");
        StartCoroutine(ForceShopkeeperLoop());
    }

    private IEnumerator ForceShopkeeperLoop()
    {
        var wait = new WaitForSeconds(1.0f);

        while (true)
        {
            try
            {
                ShopkeeperForcer.ForceAll(Logger.LogInfo, Logger.LogWarning);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Force pass failed: {ex}");
            }

            yield return wait;
        }
    }
}

internal static class ShopkeeperForcer
{
    private static readonly string[] TypeNeedles =
    {
        "shopkeeper",
        "shop keeper",
        "shopowner",
        "shop owner",
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
        "off"
    };

    public static void ForceAll(Action<string> info, Action<string> warn)
    {
        foreach (var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
        {
            if (behaviour == null)
                continue;

            var type = behaviour.GetType();
            var typeName = type.FullName ?? type.Name;

            if (!LooksShopkeeperRelated(typeName))
                continue;

            ForceMembers(behaviour, type, info, warn);

            if (!behaviour.gameObject.activeSelf)
            {
                behaviour.gameObject.SetActive(true);
                info($"Activated GameObject: {GetPath(behaviour.gameObject)} ({typeName})");
            }

            if (!behaviour.enabled)
            {
                behaviour.enabled = true;
                info($"Enabled Behaviour: {typeName}");
            }
        }
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

    private static bool LooksShopkeeperRelated(string typeName)
    {
        var lower = typeName.ToLowerInvariant();
        return TypeNeedles.Any(lower.Contains);
    }

    private static bool LooksRelevantMember(string memberName)
    {
        return EnableMemberNeedles.Any(memberName.Contains)
            || DisableMemberNeedles.Any(memberName.Contains);
    }

    private static bool ShouldBeTrue(string memberName)
    {
        // e.g. disabled/despawned/hidden flags should be false; enabled/spawned/active should be true.
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
