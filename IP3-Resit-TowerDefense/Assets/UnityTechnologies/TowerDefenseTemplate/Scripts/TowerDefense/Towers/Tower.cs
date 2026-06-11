using System;
using ActionGameFramework.Health;
using Core.Utilities;
using TowerDefense.Level;
using TowerDefense.Towers.Data;
using TowerDefense.Towers.Placement;
using TowerDefense.UI.HUD;
using UnityEngine;

namespace TowerDefense.Towers
{
	/// <summary>
	/// The two branching upgrade axes a player can pick from.
	/// Shader by Tower, the upgrade choice UI and GameUI.
	/// </summary>
	public enum TowerUpgradeChoice
    {
        Range,
		Power
    }

    /// <summary>
    /// Common functionality for all types of towers
    /// </summary>
    public class Tower : Targetable
	{
		/// <summary>
		/// The tower levels associated with this tower.
		/// In the branching system these are used ONLY as visual tiers (mesh A/B/C),
		/// not as the source of stat progression.
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

        // ---------------------------------------------------------
        // BRANCHING UPGRADE SETTINGS (tunable from the Inspector)
        // ---------------------------------------------------------

        [Header("Branching Upgrade Settings")]

		/// <summary>
		/// Total number of upgrade picks allowed before the tower is maxed.
		/// </summary>
		public int maxUpgrades = 3;

		/// <summary>
		/// Seconds that must elapse after an upgrade before the next is allowed,
		/// even when the player can afford if (the time gate).
		/// </summary>
		public float upgradeCooldown = 5f;

		/// <summary>
		/// Base cost of the first upgrade pick.
		/// </summary>
		public int baseUpgradeCost = 50;

        /// <summary>
        /// Cost multiplier applied per pick already taken (the money gate).
        /// Cost of pick n = baseUpgradeCost * upgradeCostMultiplier^n
        /// </summary>
		public float upgradeCostMultiplier = 1.5f;

		/// <summary>
		/// Flat increase to attack radius per Range pick (applied in Module 2).
		/// </summary>
		public float rangePerPick = 1f;

        /// <summary>
        /// Fractional fire-rate increase per Power pick, e.g. 0.25 = +25% per pick (Module 2).
        /// </summary>
		public float powerPerPick = 0.25f;

		[Header("Sell Settings")]

		/// <summary>
		/// Fraction of total money invested that is refunded on sell, in every phase.
		/// </summary>
		[Range(0f, 1f)]
		public float sellRefundRate = 0.5f;

        // -----------------------------------------
        // BRANCHING UPGRADE RUNTIME STATE
        // -----------------------------------------

        /// <summary>
        /// How many picks have been taken so far.
        /// </summary>
		public int upgradesUsed { get; protected set; }

        /// <summary>
        /// How many of the taken picks were Range picks.
        /// </summary>
		public int rangePicks { get; protected set; }

        /// <summary>
        /// How many of the taken picks were Power picks.
        /// </summary>
		public int powerPicks { get; protected set; }

        /// <summary>
        /// Total currency spent on this tower (purches + every upgrade).
		/// Drives the percentage sell refund.
        /// </summary>
		public int totalInvested { get; protected set; }

        /// <summary>
        /// Time.time at which the last upgrade was taken. Navigate infinity means
		/// "never upgraded", so the first upgrade is never blocked by cooldown.
        /// </summary>
		protected float m_LastUpgradeTime = float.NegativeInfinity;

        /// <summary>
        /// The current level of the tower (used here as the current VISUAL tier)
        /// </summary>
        public int currentLevel { get; protected set; }

		/// <summary>
		/// Reference to the data of the current level
		/// </summary>
		public TowerLevel currentTowerLevel { get; protected set; }

        /// <summary>
        /// Gets whether the tower can be upgraded any further.
		/// In the branching system this is driven by picks used, not the level array.
        /// </summary>
        public bool isAtMaxLevel
		{
			get { return upgradesUsed >= maxUpgrades; }
		}

        /// <summary>
        /// True while the cooldown after the last upgrade has not yet elapsed.
        /// </summary>
		public bool isUpgradeOnCooldown
		{
			get { return Time.time < m_LastUpgradeTime + upgradeCooldown; }
        }

        /// <summary>
        /// Seconds remaining on the upgrade cooldown (0 when ready).
        /// </summary>
        public float upgradeCooldownRemaining
		{
			get { return Mathf.Max(0f, m_LastUpgradeTime + upgradeCooldown - Time.time); }
        }

        /// <summary>
        /// Cooldown progress in the 0..1 range (1 = fully recharged). Useful for a fill image.
        /// </summary>
		public float upgradeCooldownNormalized
		{
			get
			{
                if (upgradeCooldown <= 0f)
                {
                    return 1f;
                }
                return Mathf.Clamp01((Time.time - m_LastUpgradeTime) / upgradeCooldown);
            }
		}

