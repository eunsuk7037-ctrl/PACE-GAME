using UnityEngine;
using TMPro;

// The name field on the level-select profile badge. Logged in it shows the account name
// (read-only); as a guest it's editable and saved as GameSave.GuestName, which the outro uses.
[RequireComponent(typeof(TMP_InputField))]
public class PlayerNameField : MonoBehaviour
{
    private TMP_InputField field;
    private bool Guest => string.IsNullOrEmpty(GameSave.ActiveProfile);

    private void Awake()
    {
        field = GetComponent<TMP_InputField>();
        field.onValueChanged.AddListener(OnNameChanged);
    }

    private void OnEnable()
    {
        LocalizationManager.LanguageChanged += OnLanguageChanged;
        field.readOnly = !Guest;
        field.SetTextWithoutNotify(GameSave.DisplayName);
        RefreshPlaceholder();
    }

    private void OnDisable() => LocalizationManager.LanguageChanged -= OnLanguageChanged;

    private void OnNameChanged(string name)
    {
        if (Guest) GameSave.SetGuestName(name);
    }

    private void OnLanguageChanged(int _) => RefreshPlaceholder();

    private void RefreshPlaceholder()
    {
        if (field.placeholder is TMP_Text placeholder)
            placeholder.text = LocalizationManager.Get("player_enter_name", "Enter your name");
    }
}
