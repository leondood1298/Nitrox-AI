using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures.GameLogic;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NitroxClient.MonoBehaviours.Gui.HUD;

public class RemotePlayerVitals : MonoBehaviour
{
    private static readonly Color OXYGEN_BAR_COLOR = new(0.168f, 0.666f, 0.60f, 1.0f);
    private static readonly Color OXYGEN_BAR_BORDER_COLOR = new(0.227f, 0.949f, 0.969f, 1.0f);
    private static readonly Color HEALTH_BAR_COLOR = new(0.859f, 0.373f, 0.251f, 1.0f);
    private static readonly Color HEALTH_BAR_BORDER_COLOR = new(0.824f, 0.651f, 0.424f, 1.0f);
    private static readonly Color FOOD_BAR_COLOR = new(0.965f, 0.655f, 0.149f, 1.0f);
    private static readonly Color FOOD_BAR_BORDER_COLOR = new(0.957f, 0.914f, 0.251f, 1.0f);
    private static readonly Color WATER_BAR_COLOR = new(0.212f, 0.663f, 0.855f, 1.0f);
    private static readonly Color WATER_BAR_BORDER_COLOR = new(0.227f, 0.949f, 0.969f, 1.0f);
    /// <summary>
    ///     The canvas turns to the camera about the middle of the bars. Turning about the feet, as before, swung the bars
    ///     around as the camera moved (upstream issue 2158).
    /// </summary>
    private const float CANVAS_HEIGHT = 0.29f;

    /// <summary>
    ///     Drawn after the game's transparent effects, so that clouds and particles behind the bars do not hide them
    ///     (upstream issue 2158); walls still do.
    /// </summary>
    private const int OVERLAY_RENDER_QUEUE = 4000;

    private readonly List<Material> overlayMaterials = [];
    private SubnauticaGameMode gameMode;
    private bool showsHealthAndOxygen = true;
    private bool showsFoodAndWater = true;
    private Canvas canvas;
    private Bar foodBar;
    private Bar healthBar;
    private Bar oxygenBar;

    private string playerName;

    private Bar waterBar;

    /// <summary>
    ///     Creates a player vitals UI elements for the player id.
    /// </summary>
    public static RemotePlayerVitals CreateForPlayer(RemotePlayer remotePlayer)
    {
        RemotePlayerVitals vitals = new GameObject("RemotePlayerVitals").AddComponent<RemotePlayerVitals>();

        try
        {
            vitals.canvas = vitals.CreateCanvas(remotePlayer.Body.transform);

            vitals.playerName = remotePlayer.PlayerName;
            vitals.CreatePlayerName(vitals.canvas);
            vitals.CreateStats(vitals.canvas);
        } catch (Exception ex)
        {
            Log.Error(ex, $"Encountered an error while creating vitals for player {remotePlayer.SessionId}, destroying them.");
            Destroy(vitals.gameObject);
            return null;
        }

        return vitals;
    }

    public void SetGameMode(SubnauticaGameMode mode)
    {
        gameMode = mode;
        UpdateBarsVisibility(true);
    }

    private void UpdateBarsVisibility(bool force)
    {
        EnsureBars();
        Camera camera = Camera.main;
        float distance = camera ? Vector3.Distance(camera.transform.position, transform.position) : 0f;
        bool healthAndOxygen = RemotePlayerVitalsRules.ShowsHealthAndOxygen(gameMode, distance);
        bool foodAndWater = RemotePlayerVitalsRules.ShowsFoodAndWater(gameMode, distance);
        if (!force && healthAndOxygen == showsHealthAndOxygen && foodAndWater == showsFoodAndWater)
        {
            return;
        }
        showsHealthAndOxygen = healthAndOxygen;
        showsFoodAndWater = foodAndWater;
        oxygenBar.SetVisible(healthAndOxygen);
        healthBar.SetVisible(healthAndOxygen);
        foodBar.SetVisible(foodAndWater);
        waterBar.SetVisible(foodAndWater);
    }

    public void SetOxygen(float oxygen, float maxOxygen)
    {
        EnsureBars();
        oxygenBar.SetTargetValue(oxygen);
        oxygenBar.SetMaxValue(maxOxygen);
    }

    public void SetHealth(float health)
    {
        EnsureBars();
        healthBar.SetTargetValue(health);
    }

    public void SetFood(float food)
    {
        EnsureBars();
        foodBar.SetTargetValue(food);
    }