        /// <summary>
        /// True only when the tower can be upgraded right now (not maxed AND off cooldown).
		/// Note: affordability is still checked separately via the Currency system.
        /// </summary>
		public bool canUpgrade
		{
			get { return !isAtMaxLevel && !isUpgradeOnCooldown; }
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

			// Seed the investment tracker with the purchase cost the player just paid.
			totalInvested = purchaseCost;

			if (LevelManager.instanceExists)
			{
				LevelManager.instance.levelStateChanged += OnLevelStateChanged;
			}
		}

		/// <summary>
		/// Provides information on the cost to upgrade
		/// Cost scales with the number of picks already taken (the money gate).
		/// </summary>
		/// <returns>Returns -1 if the towers is already at max level, other returns the cost to upgrade</returns>
		public int GetCostForNextLevel()
		{
			if (isAtMaxLevel)
			{
				return -1;
			}
			return Mathf.RoundToInt(baseUpgradeCost * Mathf.Pow(upgradeCostMultiplier, upgradesUsed));
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
		/// Provides the value recived for selling this tower:
		/// a percentage of the total money invested, in every phase.
		/// </summary>
		/// <returns>A sell value of the tower</returns>
		public int GetSellLevel()
		{
			return Mathf.FloorToInt(totalInvested * sellRefundRate);
		}

		/// <summary>
		/// Legacy overload kept for callers such as TowerInfoDisplay.
		/// The level argument is no longer used; the refund is based on total investments.
		/// </summary>
		/// <param name="level">Level of tower</param>
		/// <returns>A sell value of the tower</returns>
		public int GetSellLevel(int level)
		{
			return GetSellLevel();
        }

		/// <summary>
		/// Records a branching upgrade pick (Range or Power), advances the pick count,
		/// starts the cooldown, swaps the visual tier if it changed, and (re)applies
		/// the accumulated stat modifiers.
		///
		/// Affordability and the cooldown gate are expected to be checked by the caller
		/// (GameUI / the upgrade button) BEFORE charging the player, so this returns false
		/// defensively if called when it shouldn't be.
		/// </summary>
		/// <param name="choice">Which axis the player chose.</param>
		/// <returns>True if the upgrade was applied.</returns>
		public virtual bool UpgradeTower(TowerUpgradeChoice choice)
		{
			if (!canUpgrade)
			{
				return false;
			}

			// record the spend. GetCostForNextLevel is read BEFORE incrementing upgradesUsed,
			// so it mathces the amount the caller charged via the Currency system.
			totalInvested += GetCostForNextLevel();

			// Record the pick
			if (choice == TowerUpgradeChoice.Range)
			{
				rangePicks++;
			}
			else
			{
				powerPicks++;
			}

			upgradesUsed++;
            m_LastUpgradeTime = Time.time;

			//Swap the visual tier only if it changed (so the middle pick holds the mesh steady).
			int newTier = GetVisualTier();
            if (newTier != currentLevel)
			{
				SetLevel(newTier);
			}

			// Re-apply accumulated modifiers AFTER any visual swap, so the freshly
			// instantiated TowerLevel's baked stats don't reset the player's picks.
			ApplyUpgradeModifiers();

			return true;
        }

        /// <summary>
        /// Maps the number of upgrades used to a visual tier (index into <see cref="levels"/>).
		/// Default mapping for 3 picks / 3 meshes: 0 -> A, 1-2 -> B, 3 -> C.
		/// Clamps gracefully if the counts differ.
        /// </summary>
		protected int GetVisualTier()
		{
			int maxTier = levels.Length - 1;
			if(upgradesUsed >= maxUpgrades)
            {
                return maxTier;
            }
			if(upgradesUsed >= 1)
			{
				return Mathf.Min(1, maxTier); // any non-final pick -> middle mesh
			}
			return 0; // bse -> first mesh
        }

        /// <summary>
        /// Applies the accumulated Range and Power picks to the tower's live stats.
        ///
        /// TODO (Module 2): cache the neutral base stats on the current TowerLevel, then set:
        ///   - attack radius  = baseRadius   + (rangePicks * rangePerPick)   on the Targetter collider
        ///   - fire rate      = baseFireRate * (1 + powerPicks * powerPerPick) on the AttackAffector
        /// This MUST be called after any SetLevel() swap, and must always recompute from the
        /// cached base (never compound onto the current value), so re-application is idempotent.
        /// </summary>
		public virtual void ApplyUpgradeModifiers()
		{
			// Intentionally empty in Module 1 - the data layer is in place; stat application
			// is wired up in Module 2.
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
			if(LevelManager.instanceExists)
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
			currentLevel = level;
			if (currentTowerLevel != null)
			{
				Destroy(currentTowerLevel.gameObject);
			}

			// instantiate the visual representation
			currentTowerLevel = Instantiate(levels[currentLevel], transform);

			// initialize TowerLevel
			currentTowerLevel.Initialize(this, enemyLayerMask, configuration.alignmentProvider);

			// health data
			ScaleHealth();

			// disable affectors
			LevelState levelState = LevelManager.instance.levelState;
			bool initialise = levelState == LevelState.AllEnemiesSpawned || levelState == LevelState.SpawningEnemies;
			currentTowerLevel.SetAffectorState(initialise);
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