using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Local accounts: every account signed up on this device gets its own separate save
// space (see GameSave), so several players can share a device without mixing progress.
// Not playing while logged in uses the guest save. There is no server - nothing leaves
// the device, and the password hash is only there to avoid storing it as plain text.
public class AccountManager : MonoBehaviour
{
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public TMP_Text statusText;
    public TMP_Text loggedInText;
    public GameObject loggedOutGroup;
    public GameObject loggedInGroup;

    [Header("Sign-up panel")]
    public GameObject signUpGroup;
    public TMP_InputField signUpUsernameInput;
    public TMP_InputField signUpPasswordInput;
    public TMP_InputField signUpConfirmInput;
    public TMP_Text signUpStatusText;

    private const string AccountListKey = "Account_List";
    private const string PasswordHashKeyPrefix = "Account_Hash_";
    private const int MaxUsernameLength = 16;

    // Single-account keys from before multi-account support.
    private const string LegacyUsernameKey = "Account_Username";
    private const string LegacyPasswordHashKey = "Account_PasswordHash";

    private void Awake() => MigrateLegacyAccount();
    private void OnEnable() => RefreshUI();

    private static string Hash(string input)
    {
        using (var sha = SHA256.Create())
        {
            var sb = new StringBuilder();
            foreach (var b in sha.ComputeHash(Encoding.UTF8.GetBytes(input))) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    private static List<string> GetAccounts()
    {
        string list = PlayerPrefs.GetString(AccountListKey, "");
        return string.IsNullOrEmpty(list) ? new List<string>() : new List<string>(list.Split('|'));
    }

    private static void AddAccount(string username, string passwordHash)
    {
        var accounts = GetAccounts();
        accounts.Add(username);
        PlayerPrefs.SetString(AccountListKey, string.Join("|", accounts));
        PlayerPrefs.SetString(PasswordHashKeyPrefix + username, passwordHash);
        PlayerPrefs.Save();
    }

    // The old single account keeps playing on the guest save it was already using.
    private static void MigrateLegacyAccount()
    {
        string legacyUser = PlayerPrefs.GetString(LegacyUsernameKey, "");
        if (string.IsNullOrEmpty(legacyUser)) return;

        if (!GetAccounts().Contains(legacyUser)) AddAccount(legacyUser, PlayerPrefs.GetString(LegacyPasswordHashKey, ""));
        PlayerPrefs.DeleteKey(LegacyUsernameKey);
        PlayerPrefs.DeleteKey(LegacyPasswordHashKey);
        PlayerPrefs.DeleteKey("Account_LoggedIn");
        PlayerPrefs.Save();
    }

    // The log-in view's SIGN UP button: swap to the separate sign-up panel.
    public void OnOpenSignUpPressed() => ShowSignUp(true);
    public void OnSignUpBackPressed() => ShowSignUp(false);

    private void ShowSignUp(bool show)
    {
        if (loggedOutGroup != null) loggedOutGroup.SetActive(!show);
        if (signUpGroup != null) signUpGroup.SetActive(show);
        SetStatus(null, "");
        if (signUpUsernameInput != null) signUpUsernameInput.text = show && usernameInput != null ? usernameInput.text.Trim() : "";
        if (signUpPasswordInput != null) signUpPasswordInput.text = "";
        if (signUpConfirmInput != null) signUpConfirmInput.text = "";
    }

    public void OnSignUpPressed()
    {
        string username = signUpUsernameInput.text.Trim();
        string password = signUpPasswordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            SetStatus("account_enter_both", "Enter a username and password.");
            return;
        }
        if (password != signUpConfirmInput.text)
        {
            SetStatus("account_password_mismatch", "The passwords don't match.");
            return;
        }
        if (username.Length > MaxUsernameLength || username.Contains("|"))
        {
            SetStatus("account_invalid_name", "Use up to 16 characters, without '|'.");
            return;
        }
        if (GetAccounts().Contains(username))
        {
            SetStatus("account_exists", "That username is already taken.");
            return;
        }

        AddAccount(username, Hash(password));
        SwitchProfile(username); // a new account starts with its own fresh save
    }

    public void OnLoginPressed()
    {
        string username = usernameInput.text.Trim();
        if (!GetAccounts().Contains(username))
        {
            SetStatus("account_not_found", "No account with that username on this device.");
            return;
        }
        if (Hash(passwordInput.text) != PlayerPrefs.GetString(PasswordHashKeyPrefix + username, ""))
        {
            SetStatus("account_wrong_password", "Incorrect username or password.");
            return;
        }

        SwitchProfile(username);
    }

    public void OnLogoutPressed() => SwitchProfile("");

    // Every manager reads its save on load, so reloading the scene is the simplest way to
    // make the whole game pick up the other account's progress.
    private static void SwitchProfile(string profile)
    {
        GameSave.SetActiveProfile(profile);
        Time.timeScale = 1f;
        ScreenNavigator.OpenLevelSelectOnLoad = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Each view has its own status line; only the visible one matters.
    private void SetStatus(string key, string fallback)
    {
        string text = string.IsNullOrEmpty(key) ? "" : LocalizationManager.Get(key, fallback);
        if (statusText != null) statusText.text = text;
        if (signUpStatusText != null) signUpStatusText.text = text;
    }

    private void RefreshUI()
    {
        string profile = GameSave.ActiveProfile;
        bool loggedIn = !string.IsNullOrEmpty(profile);

        if (loggedOutGroup != null) loggedOutGroup.SetActive(!loggedIn);
        if (loggedInGroup != null) loggedInGroup.SetActive(loggedIn);
        if (signUpGroup != null) signUpGroup.SetActive(false);        if (loggedIn && loggedInText != null) loggedInText.text = string.Format(LocalizationManager.Get("account_logged_in", "Logged in as {0}"), profile);

        SetStatus(null, "");
        if (usernameInput != null) usernameInput.text = "";
        if (passwordInput != null) passwordInput.text = "";
    }
}
