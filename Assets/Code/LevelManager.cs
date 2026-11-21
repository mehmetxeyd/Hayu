using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runs the whole game: title screen, the three levels, the heart health bar,
/// game over and victory. It creates itself automatically when a scene with a
/// "Player"-tagged object loads, so nothing has to be wired up in the scene.
/// </summary>
public class LevelManager : MonoBehaviour
{
    public enum State { Title, Intro, Playing, LevelClear, GameOver, Victory }

    [System.Serializable]
    public class LevelSettings
    {
        public string title;
        [Tooltip("Banner image in Resources/Hayu shown when the level starts.")]
        public string banner;
        [Tooltip("One short hint under the banner (leave empty for none).")]
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

    public static LevelManager Instance { get; private set; }
    static bool skipTitleOnLoad;

    public LevelSettings[] levels =
    {
        new LevelSettings { title = "LEVEL 1", banner = "banner_level1", subtitle = "dodge 25 swords", swordCount = 25, spawnInterval = 0.85f, swordSpeed = 9f, diagonalChance = 0f, backgroundSpeed = 0.5f, background = "bg_level1" },
        new LevelSettings { title = "LEVEL 2", banner = "banner_level2", subtitle = "dodge 25 swords", swordCount = 25, spawnInterval = 0.8f, swordSpeed = 10f, diagonalChance = 0.45f, backgroundSpeed = 0.7f, background = "bg_level2" },
        new LevelSettings { title = "LEVEL 3", banner = "banner_level3", subtitle = "catch gold swords - SPACE to throw", spawnInterval = 1.7f, swordSpeed = 8f, diagonalChance = 0.35f, backgroundSpeed = 0.3f, background = "bg_level3", barrierLevel = true },
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
    Sprite clearBanner, winBanner, pausedBanner, startButton, retryButton, againButton;
    Sprite[] barrierSprites;

    // Level 3
    Barrier barrier;
    GoalHeart goalHeart;

    // UI
    TMP_FontAsset font;
    Material textMaterial;
    HealthBar healthBar;
    TextMeshProUGUI progressText, ammoText, bannerSub, overlayMessage;
    GameObject ammoGroup, overlay, pauseImage;
    Image overlayImage, bannerImage, fader;
    CanvasGroup bannerGroup;
    Button overlayButton;
    Image overlayButtonImage;
    System.Action overlayAction;
    Coroutine shakeRoutine;
    GameManager startScreen;
    bool waitingForStartPanel;
    bool autoPressStart;

    // Same colours as the title art.
    public static Color Pink = new Color32(254, 115, 164, 255);
    public static Color DeepPink = new Color32(206, 0, 73, 255);

    // ------------------------------------------------------------------ bootstrap

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterBootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var existing in FindObjectsByType<LevelManager>(FindObjectsSortMode.None))
            if (existing.gameObject.scene == scene) return;
        if (GameObject.FindGameObjectWithTag("Player") == null) return;
        new GameObject("Hayu Level Manager").AddComponent<LevelManager>();
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

        // The scene's score / high-score labels overlapped the heart; the HUD and end screens show these now.
        foreach (var sm in FindObjectsByType<ScoreManager>(FindObjectsSortMode.None))
            if (sm.scoreText != null) sm.scoreText.gameObject.SetActive(false);
        foreach (var hs in FindObjectsByType<HighScore>(FindObjectsSortMode.None))
            if (hs.highScoreText != null) hs.highScoreText.gameObject.SetActive(false);
        if (oldGameOver != null && oldGameOver.highScoreText != null) oldGameOver.highScoreText.gameObject.SetActive(false);

        BuildUI();
        ApplyBackground(0);
        healthBar.Init(heartSprites);

        startScreen = FindAnyObjectByType<GameManager>();

        if (skipTitleOnLoad)
        {
            skipTitleOnLoad = false;
            if (startScreen != null && startScreen.startPanel != null)
            {
                // Press the start panel's button for the player on the first frame
                // (after the scene's GameManager.Start has paused time).
                CurrentState = State.Title;
                waitingForStartPanel = true;
                autoPressStart = true;
            }
            else
            {
                StartCoroutine(BeginLevel(0));
            }
        }
        else if (startScreen != null && startScreen.startPanel != null)
        {
            // The scene's own start panel is the title screen; wait for its Start button.
            CurrentState = State.Title;
            player.SetControllable(false);
            waitingForStartPanel = true;
        }
        else
        {
            ShowTitle();
        }
    }

