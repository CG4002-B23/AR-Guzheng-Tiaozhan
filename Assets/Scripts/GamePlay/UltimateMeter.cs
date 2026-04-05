using UnityEngine;
using UnityEngine.UI;
using System;

public class UltimateMeterManager : MonoBehaviour
{
    [Header("UI References")]
    public Slider chargeSlider;
    [Tooltip("Drag the 'Fill' image from your Slider child objects here")]
    public Image fillImage; 
    public RectTransform gaugeRectTransform; // The UI element to pulsate
    public GameObject fireEffectObject; 

    [Header("Color Settings")]
    public Color normalColor = Color.white;
    public Color fullColor = new Color(1f, 0.6f, 0f); // Bright orangey yellow

    [Header("Charge Settings")]
    public int maxCharge = 100;
    public int currentCharge = 0;

    [Header("Pulsate Settings")]
    public float pulseSpeed = 5f;
    [Tooltip("How much larger the meter gets when pulsing (0.1 = 10% larger)")]
    public float pulseScale = 0.1f; 
    private Vector3 originalScale;

    public bool IsFull => currentCharge >= maxCharge;
    public event Action OnUltimateReady;

    void Awake()
    {
        if (gaugeRectTransform != null)
            originalScale = gaugeRectTransform.localScale;

        if (fillImage != null)
            fillImage.color = normalColor;
            
        UpdateUI();
    }

    void Update()
    {
        if (IsFull && gaugeRectTransform != null)
        {
            // Normalize the Sine wave from (-1 to 1) into (0 to 1) for smooth scaling
            float scaleModifier = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f; 
            float currentScale = 1f + (scaleModifier * pulseScale);
            
            gaugeRectTransform.localScale = originalScale * currentScale;
        }
    }

    public void AddCharge(int amount)
    {
        if (IsFull) return;

        currentCharge += amount;
        if (currentCharge >= maxCharge)
        {
            currentCharge = maxCharge;
            OnMeterFull();
        }
        
        UpdateUI();
    }

    public void ConsumeUltimate()
    {
        currentCharge = 0;
        
        // Reset scale, color, and disable effects
        if (gaugeRectTransform != null)
            gaugeRectTransform.localScale = originalScale;
            
        if (fillImage != null)
            fillImage.color = normalColor;
            
        if (fireEffectObject != null)
            fireEffectObject.SetActive(false);

        UpdateUI();
    }

    private void OnMeterFull()
    {
        // Change color to orangey-yellow and trigger the fire effect
        if (fillImage != null)
            fillImage.color = fullColor;

        if (fireEffectObject != null)
            fireEffectObject.SetActive(true);
            
        Debug.Log("<color=orange>Ultimate Meter is FULL!</color>");
        OnUltimateReady?.Invoke();
    }

    private void UpdateUI()
    {
        if (chargeSlider != null)
        {
            chargeSlider.maxValue = maxCharge;
            chargeSlider.value = currentCharge;
        }
    }
}