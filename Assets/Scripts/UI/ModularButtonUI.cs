using UnityEngine;
using UnityEngine.EventSystems;

public class ModularButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IDragHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [Tooltip("Masukkan GameObject Ornament_L ke sini")]
    public GameObject ornament;

    void Start()
    {
        // Pastikan ornamen mati saat awal mulai
        if (ornament != null) ornament.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Ornamen HANYA aktif jika kursor masuk DAN klik kiri (0) tidak sedang ditahan
        if (!Input.GetMouseButton(0) && ornament != null)
        {
            ornament.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ornament != null) ornament.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (ornament != null) ornament.SetActive(false);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ornament != null) ornament.SetActive(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (ornament != null) ornament.SetActive(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (ornament != null) ornament.SetActive(false);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (ornament != null) ornament.SetActive(false);
    }
}