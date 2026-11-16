using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tools > Items > Set Up Held Items
// Adds HeldItemThirdPerson next to the Hotbar on the player prefab, so other players see the
// item you have out in your model's right hand. Uses the same models as the first-person items
// (binoculars, map, anemometer) plus the Roman candle pickup for the firework.
// Safe to re-run: items already in the list keep their tuning.
public static class HeldItemSetup
{
    const string PlayerPrefabPath = "Assets/Prefabs/MainPlayer Variant.prefab";
    const string FireworkModelPath = "Assets/Prefabs/Items/RomanCandle.prefab";

    [MenuItem("Tools/Items/Set Up Held Items")]
    public static void Run()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var hotbar = root.GetComponentInChildren<Hotbar>(true);
            if (hotbar == null)
            {
                Debug.LogError($"[HeldItemSetup] No Hotbar in {PlayerPrefabPath}.");
                return;
            }

            var held = hotbar.GetComponent<HeldItemThirdPerson>();
            if (held == null) held = hotbar.gameObject.AddComponent<HeldItemThirdPerson>();

            var list = new List<HeldItemThirdPerson.HeldItem>(held.items ?? new HeldItemThirdPerson.HeldItem[0]);
            list.RemoveAll(i => i == null);

            AddFromItem(list, ItemType.Binoculars, hotbar.GetComponent<Binoculars>(), new Vector3(0f, 0f, 0.05f), Vector3.zero);
            AddFromItem(list, ItemType.Map, hotbar.GetComponent<MapItem>(), new Vector3(0f, 0f, 0.05f), new Vector3(-65f, 0f, 0f));
            AddFromItem(list, ItemType.Anemometer, hotbar.GetComponent<AnemometerItem>(), new Vector3(0f, 0f, 0.05f), Vector3.zero);

            var firework = AssetDatabase.LoadAssetAtPath<GameObject>(FireworkModelPath);
            if (firework == null) Debug.LogWarning($"[HeldItemSetup] {FireworkModelPath} not found, firework not added.");
            else Add(list, ItemType.Firework, firework, new Vector3(0f, 0f, 0.05f), Vector3.zero, 1f);

            held.items = list.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log($"[HeldItemSetup] Done: {held.items.Length} held items on {PlayerPrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // heldModel and holdScale are private on the first-person items, so read them through SerializedObject
    static void AddFromItem(List<HeldItemThirdPerson.HeldItem> list, ItemType type, Component item, Vector3 offset, Vector3 euler)
    {
        if (item == null)
        {
            Debug.LogWarning($"[HeldItemSetup] No {type} component next to the Hotbar, skipped.");
            return;
        }

        var so = new SerializedObject(item);
        var model = so.FindProperty("heldModel")?.objectReferenceValue as GameObject;
        float scale = so.FindProperty("holdScale")?.floatValue ?? 1f;
        if (model == null)
        {
            Debug.LogWarning($"[HeldItemSetup] {type} has no heldModel, skipped.");
            return;
        }
        Add(list, type, model, offset, euler, scale);
    }

    static void Add(List<HeldItemThirdPerson.HeldItem> list, ItemType type, GameObject model, Vector3 offset, Vector3 euler, float scale)
    {
        if (list.Exists(i => i.type == type)) return;
        list.Add(new HeldItemThirdPerson.HeldItem { type = type, model = model, offset = offset, euler = euler, scale = scale });
    }
}
