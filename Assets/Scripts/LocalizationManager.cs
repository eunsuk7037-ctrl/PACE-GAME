using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LocalizationManager : MonoBehaviour
{
    // Fires whenever the active language changes, so UI that builds its own text
    // on demand (rather than being auto-registered above) can refresh itself.
    public static event Action<int> LanguageChanged;

    // Language order matches the Settings dropdown: 0=English 1=Korean
    private static readonly string[] NativeNames =
    {
        "English", "한국어"
    };

    private static readonly Dictionary<string, string[]> Translations = new Dictionary<string, string[]>
    {
        { "space_game",   new[] { "SPACE GAME", "스페이스 게임" } },
        { "level_select", new[] { "LEVEL SELECT", "레벨 선택" } },
        { "mercury",      new[] { "MERCURY", "수성" } },
        { "locked",       new[] { "LOCKED", "잠김" } },
        { "gold",         new[] { "GOLD", "골드" } },
        { "gem",          new[] { "GEM", "젬" } },
        { "item",         new[] { "ITEM", "아이템" } },
        { "shop",         new[] { "SHOP", "상점" } },
        { "shop_title",   new[] { "SHOP\n", "상점\n" } },   // trailing newline is part of the title's layout
        { "mission",      new[] { "MISSION", "미션" } },
        { "achievements", new[] { "ACHIEVEMENTS", "도전 과제" } },
        { "inventory",    new[] { "INVENTORY", "인벤토리" } },
        { "story_raygun", new[]
            {
                "Long ago, a space outlaw envied the guns carried by the old space police.\n\nHe modified one himself and became the first to use it - and that gun has been passed down ever since.",
                "옛날 우주 경찰들이 쓰던 총이 부러웠던 우주의 무법자가 있었다.\n\n그는 그 총을 직접 개조해 처음으로 사용했고, 그 총은 지금까지 대대로 이어져 내려오고 있다."
            } },
        { "story_sword",  new[]
            {
                "An alien once heard the tales of a human hero and fell completely under their spell.\n\nHe spread the way of the sword throughout space society.",
                "어느 한 외계인이 인간 용사의 이야기를 듣고 푹 빠지게 되었다.\n\n그는 검의 길을 우주 사회에도 널리 퍼트렸다."
            } },
        { "story_satellite", new[]
            {
                "A commander who loved war wanted to wipe out his enemies more efficiently.\n\nAfter long deliberation with a scientist, he created this weapon.",
                "전쟁을 좋아하던 한 사령관이 적을 더욱 효율적으로 섬멸하고 싶어 했다.\n\n그는 과학자와 오랜 고심 끝에 이 무기를 만들어냈다."
            } },
        { "story_drill",  new[]
            {
                "A space miner digging for ore accidentally pressed the launch button.\n\nHe found it so fun that he began firing the drill as a weapon, and it became a craze among miners stuck in dull routines.",
                "한 우주 광부가 광석을 캐다가 실수로 발사 버튼을 눌렀다.\n\n그 재미에 빠져 드릴을 날리는 무기로 쓰기 시작했고, 지루한 일상을 보내던 광부들 사이에서 유행이 되었다."
            } },
        { "story_mercury", new[]
            {
                "Mercury, the planet closest to the Sun, is always scorching hot.\n\nFed up with the heat, its people secretly planned to move to cool, comfortable Earth.",
                "태양과 가장 가까워 늘 푹푹 찌는 별, 수성.\n\n더위를 참다못한 주민들은 서늘한 지구로 몰래 이사 갈 계획을 세웠다."
            } },
        { "story_venus",  new[]
            {
                "Venus, wrapped in thick clouds that no one can see through.\n\nInside, the aliens were quietly plotting their conquest of Earth.",
                "짙은 구름에 덮여 속을 아무도 볼 수 없는 별, 금성.\n\n그 안에서 외계인들은 지구 정복 작전을 조용히 짜고 있었다."
            } },
        { "story_mars",   new[]
            {
                "Mars, the red planet that once overflowed with water.\n\nTired of the dry land, the aliens set their eyes on Earth's blue oceans.",
                "한때 물이 넘쳤다던 붉은 별, 화성.\n\n메마른 땅이 지겨워진 외계인들이 지구의 푸른 바다를 노리기 시작했다."
            } },
        { "story_jupiter", new[]
            {
                "Jupiter, the largest planet in the solar system.\n\nDeep within its giant storms hide the alien alliance's headquarters and fleet.",
                "태양계에서 가장 큰 별, 목성.\n\n거대한 폭풍 속에는 외계 연합의 본부와 함대가 숨어 있다."
            } },
        { "story_saturn", new[]
            {
                "Saturn, famous for its beautiful rings.\n\nBut those rings are really the space junk and worn-out ship wrecks the aliens dumped.",
                "아름다운 고리로 유명한 별, 토성.\n\n하지만 그 고리의 정체는 외계인들이 내다 버린 우주 쓰레기와 낡은 함선 잔해다."
            } },
        { "story_uranus", new[]
            {
                "Uranus, the planet that spins on its side.\n\nIts people are all contrarian oddballs who never follow orders.",
                "옆으로 누워서 도는 별, 천왕성.\n\n이곳 주민들은 하나같이 비딱한 괴짜들이라 명령을 듣는 법이 없다."
            } },
        { "story_neptune", new[]
            {
                "Neptune, a planet that ripples like a deep blue sea.\n\nHere the aliens built a fake city that mimics Earth.",
                "푸른 바다처럼 일렁이는 별, 해왕성.\n\n외계인들은 이곳에 지구를 흉내 낸 가짜 도시를 만들어 놓았다."
            } },
        { "story_pluto",  new[]
            {
                "Pluto, a world that still grieves being kicked out of the planet club.\n\nThat resentment united the aliens as one.",
                "행성 자리에서 쫓겨난 서러움을 품은 별, 명왕성.\n\n그 한이 외계인들을 하나로 똘똘 뭉치게 만들었다."
            } },
        { "story_sun",    new[]
            {
                "The Sun, where the leader of all the aliens hides.\n\nHe could not stand that Earth was so peaceful.",
                "태양, 모든 외계인의 우두머리가 숨어 있는 곳.\n\n그는 지구가 평화롭다는 사실이 못마땅했다."
            } },
        { "story_earth",  new[]
            {
                "Earth, the peaceful blue planet.\n\nBut aliens have quietly slipped in, and Earth is slowly falling into danger.",
                "평화로운 푸른 별, 지구.\n\n하지만 어느새 외계인들이 몰래 숨어들었고, 지구는 조금씩 위험에 빠지고 있다."
            } },

        // Achievement stories
        { "story_achv_defeat_monsters", new[]
            {
                "Page one of the Earth Defense Force rookie handbook.\n\n\"Don't be scared. They're scared of us too.\"",
                "지구 방위대 신병 수첩 첫 장.\n\n\"겁먹지 말 것. 저들도 우리가 무섭다.\""
            } },
        { "story_achv_defeat_boss", new[]
            {
                "The aliens gave a name to whoever brought down their boss.\n\nIn their tongue it means 'never run into this one'.",
                "외계인들은 보스를 쓰러뜨린 자에게 이름을 붙였다.\n\n그들 말로 '절대 마주치지 말 것'."
            } },
        { "story_achv_equip_weapon", new[]
            {
                "The days of holding the line bare-handed are over.\n\nFor the first time, the weapon in your hand feels reassuring.",
                "맨손으로 버티던 날들은 끝났다.\n\n손에 쥔 무기가 처음으로 든든하게 느껴졌다."
            } },
        { "story_achv_clear_stage", new[]
            {
                "It was a small victory,\n\nbut for the first time, tension filled the alien alliance's war room.",
                "작은 승리였지만,\n\n외계 연합의 작전 회의실에 처음으로 긴장감이 돌았다."
            } },
        { "story_achv_stage_3", new[]
            {
                "Alarms don't startle you anymore.\n\nYou can finish your coffee and still make it in time.",
                "이제 경보가 울려도 놀라지 않는다.\n\n커피를 다 마시고 출동해도 늦지 않으니까."
            } },
        { "story_achv_stage_5", new[]
            {
                "Your face made it onto the alien alliance's wanted poster.\n\nYour only complaint: the photo doesn't do you justice.",
                "외계 연합 수배 전단에 당신 얼굴이 실렸다.\n\n사진이 실물보다 못 나온 게 유일한 불만이다."
            } },
        { "story_achv_stage_8", new[]
            {
                "Word has spread beyond Pluto.\n\n\"There's one on Earth you do NOT mess with.\"",
                "명왕성 너머까지 소문이 퍼졌다.\n\n\"지구에 건드리면 안 되는 녀석이 있다.\""
            } },
        { "story_achv_reach_level", new[]
            {
                "Your old boot-camp buddies came by for an autograph.\n\nYou still haven't practiced your signature.",
                "훈련병 시절 동기들이 사인을 받으러 왔다.\n\n아직 사인 연습은 못 했는데."
            } },
        { "story_achv_gold_500", new[]
            {
                "Gold that feels like your first paycheck.\n\nJust thinking about how to spend it is fun.",
                "첫 월급 같은 골드.\n\n어디에 쓸지 고민하는 것만으로도 즐겁다."
            } },
        { "story_achv_gold_5000", new[]
            {
                "For the first time, the shopkeeper greeted you first.\n\nMoney is a mighty thing.",
                "상점 주인이 처음으로 당신에게 먼저 인사했다.\n\n돈의 힘은 위대하다."
            } },
        { "story_achv_gold_25000", new[]
            {
                "You need a safe now.\n\nScarier than aliens are pickpockets.",
                "금고가 필요해졌다.\n\n외계인보다 무서운 건 소매치기다."
            } },
        { "story_achv_gold_100000", new[]
            {
                "Space merchants have started calling you a regular.\n\nStill no discount, though.",
                "우주 상인들이 당신을 '단골'이라 부르기 시작했다.\n\n할인은 여전히 없지만."
            } },
        { "story_achv_gold_500000", new[]
            {
                "With this much, you could buy a whole planet.\n\nBut Earth alone is enough for you.",
                "이 정도면 행성 하나를 살 수도 있다.\n\n하지만 당신에게는 지구 하나로 충분하다."
            } },
        { "story_achv_ufo_50", new[]
            {
                "You built a small house out of fallen UFO wreckage.\n\nThe neighbors still don't know what it is.",
                "떨어진 UFO 잔해로 작은 집 한 채를 지었다.\n\n이웃들은 아직 그게 무엇인지 모른다."
            } },
        { "story_achv_ufo_300", new[]
            {
                "The alien alliance built one more UFO factory.\n\nAll because of you.",
                "외계 연합이 UFO 생산 공장을 하나 더 지었다.\n\n전부 당신 때문이다."
            } },
        { "story_achv_monsters_1000", new[]
            {
                "When the thousandth invader fell, alarms rang through alien alliance HQ.\n\nThat night, a warrant bearing your name went out across the whole solar system.",
                "천 번째 침입자가 쓰러졌을 때, 외계 연합 본부에 경보가 울렸다.\n\n그날 밤, 태양계 전역에 당신의 이름이 적힌 수배령이 내려졌다."
            } },
        { "story_achv_weapon_maxed_1", new[]
            {
                "Every time you polish it, the weapon glows faintly.\n\nThe more worn it gets, the stronger it feels.",
                "무기를 닦을 때마다 은은하게 빛이 난다.\n\n손때가 묻을수록 더 강해지는 기분이다."
            } },
        { "story_achv_weapon_maxed_4", new[]
            {
                "Gun, sword, satellite, drill. All in peak condition.\n\nNow all that's left is something to shoot at.",
                "총, 검, 위성, 드릴. 모두 최고의 상태다.\n\n이제 남은 건 쏠 곳뿐이다."
            } },
        { "story_achv_no_hit", new[]
            {
                "The battle ended without a speck of dust on your clothes.\n\nThat night, the aliens redrew their battle plans from scratch.",
                "전투가 끝났는데 옷에 먼지 하나 묻지 않았다.\n\n외계인들은 그날 밤 작전을 처음부터 다시 짰다."
            } },
        { "story_achv_heal_10", new[]
            {
                "Mushrooms, ferns, energy vials. Your bag has become a pharmacy.\n\nEven though you know the best healing is not getting hurt.",
                "버섯, 고사리, 에너지 병. 가방 속이 약국이 되었다.\n\n아프지 않는 게 최고의 회복이라는 건 알지만."
            } },
        { "story_achv_level_20", new[]
            {
                "Your star is easy to find in the night sky now.\n\nIt's the one shining brightest.",
                "이제 밤하늘에서 당신의 별을 찾기 쉬워졌다.\n\n가장 밝게 빛나는 게 바로 그것이니까."
            } },
        { "story_achv_game_complete", new[]
            {
                "The last invader vanished into the Sun.\n\nFrom tonight on, Earth's nights are quiet again.",
                "마지막 침략자가 태양 속으로 사라졌다.\n\n오늘부터 지구의 밤은 다시 조용하다."
            } },

        // Stage flow
        { "stage_label",  new[] { "STAGE {0}", "{0}단계" } },
        { "stage_clear",  new[] { "STAGE CLEAR", "스테이지 클리어" } },
        { "game_over",    new[] { "GAME OVER", "게임 오버" } },
        { "boss_phase",   new[] { "PHASE {0}", "{0}페이즈" } },
        { "level_up",     new[] { "LEVEL UP! Lv.{0} (Max HP +{1})", "레벨 업! Lv.{0} (최대 체력 +{1})" } },

        // Account
        { "account_enter_both",     new[] { "Enter a username and password.", "아이디와 비밀번호를 입력하세요." } },
        { "account_invalid_name",   new[] { "Use up to 16 characters, without '|'.", "아이디는 16자 이내, '|' 없이 입력하세요." } },
        { "account_exists",         new[] { "That username is already taken.", "이미 있는 아이디입니다." } },
        { "account_not_found",      new[] { "No account with that username on this device.", "이 기기에 없는 아이디입니다." } },
        { "account_wrong_password", new[] { "Incorrect username or password.", "아이디 또는 비밀번호가 틀렸습니다." } },
        { "account_logged_in",      new[] { "Logged in as {0}", "{0} 님으로 로그인 중" } },
        { "account_title",          new[] { "ACCOUNT", "계정" } },
        { "account_username",       new[] { "Username", "아이디" } },
        { "account_password",       new[] { "Password", "비밀번호" } },
        { "account_log_in",         new[] { "LOG IN", "로그인" } },
        { "account_sign_up",        new[] { "SIGN UP", "회원가입" } },
        { "account_log_out",        new[] { "LOG OUT", "로그아웃" } },
        { "account_enter_username", new[] { "Enter username", "아이디를 입력하세요" } },
        { "account_enter_password", new[] { "Enter password", "비밀번호를 입력하세요" } },
        { "account_password_confirm", new[] { "Confirm", "비밀번호 확인" } },
        { "account_enter_password_confirm", new[] { "Enter password again", "비밀번호를 한 번 더 입력하세요" } },
        { "account_password_mismatch", new[] { "The passwords don't match.", "비밀번호가 서로 다릅니다." } },
        { "account_back",             new[] { "BACK", "뒤로" } },
        { "player_enter_name",        new[] { "Enter your name", "이름을 입력하세요" } },

        // Ending credits
        { "intro_placeholder",  new[] { "INTRO\n(coming soon)", "인트로\n(추후 삽입 예정)" } },
        { "credits_developer",  new[] { "Developer", "개발자" } },
        { "credits_producer",   new[] { "Producer", "제작자" } },
        { "credits_days",       new[] { "Production Time", "제작 기간" } },
        { "credits_days_value", new[] { "{0} days", "{0}일" } },
        { "credits_thanks",     new[] { "Thank you for playing!", "플레이해 주셔서 감사합니다!" } },

        { "settings",     new[] { "SETTINGS", "설정" } },
        { "equip",        new[] { "EQUIP", "장착" } },
        { "unequip",      new[] { "UNEQUIP", "해제" } },
        { "upgrade",      new[] { "UPGRADE", "강화" } },
        { "sound",        new[] { "Sound", "사운드" } },
        { "music",        new[] { "Music", "음악" } },
        { "language",     new[] { "Language", "언어" } },
        { "intro",        new[] { "Intro", "인트로" } },
        { "on",           new[] { "ON", "켜짐" } },
        { "off",          new[] { "OFF", "꺼짐" } },
        { "empty",        new[] { "Empty", "비어있음" } },
        { "empty_desc",   new[] { "You haven't found anything here yet.", "아직 아무것도 발견하지 못했어요." } },
        { "claim",        new[] { "CLAIM", "받기" } },
        { "done",         new[] { "DONE", "완료" } },
        { "mission_defeat_monsters", new[] { "Defeat 10 Monsters", "몬스터 10마리 잡기" } },
        { "mission_clear_stage",     new[] { "Clear 1 Stage", "스테이지 1개 클리어" } },
        { "mission_earn_gold",       new[] { "Reach 500 Gold", "골드 500 모으기" } },
        { "mission_equip_weapon",    new[] { "Equip a Weapon", "무기 장착하기" } },
        { "mission_defeat_boss",     new[] { "Defeat the Boss", "보스 처치" } },
        { "mission_defeat_ufos",     new[] { "Defeat 15 UFOs", "UFO 15마리 처치" } },
        { "mission_use_heal_item",   new[] { "Use a Heal Item", "회복 아이템 사용 1회" } },
        { "mission_attack_50",       new[] { "Attack 50 Times", "공격 50번" } },
        { "achv_locked_desc",        new[] { "Unlock by completing the challenge.", "도전 과제를 완료하면 해금됩니다." } },
        { "achv_clear_stage",        new[] { "First Victory", "첫 승리" } },
        { "achv_clear_stage_desc",   new[] { "Clear your first stage.", "첫 스테이지 클리어" } },
        { "achv_defeat_monsters",      new[] { "Monster\nHunter", "몬스터\n사냥꾼" } },
        { "achv_defeat_monsters_desc", new[] { "Defeat 10 monsters.", "몬스터 10마리 처치" } },
        { "achv_equip_weapon",       new[] { "Armed", "무장 완료" } },
        { "achv_equip_weapon_desc",  new[] { "Equip a weapon.", "무기 장착하기" } },
        { "achv_earn_gold",          new[] { "Wealthy", "부자" } },
        { "achv_earn_gold_desc",     new[] { "Reach 500 gold.", "골드 500 모으기" } },
        { "achv_reach_level",        new[] { "Rising\nStar", "떠오르는\n별" } },
        { "achv_reach_level_desc",   new[] { "Reach level 10.", "레벨 10 달성" } },
        { "achv_defeat_boss",        new[] { "Boss\nSlayer", "보스\n사냥꾼" } },
        { "achv_defeat_boss_desc",   new[] { "Defeat a boss.", "보스 처치" } },
    };

    private const string LanguageKey = "SelectedLanguage";

    public static LocalizationManager Instance { get; private set; }

    public int CurrentLanguageIndex => currentLanguage;

    public Transform titleRoot;
    public Transform levelSelectRoot;
    public TMP_Dropdown languageDropdown;

    private readonly List<(TMP_Text text, string key)> registeredTexts = new List<(TMP_Text, string)>();
    private int currentLanguage;

    private void Awake()
    {
        Instance = this;
        RegisterAll();
        SetupDropdown();

        currentLanguage = Mathf.Clamp(PlayerPrefs.GetInt(LanguageKey, 0), 0, NativeNames.Length - 1);
        ApplyLanguage(currentLanguage, notifyDropdown: true);
    }

    private void Register(Transform root, string path, string key)
    {
        var t = root.Find(path);
        if (t == null)
        {
            Debug.LogWarning("LocalizationManager: could not find " + path);
            return;
        }
        var txt = t.GetComponent<TMP_Text>();
        if (txt == null)
        {
            Debug.LogWarning("LocalizationManager: no TMP_Text on " + path);
            return;
        }
        registeredTexts.Add((txt, key));
    }

    // Logo-style screen titles (SPACE GAME, PLAY, LEVEL SELECT) stay in English; planet
    // names/LOCKED are owned by StageProgress.
    private void RegisterAll()
    {
        const string settings = "Settings_Container/Settings Container/";
        Register(levelSelectRoot, settings + "Slider_Container/Sound_Slider/Sound_TXT", "sound");
        Register(levelSelectRoot, settings + "Slider_Container/Music_Slider/Music_TXT", "music");
        Register(levelSelectRoot, settings + "Text_Container/Language", "language");

        Register(levelSelectRoot, "Shop_Button/Text (TMP)", "shop");
        Register(levelSelectRoot, "Shop_Item_Container/Shop_TXT", "shop_title");
        Register(levelSelectRoot, "Mission_Button/Text (TMP)", "mission");
        Register(levelSelectRoot, "Mission_Container/Mission_Title", "mission");
        Register(levelSelectRoot, "Inventory_Button/Text (TMP)", "achievements");
        Register(levelSelectRoot, "Inventory_Container/Inventory_TXT", "achievements");

        Register(levelSelectRoot, "Account_Container/Account_Title", "account_title");
        const string account = "Account_Container/Account Container/LoggedOutGroup/";
        Register(levelSelectRoot, account + "Username_Label", "account_username");
        Register(levelSelectRoot, account + "Password_Label", "account_password");
        Register(levelSelectRoot, account + "Login_Button/Text (TMP)", "account_log_in");
        Register(levelSelectRoot, account + "SignUp_Button/Text (TMP)", "account_sign_up");
        Register(levelSelectRoot, account + "Username_Input/Text Area/Placeholder", "account_enter_username");
        Register(levelSelectRoot, account + "Password_Input/Text Area/Placeholder", "account_enter_password");

        const string signUp = "Account_Container/Account Container/SignUpGroup/";
        Register(levelSelectRoot, signUp + "Username_Label", "account_username");
        Register(levelSelectRoot, signUp + "Password_Label", "account_password");
        Register(levelSelectRoot, signUp + "Confirm_Label", "account_password_confirm");
        Register(levelSelectRoot, signUp + "Username_Input/Text Area/Placeholder", "account_enter_username");
        Register(levelSelectRoot, signUp + "Password_Input/Text Area/Placeholder", "account_enter_password");
        Register(levelSelectRoot, signUp + "Confirm_Input/Text Area/Placeholder", "account_enter_password_confirm");
        Register(levelSelectRoot, signUp + "Submit_Button/Text (TMP)", "account_sign_up");
        Register(levelSelectRoot, signUp + "Back_Button/Text (TMP)", "account_back");
        Register(levelSelectRoot, "Account_Container/Account Container/LoggedInGroup/Logout_Button/Text (TMP)", "account_log_out");
    }

    private void SetupDropdown()
    {
        if (languageDropdown == null) return;
        var options = new List<TMP_Dropdown.OptionData>();
        foreach (var name in NativeNames) options.Add(new TMP_Dropdown.OptionData(name));
        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(options);
        languageDropdown.onValueChanged.RemoveListener(OnLanguageDropdownChanged);
        languageDropdown.onValueChanged.AddListener(OnLanguageDropdownChanged);
    }

    private void OnLanguageDropdownChanged(int index)
    {
        currentLanguage = index;
        PlayerPrefs.SetInt(LanguageKey, index);
        PlayerPrefs.Save();
        ApplyLanguage(index, notifyDropdown: false);
    }

    private void ApplyLanguage(int index, bool notifyDropdown)
    {
        foreach (var entry in registeredTexts)
        {
            if (Translations.TryGetValue(entry.key, out var values) && index >= 0 && index < values.Length)
            {
                entry.text.text = values[index];
            }
        }

        if (notifyDropdown && languageDropdown != null)
        {
            languageDropdown.SetValueWithoutNotify(index);
            languageDropdown.RefreshShownValue();
        }

        LanguageChanged?.Invoke(index);
    }

    public string GetText(string key)
    {
        if (Translations.TryGetValue(key, out var values) && currentLanguage >= 0 && currentLanguage < values.Length)
        {
            return values[currentLanguage];
        }
        return key;
    }

    // Safe to call before/without a LocalizationManager in the scene.
    public static string Get(string key, string fallback)
    {
        return Instance != null ? Instance.GetText(key) : fallback;
    }

    public static int LanguageIndex => Instance != null ? Instance.currentLanguage : 0;

    // The Korean fallback font (Malgun) is much thinner than the Latin one, so some
    // labels get bolded only in Korean.
    public static bool IsKorean => LanguageIndex == 1;
}
