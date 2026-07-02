using UnityEngine;

namespace NowUI
{
    /// <summary>
    /// Compact snapshot of the input facts that matter for interaction-driven
    /// repaints. Retained hosts (NowGraphic, NowWorldGraphic) compare the state
    /// captured at their last rebuild against a fresh sample to decide whether
    /// hosted controls could possibly react, keeping idle frames fully cached.
    /// </summary>
    internal struct NowInteractionInputState
    {
        const float PositionEpsilonSqr = 0.25f;

        public bool pointerInside;

        public Vector2 pointerPosition;

        public NowPointerButtons pointerButtonsDown;

        public NowPointerButtons pointerButtonsPressed;

        public NowPointerButtons pointerButtonsReleased;

        public Vector2 scrollDelta;

        public Vector2 navigation;

        public bool focusPreviousPressed;

        public bool focusNextPressed;

        public bool submitDown;

        public bool submitPressed;

        public bool submitReleased;

        public bool cancelDown;

        public bool cancelPressed;

        public bool cancelReleased;

        public static NowInteractionInputState FromSnapshot(NowInputSnapshot snapshot, Vector2 size)
        {
            bool inside = snapshot.hasPointer &&
                snapshot.pointerPosition.x >= 0f &&
                snapshot.pointerPosition.y >= 0f &&
                snapshot.pointerPosition.x <= size.x &&
                snapshot.pointerPosition.y <= size.y;

            return new NowInteractionInputState
            {
                pointerInside = inside,
                pointerPosition = snapshot.pointerPosition,
                pointerButtonsDown = snapshot.pointerButtonsDown,
                pointerButtonsPressed = snapshot.pointerButtonsPressed,
                pointerButtonsReleased = snapshot.pointerButtonsReleased,
                scrollDelta = snapshot.scrollDelta,
                navigation = snapshot.navigation,
                focusPreviousPressed = snapshot.focusPreviousPressed,
                focusNextPressed = snapshot.focusNextPressed,
                submitDown = snapshot.submitDown,
                submitPressed = snapshot.submitPressed,
                submitReleased = snapshot.submitReleased,
                cancelDown = snapshot.cancelDown,
                cancelPressed = snapshot.cancelPressed,
                cancelReleased = snapshot.cancelReleased
            };
        }

        public bool HasChangedSince(in NowInteractionInputState previous)
        {
            bool pointerRelevant =
                pointerInside ||
                previous.pointerInside ||
                pointerButtonsDown != NowPointerButtons.None ||
                previous.pointerButtonsDown != NowPointerButtons.None;

            if (pointerInside != previous.pointerInside)
                return true;

            if (pointerRelevant && (pointerPosition - previous.pointerPosition).sqrMagnitude > PositionEpsilonSqr)
                return true;

            if (pointerButtonsDown != previous.pointerButtonsDown)
                return true;

            if (pointerButtonsPressed != NowPointerButtons.None ||
                pointerButtonsReleased != NowPointerButtons.None)
                return true;

            if (pointerRelevant && scrollDelta != Vector2.zero)
                return true;

            if ((navigation - previous.navigation).sqrMagnitude > PositionEpsilonSqr)
                return true;

            if (submitDown != previous.submitDown || cancelDown != previous.cancelDown)
                return true;

            return focusPreviousPressed || focusNextPressed ||
                submitPressed || submitReleased || cancelPressed || cancelReleased;
        }
    }
}
