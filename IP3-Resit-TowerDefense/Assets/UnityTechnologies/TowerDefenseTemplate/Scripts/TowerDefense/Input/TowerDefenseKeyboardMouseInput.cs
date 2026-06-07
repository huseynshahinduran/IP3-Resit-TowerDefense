using Core.Input;
using TowerDefense.Level;
using TowerDefense.Towers;
using TowerDefense.UI.HUD;
using UnityEngine;
using UnityEngine.InputSystem; // Añadido para el New Input System
using State = TowerDefense.UI.HUD.GameUI.State;

namespace TowerDefense.Input
{
    [RequireComponent(typeof(GameUI))]
    public class TowerDefenseKeyboardMouseInput : KeyboardMouseInput
    {
        GameUI m_GameUI;

        protected override void OnEnable()
        {
            base.OnEnable();
            
            m_GameUI = GetComponent<GameUI>();

            if (InputController.instanceExists)
            {
                InputController controller = InputController.instance;

                controller.tapped += OnTap;
                controller.mouseMoved += OnMouseMoved;
            }
        }

        protected override void OnDisable()
        {
            if (!InputController.instanceExists)
            {
                return;
            }

            InputController controller = InputController.instance;

            controller.tapped -= OnTap;
            controller.mouseMoved -= OnMouseMoved;
        }

        protected override void Update()
        {
            base.Update();
            
            if (Keyboard.current == null) return;
            var kb = Keyboard.current;

            // Manejo de Escape con New Input System
            if (kb.escapeKey.wasPressedThisFrame)
            {
                switch (m_GameUI.state)
                {
                    case State.Normal:
                        if (m_GameUI.isTowerSelected)
                        {
                            m_GameUI.DeselectTower();
                        }
                        else
                        {
                            m_GameUI.Pause();
                        }
                        break;
                    case State.BuildingWithDrag:
                    case State.Building:
                        m_GameUI.CancelGhostPlacement();
                        break;
                }
            }
            
            // Atajos de teclado para colocar torres (1-9 y 0)
            if (LevelManager.instanceExists)
            {
                int towerLibraryCount = LevelManager.instance.towerLibrary.Count;
                
                // Mapeo de teclas numéricas (Alpha1 es el índice 0, Alpha2 el 1, etc.)
                Key[] numKeys = { 
                    Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, 
                    Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 
                };

                int count = Mathf.Min(9, towerLibraryCount);

                for (int i = 0; i < count; i++)
                {
                    if (kb[numKeys[i]].wasPressedThisFrame)
                    {
                        TrySetBuildMode(i);
                        break;
                    }
                }

                // Caso especial para la tecla 0 (mapeada al índice 9 de la librería)
                if (towerLibraryCount > 9 && kb.digit0Key.wasPressedThisFrame)
                {
                    TrySetBuildMode(9);
                }
            }
        }

        /// <summary>
        /// Método auxiliar para evitar repetición de código al presionar números
        /// </summary>
        void TrySetBuildMode(int libraryIndex)
        {
            Tower controller = LevelManager.instance.towerLibrary[libraryIndex];
            if (LevelManager.instance.currency.CanAfford(controller.purchaseCost))
            {
                if (m_GameUI.isBuilding)
                {
                    m_GameUI.CancelGhostPlacement();
                }
                GameUI.instance.SetToBuildMode(controller);
                GameUI.instance.TryMoveGhost(InputController.instance.basicMouseInfo);
            }
        }

        void OnMouseMoved(PointerInfo pointer)
        {
            var mouseInfo = pointer as MouseCursorInfo;
            if ((mouseInfo != null) && (m_GameUI.isBuilding))
            {
                m_GameUI.TryMoveGhost(pointer, false);
            }
        }

        void OnTap(PointerActionInfo pointer)
        {
            var mouseInfo = pointer as MouseButtonInfo;

            if (mouseInfo != null && !mouseInfo.startedOverUI)
            {
                if (m_GameUI.isBuilding)
                {
                    if (mouseInfo.mouseButtonId == 0) // Click izquierdo confirma
                    {
                        m_GameUI.TryPlaceTower(pointer);
                    }
                    else // Click derecho cancela
                    {
                        m_GameUI.CancelGhostPlacement();
                    }
                }
                else
                {
                    if (mouseInfo.mouseButtonId == 0)
                    {
                        m_GameUI.TrySelectTower(pointer);
                    }
                }
            }
        }
    }
}