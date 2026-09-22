using System.Collections.Generic;
using Obvious.Soap;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Woi.HazardSystem;
using Woi.InputSystem;
using Woi.Localization;
using Woi.Player;

public class ExitPanelController : MonoBehaviour
{
    [SerializeField] GameObject trPanel;
    [SerializeField] GameObject enPanel;
    [SerializeField] GameObject exitPanel;
    [SerializeField] ScriptableEventNoParam preOnGameFinishEvent;
    [SerializeField] ScriptableEventNoParam onGameFinishEvent;

    bool _open;
    bool _finished;
    CursorLockMode _savedLock;
    bool _savedCursorVisible;
    bool _gameplaySuppressed;
    GameplayInputContext _gameplayContext;
    PlayerInputActions _playerInputActions;
    readonly List<PlayerController> _suppressedPlayers = new();
    readonly List<bool> _savedPlayerInputEnabled = new();
    readonly List<Behaviour> _suspendedGameplayBehaviours = new();

    bool _closing;
    CanvasGroup _backdropGroup;
    CanvasGroup _modalGroup;
    RectTransform _modalRt;
    TextMeshProUGUI _titleText;
    TextMeshProUGUI _messageText;
    TextMeshProUGUI _yesLabel;
    TextMeshProUGUI _noLabel;
    Tween _backdropTween;
    Tween _modalTween;
    Tween _modalFadeTween;

    static Sprite _roundedSprite;

    const float ShowDuration = 0.15f;
    const float HideDuration = 0.12f;

