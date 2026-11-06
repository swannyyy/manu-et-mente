// SPDX-License-Identifier: MPL-2.0
using UnityEngine;

// Local-only player identity + ranking, persisted via PlayerPrefs. There is no server: the
// nickname is not checked for global uniqueness, it just labels this device's player. Points are
// only ever changed by online matches (see NetworkController.ReportResult) and start at 500.
public static class PlayerProfile
{
    private const string NicknameKey = "ProfileNickname";
    private const string PointsKey = "ProfilePoints";
    public const int StartingPoints = 500;

    public static bool HasProfile =>
        !string.IsNullOrEmpty(PlayerPrefs.GetString(NicknameKey, ""));

    public static string Nickname => PlayerPrefs.GetString(NicknameKey, "");

    public static int Points => PlayerPrefs.GetInt(PointsKey, StartingPoints);

    public static void Create(string nickname)
    {
        PlayerPrefs.SetString(NicknameKey, nickname.Trim());
        if (!PlayerPrefs.HasKey(PointsKey)) PlayerPrefs.SetInt(PointsKey, StartingPoints);
        PlayerPrefs.Save();
    }

    // Clamped at a 0 floor so a losing streak can't push a player negative.
    public static void AddPoints(int delta)
    {
        PlayerPrefs.SetInt(PointsKey, Mathf.Max(0, Points + delta));
        PlayerPrefs.Save();
    }
}
