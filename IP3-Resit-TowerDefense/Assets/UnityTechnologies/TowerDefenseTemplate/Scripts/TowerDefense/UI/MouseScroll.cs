using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; 

namespace TowerDefense.UI
{
	[RequireComponent(typeof(ScrollRect))]
	public class MouseScroll : MonoBehaviour
	{
		public bool clampScroll = true;
		public float scrollXBuffer;
		public float scrollYBuffer;

		protected ScrollRect m_ScrollRect;
		protected RectTransform m_ScrollRectTransform;
		protected bool m_OverrideScrolling, m_HasRightBuffer;

		public void SetHasRightBuffer(bool rightBuffer)
		{
			m_HasRightBuffer = rightBuffer;
		}

		void Start()
		{
#if UNITY_STANDALONE || UNITY_EDITOR
			m_ScrollRect = GetComponent<ScrollRect>();
			m_ScrollRect.enabled = false;
			m_OverrideScrolling = true;
			m_ScrollRectTransform = (RectTransform) m_ScrollRect.transform;
#else
			m_OverrideScrolling = false;
#endif
		}

		void Update()
		{
			if (!m_OverrideScrolling) return;

			Vector3 mousePos = Mouse.current.position.ReadValue();

			bool inside = RectTransformUtility.RectangleContainsScreenPoint(m_ScrollRectTransform, mousePos);
			if (!inside) return;

			Rect rect = m_ScrollRectTransform.rect;
			float adjustmentX = rect.width * scrollXBuffer;
			float adjustmentY = rect.height * scrollYBuffer;

			RectTransformUtility.ScreenPointToLocalPointInRectangle(m_ScrollRectTransform, mousePos, null, out Vector2 localPoint);

			Vector2 pivot = m_ScrollRectTransform.pivot;
			float x = (localPoint.x + (rect.width - adjustmentX) * pivot.x) / (rect.width - 2 * adjustmentX);
			float y = (localPoint.y + (rect.height - adjustmentY) * pivot.y) / (rect.height - 2 * adjustmentY);

			if (clampScroll)
			{
				x = Mathf.Clamp01(x);
				y = Mathf.Clamp01(y);
			}

			m_ScrollRect.normalizedPosition = new Vector2(x, y);
		}

		public void SelectChild(LevelSelectButton levelSelectButton)
		{
			int childCount = levelSelectButton.transform.parent.childCount - (m_HasRightBuffer ? 1 : 0);
			if (childCount > 1)
			{
				float normalized = (float)levelSelectButton.transform.GetSiblingIndex() / (childCount - 1);
				m_ScrollRect.normalizedPosition = new Vector2(normalized, 0);
			}
		}
	}
}
