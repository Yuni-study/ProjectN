using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrototypeCombatBootstrap
{
    private const string SceneName = "SampleScene";
    private const string MenuName = "Prototype Title Menu";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreatePrototypeIfNeeded()
    {
        if (SceneManager.GetActiveScene().name != SceneName || GameObject.Find(MenuName) != null
            || GameObject.Find("Prototype Combat") != null)
        {
            return;
        }

        new GameObject(MenuName).AddComponent<PrototypeTitleMenu>();
    }
}