    void Update()
    {
        bool confirm = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

        if (waitingForStartPanel)
        {
            if (autoPressStart)
            {
                autoPressStart = false;
                startScreen.StartGame();
            }
            if (GameManager.gameStarted || (startScreen != null && startScreen.startPanel != null && !startScreen.startPanel.activeInHierarchy))
            {
                waitingForStartPanel = false;
                Time.timeScale = 1f;
                StartCoroutine(BeginLevel(0));
            }
            else if (confirm && startScreen != null)
            {
                startScreen.StartGame();
            }
            UpdateHud();
            return;
        }

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
        ShowOverlay(startSprite, "move with W/S or the arrow keys",
            startButton, () => { HideOverlay(); StartCoroutine(BeginLevel(0)); });
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

        yield return ShowBanner(LoadSprite(Level.banner), Level.subtitle, 2f);

        CurrentState = State.Playing;
        spawner.StartWave(Level);
    }

    IEnumerator LevelCleared()
    {
        CurrentState = State.LevelClear;
        spawner.Stop();

        yield return ShowBanner(clearBanner, "", 1.3f);

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

        int best = GameOver.SaveHighScore(TotalDodged);
        ShowOverlay(gameOverSprite, TotalDodged + " swords   best " + best,
            retryButton, () => Restart(true));
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

        int best = GameOver.SaveHighScore(TotalDodged);
        ShowOverlay(winBanner, TotalDodged + " swords   best " + best,
            againButton, () => Restart(false));
    }

    public void Restart(bool skipTitle)
    {
        Time.timeScale = 1f;
        skipTitleOnLoad = skipTitle;
        GameManager.gameStarted = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void TogglePause()
    {
        Paused = !Paused;
        Time.timeScale = Paused ? 0f : 1f;
        pauseImage.SetActive(Paused);
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
        StartCoroutine(ShowBanner(null, "go to your heart", 1.6f));
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
        clearBanner = LoadSprite("banner_clear");
        winBanner = LoadSprite("banner_win");
        pausedBanner = LoadSprite("banner_paused");
        startButton = LoadSprite("button_start");
        retryButton = LoadSprite("button_retry");
        againButton = LoadSprite("button_again");
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
        CreateFont();

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
        var heartImg = CreateImage(root, "Heart Health", null, new Vector2(0f, 1f), new Vector2(120f, -115f));
        ((RectTransform)heartImg.transform).sizeDelta = new Vector2(190f, 190f);
        healthBar = heartImg.gameObject.AddComponent<HealthBar>();

        // Sword counter (top-centre)
        progressText = CreateText(root, "Progress", 46, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(600f, 70f));

        // Ammo (top-right, level 3 only)
        ammoGroup = new GameObject("Ammo", typeof(RectTransform));
        ammoGroup.transform.SetParent(root, false);
        var ammoRt = (RectTransform)ammoGroup.transform;
        ammoRt.anchorMin = ammoRt.anchorMax = new Vector2(1f, 1f);
        ammoRt.anchoredPosition = new Vector2(-170f, -100f);
        ammoRt.sizeDelta = new Vector2(300f, 120f);
        var icon = CreateImage(ammoGroup.transform, "Icon", pickupSprite, new Vector2(0.5f, 0.5f), new Vector2(-50f, 0f));
        ((RectTransform)icon.transform).sizeDelta = new Vector2(110f, 110f);
        icon.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
        ammoText = CreateText(ammoGroup.transform, "Count", 56, new Vector2(0.5f, 0.5f), new Vector2(50f, 0f), new Vector2(120f, 80f));
        ammoText.alignment = TextAlignmentOptions.Left;
        ammoGroup.SetActive(false);

        // Banner (centre): pixel-art title image + one short hint
        var bannerGo = new GameObject("Banner", typeof(RectTransform), typeof(CanvasGroup));
        bannerGo.transform.SetParent(root, false);
        var bannerRt = (RectTransform)bannerGo.transform;
        bannerRt.anchorMin = bannerRt.anchorMax = new Vector2(0.5f, 0.5f);
        bannerRt.sizeDelta = new Vector2(1600f, 300f);
        bannerRt.anchoredPosition = new Vector2(0f, 110f);
        bannerGroup = bannerGo.GetComponent<CanvasGroup>();
        bannerGroup.alpha = 0f;
        bannerGroup.blocksRaycasts = false;
        bannerImage = CreateImage(bannerGo.transform, "Title", null, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f));
        bannerSub = CreateText(bannerGo.transform, "Hint", 40, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(1600f, 60f));

        pauseImage = CreateImage(root, "Paused", pausedBanner, new Vector2(0.5f, 0.5f), Vector2.zero).gameObject;
        SetPixelSize(pauseImage.GetComponent<Image>(), pausedBanner);
        pauseImage.SetActive(false);

        // Overlay (title / game over / victory)
        overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(root, false);
        Stretch((RectTransform)overlay.transform);
        overlay.GetComponent<Image>().color = new Color(0.08f, 0.02f, 0.05f, 0.6f);

        overlayImage = CreateImage(overlay.transform, "Picture", null, new Vector2(0.5f, 0.5f), new Vector2(0f, 130f));
        overlayMessage = CreateText(overlay.transform, "Message", 40, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(1500f, 60f));

        overlayButtonImage = CreateImage(overlay.transform, "Button", null, new Vector2(0.5f, 0.5f), new Vector2(0f, -210f));
        overlayButtonImage.raycastTarget = true;
        overlayButton = overlayButtonImage.gameObject.AddComponent<Button>();
        var colors = overlayButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.85f, 0.9f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.75f);
        colors.selectedColor = Color.white;
        overlayButton.colors = colors;
        overlayButton.onClick.AddListener(PressOverlayButton);
        overlay.SetActive(false);

