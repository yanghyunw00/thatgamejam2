using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 마우스를 올리면 텍스트를 볼드 처리. 메뉴 항목(Button 또는 텍스트 오브젝트)에 붙이면 됨.
/// label을 비워두면 자식에서 TMP_Text를 자동으로 찾음.
/// </summary>
[DisallowMultipleComponent]
public class MenuTextHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] TMP_Text label;

    void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (label == null) Debug.LogWarning($"[MenuTextHover] {name}에서 TMP_Text를 찾지 못함", this);
    }

    void OnDisable() => SetBold(false);

    public void OnPointerEnter(PointerEventData eventData) => SetBold(true);
    public void OnPointerExit(PointerEventData eventData) => SetBold(false);

    void SetBold(bool on)
    {
        if (label == null) return;
        if (on) label.fontStyle |= FontStyles.Bold;
        else label.fontStyle &= ~FontStyles.Bold;
    }
}