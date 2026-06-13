using System;
using System.Collections.Generic;
using ActionGameFramework.Health;
using Core.Utilities;
using TowerDefense.Level;
using TowerDefense.Towers.Placement;
using TowerDefense.UI.HUD;
using UnityEngine;

namespace TowerDefense.Towers
{
	/// <summary>
	/// Common functionality for all types of towers
	/// </summary>
	public class Tower : Targetable
	{
		/// <summary>
		/// The tower levels associated with this tower
		/// </summary>
		public TowerLevel[] levels;

		/// <summary>
		/// A generalised name common to a levels
		/// </summary>
		public string towerName;

		/// <summary>
		/// The size of the tower's footprint
		/// </summary>
		public IntVector2 dimensions;

		/// <summary>
		/// The physics mask the tower searches on
		/// </summary>
		public LayerMask enemyLayerMask;

		/// <summary>
		/// The current level of the tower
		/// </summary>
		public int currentLevel { get; protected set; }

		/// <summary>
		/// Reference to the data of the current level
		/// </summary>
		public TowerLevel currentTowerLevel { get; protected set; }

		/// <summary>
		/// An empty options array, returned to avoid per-call allocations
		/// </summary>
		static readonly TowerLevel[] s_NoOptions = new TowerLevel[0];

        /// <summary>
        /// The actual prefabs the player has upgraded through, in order (index 0 = base level).
        /// Used to refund the EXACT path the player paid for, regardless of which branch they took.
        /// </summary>
		readonly List<TowerLevel> m_UpgradePath = new List<TowerLevel>();

        /// <summary>
        /// Gets whether the tower can level up anymore
        /// </summary>
        public bool isAtMaxLevel
		{
			get { return GetUpgradeOptions().Length == 0; }
		}

		/// <summary>
		/// Gets the first level tower ghost prefab
		/// </summary>
		public TowerPlacementGhost towerGhostPrefab
		{
			get { return levels[currentLevel].towerGhostPrefab; }
		}

		/// <summary>
		/// Gets the grid position for this tower on the <see cref="placementArea"/>
		/// </summary>
		public IntVector2 gridPosition { get; private set; }

		/// <summary>
		/// The placement area we've been built on
		/// </summary>
		public IPlacementArea placementArea { get; private set; }

		/// <summary>
		/// The purchase cost of the tower
		/// </summary>
		public int purchaseCost
		{
			get { return levels[0].cost; }
		}

		/// <summary>
		/// The event that fires off when a player deletes a tower
		/// </summary>
		public Action towerDeleted;

		/// <summary>
		/// The event that fires off when a tower has been destroyed
		/// </summary>
		public Action towerDestroyed;

		/// <summary>
		/// Provide the tower with data to initialize with
		/// </summary>
		/// <param name="targetArea">The placement area configuration</param>
		/// <param name="destination">The destination position</param>
		public virtual void Initialize(IPlacementArea targetArea, IntVector2 destination)
		{
			placementArea = targetArea;
			gridPosition = destination;

			if (targetArea != null)
			{
				transform.position = placementArea.GridToWorld(destination, dimensions);
				transform.rotation = placementArea.transform.rotation;
				targetArea.Occupy(destination, dimensions);
			}

			SetLevel(0);
			if (LevelManager.instanceExists)
			{
				LevelManager.instance.levelStateChanged += OnLevelStateChanged;
			}
		}

        /// <summary>
        /// Returns the list of upgrade options available from the current level.
        /// Falls back to the original linear chain when no explicit options are set.
        /// </summary>
        /// <returns>An array of TowerLevel prefabs the player can upgrade into. Empty if at max level.</returns>
		public TowerLevel[] GetUpgradeOptions()
		{
			if(currentTowerLevel != null && currentTowerLevel.upgradeOptions != null && currentTowerLevel.upgradeOptions.Length > 0)
			{
				return currentTowerLevel.upgradeOptions;
			}

			// Fallback: behave exactly like the original linear chain
			if(currentLevel + 1 < levels.Length)
			{
				return new[] { levels[currentLevel + 1] };
			}

			return s_NoOptions;
		}

