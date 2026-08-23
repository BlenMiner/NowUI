using System;
using UnityEngine;

namespace NowUI
{
    /// <summary>How a <see cref="NowContextTrigger"/> was produced.</summary>
    public enum NowContextTriggerSource : byte
    {
        None,
        SecondaryPointer,
        Action
    }

    /// <summary>
    /// A context-menu request with enough provenance to choose the correct
    /// anchor. Secondary-pointer requests carry the pointer position; explicit
    /// action requests carry the action control's screen-space rectangle.
    /// </summary>
    public readonly struct NowContextTrigger
    {
        readonly NowContextTriggerSource _source;
        readonly Vector2 _screenPointerPosition;
        readonly NowRect _screenActionAnchor;

        internal NowContextTrigger(
            NowContextTriggerSource source,
            Vector2 screenPointerPosition,
            NowRect screenActionAnchor)
        {
            _source = source;
            _screenPointerPosition = screenPointerPosition;
            _screenActionAnchor = screenActionAnchor;
        }

        /// <summary>True when either a secondary press or an explicit action requested a menu.</summary>
        public bool triggered => _source != NowContextTriggerSource.None;

        public NowContextTriggerSource source => _source;

        /// <summary>
        /// Screen-space pointer position for a <see cref="NowContextTriggerSource.SecondaryPointer"/>
        /// trigger; default for other sources.
        /// </summary>
        public Vector2 screenPointerPosition => _screenPointerPosition;

        /// <summary>
        /// Screen-space action-control rectangle for a <see cref="NowContextTriggerSource.Action"/>
        /// trigger; default for other sources.
        /// </summary>
        public NowRect screenActionAnchor => _screenActionAnchor;
    }

    /// <summary>
    /// Resolves the two conventional ways to request a context menu without
    /// losing which anchor belongs to the request. A valid secondary press wins
    /// when both sources occur in the same pass.
    /// </summary>
    public static class NowContextAction
    {
        /// <param name="contextRegion">
        /// Local-space region that accepts a secondary-pointer context action.
        /// Ambient masks and overlay pointer blocks are respected.
        /// </param>
        /// <param name="actionInvoked">True when a dedicated action control was invoked.</param>
        /// <param name="actionAnchor">Local-space rectangle of that action control.</param>
        public static NowContextTrigger Resolve(
            NowRect contextRegion,
            bool actionInvoked,
            NowRect actionAnchor)
        {
            if (NowInput.WasRightClicked(contextRegion))
            {
                return new NowContextTrigger(
                    NowContextTriggerSource.SecondaryPointer,
                    NowInput.current.pointerPosition,
                    default);
            }

            if (!actionInvoked)
                return default;

            if (actionAnchor.isEmpty)
                throw new ArgumentException(
                    "An invoked context action requires a non-empty anchor rectangle.",
                    nameof(actionAnchor));

            return new NowContextTrigger(
                NowContextTriggerSource.Action,
                default,
                Now.TransformScreenRect(actionAnchor));
        }

        /// <summary>
        /// Resolves a context request for a composite parent region. Secondary
        /// presses inside excluded child controls do not trigger the parent;
        /// the explicit action control remains independently invokable.
        /// </summary>
        public static NowContextTrigger Resolve(
            in NowInteractionRegion contextRegion,
            bool actionInvoked,
            NowRect actionAnchor)
        {
            if (NowInput.WasRightClicked(in contextRegion))
            {
                return new NowContextTrigger(
                    NowContextTriggerSource.SecondaryPointer,
                    NowInput.current.pointerPosition,
                    default);
            }

            if (!actionInvoked)
                return default;

            if (actionAnchor.isEmpty)
                throw new ArgumentException(
                    "An invoked context action requires a non-empty anchor rectangle.",
                    nameof(actionAnchor));

            return new NowContextTrigger(
                NowContextTriggerSource.Action,
                default,
                Now.TransformScreenRect(actionAnchor));
        }
    }
}
