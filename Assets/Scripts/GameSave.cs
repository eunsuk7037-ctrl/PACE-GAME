using UnityEngine;

// All game progress (gold, weapons, stages, missions, achievements, level...) goes
// through here instead of PlayerPrefs directly, so each local account gets its own
// separate save space. Device-wide settings (language, the account list itself) stay
// on plain PlayerPrefs.
public static class GameSave
{
    private const string ActiveProfileKey = "Account_ActiveProfile";

    // "" is the guest profile; it uses the original unprefixed keys so progress saved
    // before accounts existed carries over as the guest's save.
    public static string ActiveProfile => PlayerPrefs.GetString(ActiveProfileKey, "");

    public static void SetActiveProfile(string profile)
    {
        PlayerPrefs.SetString(ActiveProfileKey, profile ?? "");
        PlayerPrefs.Save();
    }

    // A name for playing without an account. Plain device-local PlayerPrefs like the account
    // list, so it goes away with the app's data on uninstall (Android backup is off, see
    // DisableAndroidBackup).
    private const string GuestNameKey = "Guest_Name";

    public static string GuestName => PlayerPrefs.GetString(GuestNameKey, "");

    public static void SetGuestName(string name)
    {
        PlayerPrefs.SetString(GuestNameKey, (name ?? "").Trim());
        PlayerPrefs.Save();
    }

    // The name to show for the player: the logged-in account, else the guest name ("" if neither).
    public static string DisplayName => string.IsNullOrEmpty(ActiveProfile) ? GuestName : ActiveProfile;

    private static string Key(string key)
    {
        string profile = ActiveProfile;
        return string.IsNullOrEmpty(profile) ? key : "Profile_" + profile + "_" + key;
    }

    public static int GetInt(string key, int defaultValue = 0) => PlayerPrefs.GetInt(Key(key), defaultValue);
    public static void SetInt(string key, int value) => PlayerPrefs.SetInt(Key(key), value);
    public static string GetString(string key, string defaultValue = "") => PlayerPrefs.GetString(Key(key), defaultValue);
    public static void SetString(string key, string value) => PlayerPrefs.SetString(Key(key), value);
    public static void DeleteKey(string key) => PlayerPrefs.DeleteKey(Key(key));
    public static void Save() => PlayerPrefs.Save();

    public static void Increment(string key, int amount = 1)
    {
        SetInt(key, GetInt(key) + amount);
        Save();
    }
}
