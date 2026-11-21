using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The heart in the top-left corner. Stage 0 is a whole heart (main0); each hit
/// moves it one frame along until the last frame, where the heart breaks.
/// It beats on its own, faster as it gets weaker, and shakes/flashes when hit.
/// </summary>
[RequireComponent(typeof(Image))]
public class HealthBar : MonoBehaviour
{
    public Sprite[] stages;

    Image image;
    RectTransform rt;
    Vector2 basePos;
    int stage;
    bool broken;

    float punch;       // extra scale that decays
    float shake;       // pixel shake that decays
    float tint;        // red flash that decays
    float heal;        // pink glow that decays
    float beatPhase;
    Coroutine breakRoutine;

    void Awake()
    {
        image = GetComponent<Image>();
        image.preserveAspect = true;
        rt = (RectTransform)transform;
        basePos = rt.anchoredPosition;
    }

    public void Init(Sprite[] sprites)
    {
        stages = sprites;
        stage = 0;
        broken = false;
        image.sprite = stages[0];
        image.color = Color.white;
    }

    public void SetStage(int newStage)
    {
        if (stages == null || stages.Length == 0) return;
        newStage = Mathf.Clamp(newStage, 0, stages.Length - 1);
        int old = stage;
        stage = newStage;

        if (newStage == stages.Length - 1)
        {
            if (breakRoutine != null) StopCoroutine(breakRoutine);
            breakRoutine = StartCoroutine(Break());
            return;
        }

        broken = false;
        image.sprite = stages[newStage];
        if (newStage > old)
        {
            punch = 0.35f;
            shake = 14f;
            tint = 1f;
        }
        else if (newStage < old)
        {
            punch = 0.25f;
            heal = 1f;
        }
    }

    public void Pulse(float amount) => punch = Mathf.Max(punch, amount);

    IEnumerator Break()
    {
        // Show the cracked frame shaking hard, then snap to the broken heart.
        if (stages.Length > 1) image.sprite = stages[stages.Length - 2];
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            shake = Mathf.Lerp(6f, 22f, t / 0.6f);
            tint = 1f;
            yield return null;
        }
        image.sprite = stages[stages.Length - 1];
        broken = true;
        punch = 0.5f;
        shake = 30f;
        tint = 0f;
        breakRoutine = null;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // Heartbeat: a "lub-dub" that speeds up as the heart weakens.
        float beat = 0f;
        if (!broken && stages != null && stages.Length > 1)
        {
            float weakness = stage / (float)(stages.Length - 2 > 0 ? stages.Length - 2 : 1);
            float bpm = Mathf.Lerp(60f, 140f, weakness);
            beatPhase = (beatPhase + dt * bpm / 60f) % 1f;
            beat = Bump(beatPhase, 0.0f) * 0.07f + Bump(beatPhase, 0.18f) * 0.045f;
        }

        punch = Mathf.MoveTowards(punch, 0f, dt * 1.2f);
        shake = Mathf.MoveTowards(shake, 0f, dt * 40f);
        tint = Mathf.MoveTowards(tint, 0f, dt * 3f);
        heal = Mathf.MoveTowards(heal, 0f, dt * 1.5f);

        rt.localScale = Vector3.one * (1f + beat + punch);
        rt.anchoredPosition = basePos + (shake > 0f ? Random.insideUnitCircle * shake : Vector2.zero);

        Color c = Color.white;
        c = Color.Lerp(c, new Color(1f, 0.35f, 0.35f), tint);
        c = Color.Lerp(c, new Color(1f, 0.75f, 0.9f), heal * 0.6f);
        if (broken) c.a = 0.85f;
        image.color = c;
    }

    // Short smooth spike centered at 'at' within a 0..1 cycle.
    static float Bump(float phase, float at)
    {
        float d = Mathf.Abs(phase - at);
        const float width = 0.09f;
        return d > width ? 0f : Mathf.Sin((1f - d / width) * Mathf.PI * 0.5f);
    }
}
