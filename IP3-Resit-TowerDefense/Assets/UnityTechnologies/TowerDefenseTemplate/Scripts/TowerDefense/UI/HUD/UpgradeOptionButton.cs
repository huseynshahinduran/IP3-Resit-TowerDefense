using TowerDefense.Level;
using TowerDefense.Towers;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI.HUD
{
    /// <summary>
	/// A single upgrade choice in the tower's upgrade menu.
	/// One of these represents one branch the player can take.
	/// </summary>
    public class UpgradeOptionButton : MonoBehaviour
    {
        /// <summary>
		/// The button the player clicks
		/// </summary>
        public Button button;

        /// <summary>
		/// Text showing this option's upgrade description
		/// </summary>
        public Text descriptionText;

        /// <summary>
		/// Text showing this option's cost (optional)
		/// </summary>
        public Text costText;

        /// <summary>
		/// Which upgrade option (branch) this button represents
		/// </summary>
        int m_OptionIndex;

        /// <summary>
		/// Fills this button in with the data of the given option and shows it
		/// </summary>
        public void Configure(Tower tower, int optionIndex)
        {
            m_OptionIndex = optionIndex;

            TowerLevel[] options = tower.GetUpgradeOptions();
            if(optionIndex<0 || optionIndex >= options.Length)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            TowerLevel option = options[optionIndex];
            int cost = option.cost;

            if(descriptionText != null)
            {
                descriptionText.text = option.upgradeDescription.ToUpper();
            }
            if(costText != null)
            {
                costText.text = cost.ToString();
            }
            if(button != null)
            {
                button.interactable = LevelManager.instance.currency.CanAfford(cost);
            }
        }

        /// <summary>
		/// Re-checks whether the player can currently afford this option
		/// </summary>
        public void RefreshAffordability(Tower tower)
        {
            if(button == null)
            {
                return;
            } 
            int cost = tower.GetUpgradeCost(m_OptionIndex);
            button.interactable = cost >= 0 && LevelManager.instance.currency.CanAfford(cost);
        }

        /// <summary>
		/// Hook this to the Button's OnClick event in the Inspector.
		/// Upgrades the selected tower along THIS option's branch.
		/// </summary>
        public void OnClick()
        {
            GameUI.instance.UpgradeSelectedTower(m_OptionIndex);
        }
    }
}