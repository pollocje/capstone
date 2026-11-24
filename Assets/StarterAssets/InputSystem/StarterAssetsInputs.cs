using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;
		public float scroll;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}

		public void OnScroll(InputValue value)
		{
			ScrollInput(value.Get<Vector2>().y);
		}
#endif

		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		}

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}

		public void ScrollInput(float newScrollValue)
		{
			scroll = newScrollValue;
		}

		private void Start()
		{
			SetCursorState(cursorLocked);
		}

		// Clicking into the Game View to focus it (after it lost focus to another Editor
		// panel) doesn't raise OnApplicationFocus - that only fires on OS-level focus changes,
		// not Editor-internal panel focus. Re-asserting every frame means the lock recovers
		// regardless of how it got dropped, instead of only reacting to events that don't
		// actually cover the case that keeps happening in the Editor.
		private void Update()
		{
			if (cursorLocked && Cursor.lockState != CursorLockMode.Locked)
				SetCursorState(true);
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
			Cursor.visible = !newState;
		}
	}
}