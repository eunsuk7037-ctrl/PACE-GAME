using UnityEngine;
using UnityEngine.EventSystems;

public class ClickSpark : MonoBehaviour, IPointerClickHandler
{
    public GameObject sparkPrefab;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (sparkPrefab == null) return;

        Vector3 pos = eventData.pointerCurrentRaycast.worldPosition;
        Instantiate(sparkPrefab, pos, Quaternion.identity);
    }
}