        /// <summary>
        /// Number of upgrade options available from the current level
        /// </summary>
		public int GetUpgradeOptionCount()
		{
			return GetUpgradeOptions().Length;
		}

        /// <summary>
        /// Gets the cost of a specific upgrade option
        /// </summary>
        /// <param name="optionIndex">Index into <see cref="GetUpgradeOptions"/></param>
        /// <returns>The cost, or -1 if the index is invalid</returns>
		public int GetUpgradeCost(int optionIndex)
		{
			TowerLevel[] options = GetUpgradeOptions();
			if(optionIndex < 0 || optionIndex >= options.Length)
			{
				return -1;
			}
			return options[optionIndex].cost;
		}

        /// <summary>
        /// Provides information on the cost to upgrade
        /// </summary>
        /// <returns>Returns -1 if the towers is already at max level, other returns the cost to upgrade</returns>
        public int GetCostForNextLevel()
		{
			TowerLevel[] options = GetUpgradeOptions();
			if (options.Length == 0)
			{
				return -1;
			}
			return options[0].cost;
		}

        /// <summary>
        /// The TowerLevel that best represents the tower's current state.
        /// Returns the live node when placed, or the base prefab for ghosts/library prefabs.
        /// </summary>
		public TowerLevel GetCurrentLevelTowerLevel()
		{
			if(currentTowerLevel != null)
			{
				return currentTowerLevel;
			}
			return levels.Length > 0 ? levels[0] : null;
		}

        /// <summary>
        /// Kills this tower
        /// </summary>
        public void KillTower()
		{
			// Invoke base kill method
			Kill();
		}

		/// <summary>
		/// Provides the value recived for selling this tower
		/// </summary>
		/// <returns>A sell value of the tower</returns>
		public int GetSellLevel()
		{
			return GetSellLevel(currentLevel);
		}

		/// <summary>
		/// Provides the value recived for selling this tower of a particular level
		/// </summary>
		/// <param name="level">Level of tower</param>
		/// <returns>A sell value of the tower</returns>
		public int GetSellLevel(int level)
		{
			// sell for full price if waves haven't started yet
			// We sum the ACTUAL path the player paid for, so branching is refunded correctly.
			if (LevelManager.instance.levelState == LevelState.Building)
			{
				int cost = 0;
				int count = Mathf.Min(level + 1, m_UpgradePath.Count);
				for (int i = 0; i < count; i++)
				{
					cost += m_UpgradePath[i].cost;
				}

				return cost;
			}
			return currentTowerLevel != null ? currentTowerLevel.sell : 0;
		}

		/// <summary>
		/// Used to (try to) upgrade the tower data
		/// </summary>
		public virtual bool UpgradeTower(int optionIndex = 0)
		{
			TowerLevel[] options = GetUpgradeOptions();
			if (options.Length == 0)
			{
				return false;
			}
			if(optionIndex < 0 || optionIndex >= options.Length)
			{
				return false;
			}
			ApplyLevel(options[optionIndex], currentLevel + 1);
			return true;
		}

		/// <summary>
		/// A method for downgrading tower
		/// </summary>
		/// <returns>
		/// <value>false</value> if tower is at lowest level
		/// </returns>
		public virtual bool DowngradeTower()
		{
			if (currentLevel == 0)
			{
				return false;
			}
			SetLevel(currentLevel - 1);
			return true;
		}

		/// <summary>
		/// Used to set the tower to any valid level
		/// </summary>
		/// <param name="level">
		/// The level to upgrade the tower to
		/// </param>
		/// <returns>
		/// True if successful
		/// </returns>
		public virtual bool UpgradeTowerToLevel(int level)
		{
			if (level < 0 || isAtMaxLevel || level >= levels.Length)
			{
				return false;
			}
			SetLevel(level);
			return true;
		}