    static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.45f);
    static readonly Color CardColor = new Color(0.08627451f, 0.13333334f, 0.20784314f, 1f);
    static readonly Color CardInnerColor = new Color(0.10588235f, 0.16862746f, 0.26666668f, 1f);
    static readonly Color BorderColor = new Color(0.18039216f, 0.27058825f, 0.40784314f, 1f);
    static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.48f);
    static readonly Color AccentColor = new Color(0.90588236f, 0.7019608f, 0.3529412f, 1f);
    static readonly Color TitleColor = new Color(0.96862745f, 0.94509804f, 0.9098039f, 1f);
    static readonly Color MessageColor = new Color(0.68235296f, 0.7254902f, 0.78431374f, 1f);
    static readonly Color ConfirmColor = new Color(0.23137255f, 0.50980395f, 0.9647059f, 1f);
    static readonly Color ConfirmHover = new Color(0.2901961f, 0.5568628f, 1f, 1f);
    static readonly Color ConfirmPressed = new Color(0.18431373f, 0.43529412f, 0.81960785f, 1f);
    static readonly Color CancelColor = new Color(0.29411766f, 0.3647059f, 0.47843137f, 1f);
    static readonly Color CancelHover = new Color(0.36078432f, 0.43137255f, 0.54509807f, 1f);
    static readonly Color CancelPressed = new Color(0.24313726f, 0.30980393f, 0.4117647f, 1f);
    static readonly Color CancelTextColor = new Color(0.9490196f, 0.9607843f, 0.972549f, 1f);

    public static void EnsurePcHost(GameObject pcUiRoot)
    {
        if (FirePlatformRuntime.CurrentMode == AppMode.XR)
            return;
        if (pcUiRoot == null)
            return;

        var pcCanvas = FindNamedChild(pcUiRoot.transform, "PCCanvas");
        if (pcCanvas == null)
            return;
        if (pcCanvas.Find("PC Exit Confirmation") != null)
            return;

        var source = FindXrController();
        if (source == null)
            return;

        var host = new GameObject("PC Exit Confirmation", typeof(RectTransform));
        var hostRt = host.GetComponent<RectTransform>();
        hostRt.SetParent(pcCanvas, false);
        hostRt.anchorMin = Vector2.zero;
        hostRt.anchorMax = Vector2.one;
        hostRt.offsetMin = Vector2.zero;
        hostRt.offsetMax = Vector2.zero;
        hostRt.localScale = Vector3.one;

        var overlay = host.AddComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = 200;
        host.AddComponent<GraphicRaycaster>();

        var pc = host.AddComponent<ExitPanelController>();
        pc.AdoptHistoricalVisual(source);
    }

    static ExitPanelController FindXrController()
    {
        var found = FindObjectsByType<ExitPanelController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (IsUnderNamedParent(found[i].transform, "VRCanvas (2)"))
                return found[i];
        }

        return null;
    }

    static bool IsUnderNamedParent(Transform t, string name)
    {
        while (t != null)
        {
            if (t.name == name)
                return true;
            t = t.parent;
        }

        return false;
    }

    static Transform FindNamedChild(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name.Trim() == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var hit = FindNamedChild(root.GetChild(i), name);
            if (hit != null)
                return hit;
        }

        return null;
    }

    void AdoptHistoricalVisual(ExitPanelController source)
    {
        preOnGameFinishEvent = source.preOnGameFinishEvent;
        onGameFinishEvent = source.onGameFinishEvent;
        BuildPcStyledVisual();

        if (isActiveAndEnabled && preOnGameFinishEvent != null)
            preOnGameFinishEvent.OnRaised += Show;
    }

    void BuildPcStyledVisual()
    {
        var font = ResolveFont();
        var round = GetRoundedSprite();

        var root = CreateRect("ExitPanel", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        exitPanel = root.gameObject;

        var backdrop = CreateImage("Backdrop", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, BackdropColor, null);
        backdrop.raycastTarget = true;
        _backdropGroup = backdrop.gameObject.AddComponent<CanvasGroup>();
        _backdropGroup.alpha = 0f;
        _backdropGroup.blocksRaycasts = true;

        _modalRt = CreateRect("ModalCard", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520f, 260f), Vector2.zero);
        _modalRt.localScale = Vector3.one * 0.94f;
        _modalGroup = _modalRt.gameObject.AddComponent<CanvasGroup>();
        _modalGroup.alpha = 1f;
        _modalGroup.blocksRaycasts = true;

        var shadow = CreateImage("Shadow", _modalRt, Vector2.zero, Vector2.one, new Vector2(20f, 24f), new Vector2(0f, -8f), ShadowColor, round);
        shadow.raycastTarget = false;

        var border = CreateImage("Border", _modalRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, BorderColor, round);
        border.raycastTarget = true;

        var fill = CreateImage("CardFill", _modalRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, CardColor, round);
        fill.rectTransform.offsetMin = new Vector2(2f, 2f);
        fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
        fill.raycastTarget = true;

        var accent = CreateImage("AccentTop", _modalRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(488f, 3f), new Vector2(0f, -12f), AccentColor, null);
        accent.raycastTarget = false;

        BuildIcon(font, round);

        var titleRt = CreateRect("TitleText", _modalRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-64f, 44f), new Vector2(0f, -91f));
        _titleText = CreateLabel(titleRt, "EMİN MİSİN?", font, 34f, FontStyles.Bold, TitleColor);

        var messageRt = CreateRect("MessageText", _modalRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-56f, 48f), new Vector2(0f, -143f));
        _messageText = CreateLabel(messageRt, "Eğitimi bitirip sonuç ekranına geçeceksin.", font, 18f, FontStyles.Normal, MessageColor);

        var row = CreateRect("ButtonsRow", _modalRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(376f, 54f), new Vector2(0f, -208f));
        _yesLabel = CreateButton(row, "YesButton", "EVET", new Vector2(-98f, 0f), ConfirmColor, ConfirmHover, ConfirmPressed, Color.white, Raise, font);
        _noLabel = CreateButton(row, "NoButton", "HAYIR", new Vector2(98f, 0f), CancelColor, CancelHover, CancelPressed, CancelTextColor, Hide, font);

        exitPanel.SetActive(false);
    }

    void BuildIcon(TMP_FontAsset font, Sprite round)
    {
        var holder = CreateRect("IconHolder", _modalRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(36f, 36f), new Vector2(0f, -43f));

        var ring = CreateImage("Ring", holder, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, AccentColor, round);
        ring.raycastTarget = false;

        var disc = CreateImage("Disc", holder, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, CardInnerColor, round);
        disc.rectTransform.offsetMin = new Vector2(2f, 2f);
        disc.rectTransform.offsetMax = new Vector2(-2f, -2f);
        disc.raycastTarget = false;

        var markRt = CreateRect("Mark", holder, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        CreateLabel(markRt, "!", font, 20f, FontStyles.Bold, AccentColor);
    }

    void ApplyPcCopy()
    {
        if (_titleText == null)
            return;

        bool turkish = LanguageManager.CurrentLanguage == Language.Turkish;
        if (turkish)
        {
            _titleText.text = "EMİN MİSİN?";
            _messageText.text = "Eğitimi bitirip sonuç ekranına geçeceksin.";
            _yesLabel.text = "EVET";
            _noLabel.text = "HAYIR";
            return;
        }

        _titleText.text = "ARE YOU SURE?";
        _messageText.text = "You will finish the training and move to the result screen.";
        _yesLabel.text = "YES";
        _noLabel.text = "NO";
    }

    TextMeshProUGUI CreateButton(RectTransform parent, string name, string label, Vector2 pos, Color normal, Color hover, Color pressed, Color textColor, UnityEngine.Events.UnityAction onClick, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(180f, 54f);
        rt.anchoredPosition = pos;

        var image = go.GetComponent<Image>();
        image.sprite = GetRoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = hover;
        colors.pressedColor = pressed;
        colors.selectedColor = hover;
        colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        var textRt = CreateRect("Label", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return CreateLabel(textRt, label, font, 20f, FontStyles.Bold, textColor);
    }

    static TextMeshProUGUI CreateLabel(RectTransform parent, string text, TMP_FontAsset font, float size, FontStyles style, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 anchored)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        if (anchorMin == Vector2.zero && anchorMax == Vector2.one && size == Vector2.zero)
        {
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.sizeDelta = size;
            rt.anchoredPosition = anchored;
        }

        return rt;
    }

    static Image CreateImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 anchored, Color color, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        if (anchorMin == Vector2.zero && anchorMax == Vector2.one)
        {
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            if (size != Vector2.zero)
                rt.sizeDelta = size;
            rt.anchoredPosition = anchored;
        }
        else
        {
            rt.sizeDelta = size;
            rt.anchoredPosition = anchored;
        }

        var image = go.GetComponent<Image>();
        image.color = color;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }

        return image;
    }

    static TMP_FontAsset ResolveFont()
    {
        var texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null || texts[i].font == null)
                continue;
            if (IsUnderNamedParent(texts[i].transform, "VRCanvas (2)"))
                continue;
            return texts[i].font;
        }

        return TMP_Settings.defaultFontAsset;
    }

    static Sprite GetRoundedSprite()
    {
        if (_roundedSprite != null)
            return _roundedSprite;

        const int size = 64;
        const int radius = 14;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = RoundedAlpha(x, y, size, radius);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        _roundedSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        _roundedSprite.hideFlags = HideFlags.HideAndDontSave;
        return _roundedSprite;
    }

    static float RoundedAlpha(int x, int y, int size, int radius)
    {
        int dx = 0;
        int dy = 0;
        if (x < radius)
            dx = radius - x;
        else if (x >= size - radius)
            dx = x - (size - 1 - radius);

        if (y < radius)
            dy = radius - y;
        else if (y >= size - radius)
            dy = y - (size - 1 - radius);

        if (dx == 0 || dy == 0)
            return 1f;

        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Clamp01(radius + 0.5f - dist);
    }

    void Start()
    {
        if (exitPanel != null)
            exitPanel.SetActive(false);
        if (trPanel != null)
            trPanel.SetActive(false);
        if (enPanel != null)
            enPanel.SetActive(false);
    }

    void OnEnable()
    {
        if (preOnGameFinishEvent != null)
            preOnGameFinishEvent.OnRaised += Show;
    }

    void OnDisable()
    {
        if (preOnGameFinishEvent != null)
            preOnGameFinishEvent.OnRaised -= Show;

        bool closing = _closing;
        StopIntroTweens();
        if (closing)
            FinishDismiss();
    }

    void Update()
    {
        if (FirePlatformRuntime.CurrentMode == AppMode.XR)
            return;

        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (_open && !_finished && !_closing && keyboard.escapeKey.wasPressedThisFrame)
        {
            Hide();
            return;
        }

        if (_finished || _open || _closing)
            return;

        if (keyboard.tabKey.wasPressedThisFrame)
            Show();
    }

    public void Show()
    {
        if (_finished || _open || _closing)
            return;
        if (FirePlatformRuntime.CurrentMode == AppMode.XR && !IsUnderNamedParent(transform, "VRCanvas (2)"))
            return;
        if (FirePlatformRuntime.CurrentMode != AppMode.XR && IsUnderNamedParent(transform, "VRCanvas (2)"))
            return;

        SuppressPcGameplayInput();
        UnlockCursorIfPc();

        if (exitPanel != null)
            exitPanel.SetActive(true);

        bool turkish = LanguageManager.CurrentLanguage == Language.Turkish;
        if (trPanel != null)
            trPanel.SetActive(turkish);
        if (enPanel != null)
            enPanel.SetActive(!turkish);

        ApplyPcCopy();
        PlayIntroIfPc();
        _open = true;
    }

    public void Hide()
    {
        if (_finished || _closing)
            return;

        if (CanAnimatePcModal())
        {
            PlayOutro();
            return;
        }

        CloseVisuals();
        RestorePcGameplayInput();
        RestoreCursorIfPc();
    }

    public void Raise()
    {
        if (_finished)
            return;

        _finished = true;
        CloseVisuals();
        onGameFinishEvent?.Raise();
    }

    void CloseVisuals()
    {
        _open = false;
        StopIntroTweens();
        if (exitPanel != null)
            exitPanel.SetActive(false);
        if (trPanel != null)
            trPanel.SetActive(false);
        if (enPanel != null)
            enPanel.SetActive(false);
    }

    bool CanAnimatePcModal()
    {
        return FirePlatformRuntime.CurrentMode != AppMode.XR && _modalRt != null && _backdropGroup != null;
    }

    void PlayIntroIfPc()
    {
        if (!CanAnimatePcModal())
            return;

        StopIntroTweens();

        if (_modalGroup != null)
        {
            _modalGroup.alpha = 1f;
            _modalGroup.interactable = true;
            _modalGroup.blocksRaycasts = true;
        }

        _backdropGroup.alpha = 0f;
        _backdropTween = Tween.Custom(this, 0f, 1f, ShowDuration, (t, a) =>
        {
            if (t._backdropGroup != null)
                t._backdropGroup.alpha = a;
        });

        _modalRt.localScale = Vector3.one * 0.94f;
        _modalTween = Tween.Scale(_modalRt, Vector3.one, ShowDuration, Ease.OutCubic);
    }

    void PlayOutro()
    {
        _closing = true;
        StopIntroTweens();

        if (_modalGroup != null)
        {
            _modalGroup.interactable = false;
            _modalGroup.blocksRaycasts = false;
        }

        float backdropFrom = _backdropGroup != null ? _backdropGroup.alpha : 1f;
        _backdropTween = Tween.Custom(this, backdropFrom, 0f, HideDuration, (t, a) =>
        {
            if (t._backdropGroup != null)
                t._backdropGroup.alpha = a;
        });

        if (_modalGroup != null)
        {
            _modalFadeTween = Tween.Custom(this, _modalGroup.alpha, 0f, HideDuration, (t, a) =>
            {
                if (t._modalGroup != null)
                    t._modalGroup.alpha = a;
            });
        }

        _modalRt.localScale = Vector3.one;
        _modalTween = Tween.Scale(_modalRt, Vector3.one * 0.94f, HideDuration, Ease.InCubic)
            .OnComplete(this, target => target.FinishDismiss());
    }

    void FinishDismiss()
    {
        if (!_closing)
            return;

        _closing = false;
        if (_finished)
            return;

        CloseVisuals();
        RestorePcGameplayInput();
        RestoreCursorIfPc();
    }

    void StopIntroTweens()
    {
        _backdropTween.Stop();
        _modalTween.Stop();
        _modalFadeTween.Stop();
    }

    void SuppressPcGameplayInput()
    {
        if (FirePlatformRuntime.CurrentMode == AppMode.XR || _gameplaySuppressed)
            return;

        var inputManager = FindFirstObjectByType<InputManager>(FindObjectsInactive.Include);
        _playerInputActions = inputManager != null ? inputManager.InputActions : null;
        _gameplayContext = inputManager != null ? inputManager.GetPcGameplayContext() : null;

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerController player = players[i];
            if (player == null)
                continue;

            _suppressedPlayers.Add(player);
            _savedPlayerInputEnabled.Add(player.InputEnabled);
            player.SetInputEnabled(false);
        }

        _gameplayContext?.DisableAllInputs();
        SuspendEnabledBehaviours<PcInteractionInput>();
        SuspendEnabledBehaviours<InteractableController>();
        _gameplaySuppressed = true;
    }

    void RestorePcGameplayInput()
    {
        if (FirePlatformRuntime.CurrentMode == AppMode.XR || !_gameplaySuppressed)
            return;

        _gameplayContext?.EnableAllInputs();

        for (int i = 0; i < _suppressedPlayers.Count; i++)
        {
            PlayerController player = _suppressedPlayers[i];
            if (player == null)
                continue;

            bool wasEnabled = _savedPlayerInputEnabled[i];
            player.SetInputEnabled(wasEnabled);
        }

        ResyncHeldLocomotion();

        for (int i = 0; i < _suspendedGameplayBehaviours.Count; i++)
        {
            Behaviour behaviour = _suspendedGameplayBehaviours[i];
            if (behaviour != null)
                behaviour.enabled = true;
        }

        _suppressedPlayers.Clear();
        _savedPlayerInputEnabled.Clear();
        _suspendedGameplayBehaviours.Clear();
        _gameplayContext = null;
        _playerInputActions = null;
        _gameplaySuppressed = false;
    }

    void ResyncHeldLocomotion()
    {
        if (_gameplayContext == null || _playerInputActions == null)
            return;

        bool anyEnabled = false;
        for (int i = 0; i < _savedPlayerInputEnabled.Count; i++)
        {
            if (_savedPlayerInputEnabled[i])
            {
                anyEnabled = true;
                break;
            }
        }

        if (!anyEnabled)
            return;

        _gameplayContext.MoveInputEvent?.Raise(_playerInputActions.Gameplay.Move.ReadValue<Vector2>());
        _gameplayContext.SprintInputEvent?.Raise(_playerInputActions.Gameplay.Sprint.IsPressed());
    }

    void SuspendEnabledBehaviours<T>() where T : Behaviour
    {
        var found = FindObjectsByType<T>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            T behaviour = found[i];
            if (behaviour == null || !behaviour.enabled)
                continue;

            behaviour.enabled = false;
            _suspendedGameplayBehaviours.Add(behaviour);
        }
    }

    void UnlockCursorIfPc()
    {
        if (FirePlatformRuntime.CurrentMode == AppMode.XR)
            return;

        _savedLock = Cursor.lockState;
        _savedCursorVisible = Cursor.visible;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void RestoreCursorIfPc()
    {
        if (FirePlatformRuntime.CurrentMode == AppMode.XR)
            return;

        Cursor.lockState = _savedLock;
        Cursor.visible = _savedCursorVisible;
    }
}
