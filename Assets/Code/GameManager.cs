using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runs the whole game: title screen, the three levels, the heart health bar,
/// game over and victory. It creates itself automatically when a scene with a
/// "Player"-tagged object loads, so nothing has to be wired up in the scene.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum State { Title, Intro, Playing, LevelClear, GameOver, Victory }

    [System.Serializable]
    public class LevelSettings
    {
        public string title;
        public string subtitle;
        [Tooltip("Swords that must pass before the level is cleared (ignored on the barrier level).")]
        public int swordCount = 25;
        public float spawnInterval = 0.9f;
        public float swordSpeed = 9f;
        [Range(0f, 1f)] public float diagonalChance;
        public float backgroundSpeed = 0.5f;
        [Tooltip("Texture in Resources/Hayu used for the looping background.")]
        public string background;
        [Tooltip("Level 3: collect golden swords and throw them at the barrier.")]
        public bool barrierLevel;
    }

    public static GameManager Instance { get; private set; }
    static bool skipTitleOnLoad;

    public LevelSettings[] levels =
    {
        new LevelSettings { title = "LEVEL 1", subtitle = "Dodge 25 swords", swordCount = 25, spawnInterval = 0.85f, swordSpeed = 9f, diagonalChance = 0f, backgroundSpeed = 0.5f, background = "bg_level1" },
        new LevelSettings { title = "LEVEL 2", subtitle = "Watch the diagonals - dodge 25 swords", swordCount = 25, spawnInterval = 0.8f, swordSpeed = 10f, diagonalChance = 0.45f, backgroundSpeed = 0.7f, background = "bg_level2" },
        new LevelSettings { title = "LEVEL 3", subtitle = "Catch golden swords, throw them (SPACE) at the barrier", spawnInterval = 1.7f, swordSpeed = 8f, diagonalChance = 0.35f, backgroundSpeed = 0.3f, background = "bg_level3", barrierLevel = true },
    };

    [Tooltip("Mend one crack of the heart after clearing a level.")]
    public bool healBetweenLevels = true;
    public int barrierHealth = 10;
    public float invulnerableTime = 1.2f;

    public State CurrentState { get; private set; }
    public int LevelIndex { get; private set; }
    public LevelSettings Level => levels[LevelIndex];
    public int Damage { get; private set; }
    public int MaxHits => heartSprites.Length - 1;
    public int Ammo { get; private set; }
    public int SwordsPassed { get; private set; }
    public int TotalDodged { get; private set; }
    public bool Paused { get; private set; }
    public bool CanShoot => CurrentState == State.Playing && !Paused && Level.barrierLevel && barrier != null && !barrier.IsBroken;

    // Scene references
    Player player;
    SpawnObstacles spawner;
    LoopingBackground background;
    Camera cam;
    Vector3 camBaseLocalPos;

    // Art
    Sprite[] heartSprites;
    Sprite goalHeartSprite, startSprite, gameOverSprite, pickupSprite;
    Sprite[] barrierSprites;

    // Level 3
    Barrier barrier;
    GoalHeart goalHeart;

    // UI
    Font font;
    HealthBar healthBar;
    Text levelText, progressText, ammoText, bannerTitle, bannerSub, overlayMessage, pauseText;
    GameObject ammoGroup, overlay;
    Image overlayImage, fader;
    CanvasGroup bannerGroup;
    Button overlayButton;
    Text overlayButtonText;
    System.Action overlayAction;
    Coroutine shakeRoutine;

    public static Color Pink = new Color(1f, 0.42f, 0.62f);
    public static Color DeepPink = new Color(0.45f, 0f, 0.16f);

    // ------------------------------------------------------------------ bootstrap

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterBootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var existing in FindObjectsByType<GameManager>(FindObjectsSortMode.None))
            if (existing.gameObject.scene == scene) return;
        if (GameObject.FindGameObjectWithTag("Player") == null) return;
        new GameObject("Hayu Game Manager").AddComponent<GameManager>();
    }

    // ------------------------------------------------------------------ lifecycle

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        LoadArt();

        cam = Camera.main;
        if (cam != null) camBaseLocalPos = cam.transform.localPosition;

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        player = playerGo.GetComponent<Player>();
        if (player == null) player = playerGo.AddComponent<Player>();

        spawner = FindAnyObjectByType<SpawnObstacles>();
        if (spawner == null) spawner = new GameObject("SpawnPoint").AddComponent<SpawnObstacles>();

        background = FindAnyObjectByType<LoopingBackground>();

        // The camera rig used to fly to the right; swords now fly at the player instead,
        // which keeps everything on screen predictable.
        foreach (var mover in FindObjectsByType<CameraMovement>(FindObjectsSortMode.None)) mover.enabled = false;

        // The old polling game-over panel is replaced by the new overlay.
        var oldGameOver = FindAnyObjectByType<GameOver>();
        if (oldGameOver != null && oldGameOver.gameOverPanel != null) oldGameOver.gameOverPanel.SetActive(false);

        BuildUI();
        ApplyBackground(0);
        healthBar.Init(heartSprites);

        if (skipTitleOnLoad)
        {
            skipTitleOnLoad = false;
            StartCoroutine(BeginLevel(0));
        }
        else
        {
            ShowTitle();
        }
    }

    void Update()
    {
        bool confirm = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

        switch (CurrentState)
        {
            case State.Title:
                if (confirm && overlay.activeSelf) PressOverlayButton();
                break;
            case State.GameOver:
            case State.Victory:
                if ((confirm || Input.GetKeyDown(KeyCode.R)) && overlay.activeSelf) PressOverlayButton();
                break;
            case State.Intro:
            case State.Playing:
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) TogglePause();
                break;
        }

        UpdateHud();
    }

    // ------------------------------------------------------------------ flow

    void ShowTitle()
    {
        CurrentState = State.Title;
        player.SetControllable(false);
        ShowOverlay(startSprite,
            "Protect your heart.\nMove with  W/S  or  the arrow keys.\nEach sword that hits you drives the blade deeper.",
            "START", () => { HideOverlay(); StartCoroutine(BeginLevel(0)); });
    }

    IEnumerator BeginLevel(int index)
    {
        CurrentState = State.Intro;
        LevelIndex = index;
        SwordsPassed = 0;
        Ammo = 0;

        ApplyBackground(index);
        player.ResetForLevel();
        player.SetControllable(true);

        if (Level.barrierLevel) SpawnBarrier();

        yield return ShowBanner(Level.title, Level.subtitle, 2.2f);

        CurrentState = State.Playing;
        spawner.StartWave(Level);
    }

    IEnumerator LevelCleared()
    {
        CurrentState = State.LevelClear;
        spawner.Stop();

        yield return ShowBanner("LEVEL CLEAR!", "Swords dodged: " + TotalDodged, 1.6f);

        if (healBetweenLevels && Damage > 0)
        {
            Damage--;
            healthBar.SetStage(Damage);
            yield return new WaitForSeconds(0.8f);
        }

        yield return Fade(0f, 1f, 0.4f);
        ClearProjectiles();
        ApplyBackground(LevelIndex + 1);
        player.ResetForLevel();
        yield return Fade(1f, 0f, 0.4f);

        StartCoroutine(BeginLevel(LevelIndex + 1));
    }

    IEnumerator GameOverRoutine()
    {
        CurrentState = State.GameOver;
        spawner.Stop();
        if (background != null) background.backgroundSpeed = 0f;
        player.Die();
        Shake(0.6f, 0.45f);

        yield return new WaitForSeconds(1.8f);

        ShowOverlay(gameOverSprite,
            "Your heart broke on " + Level.title + ".\nSwords dodged: " + TotalDodged,
            "TRY AGAIN", () => Restart(true));
    }

    IEnumerator VictoryRoutine()
    {
        CurrentState = State.Victory;
        spawner.Stop();
        player.SetControllable(false);
        if (goalHeart != null) goalHeart.Celebrate();

        // The heart is whole again.
        while (Damage > 0)
        {
            Damage--;
            healthBar.SetStage(Damage);
            yield return new WaitForSeconds(0.35f);
        }
        yield return new WaitForSeconds(1.2f);

        ShowOverlay(goalHeartSprite,
            "You broke through and found the heart that was waiting for you.\nSwords dodged: " + TotalDodged,
            "PLAY AGAIN", () => Restart(false));
    }

    public void Restart(bool skipTitle)
    {
        Time.timeScale = 1f;
        skipTitleOnLoad = skipTitle;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void TogglePause()
    {
        Paused = !Paused;
        Time.timeScale = Paused ? 0f : 1f;
        pauseText.gameObject.SetActive(Paused);
    }

    // ------------------------------------------------------------------ gameplay events

    /// <summary>Called by a hostile sword when the player touches it. Returns true if it hurt.</summary>
    public bool PlayerHit()
    {
        if (CurrentState != State.Playing && CurrentState != State.Intro) return false;
        if (player.IsInvulnerable) return false;

        Damage = Mathf.Min(Damage + 1, MaxHits);
        healthBar.SetStage(Damage);

        if (Damage >= MaxHits)
        {
            StartCoroutine(GameOverRoutine());
        }
        else
        {
            Shake(0.25f, 0.2f);
            player.StartInvulnerability(invulnerableTime);
        }
        return true;
    }

    /// <summary>Called once for every hostile sword that leaves play (dodged or hit).</summary>
    public void OnSwordResolved(bool dodged)
    {
        if (CurrentState != State.Playing) return;
        if (dodged) TotalDodged++;
        if (Level.barrierLevel) return;

        SwordsPassed++;
        if (SwordsPassed >= Level.swordCount && spawner.Finished)
            StartCoroutine(LevelCleared());
    }

    public void AddAmmo()
    {
        Ammo++;
        healthBar.Pulse(0.08f);
    }

    public bool TryUseAmmo()
    {
        if (!CanShoot || Ammo <= 0) return false;
        Ammo--;
        return true;
    }

    public void OnBarrierBroken(Vector3 position)
    {
        spawner.Stop();
        ClearProjectiles();
        Shake(0.5f, 0.35f);
        Ammo = 0;

        goalHeart = GoalHeart.Create(goalHeartSprite, position, new Vector3(ViewRect.center.x + ViewRect.width * 0.25f, 0f, 0f));
        player.SetFreeMovement(true);
        StartCoroutine(ShowBanner("THE BARRIER SHATTERS", "Go to your heart", 2.2f));
    }

    public void OnHeartReached()
    {
        if (CurrentState != State.Playing) return;
        StartCoroutine(VictoryRoutine());
    }

    void SpawnBarrier()
    {
        if (barrier != null) Destroy(barrier.gameObject);
        Rect view = ViewRect;
        barrier = Barrier.Create(barrierSprites, barrierHealth, new Vector3(view.xMax - 1.3f, view.center.y, 0f), view.height);
    }

    void ClearProjectiles()
    {
        foreach (var o in FindObjectsByType<Obstacle>(FindObjectsSortMode.None)) o.Shatter(false);
        foreach (var p in FindObjectsByType<SwordPickup>(FindObjectsSortMode.None)) Destroy(p.gameObject);
        foreach (var t in FindObjectsByType<ThrownSword>(FindObjectsSortMode.None)) Destroy(t.gameObject);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>World-space rectangle the camera currently sees.</summary>
    public static Rect ViewRect
    {
        get
        {
            Camera c = Camera.main;
            if (c == null) return new Rect(-9f, -5f, 18f, 10f);
            float h = c.orthographicSize;
            float w = h * c.aspect;
            Vector3 p = c.transform.position;
            return new Rect(p.x - w, p.y - h, w * 2f, h * 2f);
        }
    }

    public Vector3 PlayerPosition => player != null ? player.transform.position : Vector3.zero;

    public static Sprite LoadSprite(string name)
    {
        var s = Resources.Load<Sprite>("Hayu/" + name);
        if (s == null) Debug.LogWarning("Hayu: missing sprite Resources/Hayu/" + name);
        return s;
    }

    void LoadArt()
    {
        heartSprites = new Sprite[5];
        for (int i = 0; i < heartSprites.Length; i++) heartSprites[i] = LoadSprite("heart_" + i);
        goalHeartSprite = LoadSprite("heart_goal");
        startSprite = LoadSprite("startgame");
        gameOverSprite = LoadSprite("gameover");
        pickupSprite = LoadSprite("sword_pickup");
        barrierSprites = new[] { LoadSprite("barrier_0"), LoadSprite("barrier_1"), LoadSprite("barrier_2") };
    }

    void ApplyBackground(int index)
    {
        if (background == null || index >= levels.Length) return;
        var l = levels[index];
        background.backgroundSpeed = l.backgroundSpeed;
        if (!string.IsNullOrEmpty(l.background))
        {
            var tex = Resources.Load<Texture2D>("Hayu/" + l.background);
            if (tex != null) background.SetTexture(tex);
        }
    }

    public void Shake(float duration, float strength)
    {
        if (cam == null) return;
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine(duration, strength));
    }

    IEnumerator ShakeRoutine(float duration, float strength)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float s = strength * (1f - t / duration);
            cam.transform.localPosition = camBaseLocalPos + (Vector3)(Random.insideUnitCircle * s);
            yield return null;
        }
        cam.transform.localPosition = camBaseLocalPos;
        shakeRoutine = null;
    }

    // ------------------------------------------------------------------ UI

    void BuildUI()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var canvasGo = new GameObject("Hayu HUD");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var root = canvasGo.transform;

        // Heart health bar (top-left)
        var heartGo = new GameObject("Heart Health", typeof(RectTransform), typeof(Image));
        heartGo.transform.SetParent(root, false);
        var heartRt = (RectTransform)heartGo.transform;
        heartRt.anchorMin = heartRt.anchorMax = new Vector2(0f, 1f);
        heartRt.pivot = new Vector2(0.5f, 0.5f);
        heartRt.sizeDelta = new Vector2(190f, 190f);
        heartRt.anchoredPosition = new Vector2(120f, -115f);
        heartGo.GetComponent<Image>().raycastTarget = false;
        healthBar = heartGo.AddComponent<HealthBar>();

        levelText = CreateText(root, "Level", 44, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 60f));
        progressText = CreateText(root, "Progress", 34, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(900f, 50f));
        progressText.color = Color.white;

        // Ammo (top-right, level 3 only)
        ammoGroup = new GameObject("Ammo", typeof(RectTransform));
        ammoGroup.transform.SetParent(root, false);
        var ammoRt = (RectTransform)ammoGroup.transform;
        ammoRt.anchorMin = ammoRt.anchorMax = new Vector2(1f, 1f);
        ammoRt.anchoredPosition = new Vector2(-170f, -100f);
        ammoRt.sizeDelta = new Vector2(300f, 120f);
        var ammoIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        ammoIcon.transform.SetParent(ammoGroup.transform, false);
        var iconRt = (RectTransform)ammoIcon.transform;
        iconRt.anchoredPosition = new Vector2(-60f, 0f);
        iconRt.sizeDelta = new Vector2(120f, 120f);
        iconRt.localRotation = Quaternion.Euler(0f, 0f, -35f);
        var iconImg = ammoIcon.GetComponent<Image>();
        iconImg.sprite = pickupSprite;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        ammoText = CreateText(ammoGroup.transform, "Count", 52, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(70f, 0f), new Vector2(160f, 80f));
        ammoGroup.SetActive(false);

        // Banner (center)
        var bannerGo = new GameObject("Banner", typeof(RectTransform), typeof(CanvasGroup));
        bannerGo.transform.SetParent(root, false);
        var bannerRt = (RectTransform)bannerGo.transform;
        bannerRt.anchorMin = bannerRt.anchorMax = new Vector2(0.5f, 0.5f);
        bannerRt.sizeDelta = new Vector2(1600f, 300f);
        bannerRt.anchoredPosition = new Vector2(0f, 120f);
        bannerGroup = bannerGo.GetComponent<CanvasGroup>();
        bannerGroup.alpha = 0f;
        bannerGroup.blocksRaycasts = false;
        bannerTitle = CreateText(bannerGo.transform, "Title", 110, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1600f, 140f));
        bannerSub = CreateText(bannerGo.transform, "Subtitle", 40, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1600f, 60f));
        bannerSub.color = Color.white;

        pauseText = CreateText(root, "Paused", 90, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 300f));
        pauseText.text = "PAUSED\n<size=36>press  P  or  ESC  to continue</size>";
        pauseText.gameObject.SetActive(false);

        // Overlay (title / game over / victory)
        overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(root, false);
        Stretch((RectTransform)overlay.transform);
        overlay.GetComponent<Image>().color = new Color(0.08f, 0.03f, 0.08f, 0.72f);

        var picGo = new GameObject("Picture", typeof(RectTransform), typeof(Image));
        picGo.transform.SetParent(overlay.transform, false);
        var picRt = (RectTransform)picGo.transform;
        picRt.anchoredPosition = new Vector2(0f, 190f);
        picRt.sizeDelta = new Vector2(900f, 420f);
        overlayImage = picGo.GetComponent<Image>();
        overlayImage.preserveAspect = true;
        overlayImage.raycastTarget = false;

        overlayMessage = CreateText(overlay.transform, "Message", 36, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(1500f, 170f));
        overlayMessage.color = Color.white;

        var buttonGo = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(overlay.transform, false);
        var buttonRt = (RectTransform)buttonGo.transform;
        buttonRt.anchoredPosition = new Vector2(0f, -290f);
        buttonRt.sizeDelta = new Vector2(380f, 100f);
        buttonGo.GetComponent<Image>().color = new Color(0.89f, 0.06f, 0.34f);
        overlayButton = buttonGo.GetComponent<Button>();
        var colors = overlayButton.colors;
        colors.highlightedColor = new Color(1f, 0.8f, 0.85f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        overlayButton.colors = colors;
        overlayButton.onClick.AddListener(PressOverlayButton);
        overlayButtonText = CreateText(buttonGo.transform, "Label", 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 100f));
        overlayButtonText.color = Color.white;
        overlay.SetActive(false);

        // Full-screen fader for level transitions
        var faderGo = new GameObject("Fader", typeof(RectTransform), typeof(Image));
        faderGo.transform.SetParent(root, false);
        Stretch((RectTransform)faderGo.transform);
        fader = faderGo.GetComponent<Image>();
        fader.color = new Color(0f, 0f, 0f, 0f);
        fader.raycastTarget = false;
    }

    void UpdateHud()
    {
        if (levelText == null) return;
        bool inGame = CurrentState != State.Title;
        levelText.gameObject.SetActive(inGame);
        progressText.gameObject.SetActive(inGame);
        healthBar.gameObject.SetActive(inGame);

        levelText.text = Level.title;
        if (Level.barrierLevel)
        {
            progressText.text = barrier != null && !barrier.IsBroken
                ? "Barrier  " + barrier.Health + " / " + barrierHealth
                : "Reach your heart";
        }
        else
        {
            progressText.text = "Swords dodged  " + Mathf.Min(SwordsPassed, Level.swordCount) + " / " + Level.swordCount;
        }

        bool showAmmo = inGame && Level.barrierLevel && barrier != null && !barrier.IsBroken;
        if (ammoGroup.activeSelf != showAmmo) ammoGroup.SetActive(showAmmo);
        ammoText.text = "x " + Ammo;
    }

    void ShowOverlay(Sprite picture, string message, string button, System.Action action)
    {
        overlayImage.sprite = picture;
        overlayImage.enabled = picture != null;
        overlayMessage.text = message;
        overlayButtonText.text = button;
        overlayAction = action;
        overlay.SetActive(true);
    }

    void HideOverlay() => overlay.SetActive(false);

    void PressOverlayButton()
    {
        var a = overlayAction;
        overlayAction = null;
        a?.Invoke();
    }

    IEnumerator ShowBanner(string title, string subtitle, float hold)
    {
        bannerTitle.text = title;
        bannerSub.text = subtitle;
        var rt = (RectTransform)bannerGroup.transform;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float k = t / 0.3f;
            bannerGroup.alpha = k;
            rt.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, k);
            yield return null;
        }
        bannerGroup.alpha = 1f;
        rt.localScale = Vector3.one;
        yield return new WaitForSeconds(hold);
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            bannerGroup.alpha = 1f - t / 0.3f;
            yield return null;
        }
        bannerGroup.alpha = 0f;
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            fader.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        fader.color = new Color(0f, 0f, 0f, to);
    }

    Text CreateText(Transform parent, string name, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y >= 1f ? 1f : 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = Pink;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var o = go.GetComponent<Outline>();
        o.effectColor = DeepPink;
        o.effectDistance = new Vector2(3f, -3f);
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
