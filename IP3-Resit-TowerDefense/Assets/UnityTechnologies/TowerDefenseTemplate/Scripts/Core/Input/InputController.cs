using System;
using System.Collections.Generic;
using System.Linq;
using Core.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Debug = System.Diagnostics.Debug;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Core.Input
{
    /// <summary>
    /// Class to manage tap/drag/pinch gestures and other controls updated for Unity 6.3
    /// </summary>
    public class InputController : Singleton<InputController>
    {
        const float k_FlickAccumulationFactor = 0.8f;

        public float dragThresholdTouch = 5;
        public float dragThresholdMouse;
        public float tapTime = 0.2f;
        public float holdTime = 0.8f;
        public float mouseWheelSensitivity = 1.0f;
        public int trackMouseButtons = 2;
        public float flickThreshold = 2f;

        List<TouchInfo> m_Touches;
        List<MouseButtonInfo> m_MouseInfo;

        public int activeTouchCount => m_Touches.Count;
        public bool mouseButtonPressedThisFrame { get; private set; }
        public bool mouseMovedOnThisFrame { get; private set; }
        public bool touchPressedThisFrame { get; private set; }
        public PointerInfo basicMouseInfo { get; private set; }

        public event Action<PointerActionInfo> pressed;
        public event Action<PointerActionInfo> released;
        public event Action<PointerActionInfo> tapped;
        public event Action<PointerActionInfo> startedDrag;
        public event Action<PointerActionInfo> dragged;
        public event Action<PointerActionInfo> startedHold;
        public event Action<WheelInfo> spunWheel;
        public event Action<PinchInfo> pinched;
        public event Action<PointerInfo> mouseMoved;

        protected override void Awake()
        {
            base.Awake();
            
            // Requerido para usar Touch.activeTouches en el New Input System
            if (!EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Enable();
            }

            m_Touches = new List<TouchInfo>();

            if (Mouse.current != null)
            {
                m_MouseInfo = new List<MouseButtonInfo>();
                Vector2 currentMousePos = Mouse.current.position.ReadValue();
                basicMouseInfo = new MouseCursorInfo { currentPosition = currentMousePos };

                for (int i = 0; i < trackMouseButtons; ++i)
                {
                    m_MouseInfo.Add(new MouseButtonInfo
                    {
                        currentPosition = currentMousePos,
                        mouseButtonId = i
                    });
                }
            }
        }

        void Update()
        {
            if (Mouse.current != null)
            {
                UpdateMouse();
            }
            
            UpdateTouches();
        }

        void UpdateMouse()
        {
            var mouse = Mouse.current;
            basicMouseInfo.previousPosition = basicMouseInfo.currentPosition;
            basicMouseInfo.currentPosition = mouse.position.ReadValue();
            basicMouseInfo.delta = mouse.delta.ReadValue();
            
            mouseMovedOnThisFrame = basicMouseInfo.delta.sqrMagnitude >= Mathf.Epsilon;
            mouseButtonPressedThisFrame = false;

            if (mouseMovedOnThisFrame && mouseMoved != null)
            {
                mouseMoved(basicMouseInfo);
            }

            for (int i = 0; i < trackMouseButtons; ++i)
            {
                MouseButtonInfo mouseButton = m_MouseInfo[i];
                mouseButton.delta = basicMouseInfo.delta;
                mouseButton.previousPosition = basicMouseInfo.previousPosition;
                mouseButton.currentPosition = basicMouseInfo.currentPosition;

                bool isButtonPressed = false;
                if (i == 0) isButtonPressed = mouse.leftButton.isPressed;
                else if (i == 1) isButtonPressed = mouse.rightButton.isPressed;
                else if (i == 2) isButtonPressed = mouse.middleButton.isPressed;

                if (isButtonPressed)
                {
                    if (!mouseButton.isDown)
                    {
                        mouseButtonPressedThisFrame = true;
                        mouseButton.isDown = true;
                        mouseButton.startPosition = mouseButton.currentPosition;
                        mouseButton.startTime = Time.realtimeSinceStartup;
                        mouseButton.startedOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

                        mouseButton.totalMovement = 0;
                        mouseButton.isDrag = false;
                        mouseButton.wasHold = false;
                        mouseButton.isHold = false;
                        mouseButton.flickVelocity = Vector2.zero;

                        pressed?.Invoke(mouseButton);
                    }
                    else
                    {
                        float moveDist = mouseButton.delta.magnitude;
                        mouseButton.totalMovement += moveDist;
                        
                        if (mouseButton.totalMovement > dragThresholdMouse)
                        {
                            bool wasDrag = mouseButton.isDrag;
                            mouseButton.isDrag = true;
                            
                            if (mouseButton.isHold)
                            {
                                mouseButton.wasHold = true;
                                mouseButton.isHold = false;
                            }

                            if (!wasDrag) startedDrag?.Invoke(mouseButton);
                            dragged?.Invoke(mouseButton);

                            if (moveDist > flickThreshold)
                            {
                                mouseButton.flickVelocity = (mouseButton.flickVelocity * (1 - k_FlickAccumulationFactor)) +
                                                            (mouseButton.delta * k_FlickAccumulationFactor);
                            }
                            else
                            {
                                mouseButton.flickVelocity = Vector2.zero;
                            }
                        }
                        else if (!mouseButton.isHold && !mouseButton.isDrag && Time.realtimeSinceStartup - mouseButton.startTime >= holdTime)
                        {
                            mouseButton.isHold = true;
                            startedHold?.Invoke(mouseButton);
                        }
                    }
                }
                else if (mouseButton.isDown)
                {
                    mouseButton.isDown = false;
                    if (!mouseButton.isDrag && Time.realtimeSinceStartup - mouseButton.startTime < tapTime)
                    {
                        tapped?.Invoke(mouseButton);
                    }
                    released?.Invoke(mouseButton);
                }
            }

            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > Mathf.Epsilon)
            {
                spunWheel?.Invoke(new WheelInfo
                {
                    zoomAmount = (scrollY / 120f) * mouseWheelSensitivity // Normalizado porque el New System usa valores altos
                });
            }
        }

        void UpdateTouches()
        {
            touchPressedThisFrame = false;
            var activeTouches = Touch.activeTouches;

            foreach (var touch in activeTouches)
            {
                TouchInfo existingTouch = m_Touches.FirstOrDefault(t => t.touchId == touch.finger.index);

                if (existingTouch == null)
                {
                    existingTouch = new TouchInfo
                    {
                        touchId = touch.finger.index,
                        startPosition = touch.startScreenPosition,
                        currentPosition = touch.screenPosition,
                        previousPosition = touch.screenPosition,
                        startTime = Time.realtimeSinceStartup,
                        startedOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.finger.index)
                    };
                    m_Touches.Add(existingTouch);
                }

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        touchPressedThisFrame = true;
                        pressed?.Invoke(existingTouch);
                        break;

                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        bool wasDrag = existingTouch.isDrag;
                        
                        existingTouch.previousPosition = existingTouch.currentPosition;
                        existingTouch.currentPosition = touch.screenPosition;
                        existingTouch.delta = touch.delta;
                        existingTouch.totalMovement += touch.delta.magnitude;

                        existingTouch.isDrag = existingTouch.totalMovement >= dragThresholdTouch;

                        if (existingTouch.isDrag)
                        {
                            if (existingTouch.isHold) { existingTouch.wasHold = true; existingTouch.isHold = false; }
                            if (!wasDrag) startedDrag?.Invoke(existingTouch);
                            dragged?.Invoke(existingTouch);

                            if (existingTouch.delta.sqrMagnitude > flickThreshold * flickThreshold)
                            {
                                existingTouch.flickVelocity = (existingTouch.flickVelocity * (1 - k_FlickAccumulationFactor)) +
                                                              (existingTouch.delta * k_FlickAccumulationFactor);
                            }
                            else
                            {
                                existingTouch.flickVelocity = Vector2.zero;
                            }
                        }
                        else
                        {
                            UpdateHoldingFinger(existingTouch);
                        }
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (!existingTouch.isDrag && Time.realtimeSinceStartup - existingTouch.startTime < tapTime)
                        {
                            tapped?.Invoke(existingTouch);
                        }
                        released?.Invoke(existingTouch);
                        m_Touches.Remove(existingTouch);
                        break;
                }
            }

            if (m_Touches.Count >= 2 && (m_Touches[0].isDrag || m_Touches[1].isDrag))
            {
                pinched?.Invoke(new PinchInfo { touch1 = m_Touches[0], touch2 = m_Touches[1] });
            }
        }

        void UpdateHoldingFinger(PointerActionInfo existingTouch)
        {
            if (!existingTouch.isHold && !existingTouch.isDrag && Time.realtimeSinceStartup - existingTouch.startTime >= holdTime)
            {
                existingTouch.isHold = true;
                startedHold?.Invoke(existingTouch);
            }
        }
    }
}