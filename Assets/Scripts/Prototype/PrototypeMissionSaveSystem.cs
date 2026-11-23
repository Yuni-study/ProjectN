using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class PrototypeMissionSaveData
{
    public int version = 1;
    public List<string> activeTerminalIds = new List<string>();
    public string checkpointTerminalId = "ASHLINE";
    public int echoData;
    public bool missionCleared;
    public bool characterSwitchTutorialCompleted;
    public bool autoManualTutorialCompleted;
}

public static class PrototypeMissionSaveSystem
{
    private const int CurrentVersion = 1;
    private const string SaveFileName = "ashline_mission_save.json";

    private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public static bool HasSaveData()
    {
        return TryLoad(out _);
    }

    public static bool TryLoad(out PrototypeMissionSaveData data)
    {
        data = null;
        if (!File.Exists(SavePath)) return false;

        try
        {
            data = JsonUtility.FromJson<PrototypeMissionSaveData>(File.ReadAllText(SavePath));
            if (data == null || data.version != CurrentVersion
                || (data.checkpointTerminalId != "ASHLINE" && data.checkpointTerminalId != "RELAY"))
            {
                data = null;
                return false;
            }

            if (data.activeTerminalIds == null) data.activeTerminalIds = new List<string>();
            if (!data.activeTerminalIds.Contains("ASHLINE")) data.activeTerminalIds.Insert(0, "ASHLINE");
            if (data.checkpointTerminalId == "RELAY" && !data.activeTerminalIds.Contains("RELAY"))
            {
                data.activeTerminalIds.Add("RELAY");
            }

            return true;
        }
        catch (Exception)
        {
            data = null;
            return false;
        }
    }

    public static PrototypeMissionSaveData StartNewGame()
    {
        DeleteSave();
        PrototypeMissionSaveData data = new PrototypeMissionSaveData();
        data.activeTerminalIds.Add("ASHLINE");
        Save(data);
        return data;
    }

    public static void ActivateRelay(PrototypeMissionSaveData data)
    {
        if (!data.activeTerminalIds.Contains("RELAY")) data.activeTerminalIds.Add("RELAY");
        data.checkpointTerminalId = "RELAY";
        Save(data);
    }

    public static void MarkMissionCleared(PrototypeMissionSaveData data)
    {
        if (data.missionCleared) return;
        data.missionCleared = true;
        data.echoData += 10;
        Save(data);
    }

    public static void Save(PrototypeMissionSaveData data)
    {
        data.version = CurrentVersion;
        string directory = Path.GetDirectoryName(SavePath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
    }
}
