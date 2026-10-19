using UnityEngine;

// One final-boss bullet: flies straight, hurts the player on contact, and cleans itself up off screen.
// Named "...(Clone)" when spawned so StageProgress.ClearLeftoverEffects sweeps any left when a stage ends.
public class BossBullet : MonoBehaviour
{
    private const float HitDistance = 50f;    // player's core (~40) + bullet radius
    private const float MaxDistance = 1500f;  // from the stage centre: well past every screen edge
    private const float MaxLifetime = 12f;

    private RectTransform rt;
    private RectTransform player;
    private StageProgress stage;
    private Vector2 velocity;
    private float age;

    public void Init(StageProgress stageProgress, RectTransform playerRt, Vector2 velocityPerSecond)
    {
        rt = (RectTransform)transform;
        stage = stageProgress;
        player = playerRt;
        velocity = velocityPerSecond;
    }

    private void Update()
    {
        rt.anchoredPosition += velocity * Time.deltaTime;
        age += Time.deltaTime;

        if (player != null && player.gameObject.activeInHierarchy
            && (player.anchoredPosition - rt.anchoredPosition).sqrMagnitude < HitDistance * HitDistance)
        {
            stage.HitPlayerWithBullet();
            Destroy(gameObject);
            return;
        }

        if (age > MaxLifetime || rt.anchoredPosition.sqrMagnitude > MaxDistance * MaxDistance) Destroy(gameObject);
    }
}