    public void SetWater(float water)
    {
        EnsureBars();
        waterBar.SetTargetValue(water);
    }

    public void LateUpdate()
    {
        UpdateBarsVisibility(false);
        oxygenBar.UpdateVisual();
        healthBar.UpdateVisual();
        foodBar.UpdateVisual();
        waterBar.UpdateVisual();

        // Make canvas face camera.
        Camera camera = Camera.main;
        if (canvas && camera)
        {
            canvas.transform.forward = camera.transform.forward;
        }
    }

    private void EnsureBars()
    {
        // TODO: find what causes bars to become disposed while the player still exists
        if (oxygenBar.IsDisposed() || healthBar.IsDisposed() || foodBar.IsDisposed() || waterBar.IsDisposed())
        {
            oxygenBar.Dispose();
            healthBar.Dispose();
            foodBar.Dispose();
            waterBar.Dispose();
            CreateStats(canvas);
            oxygenBar.SetVisible(showsHealthAndOxygen);
            healthBar.SetVisible(showsHealthAndOxygen);
            foodBar.SetVisible(showsFoodAndWater);
            waterBar.SetVisible(showsFoodAndWater);
        }
    }

    /// <summary>
    ///     Gives every graphic of the canvas a copy of its material that draws in the overlay queue.
    /// </summary>
    private void DrawOverEffects(GameObject root)
    {
        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            Material material = new(graphic.materialForRendering) { renderQueue = OVERLAY_RENDER_QUEUE };
            overlayMaterials.Add(material);
            graphic.material = material;
        }
    }

    private Canvas CreateCanvas(Transform playerTransform)
    {
        // Canvas
        transform.SetParent(playerTransform, false);
        transform.localPosition = new Vector3(0, CANVAS_HEIGHT, 0);

        Canvas vitalsCanvas = gameObject.AddComponent<Canvas>();
        vitalsCanvas.renderMode = RenderMode.WorldSpace;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100;

        return vitalsCanvas;
    }

    private void CreateStats(Canvas canvas)
    {
        // uGUI is a script at the topmost of the uGUI(Clone) object which contains the uGUI_ classes whe're looking for
        uGUI uGUI = uGUI.main;
        if (!uGUI)
        {
            throw new NullReferenceException($"[{nameof(RemotePlayerVitals)}] Couldn't find uGUI main instance when creating vitals");
        }
        healthBar = CreateBar(uGUI.GetComponentInChildren<uGUI_HealthBar>(true), canvas);
        oxygenBar = CreateBar(uGUI.GetComponentInChildren<uGUI_OxygenBar>(true), canvas);
        foodBar = CreateBar(uGUI.GetComponentInChildren<uGUI_FoodBar>(true), canvas);
        waterBar = CreateBar(uGUI.GetComponentInChildren<uGUI_WaterBar>(true), canvas);
        healthBar.DrawOverEffects(DrawOverEffects);
        oxygenBar.DrawOverEffects(DrawOverEffects);
        foodBar.DrawOverEffects(DrawOverEffects);
        waterBar.DrawOverEffects(DrawOverEffects);
    }

    private Bar CreateBar<T>(T barBehaviour, Canvas canvas) where T : MonoBehaviour
    {
        GameObject originalBar = barBehaviour.gameObject;
        GameObject cloned = Instantiate(originalBar, canvas.transform, true);

        uGUI_CircularBar newBar = cloned.GetComponentInChildren<uGUI_CircularBar>(true);
        newBar.texture = originalBar.GetComponentInChildren<uGUI_CircularBar>(true).texture;
        newBar.overlay = originalBar.GetComponentInChildren<uGUI_CircularBar>(true).overlay;

        cloned.transform.localRotation = Quaternion.identity;
        // From uGUI_OxygenBar.Awake
        if (cloned.TryGetComponentInChildren(out TextMeshProUGUI text, true))
        {
            text.enableCulling = true;
        }

        switch (barBehaviour)
        {
            case uGUI_HealthBar:
                newBar.color = HEALTH_BAR_COLOR;
                newBar.borderColor = HEALTH_BAR_BORDER_COLOR;
                cloned.transform.localPosition = new Vector3(-0.05f, 0.33f - CANVAS_HEIGHT, 0f);
                cloned.transform.localScale = new Vector3(0.0014f, 0.0014f, 1f);
                break;
            case uGUI_OxygenBar:
                newBar.color = OXYGEN_BAR_COLOR;
                newBar.borderColor = OXYGEN_BAR_BORDER_COLOR;
                cloned.transform.localPosition = new Vector3(0.05f, 0.33f - CANVAS_HEIGHT, 0f);
                cloned.transform.localScale = new Vector3(0.0006f, 0.0006f, 1f);
                // PulseWave is only present for uGUI_OxygenBar
                Destroy(cloned.FindChild("PulseWave"));
                break;
            case uGUI_FoodBar:
                newBar.color = FOOD_BAR_COLOR;
                newBar.borderColor = FOOD_BAR_BORDER_COLOR;
                cloned.transform.localPosition = new Vector3(-0.05f, 0.255f - CANVAS_HEIGHT, 0f);
                cloned.transform.localScale = new Vector3(0.0014f, 0.0014f, 1f);
                break;
            case uGUI_WaterBar:
                newBar.color = WATER_BAR_COLOR;
                newBar.borderColor = WATER_BAR_BORDER_COLOR;
                cloned.transform.localPosition = new Vector3(0.05f, 0.255f - CANVAS_HEIGHT, 0f);
                cloned.transform.localScale = new Vector3(0.0014f, 0.0014f, 1f);
                break;
            default:
                Log.Info($"Unhandled bar type: {barBehaviour.GetType()}");
                break;
        }

        Destroy(cloned.FindChild("PulseHalo"));
        Destroy(cloned.GetComponent<T>());
        cloned.SetActive(true);
        return new Bar(cloned, 100f, 100f, 0.1f);
    }

    private void CreatePlayerName(Canvas canvas)
    {
        // Text
        GameObject nameObject = new("RemotePlayerName");
        nameObject.transform.parent = canvas.transform;

        Text nameText = nameObject.AddComponent<Text>();
        nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        nameText.text = playerName;
        Transform nameTransform = nameText.transform;
        nameTransform.localScale = new Vector3(0.015f, 0.015f, 1f);
        nameTransform.rotation = canvas.transform.rotation;
        nameText.fontSize = 14;
        nameText.alignment = TextAnchor.MiddleCenter;

        // Text position
        RectTransform namePosition = nameObject.GetComponent<RectTransform>();
        namePosition.localPosition = new Vector3(0, 0.4f - CANVAS_HEIGHT, 0);
        DrawOverEffects(nameObject);
        namePosition.sizeDelta = new Vector2(200, 100);
    }

    private void OnDestroy()
    {
        // Must stay optional in case the destroy originates from a broken object
        oxygenBar?.Dispose();
        healthBar?.Dispose();
        foodBar?.Dispose();
        waterBar?.Dispose();
        foreach (Material material in overlayMaterials)
        {
            Destroy(material);
        }
        overlayMaterials.Clear();
    }

    private class Bar : IDisposable
    {
        private readonly GameObject gameObject;
        private readonly uGUI_CircularBar circularBar;
        private readonly TextMeshProUGUI text;
        
        private bool isDisposed;
        private float vel;
        private float current;
        private float target;
        private float maximum;
        private float smoothTime;

        public Bar(GameObject gameObject, float current, float maximum, float smoothTime)
        {
            this.gameObject = gameObject;
            this.current = current;
            target = current;
            this.maximum = maximum;
            this.smoothTime = smoothTime;

            circularBar = gameObject.GetComponentInChildren<uGUI_CircularBar>(true);
            // text can be null
            text = gameObject.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        public void SetTargetValue(float value)
        {
            ThrowIfDisposed();
            target = value;
        }

        public void SetMaxValue(float maxValue)
        {
            ThrowIfDisposed();
            maximum = maxValue;
        }

        public void UpdateVisual()
        {
            ThrowIfDisposed();

            // Adapted from uGUI_OxygenBar
            float percentage = Mathf.Clamp01(target / maximum);
            current = Mathf.SmoothDamp(current, percentage, ref vel, smoothTime);
            circularBar.value = current;
            if (text)
            {
                text.SetText(IntStringCache.GetStringForInt(Mathf.RoundToInt(target)));
            }
        }

        public void DrawOverEffects(Action<GameObject> apply) => apply(gameObject);

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            Destroy(gameObject);
        }

        public bool IsDisposed()
        {
            return isDisposed;
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException("Tried to update visual on a disposed player stat.");
            }
        }
    }
}
