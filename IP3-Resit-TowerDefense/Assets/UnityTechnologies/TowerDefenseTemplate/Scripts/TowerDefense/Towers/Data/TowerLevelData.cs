using UnityEngine;

namespace TowerDefense.Towers.Data
{
	/// <summary>
	/// Data container for settings per tower level
	/// </summary>
	[CreateAssetMenu(fileName = "TowerData.asset", menuName = "TowerDefense/Tower Configuration", order = 1)]
	public class TowerLevelData : ScriptableObject
	{
		/// <summary>
		/// A description of the tower for displaying on the UI
		/// </summary>
		public string description;

		/// <summary>
		/// A description of the tower for displaying on the UI
		/// </summary>
		public string upgradeDescription;

		/// <summary>
		/// The cost to upgrade to this level
		/// </summary>
		public int cost;

		/// <summary>
		/// The sell cost of the tower
		/// </summary>
		public int sell;

		/// <summary>
		/// The max health
		/// </summary>
		public int maxHealth;

		/// <summary>
		/// The starting health
		/// </summary>
		public int startingHealth;

		/// <summary>
		/// The tower icon
		/// </summary>
		public Sprite icon;

		/// <summary>
		/// Visual effect spawned when the tower upgrades INTO this level.
		/// </summary>
		public GameObject upgradeEffectPrefab;

		/// <summary>
		/// Sound played when the tower upgrades INTO this level.
		/// </summary>
		public AudioClip upgradeSound;

		/// <summary>
		/// Colour the whole tower briefly flashes when it upgrades INTO this level.
		/// Leave the alpha at 0 to disable.
		/// </summary>
		public Color flashColor;
	}
}