        // Full-screen fader for level transitions
        fader = CreateImage(root, "Fader", null, Vector2.zero, Vector2.zero);
        Stretch((RectTransform)fader.transform);
        fader.color = new Color(0f, 0f, 0f, 0f);
    }

    void CreateFont()
    {
        var ttf = Resources.Load<Font>("Hayu/pixel_font");
        if (ttf != null) font = TMP_FontAsset.CreateFontAsset(ttf);
        if (font == null) font = TMP_Settings.defaultFontAsset;
        if (font == null) return;

        // Minecraft-style hard drop shadow in the title's dark pink.
        textMaterial = new Material(font.material);
        textMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        textMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, DeepPink);
        textMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.9f);
        textMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.9f);
        textMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
        textMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
    }

    void UpdateHud()
    {
        if (progressText == null) return;
        bool inGame = CurrentState != State.Title;
        healthBar.gameObject.SetActive(inGame);

        string progress = "";
        if (inGame && !Level.barrierLevel && CurrentState != State.GameOver)
            progress = Mathf.Min(SwordsPassed, Level.swordCount) + " / " + Level.swordCount;
        progressText.text = progress;

        bool showAmmo = inGame && Level.barrierLevel && barrier != null && !barrier.IsBroken && CurrentState == State.Playing;
        if (ammoGroup.activeSelf != showAmmo) ammoGroup.SetActive(showAmmo);
        ammoText.text = Ammo.ToString();
    }

    void ShowOverlay(Sprite picture, string message, Sprite button, System.Action action)
    {
        overlayImage.sprite = picture;
        overlayImage.enabled = picture != null;
        if (picture != null)
        {
            // Small sprites are pixel-art banners; big ones (like GAME OVER) are full images.
            if (picture.rect.width < 300f) SetPixelSize(overlayImage, picture);
            else ((RectTransform)overlayImage.transform).sizeDelta = picture.rect.size * (380f / picture.rect.height);
        }
        overlayMessage.text = message;
        overlayButtonImage.sprite = button;
        SetPixelSize(overlayButtonImage, button, 7f);
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

    IEnumerator ShowBanner(Sprite title, string hint, float hold)
    {
        bannerImage.sprite = title;
        bannerImage.enabled = title != null;
        if (title != null) SetPixelSize(bannerImage, title);
        bannerSub.text = hint ?? "";

        var rt = (RectTransform)bannerGroup.transform;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            float k = t / 0.25f;
            bannerGroup.alpha = k;
            rt.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, k);
            yield return null;
        }
        bannerGroup.alpha = 1f;
        rt.localScale = Vector3.one;
        yield return new WaitForSeconds(hold);
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            bannerGroup.alpha = 1f - t / 0.25f;
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

    TextMeshProUGUI CreateText(Transform parent, string name, float size, Vector2 anchor, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        if (textMaterial != null) t.fontSharedMaterial = textMaterial;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Pink;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Size a pixel-art sprite as a whole multiple of its pixels so it stays crisp.</summary>
    static void SetPixelSize(Image img, Sprite sprite, float scale = 11f)
    {
        if (sprite == null) return;
        ((RectTransform)img.transform).sizeDelta = sprite.rect.size * scale;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
