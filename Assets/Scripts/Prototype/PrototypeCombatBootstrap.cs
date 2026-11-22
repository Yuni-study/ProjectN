using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrototypeCombatBootstrap
{
    private const string SceneName = "SampleScene";
    private const string RootName = "Prototype Combat";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreatePrototypeIfNeeded()
    {
        if (SceneManager.GetActiveScene().name != SceneName || GameObject.Find(RootName) != null)
        {
            return;
        }

        new GameObject(RootName).AddComponent<PrototypeCombatScene>();
    }
}
