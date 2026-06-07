using Core.UI;
using UnityEngine;
using UnityEngine.InputSystem;  // ← AGREGAR

namespace TowerDefense.UI.HUD
{
	public class TowerDefenseMainMenu : MainMenu
	{
		public OptionsMenu optionsMenu;
		public SimpleMainMenuPage titleMenu;
		public LevelSelectScreen levelSelectMenu;

		public void ShowOptionsMenu() => ChangePage(optionsMenu);
		public void ShowLevelSelectMenu() => ChangePage(levelSelectMenu);
		public void ShowTitleScreen() => Back(titleMenu);

		protected virtual void Awake() => ShowTitleScreen();

		// ✅ FIX Línea 63
		protected virtual void Update()
		{
			if (Keyboard.current.escapeKey.wasPressedThisFrame)
			{
				if ((SimpleMainMenuPage)m_CurrentPage == titleMenu)
				{
					Application.Quit();
				}
				else
				{
					Back();
				}
			}
		}
	}
}
