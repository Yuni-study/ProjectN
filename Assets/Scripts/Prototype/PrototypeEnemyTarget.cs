using UnityEngine;
using System;

public sealed class PrototypeEnemyTarget : MonoBehaviour
{
    public double MaxHealth { get; private set; }
    public double Health { get; private set; }
    public bool IsDefeated { get; private set; }
    public bool IsTrainingDummy { get; private set; }
    public static int DefeatedCount { get; private set; }

    private Renderer bodyRenderer;
    private Color originalColor;
    private string targetName;

    public void Initialize(string displayName, float health, bool isTrainingDummy = false)
    {
        targetName = displayName;
        MaxHealth = health;
        Health = health;
        IsTrainingDummy = isTrainingDummy;
        bodyRenderer = GetComponent<Renderer>();
        originalColor = bodyRenderer.material.color;
    }

    public void TakeDamage(float damage, bool isCritical)
    {
        if (IsDefeated)
        {
            return;
        }

        double appliedDamage = isCritical ? damage * 2d : damage;
        Health = IsTrainingDummy ? Math.Max(1d, Health - appliedDamage) : Math.Max(0d, Health - appliedDamage);
        bodyRenderer.material.color = Color.white;
        CancelInvoke(nameof(ResetFlash));
        Invoke(nameof(ResetFlash), 0.08f);

        if (Health <= 0f && !IsTrainingDummy)
        {
            IsDefeated = true;
            DefeatedCount++;
            gameObject.SetActive(false);
        }
    }

    private void ResetFlash()
    {
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = originalColor;
        }
    }

    public string GetDisplayName() => targetName;
}
