using HarmonyLib;
using Kingmaker.Blueprints.JsonSystem;
using UnityModManagerNet;

namespace EmeraldWitch
{
    public static class Main
    {
        public static UnityModManager.ModEntry ModEntry;
        public static Harmony HarmonyInstance;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            ModEntry = modEntry;
            HarmonyInstance = new Harmony(modEntry.Info.Id);
            modEntry.OnGUI = OnGUI;

            try
            {
                HarmonyInstance.PatchAll();
                Log("Emerald Witch loaded.");
            }
            catch (System.Exception ex)
            {
                Log("Harmony patch failed: " + ex);
                return false;
            }

            return true;
        }

        public static void Log(string message)
        {
            ModEntry?.Logger.Log(message);
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            UnityEngine.GUILayout.Label("Adds the Emerald Witch archetype. She is a spontaneous witch with the Death patron already chosen.");
            UnityEngine.GUILayout.Label("Create a new Witch and choose Emerald Witch. There is no patron or oracle curse to pick.");
        }
    }

    [HarmonyPatch(typeof(BlueprintsCache), nameof(BlueprintsCache.Init))]
    static class BlueprintsReadyPatch
    {
        static void Postfix()
        {
            Content.Create();
        }
    }
}
