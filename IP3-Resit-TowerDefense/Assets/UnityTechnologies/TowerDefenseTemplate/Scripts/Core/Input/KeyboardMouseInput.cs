using UnityEngine;
using UnityEngine.InputSystem; // Añadido para el New Input System
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch; // Para evitar conflictos

namespace Core.Input
{
    /// <summary>
    /// Base control scheme for desktop devices, which performs CameraRig motion
    /// Updated for Unity 6.3 New Input System
    /// </summary>
    public class KeyboardMouseInput : CameraInputScheme
    {
        public float screenPanThreshold = 40f;
        public float mouseEdgePanSpeed = 30f;
        public float mouseRmbPanSpeed = 15f;
        
        public override bool shouldActivate
        {
            get
            {
                // Verificamos si hay toques activos usando el nuevo sistema
                if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
                {
                    return false;
                }

                // anyKey en el nuevo sistema
                bool anyKey = Keyboard.current != null && Keyboard.current.anyKey.isPressed;
                bool buttonPressedThisFrame = InputController.instance.mouseButtonPressedThisFrame;
                bool movedMouseThisFrame = InputController.instance.mouseMovedOnThisFrame;

                return (anyKey || buttonPressedThisFrame || movedMouseThisFrame);
            }
        }

        public override bool isDefault
        {
            get
            {
#if UNITY_STANDALONE || UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        protected virtual void OnEnable()
        {
            if (!InputController.instanceExists)
            {
                Debug.LogError("[UI] Keyboard and Mouse UI requires InputController");
                return;
            }

            InputController controller = InputController.instance;
            controller.spunWheel += OnWheel;
            controller.dragged += OnDrag;
            controller.pressed += OnPress;
        }

        protected virtual void OnDisable()
        {
            if (!InputController.instanceExists) return;

            InputController controller = InputController.instance;
            controller.pressed -= OnPress;
            controller.dragged -= OnDrag;
            controller.spunWheel -= OnWheel;
        }
        
        protected virtual void Update()
        {
            if (cameraRig != null)
            {
                DoScreenEdgePan();
                DoKeyboardPan();
                DecayZoom();
            }
        }

        protected virtual void OnDrag(PointerActionInfo pointer)
        {
            if (cameraRig != null)
            {
                DoRightMouseDragPan(pointer);
            }
        }

        protected virtual void OnWheel(WheelInfo wheel)
        {
            if (cameraRig != null)
            {
                DoWheelZoom(wheel);
            }
        }

        protected virtual void OnPress(PointerActionInfo pointer)
        {
            if (cameraRig != null)
            {
                DoMiddleMousePan(pointer);
            }
        }

        protected void DoScreenEdgePan()
        {
            if (Mouse.current == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();

            bool mouseInside = (mousePos.x >= 0) &&
                               (mousePos.x < Screen.width) &&
                               (mousePos.y >= 0) &&
                               (mousePos.y < Screen.height);

            if (mouseInside)
            {
                PanWithScreenCoordinates(mousePos, screenPanThreshold, mouseEdgePanSpeed);
            }
        }

        protected void DoKeyboardPan()
        {
            if (Keyboard.current == null) return;
            var kb = Keyboard.current;

            float zoomRatio = GetPanSpeedForZoomLevel();
            
            // Reemplazo de GetKey con la nueva API
            // Izquierda
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed)
            {
                cameraRig.PanCamera(Vector3.left * Time.deltaTime * mouseEdgePanSpeed * zoomRatio);
                cameraRig.StopTracking();
            }

            // Derecha
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed)
            {
                cameraRig.PanCamera(Vector3.right * Time.deltaTime * mouseEdgePanSpeed * zoomRatio);
                cameraRig.StopTracking();
            }

            // Abajo
            if (kb.downArrowKey.isPressed || kb.sKey.isPressed)
            {
                cameraRig.PanCamera(Vector3.back * Time.deltaTime * mouseEdgePanSpeed * zoomRatio);
                cameraRig.StopTracking();
            }

            // Arriba
            if (kb.upArrowKey.isPressed || kb.wKey.isPressed)
            {
                cameraRig.PanCamera(Vector3.forward * Time.deltaTime * mouseEdgePanSpeed * zoomRatio);
                cameraRig.StopTracking();
            }
        }

        protected void DecayZoom()
        {
            cameraRig.ZoomDecay();
        }

        protected void DoRightMouseDragPan(PointerActionInfo pointer)
        {
            var mouseInfo = pointer as MouseButtonInfo;
            if ((mouseInfo != null) && (mouseInfo.mouseButtonId == 1))
            {
                float zoomRatio = GetPanSpeedForZoomLevel();

                Vector2 panVector = mouseInfo.currentPosition - mouseInfo.startPosition;
                panVector = (panVector * Time.deltaTime * mouseRmbPanSpeed * zoomRatio) / screenPanThreshold;

                var camVector = new Vector3(panVector.x, 0, panVector.y);
                cameraRig.PanCamera(camVector);
                cameraRig.StopTracking();
            }
        }

        protected void DoWheelZoom(WheelInfo wheel)
        {
            if (Mouse.current == null) return;

            float prevZoomDist = cameraRig.zoomDist;
            cameraRig.ZoomCameraRelative(wheel.zoomAmount * -1);

            float zoomChange = cameraRig.zoomDist / prevZoomDist;

            // ScreenPointToRay usando la posición del ratón del nuevo sistema
            Ray ray = cameraRig.cachedCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            Vector3 worldPos = Vector3.zero;
            float dist;

            if (cameraRig.floorPlane.Raycast(ray, out dist))
            {
                worldPos = ray.GetPoint(dist);
            }

            Vector3 offsetValue = worldPos - cameraRig.lookPosition;
            cameraRig.PanCamera(offsetValue * (1 - zoomChange));
        }

        protected void DoMiddleMousePan(PointerActionInfo pointer)
        {
            if (Mouse.current == null) return;
            var mouseInfo = pointer as MouseButtonInfo;

            if ((mouseInfo != null) && (mouseInfo.mouseButtonId == 2))
            {
                Ray ray = cameraRig.cachedCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                float dist;

                if (cameraRig.floorPlane.Raycast(ray, out dist))
                {
                    Vector3 worldPos = ray.GetPoint(dist);
                    cameraRig.PanTo(worldPos);
                }

                cameraRig.StopTracking();
            }
        }
    }
}