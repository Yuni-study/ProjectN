using UnityEngine;
using System;
using System.Collections.Generic;

public sealed class PrototypeEnemyTarget : MonoBehaviour
{
    public double MaxHealth { get; private set; }
    public double Health { get; private set; }
    public bool IsDefeated { get; private set; }
    public bool IsTrainingDummy { get; private set; }
    public bool IsThreateningPlayer { get; private set; }
    public bool IsThreateningCharacter(PrototypeCharacter character) => threatenedCharacter == character;
    public bool IsMarked => Time.time < markedUntil;
    public static int DefeatedCount { get; private set; }
    public event Action<PrototypeEnemyTarget> Hit;
    public event Action<PrototypeEnemyTarget, PrototypeCharacter> HitByCharacter;
    public event Action<PrototypeEnemyTarget> Defeated;

    private Renderer[] bodyRenderers;
    private Color[] originalColors;
    private Vector3 originalScale;
    private string targetName;
    private float markedUntil;
    private PrototypeCharacter threatenedCharacter;
    private bool threatActive;
    private readonly Dictionary<PrototypeCharacter, float> damageByCharacter = new Dictionary<PrototypeCharacter, float>();
    public PrototypeCharacter TopDamageDealer { get; private set; }

    public static void ResetDefeatedCount() => DefeatedCount = 0;

    public void Initialize(string displayName, float health, bool isTrainingDummy = false)
    {
        targetName = displayName;
        MaxHealth = health;
        Health = health;
        IsTrainingDummy = isTrainingDummy;
        bodyRenderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[bodyRenderers.Length];
        for (int i = 0; i < bodyRenderers.Length; i++) originalColors[i] = bodyRenderers[i].material.color;
        originalScale = transform.localScale;
    }

    public void TakeDamage(float damage, bool isCritical, PrototypeCharacter attacker = null)
    {
        RegisterHit(attacker);
        ApplyResolvedDamage(damage, isCritical, attacker);
    }

    public void RegisterHit(PrototypeCharacter attacker = null)
    {
        if (IsDefeated)
        {
            return;
        }

        Hit?.Invoke(this);
        HitByCharacter?.Invoke(this, attacker);
    }

    public void ApplyMark(float duration)
    {
        markedUntil = Mathf.Max(markedUntil, Time.time + duration);
    }

    public void SetDisplayColor(Color color)
    {
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            originalColors[i] = color;
            bodyRenderers[i].material.color = color;
        }
    }

    public void SetThreateningPlayer(bool threatening)
    {
        IsThreateningPlayer = threatening;
        if (!threatening)
        {
            threatenedCharacter = null;
        }
    }

    public void SetThreateningCharacter(PrototypeCharacter character, bool threatening, bool isPlayerCharacter)
    {
        threatenedCharacter = threatening ? character : null;
        IsThreateningPlayer = threatening && isPlayerCharacter;
        threatActive = threatening;
        SetRenderColor(threatActive ? new Color(1f, 0.28f, 0.12f) : (Color?)null);
    }

    public void ApplyResolvedDamage(float damage, bool isCritical, PrototypeCharacter attacker = null)
    {
        if (IsDefeated)
        {
            return;
        }

        double appliedDamage = damage * (IsMarked ? 1.15d : 1d);
        if (isCritical)
        {
            appliedDamage *= 2d;
        }

        if (attacker != null && attacker.IsAlive && !IsTrainingDummy)
        {
            float dealtDamage = (float)Math.Min(Health, appliedDamage);
            damageByCharacter.TryGetValue(attacker, out float previousDamage);
            float totalDamage = previousDamage + dealtDamage;
            damageByCharacter[attacker] = totalDamage;
            if (TopDamageDealer == null || totalDamage > damageByCharacter[TopDamageDealer])
            {
                TopDamageDealer = attacker;
            }
        }

        Health = IsTrainingDummy ? Math.Max(1d, Health - appliedDamage) : Math.Max(0d, Health - appliedDamage);
        SetRenderColor(isCritical ? new Color(1f, 0.83f, 0.22f) : Color.white);
        transform.localScale = originalScale * (isCritical ? 1.08f : 1.045f);
        CancelInvoke(nameof(ResetFlash));
        Invoke(nameof(ResetFlash), 0.08f);

        if (Health <= 0f && !IsTrainingDummy)
        {
            IsDefeated = true;
            DefeatedCount++;
            Defeated?.Invoke(this);
            gameObject.SetActive(false);
        }
    }

    public PrototypeCharacter GetTopDamageDealer()
    {
        PrototypeCharacter top = null;
        float highestDamage = 0f;
        foreach (KeyValuePair<PrototypeCharacter, float> entry in damageByCharacter)
        {
            if (entry.Key != null && entry.Key.IsAlive && entry.Value > highestDamage)
            {
                top = entry.Key;
                highestDamage = entry.Value;
            }
        }

        return top;
    }

    private void ResetFlash()
    {
        SetRenderColor(threatActive ? new Color(1f, 0.28f, 0.12f) : (Color?)null);

        transform.localScale = originalScale;
    }

    private void SetRenderColor(Color? color)
    {
        if (bodyRenderers == null) return;
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] != null) bodyRenderers[i].material.color = color ?? originalColors[i];
        }
    }

    public string GetDisplayName() => targetName;
}
