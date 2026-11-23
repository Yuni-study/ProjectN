using UnityEngine;

public static class PrototypeCombatFeedback
{
    private static Material impactMaterial;

    public static void PlayShot(Vector3 position, int slot)
    {
        PrototypeAudioDirector.PlayShot(position, slot);
    }

    public static void PlayImpact(Vector3 position, bool critical)
    {
        PrototypeAudioDirector.PlayImpact(position, critical);
    }

    public static void SpawnImpact(Vector3 position, Vector3 normal, bool hitEnemy, bool critical)
    {
        GameObject effectObject = new GameObject("Bullet Impact Sparks");
        effectObject.transform.position = position + normal * 0.015f;
        if (normal.sqrMagnitude > 0.001f)
        {
            effectObject.transform.rotation = Quaternion.LookRotation(normal);
        }

        ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
        Color sparkColor = critical
            ? new Color(1f, 0.9f, 0.25f, 1f)
            : hitEnemy
                ? new Color(1f, 0.36f, 0.12f, 1f)
                : new Color(1f, 0.76f, 0.35f, 1f);
        main.startColor = sparkColor;
        main.maxParticles = 12;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 7) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.035f;
        shape.randomDirectionAmount = 0.85f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(sparkColor, 0f), new GradientColorKey(sparkColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.material = GetImpactMaterial();
        particles.Play();
        Object.Destroy(effectObject, 0.3f);
    }

    private static Material GetImpactMaterial()
    {
        if (impactMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            impactMaterial = new Material(shader);
        }

        return impactMaterial;
    }
}