		public void Sell()
		{
			Remove();
		}

		/// <summary>
		/// Removes tower from placement area and destroys it
		/// </summary>
		public override void Remove()
		{
			base.Remove();
			
			placementArea.Clear(gridPosition, dimensions);
			Destroy(gameObject);
		}

		/// <summary>
		/// unsubsribe when necessary
		/// </summary>
		protected virtual void OnDestroy()
		{
			if (LevelManager.instanceExists)
			{
				LevelManager.instance.levelStateChanged += OnLevelStateChanged;
			}
		}

		/// <summary>
		/// Cache and update oftenly used data
		/// </summary>
		protected void SetLevel(int level)
		{
			if (level < 0 || level >= levels.Length)
			{
				return;
			}
			ApplyLevel(levels[level], level);
		}

        /// <summary>
        /// Instantiates a specific TowerLevel prefab as the new current level and updates all cached data.
        /// This is the single funnel through which every level change passes.
        /// </summary>
        /// <param name="levelPrefab">The TowerLevel prefab to apply</param>
        /// <param name="depth">The depth this level sits at in the upgrade tree</param>
		protected void ApplyLevel(TowerLevel levelPrefab, int depth)
		{
			if(levelPrefab == null)
			{
				return;
			}

			currentLevel = depth;
			if(currentTowerLevel != null)
			{
				Destroy(currentTowerLevel.gameObject);
			}

			// instantiate the visual representation
			currentTowerLevel = Instantiate(levelPrefab, transform);

			// initialize TowerLevel
			currentTowerLevel.Initialize(this, enemyLayerMask, configuration.alignmentProvider);

			// record the path so we can refind exactly what was paid
			TrackPath(levelPrefab, depth);

			// health data
			ScaleHealth();

			// disable affectors
			LevelState levelState = LevelManager.instance.levelState;
			bool initialise = levelState == LevelState.AllEnemiesSpawned || levelState == LevelState.SpawningEnemies;
			currentTowerLevel.SetAffectorState(initialise);
		}

        /// <summary>
        /// Keeps <see cref="m_UpgradePath"/> in sync with the level prefabs actually applied at each depth
        /// </summary>
		void TrackPath(TowerLevel levelPrefab, int depth)
		{
			if(depth == 0)
			{
				m_UpgradePath.Clear();
				m_UpgradePath.Add(levelPrefab);
				return;
			}

			// Trim anything deeper than where we are now (e.g. after a downgrade)
			while (m_UpgradePath.Count > depth)
			{
				m_UpgradePath.RemoveAt(m_UpgradePath.Count - 1);
			}

			if(m_UpgradePath.Count == depth)
			{
				m_UpgradePath.Add(levelPrefab);
			}
			else
			{
				m_UpgradePath[depth] = levelPrefab;
			}
		}

        /// <summary>
        /// Scales the health based on the previous health
        /// Requires override when the rules for scaling health on upgrade changes
        /// </summary>
        protected virtual void ScaleHealth()
		{
			configuration.SetMaxHealth(currentTowerLevel.maxHealth);
			
			if (currentLevel == 0)
			{
				configuration.SetHealth(currentTowerLevel.maxHealth);
			}
			else
			{
				int currentHealth = Mathf.FloorToInt(configuration.normalisedHealth * currentTowerLevel.maxHealth);
				configuration.SetHealth(currentHealth);
			}
		}

		/// <summary>
		/// Intiailises affectors based on the level state
		/// </summary>
		protected virtual void OnLevelStateChanged(LevelState previous, LevelState current)
		{
			bool initialise = current == LevelState.AllEnemiesSpawned || current == LevelState.SpawningEnemies;
			currentTowerLevel.SetAffectorState(initialise);
		}
	}
}