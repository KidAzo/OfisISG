using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentListItemUI : MonoBehaviour
{
	public TextMeshProUGUI labelText;
	public Image eqIcon;
	
	public void Init(string text, Sprite sprite)
	{
		if(sprite != null && eqIcon != null)
			eqIcon.sprite = sprite;

		labelText.text = text;
	}
}
